using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Objective gate. A pulsing Upside Down portal with a red light beam visible across the
/// city. Shoot it to seal it. Demogorgons spawn near open gates.
/// </summary>
public class UpsideDownGate : MonoBehaviour
{
    public float maxHealth = 600f;
    public event Action<UpsideDownGate> Sealed;
    public bool IsSealed { get; private set; }
    public int Index { get; set; }

    float health;
    Transform visual, cam, beam;
    TextMeshPro label;
    Light glow;
    Vector3 visualScale;

    public static UpsideDownGate Create(Vector3 pos, GameObject portalTemplate, float hp, int index)
    {
        GameObject go = new GameObject("Upside Down Gate " + (index + 1));
        go.transform.position = pos;
        var g = go.AddComponent<UpsideDownGate>();
        g.maxHealth = hp;
        g.Index = index;
        g.Build(portalTemplate);
        return g;
    }

    void Build(GameObject template)
    {
        health = maxHealth;
        cam = Camera.main != null ? Camera.main.transform : null;

        if (template != null)
        {
            var v = Instantiate(template, transform.position + Vector3.up * 0.05f, Quaternion.identity, transform);
            v.SetActive(true);
            v.transform.localScale = Vector3.one;
            Bounds b = new Bounds(v.transform.position, Vector3.zero);
            foreach (var r in v.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            float w = Mathf.Max(b.size.x, b.size.z, 0.001f);
            float f = 4f / w; // 4 m wide gate
            visualScale = new Vector3(f, f * 0.35f, f);
            v.transform.localScale = visualScale;
            visual = v.transform;
        }

        beam = GameFx.GlowPrimitive(PrimitiveType.Cylinder, new Color(1f, 0.1f, 0.05f, 0.35f), new Vector3(0.5f, 20f, 0.5f), transform).transform;
        beam.localPosition = Vector3.up * 20f;
        glow = GameFx.CreateLight(transform, Vector3.up * 2f, new Color(1f, 0.15f, 0.1f), 9f, 3f);

        var col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = Vector3.up * 1.2f;
        col.size = new Vector3(4f, 2.4f, 4f);

        label = GameHUD.CreateText("Gate Label", 4f, new Color(1f, 0.3f, 0.2f), new Vector2(6f, 2f), transform);
        label.transform.localPosition = Vector3.up * 4.5f;
        UpdateLabel();
    }

    void Update()
    {
        if (IsSealed) return;
        float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.06f;
        if (visual != null)
        {
            visual.localScale = visualScale * pulse;
            visual.Rotate(0f, 15f * Time.deltaTime, 0f);
        }
        glow.intensity = 2.5f + Mathf.Sin(Time.time * 5f) * 1f;
        GameFx.Billboard(label.transform, cam);
        // Keep the label readable from far away.
        if (cam != null)
            label.transform.localScale = Vector3.one * Mathf.Clamp((cam.position - transform.position).magnitude / 15f, 0.6f, 2.5f);
    }

    public void TakeDamage(float dmg)
    {
        if (IsSealed) return;
        health -= dmg;
        GameFx.Haptic(VRGun.Instance != null ? VRGun.Instance.HandNode : UnityEngine.XR.XRNode.RightHand, 0.2f, 0.04f);
        if (health <= 0f) StartCoroutine(Seal());
        else UpdateLabel();
    }

    void UpdateLabel() => label.text = $"GATE {Index + 1}\n<size=60%>SEAL {Mathf.RoundToInt((1f - health / maxHealth) * 100f)}%  - SHOOT IT!</size>";

    IEnumerator Seal()
    {
        IsSealed = true;
        GetComponent<Collider>().enabled = false;
        label.text = $"GATE {Index + 1}\n<size=60%><color=#7CFF7C>SEALED</color></size>";
        GameFx.Burst(transform.position + Vector3.up, 24, 6f, 1.2f, Color.white, new Color(1f, 0.3f, 0.2f));
        GameFx.Play3D(GameFx.Boom, transform.position, 1f, 0.7f, 80f);
        Sealed?.Invoke(this);

        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            float k = 1f - t / 1.5f;
            if (visual != null) visual.localScale = visualScale * k;
            beam.localScale = new Vector3(0.5f * k, 20f, 0.5f * k);
            glow.intensity = 3f * k;
            yield return null;
        }
        if (visual != null) Destroy(visual.gameObject);
        Destroy(beam.gameObject);
        Destroy(glow.gameObject);
        yield return new WaitForSeconds(3f);
        Destroy(label.gameObject);
    }
}
