using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Covers Hawkins in Upside Down growth (the worm/vine patches) at scene start.
/// Uses raycasts to stick patches onto the ground, building walls, roofs and trees,
/// aligned to the surface. Patch templates are the children of the Worms prefab.
/// </summary>
public class UpsideDownGrowth : MonoBehaviour
{
    [Tooltip("Prefab whose children are growth patches (Assets/Prefab/Worms.prefab).")]
    [SerializeField] private GameObject growthPrefab;

    [Header("Area (centered on this object, or the player if Center On Player is on)")]
    [SerializeField] private bool centerOnPlayer = true;
    [SerializeField] private float radius = 45f;
    [SerializeField] private float rayStartHeight = 40f;

    [Header("Amounts (keep moderate for Quest performance)")]
    [SerializeField] private int groundPatches = 30;
    [SerializeField] private int wallPatches = 14;
    [SerializeField] private int roofAndTreePatches = 10;
    [Tooltip("Keep this clear around the player start so the player is not standing in a patch.")]
    [SerializeField] private float clearRadiusAroundPlayer = 3f;

    [Header("Look")]
    [Tooltip("Patch width in meters on the ground (min, max). Measured from the real mesh size.")]
    [SerializeField] private Vector2 groundSizeMeters = new Vector2(0.5f, 1.3f);
    [Tooltip("Patch width in meters on walls, roofs and trees (min, max).")]
    [SerializeField] private Vector2 surfaceSizeMeters = new Vector2(0.35f, 0.9f);
    [Tooltip("Vertical squash, so patches lie flat on surfaces.")]
    [SerializeField] private float flatness = 0.25f;
    [Tooltip("Animate the tendrils. Turn off if frame rate drops on Quest.")]
    [SerializeField] private bool animate = true;
    [Tooltip("Flip patches 180 degrees if they look inside-out (the Worms prefab root is flipped).")]
    [SerializeField] private bool flipPatches = false;
    [SerializeField] private int randomSeed = 1983;

    private readonly List<GameObject> templates = new List<GameObject>();
    private Vector3 center;
    private Transform player;
    private const int MaxAttemptsPerPatch = 8;

    void Start()
    {
        if (growthPrefab == null)
        {
            Debug.LogWarning("UpsideDownGrowth: no growth prefab assigned.");
            return;
        }

        foreach (Transform child in growthPrefab.transform)
            templates.Add(child.gameObject);
        if (templates.Count == 0)
            templates.Add(growthPrefab);

        if (Camera.main != null)
            player = Camera.main.transform;
        center = centerOnPlayer && player != null ? player.position : transform.position;

        Random.State oldState = Random.state;
        Random.InitState(randomSeed);

        Scatter(groundPatches, TryGround, groundSizeMeters);
        Scatter(wallPatches, TryWall, surfaceSizeMeters);
        Scatter(roofAndTreePatches, TryRoofOrTree, surfaceSizeMeters);

        Random.state = oldState;
    }

    delegate bool PlacementFinder(out RaycastHit hit);

    void Scatter(int count, PlacementFinder finder, Vector2 scaleRange)
    {
        for (int i = 0; i < count; i++)
        {
            for (int attempt = 0; attempt < MaxAttemptsPerPatch; attempt++)
            {
                if (finder(out RaycastHit hit))
                {
                    Place(hit, Random.Range(scaleRange.x, scaleRange.y));
                    break;
                }
            }
        }
    }

    Vector3 RandomPointInArea()
    {
        Vector2 p = Random.insideUnitCircle * radius;
        return new Vector3(center.x + p.x, center.y, center.z + p.y);
    }

    // Straight down onto open ground (mostly flat surfaces).
    bool TryGround(out RaycastHit hit)
    {
        Vector3 p = RandomPointInArea();
        if (Cast(p + Vector3.up * rayStartHeight, Vector3.down, rayStartHeight * 2f, out hit) &&
            hit.normal.y > 0.8f && IsClearOfPlayer(hit.point) && hit.point.y < center.y + 1f)
            return true;
        return false;
    }

    // Horizontal rays from above the street toward walls.
    bool TryWall(out RaycastHit hit)
    {
        Vector3 p = RandomPointInArea() + Vector3.up * Random.Range(0.5f, 7f);
        Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
        if (Cast(p, dir, 20f, out hit) && Mathf.Abs(hit.normal.y) < 0.4f)
            return true;
        return false;
    }

    // Down rays that land on something raised: roofs, tree tops, cars.
    bool TryRoofOrTree(out RaycastHit hit)
    {
        Vector3 p = RandomPointInArea();
        if (Cast(p + Vector3.up * rayStartHeight, Vector3.down, rayStartHeight * 2f, out hit) &&
            hit.point.y > center.y + 1.5f && hit.normal.y > 0.3f)
            return true;
        return false;
    }

    bool Cast(Vector3 origin, Vector3 dir, float distance, out RaycastHit hit)
    {
        if (!Physics.Raycast(origin, dir, out hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return false;

        // Skip moving things: enemies, the gun, the player rig.
        if (hit.collider.attachedRigidbody != null) return false;
        if (player != null && hit.collider.transform.IsChildOf(player.root)) return false;
        return true;
    }

    bool IsClearOfPlayer(Vector3 point)
    {
        Vector3 d = point - center;
        d.y = 0f;
        return d.magnitude > clearRadiusAroundPlayer;
    }

    static float MeasureWidth(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 0f;
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return Mathf.Max(b.size.x, b.size.z);
    }

    void Place(RaycastHit hit, float sizeMeters)
    {
        GameObject template = templates[Random.Range(0, templates.Count)];

        // Patch "up" follows the surface normal, random spin around it.
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, hit.normal) *
                         Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        if (flipPatches)
            rot *= Quaternion.Euler(180f, 0f, 0f);

        GameObject patch = Instantiate(template, hit.point + hit.normal * 0.02f, Quaternion.identity, transform);
        patch.SetActive(true);

        // Measure the real mesh width at unit scale, then scale to the wanted size in meters.
        // (The FBX import and bone scale make the raw mesh size unpredictable.)
        patch.transform.localScale = Vector3.one;
        float width = MeasureWidth(patch);
        float f = width > 0.0001f ? sizeMeters / width : 1f;
        patch.transform.localScale = new Vector3(f, f * flatness, f);
        patch.transform.rotation = rot;

        Animator anim = patch.GetComponent<Animator>();
        if (anim != null)
        {
            if (animate)
            {
                // Only animate patches the player can see; vary speed so they do not move in sync.
                anim.cullingMode = AnimatorCullingMode.CullCompletely;
                anim.speed = Random.Range(0.6f, 1.2f);
            }
            else
            {
                anim.enabled = false;
            }
        }
    }
}
