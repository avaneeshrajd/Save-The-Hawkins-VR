using System.Collections;
using UnityEngine;
using TMPro;

public class SceneFadeIn : MonoBehaviour
{
    [SerializeField] private CanvasGroup fadeCanvas;     
    [SerializeField] private TextMeshProUGUI enteringText;
    [SerializeField] private float waitTime = 10f;
    [SerializeField] private float fadeSpeed = 0.5f;
    [SerializeField] private GameObject gun;
    [SerializeField] private AudioSource gunEquip;

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