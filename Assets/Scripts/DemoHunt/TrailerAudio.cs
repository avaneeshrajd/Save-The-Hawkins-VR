using UnityEngine;

/// <summary>
/// Procedural, original trailer score and sound design (no licensed music):
/// low drone bed, pulsing synth arpeggio, BRAAM hits, sub impacts, whooshes, risers, title "ting".
/// All clips are generated once at 44.1 kHz.
/// </summary>
public static class TrailerAudio
{
    const int Rate = 44100;
    static AudioClip drone, arp, braam, impact, whoosh, ting;
    static readonly System.Collections.Generic.Dictionary<float, AudioClip> risers = new System.Collections.Generic.Dictionary<float, AudioClip>();

    public static AudioClip Drone => drone != null ? drone : (drone = MakeDrone(16f));
    public static AudioClip Arp => arp != null ? arp : (arp = MakeArp());
    public static AudioClip Braam => braam != null ? braam : (braam = MakeBraam());
    public static AudioClip Impact => impact != null ? impact : (impact = MakeImpact());
    public static AudioClip Whoosh => whoosh != null ? whoosh : (whoosh = MakeWhoosh(1.1f));
    public static AudioClip Ting => ting != null ? ting : (ting = MakeTing());

    public static AudioClip Riser(float seconds)
    {
        if (!risers.TryGetValue(seconds, out var c) || c == null) risers[seconds] = c = MakeRiser(seconds);
        return c;
    }

    static float Saw(double phase) => (float)(2.0 * (phase - System.Math.Floor(phase + 0.5)));
    static float Coef(float hz) => 1f - Mathf.Exp(-2f * Mathf.PI * hz / Rate);
    static float Tanh(float x) => (float)System.Math.Tanh(x);

    static AudioClip Clip(string name, float[] d)
    {
        // Normalise to -1 dBFS.
        float peak = 0.0001f;
        foreach (float v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
        float g = 0.89f / peak;
        for (int i = 0; i < d.Length; i++) d[i] *= g;
        var c = AudioClip.Create(name, d.Length, 1, Rate, false);
        c.SetData(d, 0);
        return c;
    }

    // Dark, slowly breathing bed (seamless loop: all LFOs complete whole cycles).
    static AudioClip MakeDrone(float seconds)
    {
        int n = (int)(seconds * Rate);
        var d = new float[n];
        float[] f = { 55f, 55.25f, 82.5f, 110.5f };
        double[] ph = new double[f.Length];
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float s = 0f;
            for (int k = 0; k < f.Length; k++) { ph[k] += f[k] / Rate; s += Saw(ph[k]) * (k == 0 ? 1f : 0.6f); }
            float cutoff = 180f + 140f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / seconds * 2f));
            lp += Coef(cutoff) * (s - lp);
            lp2 += Coef(cutoff) * (lp - lp2);
            float shimmer = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.015f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / seconds * 4f));
            d[i] = lp2 * 0.5f + shimmer + Mathf.Sin(2f * Mathf.PI * 41.25f * t) * 0.25f;
        }
        return Clip("TrailerDrone", d);
    }

    // Original minor-key 16th-note synth pulse, 112 BPM, 4 bars (Am - Am - F - G), loops.
    static AudioClip MakeArp()
    {
        const float bpm = 112f;
        float step = 60f / bpm / 4f;
        int steps = 64;
        int n = (int)(step * steps * Rate);
        var d = new float[n];
        // Semitones from A3 (220 Hz) per chord, 4-note up/down pattern.
        int[][] chords = { new[] { 0, 3, 7, 12 }, new[] { 0, 3, 7, 12 }, new[] { -4, 0, 3, 8 }, new[] { -2, 2, 5, 10 } };
        int[] order = { 0, 1, 2, 3, 2, 1, 2, 3, 0, 1, 2, 3, 2, 3, 1, 2 };
        int[] bassRoot = { 0, 0, -4, -2 };
        float lp = 0f;
        for (int st = 0; st < steps; st++)
        {
            int bar = st / 16;
            int semis = chords[bar][order[st % 16]];
            float freq = 220f * Mathf.Pow(2f, semis / 12f);
            float bassF = 55f * Mathf.Pow(2f, bassRoot[bar] / 12f);
            int start = (int)(st * step * Rate);
            int len = Mathf.Min(n - start, (int)(step * 2.5f * Rate));
            double p1 = 0, p2 = 0, pb = 0;
            for (int j = 0; j < len; j++)
            {
                float t = (float)j / Rate;
                p1 += freq / Rate; p2 += freq * 1.007f / Rate; pb += bassF / Rate;
                float env = Mathf.Exp(-t * 9f) * Mathf.Clamp01(t * 400f);
                float pluck = (Saw(p1) + Saw(p2)) * 0.5f * env;
                float bass = (st % 4 == 0) ? Saw(pb) * Mathf.Exp(-t * 5f) * Mathf.Clamp01(t * 300f) * 0.9f : 0f;
                d[(start + j) % n] += pluck * 0.6f + bass;
            }
        }
        for (int i = 0; i < n; i++) { lp += Coef(1400f) * (d[i] - lp); d[i] = Tanh(lp * 1.4f); }
        return Clip("TrailerArp", d);
    }

    // Huge brassy "BRAAM": detuned low saws, filter swells open then closes, saturated.
    static AudioClip MakeBraam()
    {
        float seconds = 4.5f;
        int n = (int)(seconds * Rate);
        var d = new float[n];
        float[] f = { 55f, 55.4f, 54.6f, 110f, 110.7f, 82.4f, 27.5f };
        double[] ph = new double[f.Length];
        float lp = 0f, lp2 = 0f;
        var rng = new System.Random(7);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float s = 0f;
            for (int k = 0; k < f.Length; k++) { ph[k] += f[k] / Rate; s += Saw(ph[k]); }
            float cut = 120f + 2200f * Mathf.Clamp01(t / 0.12f) * Mathf.Exp(-t * 1.1f);
            lp += Coef(cut) * (s - lp);
            lp2 += Coef(cut) * (lp - lp2);
            float env = Mathf.Clamp01(t / 0.04f) * Mathf.Exp(-t * 0.75f) * Mathf.Clamp01((seconds - t) / 0.5f);
            float noise = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 12f) * 0.4f;
            d[i] = Tanh((lp2 * 0.6f + noise) * 2.2f) * env;
        }
        return Clip("TrailerBraam", d);
    }

    // Cinematic hit: sub-bass drop + noise crack + long rumble tail.
    static AudioClip MakeImpact()
    {
        float seconds = 3.5f;
        int n = (int)(seconds * Rate);
        var d = new float[n];
        var rng = new System.Random(11);
        double ph = 0;
        float lp = 0f, lpT = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float f = 32f + 90f * Mathf.Exp(-t * 4f);
            ph += f / Rate;
            float sub = Mathf.Sin((float)(2 * System.Math.PI * ph)) * Mathf.Exp(-t * 1.4f);
            float w = (float)(rng.NextDouble() * 2 - 1);
            lp += Coef(3500f) * (w - lp);
            lpT += Coef(160f) * (w - lpT);
            float crack = lp * Mathf.Exp(-t * 18f);
            float tail = lpT * 3f * Mathf.Exp(-t * 1.2f);
            d[i] = Tanh((sub * 1.2f + crack * 0.8f + tail * 0.6f) * 1.6f) * Mathf.Clamp01(t * 2000f);
        }
        return Clip("TrailerImpact", d);
    }

    // Air whoosh that swells and peaks near the end (cut / title reveal).
    static AudioClip MakeWhoosh(float seconds)
    {
        int n = (int)(seconds * Rate);
        var d = new float[n];
        var rng = new System.Random(3);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float k = t / seconds;
            float shape = k < 0.8f ? Mathf.Pow(k / 0.8f, 2.5f) : Mathf.Pow(1f - (k - 0.8f) / 0.2f, 1.5f);
            float cut = 300f + 4000f * shape;
            float w = (float)(rng.NextDouble() * 2 - 1);
            lp += Coef(cut) * (w - lp);
            lp2 += Coef(cut * 0.5f) * (lp - lp2);
            d[i] = (lp - lp2 * 0.5f) * shape;
        }
        return Clip("TrailerWhoosh", d);
    }

    // Tension riser: noise + rising tone, crescendo that stops dead at the end.
    static AudioClip MakeRiser(float seconds)
    {
        int n = (int)(seconds * Rate);
        var d = new float[n];
        var rng = new System.Random(5);
        double p1 = 0, p2 = 0;
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float k = t / seconds;
            float f = 120f * Mathf.Pow(14f, k);
            p1 += f / Rate; p2 += f * 1.5f / Rate;
            float w = (float)(rng.NextDouble() * 2 - 1);
            lp += Coef(400f + 7000f * k * k) * (w - lp);
            float tone = (Saw(p1) * 0.5f + Saw(p2) * 0.3f) * 0.5f;
            float trem = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * (4f + 18f * k) * t);
            d[i] = (lp * 0.8f + tone) * k * k * trem * Mathf.Clamp01((seconds - t) / 0.02f);
        }
        return Clip("TrailerRiser", d);
    }

    // Metallic "ting" for text reveals.
    static AudioClip MakeTing()
    {
        float seconds = 2.5f;
        int n = (int)(seconds * Rate);
        var d = new float[n];
        float[] f = { 1318.5f, 1975f, 2793f, 659.3f };
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float s = 0f;
            for (int k = 0; k < f.Length; k++) s += Mathf.Sin(2f * Mathf.PI * f[k] * t) * Mathf.Exp(-t * (1.5f + k)) / (k + 1);
            d[i] = s * Mathf.Clamp01(t * 3000f);
        }
        return Clip("TrailerTing", d);
    }
}
