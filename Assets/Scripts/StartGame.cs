using UnityEngine;
using UnityEngine.SceneManagement;

public class StartGame : MonoBehaviour
{
    public GameObject upsideDownGate;
    public GameObject btnPanel;
    public GameObject gameTitle;
    public GameObject upsideDownTitle;

    public void StartButton()
    { 
        btnPanel.SetActive(false);
        gameTitle.SetActive(false);
        upsideDownTitle.SetActive(true);
        upsideDownGate.SetActive(true);
    }

    public void EnterUpsideDown()
    {
        SceneManager.LoadScene("Hawkin City");
    }

    public void quitbtn()
    {
        Application.Quit();
    }
}
