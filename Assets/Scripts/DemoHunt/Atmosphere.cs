using System.Collections;
using UnityEngine;

/// <summary>
/// Horror atmosphere: gun flashlight that flickers when a Demogorgon is within 15 m,
/// random lightning with thunder, and a heartbeat that speeds up as Demogorgons close in.
/// </summary>
public class Atmosphere : MonoBehaviour
{
    const float FlickerRange = 15f, HeartFar = 20f, HeartNear = 3f;
    Light flashlight, sun;
    AudioSource heart;
    Transform cam;
    float flashBase = 2.5f, nextFlicker;
    Color sunColor;
    float sunIntensity;
    float blackoutUntil;
    static Atmosphere instance;

    /// <summary>Kills the flashlight briefly (jump scares).</summary>
    public static void Blackout(float seconds)
    {
        if (instance != null) instance.blackoutUntil = Time.time + seconds;
    }

    public static Atmosphere Create(AudioClip heartbeat)
    {
        var a = new GameObject("Atmosphere").AddComponent<Atmosphere>();
        a.Init(heartbeat);
        return a;
    }

    void Init(AudioClip heartbeat)
    {
        instance = this;
        cam = Camera.main != null ? Camera.main.transform : null;

        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sun = l; break; }
        if (sun != null) { sunColor = sun.color; sunIntensity = sun.intensity; }

        if (heartbeat != null)
        {
            heart = gameObject.AddComponent<AudioSource>();
            heart.clip = heartbeat;
            heart.loop = true;
            heart.volume = 0f;
            heart.spatialBlend = 0f;
            heart.Play();
        }
        StartCoroutine(LightningLoop());
    }

    void Update()
    {
        if (flashlight == null && VRGun.Instance != null && VRGun.Instance.FirePoint != null)
        {
            flashlight = GameFx.CreateLight(VRGun.Instance.FirePoint, Vector3.zero, new Color(1f, 0.95f, 0.8f), 28f, flashBase, LightType.Spot);
            flashlight.spotAngle = 45f;
            flashlight.innerSpotAngle = 25f;
        }

        float nearest = cam != null ? DemogorgonAI.NearestDistance(cam.position) : float.MaxValue;

        if (flashlight != null && Time.time < blackoutUntil)
            flashlight.intensity = 0f;
        else if (flashlight != null)
        {
            if (nearest < FlickerRange && Time.time >= nextFlicker)
            {
                // Closer Demogorgon = faster, harsher flicker.
                float danger = 1f - nearest / FlickerRange;
                flashlight.intensity = Random.value < 0.25f + danger * 0.4f ? 0f : flashBase * Random.Range(0.3f, 1f);
                nextFlicker = Time.time + Random.Range(0.03f, 0.2f) * (1.2f - danger);
            }
            else if (nearest >= FlickerRange)
                flashlight.intensity = flashBase;
        }

        if (heart != null)
        {
            float v = Mathf.InverseLerp(HeartFar, HeartNear, nearest);
            heart.volume = Mathf.Lerp(heart.volume, v * 0.9f, 3f * Time.deltaTime);
            heart.pitch = Mathf.Lerp(0.85f, 1.7f, v);
        }
    }

    IEnumerator LightningLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(20f, 45f));
            Color flash = new Color(0.75f, 0.8f, 1f);
            for (int i = 0; i < Random.Range(2, 4); i++)
            {
                SetSun(flash, 1.6f);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.12f));
                SetSun(sunColor, sunIntensity);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
            }
            yield return new WaitForSeconds(Random.Range(0.4f, 1.4f));
            GameFx.Play2D(GameFx.Thunder, 0.8f, Random.Range(0.8f, 1.1f));
            GameFx.HapticBoth(0.25f, 0.4f);
        }
    }

    void SetSun(Color c, float intensity)
    {
        if (sun == null) return;
        sun.color = c;
        sun.intensity = intensity;
    }
}
