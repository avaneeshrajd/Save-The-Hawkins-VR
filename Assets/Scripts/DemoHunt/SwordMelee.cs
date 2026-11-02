using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Neon sword held in the hand that is not holding the gun. Swing it (hand speed
/// above a threshold) to slash Demogorgons and bats. Follows hand swaps.
/// </summary>
public class SwordMelee : MonoBehaviour
{
    const float Length = 0.9f, MinSwingSpeed = 1.8f, Damage = 50f, HitCooldown = 0.4f;
    Transform model;
    Vector3 lastPos;
    float speed;
    readonly Dictionary<Object, float> lastHit = new Dictionary<Object, float>();

    public static SwordMelee Create(GameObject swordModel, Material material, Texture2D texture, bool flip)
    {
        if (swordModel == null || VRGun.Instance == null) return null;
        if (material == null && texture != null)
        {
            // Crystal look: textured, glossy, with a cyan glow.
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 0.85f);
            material.EnableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", texture);
            material.SetColor("_EmissionColor", new Color(0.3f, 0.8f, 1f) * 1.5f);
        }
        var root = new GameObject("Neon Sword");
        var sword = root.AddComponent<SwordMelee>();
        sword.Build(swordModel, material, flip);
        VRGun.Instance.HandChanged += sword.AttachToFreeHand;
        sword.AttachToFreeHand();
        return sword;
    }

    void Build(GameObject swordModel, Material material, bool flip)
    {
        var m = Instantiate(swordModel, transform);
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.identity;
        foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (var r in m.GetComponentsInChildren<Renderer>())
        {
            if (material != null) r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Align longest axis with the controller's forward, hilt in the hand.
        Bounds b = LocalBounds(m);
        Vector3 s = b.size;
        Quaternion align = s.x >= s.y && s.x >= s.z ? Quaternion.FromToRotation(Vector3.right, Vector3.forward)
            : s.y >= s.z ? Quaternion.FromToRotation(Vector3.up, Vector3.forward) : Quaternion.identity;
        if (flip) align = Quaternion.AngleAxis(180f, Vector3.up) * align;
        m.transform.localRotation = align;
        b = LocalBounds(m);
        float f = Length / Mathf.Max(0.0001f, b.size.z);
        m.transform.localScale *= f;
        b = LocalBounds(m);
        m.transform.localPosition -= new Vector3(b.center.x, b.center.y, b.min.z + 0.08f); // hilt end ~8 cm behind grip
        model = m.transform;

        var col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0f, 0f, Length * 0.55f);
        col.size = new Vector3(0.08f, 0.08f, Length * 0.8f);
        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    Bounds LocalBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(transform.InverseTransformPoint(rs.Length > 0 ? rs[0].bounds.center : go.transform.position), Vector3.zero);
        foreach (var r in rs)
        {
            Bounds wb = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                b.Encapsulate(transform.InverseTransformPoint(corner));
            }
        }
        return b;
    }

    void AttachToFreeHand()
    {
        Transform hand = VRGun.Instance != null ? VRGun.Instance.FreeHand : null;
        gameObject.SetActive(hand != null);
        if (hand == null) return;
        transform.SetParent(hand, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
        lastPos = transform.position;
    }

    void Update()
    {
        Vector3 tip = transform.TransformPoint(0f, 0f, Length * 0.7f);
        speed = Mathf.Lerp(speed, (tip - lastPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f), 0.5f);
        lastPos = tip;
    }

    void OnTriggerEnter(Collider other) => TryHit(other);
    void OnTriggerStay(Collider other) => TryHit(other);

    void TryHit(Collider other)
    {
        if (speed < MinSwingSpeed) return;
        var enemy = other.GetComponentInParent<DemogorgonHealth>();
        var bat = enemy == null ? other.GetComponentInParent<BatFlyer>() : null;
        Object key = enemy != null ? enemy : (Object)bat;
        if (key == null) return;
        if (lastHit.TryGetValue(key, out float t) && Time.time - t < HitCooldown) return;
        lastHit[key] = Time.time;

        if (enemy != null)
        {
            enemy.TakeDamage(Damage * Mathf.Clamp(speed / 3f, 1f, 2f));
            if (enemy.TryGetComponent(out DemogorgonAI ai)) ai.Stun(0.4f);
        }
        else bat.Hit();

        GameFx.Burst(other.ClosestPoint(transform.position), 8, 3f, 0.4f, Color.cyan, Color.magenta);
        GameFx.Haptic(VRGun.Instance != null ? VRGun.Instance.FreeHandNode : UnityEngine.XR.XRNode.LeftHand, 1f, 0.12f);
    }

    void OnDestroy()
    {
        if (VRGun.Instance != null) VRGun.Instance.HandChanged -= AttachToFreeHand;
    }
}
