using TMPro;
using UnityEngine;

public enum PickupType { Health, RapidFire, Firework }

/// <summary>
/// Floating power-up dropped by Demogorgons. Collect by shooting it or touching it
/// with your head or hands. Health = +40 HP, Rapid Fire = 10 s full-auto,
/// Firework = explosion that damages every Demogorgon nearby.
/// </summary>
public class Pickup : MonoBehaviour
{
    const float Lifetime = 15f, TouchRadius = 1.3f;
    PickupType type;
    float age;
    Vector3 basePos;
    Transform visual, cam;
    TextMeshPro label;
    bool collected;

    public static void TryDrop(Vector3 position, float chance)
    {
        if (Random.value > chance) return;
        float r = Random.value;
        PickupType t = r < 0.45f ? PickupType.Health : r < 0.75f ? PickupType.RapidFire : PickupType.Firework;
        Spawn(t, position);
    }

    public static Pickup Spawn(PickupType type, Vector3 position)
    {
        GameObject go = new GameObject("Pickup " + type);
        go.transform.position = position;
        Pickup p = go.AddComponent<Pickup>();
        p.type = type;
        return p;
    }

    static Color ColorOf(PickupType t) => t == PickupType.Health ? new Color(0.2f, 1f, 0.3f)
        : t == PickupType.RapidFire ? new Color(1f, 0.85f, 0.1f) : new Color(1f, 0.2f, 0.9f);

    void Start()
    {
        basePos = transform.position;
        cam = Camera.main != null ? Camera.main.transform : null;
        Color c = ColorOf(type);
        visual = GameFx.GlowPrimitive(type == PickupType.Health ? PrimitiveType.Cube : PrimitiveType.Sphere, c, Vector3.one * 0.3f, transform).transform;
        var col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.35f;
        label = GameHUD.CreateText("Label", 0.7f, c, new Vector2(1.5f, 0.4f), transform);
        label.transform.localPosition = Vector3.up * 0.45f;
        label.text = type == PickupType.Health ? "+HEALTH" : type == PickupType.RapidFire ? "RAPID FIRE" : "FIREWORK";
    }

    void Update()
    {
        age += Time.deltaTime;
        transform.position = basePos + Vector3.up * Mathf.Sin(age * 2f) * 0.1f;
        visual.Rotate(40f * Time.deltaTime, 90f * Time.deltaTime, 0f);
        visual.gameObject.SetActive(age < Lifetime - 4f || Mathf.Repeat(age * 6f, 1f) > 0.4f); // blink before vanishing
        GameFx.Billboard(label.transform, cam);
        if (age > Lifetime) { Destroy(gameObject); return; }

        if (cam != null && (cam.position - transform.position).magnitude < TouchRadius) Collect();
        else if (VRGun.Instance != null)
        {
            foreach (Transform hand in VRGun.Instance.HandTransforms())
                if (hand != null && (hand.position - transform.position).magnitude < 0.4f) { Collect(); break; }
        }
    }

    /// <summary>Called by Bullet or by touch.</summary>
    public void Collect()
    {
        if (collected) return;
        collected = true;
        GameFx.Play2D(GameFx.Beep, 0.6f, 1.5f);
        GameFx.HapticBoth(0.4f, 0.1f);

        switch (type)
        {
            case PickupType.Health:
                var ph = FindFirstObjectByType<PlayerHealth>();
                if (ph != null) ph.Heal(40f);
                GameHUD.Radio("Health pack! Patch yourself up.");
                break;
            case PickupType.RapidFire:
                if (VRGun.Instance != null) VRGun.Instance.ActivateRapidFire(10f);
                GameHUD.Radio("Rapid fire! Hold the trigger!");
                break;
            case PickupType.Firework:
                Explode(transform.position, 9f, 120f);
                GameHUD.Radio("Fireworks! Light 'em up!");
                break;
        }
        Destroy(gameObject);
    }

    public static void Explode(Vector3 center, float radius, float damage)
    {
        GameFx.Burst(center, 28, 7f, 1.4f, Color.red, Color.yellow, Color.cyan, Color.magenta, Color.white);
        GameFx.Play3D(GameFx.Boom, center, 1f, 1f, 60f);
        GameFx.HapticBoth(1f, 0.3f);
        foreach (DemogorgonAI d in DemogorgonAI.All.ToArray())
        {
            if (d == null || (d.transform.position - center).magnitude > radius) continue;
            d.Stun(1.6f);
            d.PlayAnim("Stun", 0.05f);
            if (d.TryGetComponent(out DemogorgonHealth h)) h.TakeDamage(damage);
        }
    }
}
