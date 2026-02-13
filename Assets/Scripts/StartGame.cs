using UnityEngine;
using UnityEngine.SceneManagement;

public class StartGame : MonoBehaviour
{
    public GameObject upsideDownGate;
    public GameObject btnPanel;
    public GameObject gameTitle;
    public GameObject upsideDownTitle;
    public GameObject modesPanel;
    public GameObject introPanel;
    public GameObject storylinePanel;
    public GameObject firstStoryPanel;
    public GameObject secondStoryPanel;
    public GameObject thirdStoryPanel;
    public GameObject fourthStoryPanel;
    public GameObject fifthStoryPanel;


    public void StartButton()
    { 
        btnPanel.SetActive(false);
        gameTitle.SetActive(false);
        modesPanel.SetActive(true);
    }

    public void OnDemoHuntClick()
    {
        modesPanel.SetActive(false);
        upsideDownTitle.SetActive(true);
        upsideDownGate.SetActive(true);
    }

    public void OnBackBtnClick()
    {
        modesPanel.SetActive(false);
        btnPanel.SetActive(true);
        gameTitle.SetActive(true);
        
    }

    public void EnterUpsideDown()
    {
        SceneManager.LoadScene("Hawkin City");
    }

    public void quitbtn()
    {
        Application.Quit();
    }

    public void OnNextbtnClick()
    {
        introPanel.SetActive(false);
        storylinePanel.SetActive(true);
    }

    public void OnNextbtn2Click()
    {
        firstStoryPanel.SetActive(false);
        secondStoryPanel.SetActive(true);
    }
    
    public void OnNextbtn3Click()
    {
        secondStoryPanel.SetActive(false);
        thirdStoryPanel.SetActive(true);
    }

    public void OnNextbtn4Click()
    {
        thirdStoryPanel.SetActive(false);
        fourthStoryPanel.SetActive(true);
    }

    public void OnNextbtn5Click()
    {
        fourthStoryPanel.SetActive(false);
        fifthStoryPanel.SetActive(true);
    }

    public void OnNextbtn6Click()
    {
        fifthStoryPanel.SetActive(false);
        storylinePanel.SetActive(false);
        gameTitle.SetActive(true);
        btnPanel.SetActive(true);
    }

    public void OnBackBtn2Click()
    {
        introPanel.SetActive(true);
        storylinePanel.SetActive(false);
    }

    public void OnPreviousBtnClick()
    {
        firstStoryPanel.SetActive(true);
        secondStoryPanel.SetActive(false);
    }

    public void OnPreviousBtn2Click()
    {
        secondStoryPanel.SetActive(true);
        thirdStoryPanel.SetActive(false);
    }

    public void OnPreviousBtn3Click()
    {
        thirdStoryPanel.SetActive(true);
        fourthStoryPanel.SetActive(false);
    }

    public void OnPreviousBtn4Click()
    {
        fourthStoryPanel.SetActive(true);
        fifthStoryPanel.SetActive(false);
    }
    
    public void OnSkipBtnClick()
    {
        introPanel.SetActive(false);
        storylinePanel.SetActive(false);
        btnPanel.SetActive(true);
        gameTitle.SetActive(true);
    }
}
