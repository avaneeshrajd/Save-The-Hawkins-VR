using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
public class UIManager : MonoBehaviour
{
    public GameObject pauseBtn;
    public GameObject pausePanel;
    public GameObject homeBtn;
    public GameObject resumeBtn;
    public GameObject resumeBtn2;
    public GameObject quitBtn;
    public GameObject settingsBtn;
    public GameObject settingPanel;
    public GameObject sliderBar;
    public GameObject gun;
    public TextMeshProUGUI scoreText;
    public GameObject mainPanel;

    public void Onpausebtn()
    {
        Time.timeScale = 0f;
        mainPanel.SetActive(false);
        pausePanel.SetActive(true);
        gun.SetActive(false);
        scoreText.text = DemogorgonHealth.Instance.ScoreTxt();
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
}
