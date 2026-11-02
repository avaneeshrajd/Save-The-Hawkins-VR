using UnityEngine;

/// <summary>Kill combo multiplier, survival time and local high scores.</summary>
public static class ScoreSystem
{
    public const float ComboWindow = 3f;
    public const int MaxMultiplier = 4;
    static int combo;
    static float lastKillTime = -99f;

    public static int Multiplier => Time.time - lastKillTime <= ComboWindow ? Mathf.Clamp(combo, 1, MaxMultiplier) : 1;
    public static float ComboTimeLeft => Mathf.Max(0f, ComboWindow - (Time.time - lastKillTime));

    public static void ResetRun() { combo = 0; lastKillTime = -99f; }

    /// <summary>Adds points with the current combo multiplier. Returns points given.</summary>
    public static int AddKill(int basePoints)
    {
        combo = Time.time - lastKillTime <= ComboWindow ? combo + 1 : 1;
        lastKillTime = Time.time;
        int points = basePoints * Multiplier;
        if (UIManager.Instance != null) UIManager.Instance.EnemyDie(points);
        return points;
    }

    public static int BestScore => PlayerPrefs.GetInt("DH_BestScore", 0);
    public static float BestTime => PlayerPrefs.GetFloat("DH_BestTime", 0f);

    /// <summary>Saves score/time if they beat the records. Returns true if new best score.</summary>
    public static bool SaveRun(int score, float seconds)
    {
        bool newBest = score > BestScore;
        if (newBest) PlayerPrefs.SetInt("DH_BestScore", score);
        if (seconds > BestTime) PlayerPrefs.SetFloat("DH_BestTime", seconds);
        PlayerPrefs.Save();
        return newBest;
    }

    public static string FormatTime(float s) => $"{(int)s / 60}:{(int)s % 60:00}";
}
