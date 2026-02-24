using UnityEngine;
using UnityEngine.SceneManagement;

public class StartGame : MonoBehaviour
{
    [SerializeField] private GameObject upsideDownGate;
    [SerializeField] private GameObject btnPanel;
    [SerializeField] private GameObject gameTitle;
    [SerializeField] private GameObject upsideDownTitle;
    [SerializeField] private GameObject modesPanel;
    [SerializeField] private GameObject introPanel;
    [SerializeField] private GameObject storylinePanel;
    [SerializeField] private GameObject firstStoryPanel;
    [SerializeField] private GameObject secondStoryPanel;
    [SerializeField] private GameObject thirdStoryPanel;
    [SerializeField] private GameObject fourthStoryPanel;
    [SerializeField] private GameObject fifthStoryPanel;


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
