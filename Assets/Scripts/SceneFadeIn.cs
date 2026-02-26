using System.Collections;
using UnityEngine;
using TMPro;

public class SceneFadeIn : MonoBehaviour
{
    [SerializeField] private CanvasGroup fadeCanvas;     
    [SerializeField] private TextMeshProUGUI enteringText;
    [SerializeField] private GameObject gun;
    [SerializeField] private AudioSource gunEquip;
    [SerializeField] private AudioSource thunderSound;
    
    private float waitTime = 10f;
    private float fadeSpeed = 0.5f;

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
        thunderSound.Play();
        gun.SetActive(true);
        gunEquip.Play();
        
    }
}