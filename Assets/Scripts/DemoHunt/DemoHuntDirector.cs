using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Runs the Demo Hunt level: HUD, atmosphere, sword, 3 Upside Down gates (objective),
/// boss at minute 5 (or once all gates are sealed), and the win condition
/// (all gates sealed + boss defeated).
/// </summary>
public class DemoHuntDirector : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip heartbeatClip;
    [SerializeField] private AudioClip attackClip;
    [SerializeField] private AudioClip bossMusic;

    [Header("Sword (free hand)")]
    [SerializeField] private GameObject swordModel;
    [SerializeField] private Material swordMaterial;
    [SerializeField] private Texture2D swordTexture;
    [Tooltip("Tick if the sword is held by the blade.")]
    [SerializeField] private bool flipSword;

    [Header("UI style (same as menu panels)")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite panelButtonSprite;
    [SerializeField] private TMP_FontAsset panelFont;

    [Header("Objective")]
    [SerializeField] private int gateCount = 3;
    [SerializeField] private float gateHealth = 600f;
    [Tooltip("Optional exact gate spots (Gate 1, 2, 3). Empty = in front of Hawkins Middle School, 777, Hawkins Public Library.")]
    [SerializeField] private Transform[] gatePoints = new Transform[3];
    [Tooltip("How far in front of the building the gate is placed (m).")]
    [SerializeField] private float gateFrontDistance = 3f;
    [SerializeField] private float bossTime = 300f;

    EnemySpawner spawner;
    readonly List<UpsideDownGate> gates = new List<UpsideDownGate>();
    DemogorgonHealth boss;
    bool bossSpawned, bossDefeated, won;
    int sealedCount;
    Transform cam;
    float levelStartTime;

    /// <summary>True once the intro fade and task briefing are done. Spawner waits for this.</summary>
    public static bool LevelStarted { get; private set; }

    IEnumerator Start()
    {
        ScoreSystem.ResetRun();
        DemogorgonAI.AttackClip = attackClip;
        cam = Camera.main != null ? Camera.main.transform : null;
        GameHUD.PanelSprite = panelSprite;
        GameHUD.ButtonSprite = panelButtonSprite;
        GameHUD.PanelFont = panelFont;
        GameHUD.Create();
        Atmosphere.Create(heartbeatClip);
        spawner = FindFirstObjectByType<EnemySpawner>();

        LevelStarted = false;
        // Wait for the "Entering Hawkins 1986" fade; the gun only exists after it.
        yield return new WaitUntil(() => SceneFadeIn.Done || Time.timeSinceLevelLoad > 20f);
        yield return null;
        SwordMelee.Create(swordModel, swordMaterial, swordTexture, flipSword);
        CreateGates();

        yield return GameHUD.ShowTaskBoard("DEMO HUNT  -  LEVEL 1", TaskLines(), true, 20f);

        levelStartTime = Time.time;
        LevelStarted = true;
        GameHUD.Radio("This is Hawkins Lab. The Upside Down is breaking through!");
        GameHUD.Radio($"Gate 1 is open at {GatePlaceNames[0]}. Follow the red beam and SHOOT it to break the seal!");
    }

    // "1|" = done, "0|" = open, "B|" = bonus line (see GameHUD.ShowTaskBoard).
    string[] TaskLines()
    {
        int total = TotalGates;
        bool gatesDone = sealedCount >= total;
        return new[]
        {
            (gatesDone ? "1|" : "0|") + $"Break the gate seals in order  ({sealedCount}/{total})",
            "0|Survive the Demogorgon waves",
            (bossDefeated ? "1|" : "0|") + "Defeat the Alpha Demogorgon",
            "B|BONUS: chain kills for combos, grab pickups, use your sword",
        };
    }

    // Gates open one at a time in a fixed order, in front of these landmarks.
    static readonly string[] GateLandmarks = { "Hawkins Middle School", "777", "Hawkins Public Library" };
    static readonly string[] GatePlaceNames = { "Hawkins Middle School", "the 777", "Hawkins Public Library" };
    int TotalGates => Mathf.Max(1, Mathf.Min(gateCount, GateLandmarks.Length));
    int openedGates;

    void CreateGates() => OpenGate(0);

    void OpenGate(int index)
    {
        if (index >= TotalGates || cam == null) return;
        Vector3 pos = GatePosition(index);
        var gate = UpsideDownGate.Create(pos, spawner != null ? spawner.PortalTemplate : null, gateHealth, index);
        gate.Sealed += OnGateSealed;
        gates.Add(gate);
        openedGates = index + 1;
        if (spawner != null) { spawner.Gates.Clear(); spawner.Gates.Add(gate); }
    }

    // Inspector override first; otherwise the walkable spot in front of the landmark.
    Vector3 GatePosition(int index)
    {
        if (gatePoints != null && index < gatePoints.Length && gatePoints[index] != null)
            return Snap(gatePoints[index].position);

        GameObject landmark = GameObject.Find(GateLandmarks[index]);
        if (landmark == null)
        {
            Debug.LogWarning($"Gate {index + 1}: '{GateLandmarks[index]}' not found, using a random spot.");
            var pts = RandomNavMeshPoints(50);
            return pts.Count > 0 ? pts[0] : cam.position;
        }
        return FrontOf(landmark);
    }

    // Tries all four sides of the building; picks the walkable point that faces the street
    // (closest to where the player starts), just outside the building footprint.
    Vector3 FrontOf(GameObject building)
    {
        Bounds b = new Bounds(building.transform.position, Vector3.zero);
        foreach (var r in building.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        Vector3 c = b.center; c.y = b.min.y;
        Vector3 start = cam.position;

        Vector3 best = c;
        float bestScore = float.MaxValue;
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        foreach (Vector3 d in dirs)
        {
            float extent = Mathf.Abs(d.x) > 0f ? b.extents.x : b.extents.z;
            Vector3 probe = c + d * (extent + gateFrontDistance);
            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 6f, NavMesh.AllAreas)) continue;
            // Must really be outside the footprint.
            Vector3 flat = hit.position; flat.y = b.center.y;
            if (b.Contains(flat)) continue;
            float score = FlatDistance(hit.position, start);
            if (score < bestScore) { bestScore = score; best = hit.position; }
        }
        return bestScore < float.MaxValue ? best : Snap(c + Vector3.forward * (b.extents.z + gateFrontDistance));
    }

    static Vector3 Snap(Vector3 p) =>
        NavMesh.SamplePosition(p, out NavMeshHit h, 8f, NavMesh.AllAreas) ? h.position : p;

    // Area-weighted random points on the baked NavMesh.
    static List<Vector3> RandomNavMeshPoints(int count)
    {
        var points = new List<Vector3>();
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        int triCount = tri.indices.Length / 3;
        if (triCount == 0) return points;

        float[] cumulative = new float[triCount];
        float total = 0f;
        for (int t = 0; t < triCount; t++)
        {
            Vector3 a = tri.vertices[tri.indices[t * 3]], b = tri.vertices[tri.indices[t * 3 + 1]], c = tri.vertices[tri.indices[t * 3 + 2]];
            total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            cumulative[t] = total;
        }
        for (int n = 0; n < count; n++)
        {
            float r = Random.value * total;
            int t = System.Array.BinarySearch(cumulative, r);
            if (t < 0) t = ~t;
            t = Mathf.Min(t, triCount - 1);
            Vector3 a = tri.vertices[tri.indices[t * 3]], b = tri.vertices[tri.indices[t * 3 + 1]], c = tri.vertices[tri.indices[t * 3 + 2]];
            float u = Random.value, v = Random.value;
            if (u + v > 1f) { u = 1f - u; v = 1f - v; }
            points.Add(a + (b - a) * u + (c - a) * v);
        }
        return points;
    }

    static float FlatDistance(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    static void Shuffle(List<Vector3> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int k = Random.Range(0, i + 1);
            (list[i], list[k]) = (list[k], list[i]);
        }
    }

    void OnGateSealed(UpsideDownGate gate)
    {
        sealedCount++;
        ScoreSystem.AddKill(50);
        GameHUD.ShowBanner($"GATE {sealedCount} SEALED", $"{sealedCount}/{TotalGates}");
        if (sealedCount < TotalGates)
        {
            // Next gate tears open.
            OpenGate(sealedCount);
            GameHUD.ShowTaskBoard("TASKS UPDATED", TaskLines(), false, 4f);
            GameHUD.Radio($"Gate sealed! A new gate just opened at {GatePlaceNames[sealedCount]}!");
        }
        else GameHUD.ShowTaskBoard("TASKS UPDATED", TaskLines(), false, 4f);
        if (sealedCount >= TotalGates && !bossSpawned)
            StartCoroutine(BossFinale("All gates sealed... wait. Something BIG is coming through!"));
        CheckWin();
    }

    void Update()
    {
        if (LevelStarted && !bossSpawned && Time.time - levelStartTime >= bossTime)
            StartCoroutine(BossFinale("Huge readings! The Alpha is here!"));
    }

    IEnumerator BossFinale(string radioLine)
    {
        if (bossSpawned) yield break;
        bossSpawned = true;
        GameHUD.Radio(radioLine);
        GameFx.HapticBoth(0.6f, 1f);
        yield return new WaitForSeconds(3f);
        GameHUD.ShowBanner("BOSS", "THE ALPHA DEMOGORGON", 4f);
        GameHUD.Radio("When it roars and glows, its weak spot is open - unload on it!");

        boss = spawner != null ? spawner.SpawnBoss() : null;
        if (boss == null) { bossDefeated = true; CheckWin(); yield break; }

        AudioSource music = null;
        if (bossMusic != null)
        {
            music = gameObject.AddComponent<AudioSource>();
            music.clip = bossMusic;
            music.loop = true;
            music.volume = 0.6f;
            music.Play();
        }
        boss.Died += _ =>
        {
            bossDefeated = true;
            if (music != null) music.Stop();
            GameHUD.ShowBanner("ALPHA DEFEATED!");
            if (sealedCount < TotalGates)
                GameHUD.Radio("The Alpha is down! Seal the remaining gates!");
            CheckWin();
        };
    }

    void CheckWin()
    {
        if (won) return;
        bool gatesDone = sealedCount >= TotalGates;
        if (!gatesDone || !bossSpawned || !bossDefeated) return;
        won = true;
        StartCoroutine(WinRoutine());
    }

    IEnumerator WinRoutine()
    {
        GameHUD.ShowBanner("HAWKINS IS SAVED!", "The gates are closed", 4f);
        GameHUD.Radio("You did it! The Upside Down is sealed. Get home safe.");
        for (int i = 0; i < 5; i++)
        {
            if (cam != null)
                GameFx.Burst(cam.position + cam.forward * 8f + Random.insideUnitSphere * 4f + Vector3.up * 5f, 24, 6f, 1.5f,
                    Color.red, Color.yellow, Color.cyan, Color.magenta);
            yield return new WaitForSeconds(0.5f);
        }
        yield return new WaitForSeconds(1.5f);
        if (UIManager.Instance != null) UIManager.Instance.LevelComplete();
    }
}
