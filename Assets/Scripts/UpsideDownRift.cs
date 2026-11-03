using System.Collections;
using UnityEngine;

/// <summary>
/// Replaces the Home-scene mirror with a living Upside Down rift: an organic, pulsing
/// tear in the air with a red glow, drifting spores and a low rumble. Walk into it and
/// you are pulled through: rumble + haptics build, the view fades to black, and the
/// "Entering Hawkins" level loads.
/// Put on the "Door to UpsideDown" object (activated when Demo Hunt is chosen).
/// </summary>
public class UpsideDownRift : MonoBehaviour
{
    [Tooltip("Prefab whose first child is the organic portal mesh (Assets/Prefab/Worms.prefab).")]
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private AudioClip enterSound;
    [Tooltip("Use the oval mirror's exact size and position.")]
    [SerializeField] private bool matchMirrorSize = true;
    [Tooltip("Used only when Match Mirror Size is off.")]
    [SerializeField] private float riftHeight = 2.3f;
    [SerializeField] private float riftWidth = 1.4f;
    [Tooltip("Number of drifting spore particles around the rift.")]
    [SerializeField] private int sporeCount = 110;
    [Tooltip("Walk this close (m) to the centre of the rift to enter.")]
    [SerializeField] private float enterDistance = 1.1f;

    Transform cam, rift, tendrils, core, halo;
    Light glow;
    AudioSource hum;
    Transform[] spores;
    Vector3[] sporeVel;
    Vector3 center;
    bool entering;
    readonly System.Collections.Generic.List<GameObject> enabledObjects = new System.Collections.Generic.List<GameObject>();
    readonly System.Collections.Generic.List<Behaviour> enabledComponents = new System.Collections.Generic.List<Behaviour>();

    // Home has locomotion off; switch walking + turning on while the rift is open so the
    // player can walk into it, and back off if they leave the Demo Hunt screen.
    static readonly string[] LocomotionObjects = { "Locomotion", "Move", "Turn", "Gravity" };

    void EnableLocomotion()
    {
        if (cam == null) return;
        foreach (Transform t in cam.root.GetComponentsInChildren<Transform>(true))
        {
            if (System.Array.IndexOf(LocomotionObjects, t.name) < 0) continue;
            if (!t.gameObject.activeSelf) { t.gameObject.SetActive(true); enabledObjects.Add(t.gameObject); }
            foreach (var p in t.GetComponents<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>())
                if (!p.enabled) { p.enabled = true; enabledComponents.Add(p); }
        }
    }

    void OnDisable()
    {
        if (entering) return; // keep moving while being pulled in
        foreach (var c in enabledComponents) if (c != null) c.enabled = false;
        foreach (var g in enabledObjects) if (g != null) g.SetActive(false);
        enabledComponents.Clear();
        enabledObjects.Clear();
    }

    void OnEnable()
    {
        if (cam != null && enabledObjects.Count == 0) EnableLocomotion();
    }

    void Start()
    {
        cam = Camera.main != null ? Camera.main.transform : null;
        EnableLocomotion();

        // Hide the mirror; the rift takes its place (same spot, same height).
        Bounds b = new Bounds(transform.position, Vector3.zero);
        bool any = false;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            b = any ? Enc(b, r.bounds) : r.bounds;
            any = true;
            r.enabled = false;
        }
        // Walking in replaces the old point-and-trigger entry.
        foreach (var t in GetComponentsInChildren<TriggerActivateInteractable>()) t.enabled = false;
        foreach (var i in GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>()) i.enabled = false;

        // Same size and position as the oval mirror it replaces.
        if (any && matchMirrorSize)
        {
            riftHeight = b.size.y;
            riftWidth = Mathf.Max(b.size.x, b.size.z);
            center = b.center;
        }
        else
        {
            center = any ? b.center : transform.position + Vector3.up * 1.2f;
            center.y = (any ? b.min.y : transform.position.y) + riftHeight * 0.5f + 0.1f;
        }

        Build();
    }

    static Bounds Enc(Bounds a, Bounds b) { a.Encapsulate(b); return a; }

    void Build()
    {
        rift = new GameObject("Upside Down Rift").transform;
        rift.SetParent(transform, true);
        rift.position = center;
        FacePlayer();

        // Dark inner void + hot red halo behind it (flattened glowing ellipses).
        halo = GameFx.GlowPrimitive(PrimitiveType.Sphere, new Color(1f, 0.25f, 0.08f, 0.55f), new Vector3(riftWidth * 1.25f, riftHeight * 1.12f, 0.04f), rift).transform;
        halo.localPosition = new Vector3(0f, 0f, 0.06f);
        core = GameFx.GlowPrimitive(PrimitiveType.Sphere, new Color(0.12f, 0.0f, 0.02f, 1f), new Vector3(riftWidth, riftHeight, 0.05f), rift).transform;

        // Organic tendrils around the opening: the Upside Down portal mesh, stood upright.
        GameObject template = portalPrefab != null && portalPrefab.transform.childCount > 0 ? portalPrefab.transform.GetChild(0).gameObject : portalPrefab;
        if (template != null)
        {
            var t = Instantiate(template, rift);
            t.SetActive(true);
            t.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            t.transform.localRotation = Quaternion.identity;
            t.transform.localScale = Vector3.one;
            Bounds tb = new Bounds(t.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in t.GetComponentsInChildren<Renderer>()) { tb = first ? r.bounds : Enc(tb, r.bounds); first = false; }
            float w = Mathf.Max(tb.size.x, tb.size.z, 0.001f);
            float f = 1f / w;
            // Disc normal (local Y) faces the player; local Z becomes vertical.
            t.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            t.transform.localScale = new Vector3(f * riftWidth * 1.35f, f * 0.6f, f * riftHeight * 1.2f);
            tendrils = t.transform;
        }

        glow = GameFx.CreateLight(rift, new Vector3(0f, 0f, -0.6f), new Color(1f, 0.18f, 0.06f), 7f, 4f);

        // Drifting spores / ash pulled toward the opening.
        spores = new Transform[Mathf.Max(1, sporeCount)];
        sporeVel = new Vector3[spores.Length];
        for (int i = 0; i < spores.Length; i++)
        {
            spores[i] = GameFx.GlowPrimitive(PrimitiveType.Sphere, new Color(1f, 0.85f, 0.8f, 0.8f), Vector3.one * Random.Range(0.012f, 0.03f), rift).transform;
            ResetSpore(i, true);
        }

        hum = rift.gameObject.AddComponent<AudioSource>();
        hum.clip = GameFx.Thunder;
        hum.loop = true;
        hum.pitch = 0.45f;
        hum.volume = 0.5f;
        hum.spatialBlend = 1f;
        hum.minDistance = 1.5f;
        hum.maxDistance = 15f;
        hum.Play();

    }

    void ResetSpore(int i, bool anywhere)
    {
        Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(anywhere ? 0.2f : 1.2f, 1.8f);
        spores[i].localPosition = new Vector3(ring.x * riftWidth, ring.y * riftHeight * 0.6f, Random.Range(-1.2f, -0.2f));
        sporeVel[i] = Random.insideUnitSphere * 0.05f;
    }

    void FacePlayer()
    {
        if (cam == null) return;
        Vector3 d = cam.position - center; d.y = 0f;
        // Rift's local -Z faces the player (front of the quads/ellipses).
        if (d.sqrMagnitude > 0.01f) rift.rotation = Quaternion.LookRotation(-d.normalized, Vector3.up);
    }

    void Update()
    {
        if (rift == null) return;
        float t = Time.time;
        float dist = cam != null ? Vector3.Distance(new Vector3(cam.position.x, center.y, cam.position.z), center) : 99f;
        float near = Mathf.InverseLerp(4f, enterDistance, dist); // 0 far .. 1 at the edge

        // Breathing, pulsing opening; stronger as you approach.
        float pulse = 1f + Mathf.Sin(t * 2.2f) * 0.04f + Mathf.Sin(t * 5.3f) * 0.015f;
        core.localScale = new Vector3(riftWidth * pulse, riftHeight * pulse, 0.05f);
        halo.localScale = new Vector3(riftWidth * 1.25f * (pulse + 0.03f), riftHeight * 1.12f * (pulse + 0.03f), 0.04f);
        if (tendrils != null) tendrils.localRotation = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, t * 8f, 0f);
        glow.intensity = (3.5f + Mathf.Sin(t * 3f) * 1.2f) * (1f + near * 1.5f) * (Random.value < 0.03f ? 0.3f : 1f);
        hum.volume = 0.35f + near * 0.65f;
        hum.pitch = 0.45f + near * 0.25f;

        // Spores spiral into the void.
        for (int i = 0; i < spores.Length; i++)
        {
            Vector3 p = spores[i].localPosition;
            Vector3 toCore = (Vector3.zero - p);
            sporeVel[i] += (toCore.normalized * 0.25f + Vector3.Cross(Vector3.forward, toCore).normalized * 0.15f) * Time.deltaTime;
            spores[i].localPosition = p + sporeVel[i] * Time.deltaTime;
            if (toCore.magnitude < 0.15f) ResetSpore(i, false);
        }

        if (!entering && cam != null)
        {
            if (near > 0.3f && Random.value < near * 0.08f) GameFx.HapticBoth(near * 0.4f, 0.05f);
            if (dist <= enterDistance) StartCoroutine(Enter());
        }
    }

    // Pulled through: rumble, red-to-black fade, then load the level (its own
    // "Entering Hawkins" fade screen takes over).
    IEnumerator Enter()
    {
        entering = true;
        if (enterSound != null) GameFx.Play2D(enterSound, 1f, 0.8f);
        GameFx.Play2D(GameFx.Boom, 0.9f, 0.6f);
        GameFx.HapticBoth(1f, 0.8f);

        var veil = GameFx.GlowPrimitive(PrimitiveType.Quad, new Color(0.4f, 0f, 0f, 0f), new Vector3(2f, 2f, 1f), cam);
        veil.transform.localPosition = new Vector3(0f, 0f, 0.15f);
        var mr = veil.GetComponent<Renderer>();
        mr.material = new Material(mr.sharedMaterial);
        for (float e = 0f; e < 0.9f; e += Time.deltaTime)
        {
            float k = e / 0.9f;
            mr.material.color = Color.Lerp(new Color(0.5f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 1f), k);
            yield return null;
        }
        mr.material.color = Color.black;

        if (StartGame.Instance != null) StartGame.Instance.EnterUpsideDown();
        else UnityEngine.SceneManagement.SceneManager.LoadScene("Hawkin City");
    }
}
