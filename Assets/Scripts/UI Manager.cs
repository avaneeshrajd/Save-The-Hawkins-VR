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
        SceneManager.LoadScene("Home");
        StartGame.Instance.Start();
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
        scoreText3.text = _currentDeathScore.ToString();
        mainPanel.SetActive(false);
        gun.SetActive(false);
    }
}
