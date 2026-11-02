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
    [Tooltip("Gates are placed anywhere on the city NavMesh, at least this far from the player.")]
    [SerializeField] private float gateMinDistanceFromPlayer = 35f;
    [Tooltip("Minimum distance between two gates.")]
    [SerializeField] private float gateMinSpacing = 30f;
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
        GameHUD.Radio($"Follow the red beams and SHOOT the gates to break their seals.");
    }

    string[] TaskLines()
    {
        string box(bool done) => done ? "<color=#7CFF7C>[X]</color>" : "<color=#FF3131>[  ]</color>";
        bool gatesDone = gates.Count > 0 && sealedCount >= gates.Count;
        return new[]
        {
            $"{box(gatesDone)}  Break the seals of all {Mathf.Max(gates.Count, gateCount)} Upside Down gates  <color=#FF6A5A>({sealedCount}/{Mathf.Max(gates.Count, gateCount)})</color>",
            $"{box(false)}  Survive the Demogorgon waves",
            $"{box(bossDefeated)}  Defeat the Alpha Demogorgon",
            "<color=#AAAAAA>BONUS: chain kills for COMBO x4, grab pickups, slash with your sword</color>",
        };
    }

    // Places gates at random spots across the whole NavMesh (the whole city), far from the
    // player and far from each other, and only where the player can actually walk to.
    void CreateGates()
    {
        if (cam == null) return;
        GameObject template = spawner != null ? spawner.PortalTemplate : null;

        List<Vector3> candidates = RandomNavMeshPoints(300);
        Vector3 playerPos = cam.position;
        NavMesh.SamplePosition(playerPos, out NavMeshHit playerHit, 5f, NavMesh.AllAreas);
        var path = new NavMeshPath();

        // Start strict, relax the spacing rules if the map is too small.
        float minFromPlayer = gateMinDistanceFromPlayer, minBetween = gateMinSpacing;
        for (int pass = 0; pass < 4 && gates.Count < gateCount; pass++)
        {
            Shuffle(candidates);
            foreach (Vector3 c in candidates)
            {
                if (gates.Count >= gateCount) break;
                if (FlatDistance(c, playerPos) < minFromPlayer) continue;
                if (gates.Exists(g => FlatDistance(g.transform.position, c) < minBetween)) continue;
                if (playerHit.hit && (!NavMesh.CalculatePath(playerHit.position, c, NavMesh.AllAreas, path)
                                      || path.status != NavMeshPathStatus.PathComplete)) continue;

                var gate = UpsideDownGate.Create(c, template, gateHealth, gates.Count);
                gate.Sealed += OnGateSealed;
                gates.Add(gate);
                if (spawner != null) spawner.Gates.Add(gate);
            }
            minFromPlayer *= 0.6f;
            minBetween *= 0.6f;
        }
    }

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
        GameHUD.ShowBanner($"GATE SEALED {sealedCount}/{gates.Count}");
        GameHUD.ShowTaskBoard("TASKS UPDATED", TaskLines(), false, 4f);
        if (sealedCount < gates.Count)
            GameHUD.Radio($"Gate sealed! {gates.Count - sealedCount} to go.");
        else if (!bossSpawned)
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
            if (sealedCount < gates.Count)
                GameHUD.Radio("The Alpha is down! Seal the remaining gates!");
            CheckWin();
        };
    }

    void CheckWin()
    {
        if (won) return;
        bool gatesDone = gates.Count == 0 || sealedCount >= gates.Count;
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
