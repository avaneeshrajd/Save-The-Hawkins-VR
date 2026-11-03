using UnityEngine;

/// <summary>
/// Dense white Upside Down spores floating all over the city. One particle system
/// (single draw call) whose spawn volume follows the player, so the air is thick with
/// drifting spores wherever they go. Particles live in world space and drift/swirl slowly.
/// </summary>
public class UpsideDownSpores : MonoBehaviour
{
    [SerializeField] private float rate = 450f;
    [SerializeField] private int maxParticles = 4000;
    [SerializeField] private Vector3 area = new Vector3(60f, 18f, 60f);
    [SerializeField] private Vector2 size = new Vector2(0.015f, 0.05f);

    Transform cam, spores;

    void Start()
    {
        cam = Camera.main != null ? Camera.main.transform : null;

        // Own root object: moving it must not drag the bats/growth on this object along.
        var go = new GameObject("Upside Down Spores");
        spores = go.transform;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 10f;
        main.loop = true;
        main.prewarm = true; // already full when the level starts
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.6f), new Color(1f, 0.92f, 0.9f, 1f));
        main.gravityModifier = -0.004f; // float upward very slowly
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;
        main.scalingMode = ParticleSystemScalingMode.Shape;

        var emission = ps.emission;
        emission.rateOverTime = rate;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = area;
        shape.position = new Vector3(0f, area.y * 0.5f - 1.5f, 0f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.15f;
        noise.quality = ParticleSystemNoiseQuality.Low;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.maxParticleSize = 0.006f; // spores right at the eye never turn into big blobs
        var mat = new Material(Shader.Find("Sprites/Default")); // always included in builds
        mat.mainTexture = SoftDot();
        r.sharedMaterial = mat;

        Follow();
        ps.Play();
    }

    void LateUpdate() => Follow();

    void Follow()
    {
        if (cam != null && spores != null) spores.position = cam.position;
    }

    // Small radial-gradient texture so each spore is a soft glowing dot.
    static Texture2D SoftDot()
    {
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f - 0.5f, n / 2f - 0.5f)) / (n / 2f);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        return tex;
    }
}
