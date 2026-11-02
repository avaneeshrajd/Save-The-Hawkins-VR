using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    private int _currentDeathScore;
    
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingPanel;
    [SerializeField] public GameObject resultPanel;
    [SerializeField] private GameObject lowHealthPanel;
    [SerializeField] private GameObject gun;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI scoreText2;
    [SerializeField] private TextMeshProUGUI scoreText3;
    [SerializeField] private GameObject mainPanel;


    void Awake()
    {
        Instance = this;
    }
    
    public void Onpausebtn()
    {
        Time.timeScale = 0f;
        mainPanel.SetActive(false);
        pausePanel.SetActive(true);
        gun.SetActive(false);
        scoreText2.text = _currentDeathScore.ToString();
        lowHealthPanel.SetActive(false);
    }

    public void Onhomebtn()
    {
        Time.timeScale = 1f; // un-freeze after pause/death
        SceneManager.LoadScene("Home"); // Home's StartGame.Start runs on load
    }

    public void Onresumebtn()
    {
        Time.timeScale = 1f;
        mainPanel.SetActive(true);
        pausePanel.SetActive(false);
        settingPanel.SetActive(false);
        gun.SetActive(true);
    }

    public void Onsettingsbtn()
    {
        Time.timeScale = 0f;
        mainPanel.SetActive(false);
        settingPanel.SetActive(true);
        gun.SetActive(false);
        lowHealthPanel.SetActive(false);
    }

    public void Onquitbtn()
    {
        Application.Quit();
    }

    public void OnReplayBtnClick()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Hawkin City");
    }

    public void EnemyDie(int deathScore)
    {
        _currentDeathScore += deathScore;
        scoreText.text = $"HellFire Points: {_currentDeathScore}";
    }

    public void PlayerDie()
    {
        lowHealthPanel.SetActive(false);
        resultPanel.SetActive(true);
        ShowRunResults();
        mainPanel.SetActive(false);
        gun.SetActive(false);
    }

    public int CurrentScore => _currentDeathScore;

    /// <summary>Win: all gates sealed and boss defeated.</summary>
    public void LevelComplete()
    {
        lowHealthPanel.SetActive(false);
        resultPanel.SetActive(true);
        ShowRunResults();
        // Retitle the results panel for a win.
        foreach (var t in resultPanel.GetComponentsInChildren<TMP_Text>(true))
        {
            string low = t.text.ToLowerInvariant();
            if (t != scoreText3 && (low.Contains("over") || low.Contains("dead") || low.Contains("died") || low.Contains("lost")))
                t.text = "HAWKINS SAVED!";
        }
        mainPanel.SetActive(false);
        gun.SetActive(false);
        Time.timeScale = 0f;
    }

    // Score, survival time and local high scores on the results panel.
    void ShowRunResults()
    {
        float time = Time.timeSinceLevelLoad;
        bool newBest = ScoreSystem.SaveRun(_currentDeathScore, time);
        scoreText3.enableAutoSizing = true;
        scoreText3.fontSizeMin = 4;
        scoreText3.text = $"{_currentDeathScore}\n<size=55%>TIME {ScoreSystem.FormatTime(time)}\n" +
                          (newBest ? "<color=#FFD91A>NEW BEST!</color>" : $"BEST {ScoreSystem.BestScore}") +
                          $"  LONGEST {ScoreSystem.FormatTime(ScoreSystem.BestTime)}</size>";
    }
}
