using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Shared helpers for Demo Hunt: haptics, glow objects, lights, tinting,
/// and procedurally generated sounds (radio static, thunder, clicks, booms).
/// Everything here is created at runtime, so no extra assets are needed.
/// </summary>
public static class GameFx
{
    static Shader unlitShader;
    static readonly Dictionary<Color, Material> unlitCache = new Dictionary<Color, Material>();
    static readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    // ---------- Haptics ----------

    public static void Haptic(XRNode node, float amplitude, float duration)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (device.isValid)
            device.SendHapticImpulse(0, Mathf.Clamp01(amplitude), duration);
    }

    public static void HapticBoth(float amplitude, float duration)
    {
        Haptic(XRNode.LeftHand, amplitude, duration);
        Haptic(XRNode.RightHand, amplitude, duration);
    }

    // ---------- Visuals ----------

    /// <summary>Unlit material (Sprites/Default is always included in builds).</summary>
    public static Material Unlit(Color color)
    {
        if (unlitCache.TryGetValue(color, out Material m) && m != null)
            return m;
        if (unlitShader == null)
            unlitShader = Shader.Find("Sprites/Default");
        m = new Material(unlitShader) { color = color };
        unlitCache[color] = m;
        return m;
    }

    public static GameObject GlowPrimitive(PrimitiveType type, Color color, Vector3 scale, Transform parent = null, bool keepCollider = false)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        if (!keepCollider)
            Object.Destroy(go.GetComponent<Collider>());
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = Unlit(color);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (parent != null)
            go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        return go;
    }

    public static Light CreateLight(Transform parent, Vector3 localPos, Color color, float range, float intensity, LightType type = LightType.Point)
    {
        GameObject go = new GameObject(type + " Light");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        Light l = go.AddComponent<Light>();
        l.type = type;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        return l;
    }

    /// <summary>Tints every renderer under go. emission adds glow on Lit materials.</summary>
    public static void SetTint(GameObject go, Color color, float emission = 0f)
    {
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (r.GetComponentInParent<Canvas>() != null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, color);
            mpb.SetColor(ColorId, color);
            mpb.SetColor(EmissionId, color * emission);
            r.SetPropertyBlock(mpb);
        }
    }

    /// <summary>Faces a transform toward the camera (for floating text).</summary>
    public static void Billboard(Transform t, Transform cam)
    {
        if (cam == null) return;
        Vector3 dir = t.position - cam.position;
        if (dir.sqrMagnitude > 0.0001f)
            t.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    /// <summary>Colourful burst of glowing bits plus a light flash (fireworks / explosions).</summary>
    public static void Burst(Vector3 position, int count, float speed, float lifetime, params Color[] colors)
    {
        GameObject host = new GameObject("Burst");
        host.transform.position = position;
        host.AddComponent<BurstFx>().Init(count, speed, lifetime, colors);
    }

    // ---------- Audio ----------

    static AudioClip staticClip, thunderClip, clickClip, boomClip, beepClip;

    public static AudioClip RadioStatic => staticClip != null ? staticClip : (staticClip = MakeNoise("RadioStatic", 0.35f, 0.6f, 4f, 0.25f));
    public static AudioClip Thunder => thunderClip != null ? thunderClip : (thunderClip = MakeNoise("Thunder", 3.2f, 0.03f, 1.2f, 0.9f));
    public static AudioClip EmptyClick => clickClip != null ? clickClip : (clickClip = MakeNoise("Click", 0.05f, 0.9f, 60f, 0.5f));
    public static AudioClip Boom => boomClip != null ? boomClip : (boomClip = MakeNoise("Boom", 1.2f, 0.08f, 3f, 1f));
    public static AudioClip Beep => beepClip != null ? beepClip : (beepClip = MakeTone("Beep", 0.12f, 1200f, 0.3f));

    /// <summary>Filtered noise with exponential decay. lowpass 0..1 (lower = deeper).</summary>
    static AudioClip MakeNoise(string name, float seconds, float lowpass, float decay, float volume)
    {
        const int rate = 22050;
        int n = Mathf.CeilToInt(seconds * rate);
        float[] data = new float[n];
        System.Random rng = new System.Random(name.GetHashCode());
        float y = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            y += lowpass * (white - y);
            float t = (float)i / rate;
            float env = Mathf.Exp(-decay * t) * Mathf.Clamp01(t * 200f);
            // A little crackle for thunder/static
            if (rng.NextDouble() < 0.002) y += (float)(rng.NextDouble() - 0.5) * 2f;
            data[i] = Mathf.Clamp(y * env * volume * (lowpass < 0.2f ? 6f : 1.5f), -1f, 1f);
        }
        AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip MakeTone(string name, float seconds, float freq, float volume)
    {
        const int rate = 22050;
        int n = Mathf.CeilToInt(seconds * rate);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * volume * (1f - t / seconds);
        }
        AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Plays a clip without position (radio, UI). Works while paused/time stopped.</summary>
    public static AudioSource Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return null;
        GameObject go = new GameObject("2D Sound " + clip.name);
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch;
        src.spatialBlend = 0f;
        src.ignoreListenerPause = true;
        src.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        return src;
    }

    public static void Play3D(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float maxDistance = 40f)
    {
        if (clip == null) return;
        GameObject go = new GameObject("3D Sound " + clip.name);
        go.transform.position = position;
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 2f;
        src.maxDistance = maxDistance;
        src.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
    }
}

/// <summary>Runtime particle-like burst made of small glowing spheres (cheap, no assets).</summary>
public class BurstFx : MonoBehaviour
{
    Transform[] bits;
    Vector3[] velocities;
    float lifetime, age;
    Light flash;

    public void Init(int count, float speed, float life, Color[] colors)
    {
        lifetime = life;
        bits = new Transform[count];
        velocities = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            Color c = colors.Length > 0 ? colors[i % colors.Length] : Color.white;
            bits[i] = GameFx.GlowPrimitive(PrimitiveType.Sphere, c, Vector3.one * 0.12f, transform).transform;
            velocities[i] = Random.onUnitSphere * speed * Random.Range(0.5f, 1f) + Vector3.up * speed * 0.3f;
        }
        flash = GameFx.CreateLight(transform, Vector3.zero, colors.Length > 0 ? colors[0] : Color.white, 12f, 6f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float k = 1f - age / lifetime;
        for (int i = 0; i < bits.Length; i++)
        {
            velocities[i] += Physics.gravity * 0.4f * Time.deltaTime;
            bits[i].localPosition += velocities[i] * Time.deltaTime;
            bits[i].localScale = Vector3.one * 0.12f * Mathf.Max(0f, k);
        }
        if (flash != null)
            flash.intensity = Mathf.Max(0f, 6f * (1f - age / 0.35f));
        if (age >= lifetime)
            Destroy(gameObject);
    }
}
