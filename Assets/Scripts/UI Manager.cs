using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    public GameObject pausePanel;
    public GameObject settingPanel;
    public GameObject resultPanel;
    public GameObject gun;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI scoreText2;
    public TextMeshProUGUI scoreText3;
    public GameObject mainPanel;
    
    private int _currentDeathScore;


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
    }

    public void Onhomebtn()
    {
        SceneManager.LoadScene("Home");
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
        resultPanel.SetActive(true);
        scoreText3.text = _currentDeathScore.ToString();
        mainPanel.SetActive(false);
        gun.SetActive(false);
    }
}
