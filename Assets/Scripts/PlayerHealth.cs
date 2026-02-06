using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("UI")]
    public Image healthFill;
    public GameObject gameEnd;
    public GameObject lowHealth;


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
            gameEnd.SetActive(true);
            Die();
        }

        if (currentHealth <= 30)
        {
            lowHealth.SetActive(true);
        }
    }

    void UpdateHealthUI()
    {
        if (healthFill != null)
        {
            healthFill.fillAmount = currentHealth / maxHealth;
            lowHealth.SetActive(false);
        }
    }

    void Die()
    {
        StopAllCoroutines();
        StartCoroutine(FallDown());

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