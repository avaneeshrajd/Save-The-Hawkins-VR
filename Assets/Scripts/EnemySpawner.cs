using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Wave spawner for Demo Hunt.
/// Wave 1 is a tutorial (one slow Demogorgon). Each later wave is bigger, faster,
/// adds Runner / Brute / Pouncer variants, spawns closer to the player (often behind)
/// and near open Upside Down gates. Banner + radio announce every wave.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject demoPrefab;
    [SerializeField] private Transform[] spawnPoints;

    [Header("Waves")]
    [SerializeField] private float breakBetweenWaves = 5f;
    [SerializeField] private float waveTimeout = 60f;
    [Tooltip("Wave at which difficulty is maxed.")]
    [SerializeField] private int maxDifficultyWave = 9;
    [SerializeField] private int maxAliveCap = 12;
    [SerializeField] private float maxSpeedMultiplier = 1.5f;

    [Header("Spawn near player")]
    [SerializeField] private float nearMinDistance = 9f;
    [SerializeField] private float nearMaxDistance = 18f;
    [SerializeField] private float safeRadius = 6f;

    [Header("Spawn effects")]
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private float portalSizeMeters = 1.8f;
    [SerializeField] private float portalLifetime = 3f;
    [SerializeField] private AudioClip spawnSound;
    [SerializeField] private float spawnSoundVolume = 0.8f;

    static readonly string[] WaveNames =
    {
        "THE HUNT BEGINS", "THE GATE WIDENS", "THEY SMELL BLOOD", "THE HIVE AWAKENS",
        "SHADOWS IN THE STREETS", "NO WAY OUT", "THE UPSIDE DOWN SPILLS OVER", "HAWKINS FALLS"
    };

    /// <summary>0 at start, 1 at max difficulty. Bats and other systems read this.</summary>
    public static float Difficulty { get; private set; }
    public static EnemySpawner Instance { get; private set; }
    public int Wave { get; private set; }
    public GameObject PortalTemplate => portalTemplate;
    /// <summary>Open gates; spawns prefer these (set by DemoHuntDirector).</summary>
    public readonly List<UpsideDownGate> Gates = new List<UpsideDownGate>();

    private Transform player;
    private GameObject portalTemplate;
    private float lastBehindCallout = -99f;
    private readonly List<DemogorgonHealth> waveEnemies = new List<DemogorgonHealth>();

    void Awake() { Instance = this; }

    void Start()
    {
        Difficulty = 0f;
        if (Camera.main != null) player = Camera.main.transform;
        if (portalPrefab != null)
            portalTemplate = portalPrefab.transform.childCount > 0 ? portalPrefab.transform.GetChild(0).gameObject : portalPrefab;

        // The template Demogorgon placed in the scene must not walk around on its own.
        if (demoPrefab != null && demoPrefab.scene.IsValid())
            demoPrefab.SetActive(false);

        StartCoroutine(WaveLoop());
    }

    IEnumerator WaveLoop()
    {
        // Start right after the intro fade + task briefing.
        if (FindFirstObjectByType<DemoHuntDirector>() != null)
            yield return new WaitUntil(() => DemoHuntDirector.LevelStarted);
        StartCoroutine(JumpScareLoop());
        while (true)
        {
            Wave++;
            Difficulty = Mathf.Clamp01((Wave - 1f) / (maxDifficultyWave - 1f));
            string name = WaveNames[Mathf.Min(Wave - 1, WaveNames.Length - 1)];
            GameHUD.ShowBanner($"WAVE {Wave}", name);

            if (Wave == 1)
            {
                GameHUD.Radio("Something's coming. Pull the TRIGGER to shoot.");
                GameHUD.Radio("Out of ammo? Press A or X, or point the gun at the ground to reload.");
                GameHUD.Radio("Swing the sword in your other hand to slash anything that gets close!");
            }
            else if (Wave == 3) GameHUD.Radio("Fast ones incoming - the small ones are quick but weak!");
            else if (Wave == 4) GameHUD.Radio("Big one! Brutes take a lot of bullets. Keep your distance.");
            else if (Wave == 5) GameHUD.Radio("Watch out - some of them POUNCE. Shoot them mid-air!");
            else GameHUD.Radio($"Wave {Wave}. Stay sharp, they're getting faster.");

            yield return new WaitForSeconds(2f);
            yield return StartCoroutine(RunWave());
            GameHUD.ShowBanner("WAVE CLEARED", $"Next wave in {breakBetweenWaves:0} seconds", 2.5f);
            yield return new WaitForSeconds(breakBetweenWaves);
        }
    }

    IEnumerator RunWave()
    {
        waveEnemies.Clear();
        int count = 3 + Wave * 2;
        int maxAlive = Mathf.RoundToInt(Mathf.Lerp(4, maxAliveCap, Difficulty));
        float speedMul = Mathf.Lerp(1f, maxSpeedMultiplier, Difficulty);
        float gap = Mathf.Lerp(1.2f, 0.5f, Difficulty) * Random.Range(0.5f, 1.5f);
        float start = Time.time;

        int spawned = 0;
        while (spawned < count && Time.time - start < waveTimeout)
        {
            if (DemogorgonAI.AliveCount < maxAlive)
            {
                var h = SpawnOne(PickType(), speedMul, Difficulty);
                if (h != null) waveEnemies.Add(h);
                spawned++;
            }
            // Random gaps: sometimes two at once, sometimes a tense pause.
            yield return new WaitForSeconds(Random.value < 0.25f ? 0.1f : gap * Random.Range(0.5f, 1.8f));
        }
        // Wave ends when its Demogorgons are dead (or timeout).
        while (Time.time - start < waveTimeout && waveEnemies.Exists(e => e != null && !e.IsDead))
            yield return new WaitForSeconds(0.5f);
    }

    DemogorgonType PickType()
    {
        float r = Random.value;
        if (Wave >= 2 && r < 0.2f) return DemogorgonType.Pouncer;
        if (Wave >= 4 && r < 0.32f) return DemogorgonType.Brute;
        if (r < 0.6f) return DemogorgonType.Runner;
        return DemogorgonType.Normal;
    }

    /// <summary>Spawns one Demogorgon. Returns its health component (null if no spot found).</summary>
    public DemogorgonHealth SpawnOne(DemogorgonType type, float speedMul, float nearChance01)
    {
        float nearChance = Mathf.Lerp(0.35f, 0.85f, nearChance01);
        Vector3 pos;
        bool behind = false;
        bool found = TryNearGate(out pos)
                     || (Random.value < nearChance && TryGetNearPlayerPoint(out pos, out behind))
                     || TryGetSpawnPoint(out pos);
        if (!found) return null;

        Vector3 look = player != null ? player.position - pos : Vector3.forward;
        look.y = 0f;
        Quaternion rot = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;

        GameObject demo = Instantiate(demoPrefab, pos, rot);
        demo.SetActive(false);
        EnemyVariants.Apply(demo, type, speedMul);
        demo.SetActive(true);
        // Slow types climb out of the portal; fast ones burst out already running.
        bool fast = type == DemogorgonType.Runner || type == DemogorgonType.Pouncer;
        if ((!fast || Random.value < 0.3f) && demo.TryGetComponent(out DemogorgonAI emergeAi)) emergeAi.Emerge();

        SpawnPortal(pos, type == DemogorgonType.Boss ? 2.5f : 1f);
        if (spawnSound != null)
            GameFx.Play3D(spawnSound, pos + Vector3.up, spawnSoundVolume, type == DemogorgonType.Boss ? 0.6f : 1f, 50f);

        if (behind && Time.time - lastBehindCallout > 10f)
        {
            lastBehindCallout = Time.time;
            GameHUD.Radio("Behind you! Turn around!");
        }
        return demo.GetComponent<DemogorgonHealth>();
    }

    /// <summary>
    /// Shock moments: every 20-40 s a Runner or Pouncer bursts out of the ground only
    /// 5-7 m away, in view, with a roar, flashlight blackout and strong vibration.
    /// </summary>
    IEnumerator JumpScareLoop()
    {
        yield return new WaitForSeconds(Random.Range(12f, 20f));
        while (true)
        {
            if (player != null && DemogorgonAI.AliveCount < maxAliveCap + 2)
            {
                Vector3 fwd = player.forward; fwd.y = 0f; fwd.Normalize();
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = player.position + Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * fwd * Random.Range(5f, 7f);
                    if (!NavMesh.SamplePosition(c, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) continue;
                    JumpScare(hit.position);
                    break;
                }
            }
            yield return new WaitForSeconds(Random.Range(20f, 40f));
        }
    }

    void JumpScare(Vector3 pos)
    {
        var type = Random.value < 0.5f ? DemogorgonType.Pouncer : DemogorgonType.Runner;
        Vector3 look = player.position - pos; look.y = 0f;
        GameObject demo = Instantiate(demoPrefab, pos, Quaternion.LookRotation(look));
        demo.SetActive(false);
        EnemyVariants.Apply(demo, type, Mathf.Lerp(1.1f, maxSpeedMultiplier, Difficulty));
        demo.SetActive(true);
        if (demo.TryGetComponent(out PounceBehaviour pounce)) pounce.LeapSoon(0.4f);
        else if (demo.TryGetComponent(out DemogorgonAI ai)) ai.PlayAnim("Jump", 0f);

        SpawnPortal(pos, 1.3f);
        GameFx.Play2D(DemogorgonAI.AttackClip, 1f, 0.75f);
        if (spawnSound != null) GameFx.Play2D(spawnSound, 0.9f, 1.2f);
        GameFx.HapticBoth(1f, 0.45f);
        Atmosphere.Blackout(0.35f);
    }

    /// <summary>Spawns a Demogorgon at an exact spot (trailer / scripted moments).</summary>
    public GameObject SpawnAt(Vector3 pos, DemogorgonType type, bool emerge, float speedMul = 1f)
    {
        Vector3 look = player != null ? player.position - pos : Vector3.forward; look.y = 0f;
        GameObject demo = Instantiate(demoPrefab, pos, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
        demo.SetActive(false);
        EnemyVariants.Apply(demo, type, speedMul);
        demo.SetActive(true);
        if (emerge && demo.TryGetComponent(out DemogorgonAI ai)) ai.Emerge();
        SpawnPortal(pos, type == DemogorgonType.Boss ? 2.5f : 1f);
        if (spawnSound != null) GameFx.Play3D(spawnSound, pos + Vector3.up, spawnSoundVolume, 1f, 50f);
        return demo;
    }

    public DemogorgonHealth SpawnBoss()
    {
        var h = SpawnOne(DemogorgonType.Boss, 1f, 0f);
        if (h == null && player != null)
        {
            // Fallback: any NavMesh point near the player.
            if (NavMesh.SamplePosition(player.position + player.forward * 15f, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                GameObject demo = Instantiate(demoPrefab, hit.position, Quaternion.identity);
                demo.SetActive(false);
                EnemyVariants.Apply(demo, DemogorgonType.Boss, 1f);
                demo.SetActive(true);
                h = demo.GetComponent<DemogorgonHealth>();
            }
        }
        return h;
    }

    bool TryNearGate(out Vector3 result)
    {
        result = Vector3.zero;
        var open = Gates.FindAll(g => g != null && !g.IsSealed);
        if (open.Count == 0 || Random.value > 0.4f) return false;
        Vector3 c = open[Random.Range(0, open.Count)].transform.position;
        Vector2 o = Random.insideUnitCircle.normalized * Random.Range(3f, 5f);
        if (NavMesh.SamplePosition(c + new Vector3(o.x, 0f, o.y), out NavMeshHit hit, 4f, NavMesh.AllAreas) && FarEnoughFromPlayer(hit.position))
        {
            result = hit.position;
            return true;
        }
        return false;
    }

    bool TryGetNearPlayerPoint(out Vector3 result, out bool behind)
    {
        result = Vector3.zero;
        behind = Random.value < 0.6f;
        if (player == null) return false;

        Vector3 forward = player.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();

        for (int attempt = 0; attempt < 10; attempt++)
        {
            float angle = behind ? Random.Range(100f, 260f) : Random.Range(0f, 360f);
            Vector3 candidate = player.position + Quaternion.Euler(0f, angle, 0f) * forward * Random.Range(nearMinDistance, nearMaxDistance);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4f, NavMesh.AllAreas) && FarEnoughFromPlayer(hit.position))
            {
                result = hit.position;
                return true;
            }
        }
        return false;
    }

    bool TryGetSpawnPoint(out Vector3 result)
    {
        result = Vector3.zero;
        if (spawnPoints == null || spawnPoints.Length == 0) return false;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (point != null && FarEnoughFromPlayer(point.position))
            {
                result = point.position;
                return true;
            }
        }
        return false;
    }

    bool FarEnoughFromPlayer(Vector3 pos)
    {
        if (player == null) return true;
        Vector3 d = pos - player.position;
        d.y = 0f;
        return d.magnitude >= safeRadius;
    }

    /// <summary>Opens a temporary portal (used by ambush dives too).</summary>
    public void OpenPortal(Vector3 pos, float sizeMul = 1f) => SpawnPortal(pos, sizeMul);

    /// <summary>NavMesh point behind the player for ambushes.</summary>
    public bool FindAmbushPoint(out Vector3 pos)
    {
        bool found = TryGetNearPlayerPoint(out pos, out bool behind);
        return found;
    }

    void SpawnPortal(Vector3 pos, float sizeMul)
    {
        if (portalTemplate == null) return;
        GameObject portal = Instantiate(portalTemplate, pos + Vector3.up * 0.05f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        portal.SetActive(true);
        portal.transform.localScale = Vector3.one;
        float width = 0f;
        Renderer[] renderers = portal.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            width = Mathf.Max(b.size.x, b.size.z);
        }
        float f = width > 0.0001f ? portalSizeMeters * sizeMul / width : 1f;
        StartCoroutine(AnimatePortal(portal.transform, f));
    }

    IEnumerator AnimatePortal(Transform portal, float scale)
    {
        portal.localScale = Vector3.zero;
        Vector3 full = new Vector3(scale, scale * 0.25f, scale);
        float grow = 0.35f, shrink = 0.6f;
        for (float t = 0f; t < grow && portal != null; t += Time.deltaTime)
        {
            portal.localScale = Vector3.Lerp(Vector3.zero, full, t / grow);
            yield return null;
        }
        if (portal == null) yield break;
        portal.localScale = full;
        yield return new WaitForSeconds(Mathf.Max(0f, portalLifetime - grow - shrink));
        for (float t = 0f; t < shrink && portal != null; t += Time.deltaTime)
        {
            portal.localScale = Vector3.Lerp(full, Vector3.zero, t / shrink);
            yield return null;
        }
        if (portal != null) Destroy(portal.gameObject);
    }
}
