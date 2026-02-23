using System.Collections;
using UnityEngine;
using TMPro;

public class SceneFadeIn : MonoBehaviour
{
    public CanvasGroup fadeCanvas;     
    public TextMeshProUGUI enteringText;
    public float waitTime = 10f;
    public float fadeSpeed = 0.5f;
    public GameObject gun;
    public AudioSource gunEquip;

    void Start()
    {
        StartCoroutine(FadeInSequence());
        
    }

    IEnumerator FadeInSequence()
    {
        yield return new WaitForSeconds(waitTime);

        
        if (enteringText != null)
            enteringText.gameObject.SetActive(false);

       
        while (fadeCanvas.alpha > 0)
        {
            fadeCanvas.alpha -= Time.deltaTime * fadeSpeed;
            yield return null;
        }

        fadeCanvas.alpha = 0;
        gun.SetActive(true);
        gunEquip.Play();
        
    }
}