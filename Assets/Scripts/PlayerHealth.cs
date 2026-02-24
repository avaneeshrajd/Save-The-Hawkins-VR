using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Reflection;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("UI")]
    public Image healthFill;
    public GameObject lowHealth;
    public AudioSource heartBeat;


    [Header("Auto Heal")]
    public float healDelay = 5f;    
    public float healSpeed = 10f;    

    float lastHitTime;
    
    public Transform mainCamera;
    public float fallSpeed = 1.5f;
    public float fallAngle = -90f;


    void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthUI();
    }

    void Update()
    { 
        if (Time.time - lastHitTime > healDelay && currentHealth < maxHealth)
        {
            currentHealth += healSpeed * Time.deltaTime;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
            UpdateHealthUI();
        }
    }
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        lastHitTime = Time.time;

        if (currentHealth < 0)
            currentHealth = 0;

        UpdateHealthUI();

        if (currentHealth <= 0)
        {
            //StartCoroutine(Die());
            mainCamera.localRotation = Quaternion.identity;
            StopCoroutines();
            lowHealth.SetActive(false);
            heartBeat.Stop();
            StartCoroutine(FallDown());
            Time.timeScale = 0;
            UIManager.Instance.PlayerDie();
        }

        if (currentHealth <= 50 && UIManager.Instance.resultPanel.activeSelf == false)
        {
            lowHealth.SetActive(true);
            heartBeat.Play();
        }

        if (currentHealth > 50)
        {
            lowHealth.SetActive(false);
            heartBeat.Stop();
        }
    }

    void UpdateHealthUI()
    {
        if (healthFill != null)
        {
            healthFill.fillAmount = currentHealth / maxHealth;
        }
    }

    void StopCoroutines()
    {
        StopAllCoroutines();

        // yield return new WaitForSeconds(2f);
        // Quaternion startRot = mainCamera.localRotation;
        // Quaternion endRot = Quaternion.Euler(0, 0, 0);
        // gameEnd.SetActive(true);
    }
    
    IEnumerator FallDown()
    {
        float t = 0f;
        Quaternion startRot = mainCamera.localRotation;
        Quaternion endRot = Quaternion.Euler(0, 0, fallAngle);
    
        Vector3 startPos = mainCamera.localPosition;
        Vector3 endPos = startPos + Vector3.down * 0.5f;
    
        while (t < 1f)
        {
            t += Time.deltaTime * fallSpeed;
    
            mainCamera.localRotation = Quaternion.Slerp(startRot, endRot, t);
            mainCamera.localPosition = Vector3.Lerp(startPos, endPos, t);
    
            yield return null;
        }
    }

    public void OnHomeClick()
    {
        SceneManager.LoadScene("Home");
        transform.position = Vector3.zero;
    }

    public void OnQuitClick()
    {
        Application.Quit();
    }

    
}