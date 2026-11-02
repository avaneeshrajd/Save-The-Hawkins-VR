using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Reflection;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("UI")]
    [SerializeField] private Image healthFill;
    [SerializeField] private GameObject lowHealth;
    [SerializeField] private AudioSource heartBeat;


    [Header("Auto Heal")]
    [SerializeField] private float healDelay = 5f;    
    [SerializeField] private float healSpeed = 10f;    

    [SerializeField] private float lastHitTime;
    
    [SerializeField] private Transform mainCamera;
    [SerializeField] private float fallSpeed = 1.5f;
    [SerializeField] private float fallAngle = -90f;


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
    public bool IsDead => currentHealth <= 0f;

    public void Heal(float amount)
    {
        if (IsDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UpdateHealthUI();
        if (currentHealth >= 50 && lowHealth != null)
        {
            lowHealth.SetActive(false);
            heartBeat.Stop();
        }
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || Time.timeScale == 0f || TrailerDirector.Active) return;
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

        if (currentHealth < 50 && UIManager.Instance.resultPanel.activeSelf == false)
        {
            lowHealth.SetActive(true);
            heartBeat.Play();
            UpdateHealthUI();
        }

        if (currentHealth >= 50)
        {
            lowHealth.SetActive(false);
            heartBeat.Stop();
            UpdateHealthUI();
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
        Time.timeScale = 1f;
        SceneManager.LoadScene("Home");
        transform.position = Vector3.zero;
    }

    public void OnQuitClick()
    {
        Application.Quit();
    }

    
}