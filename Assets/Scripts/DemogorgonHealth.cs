using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DemogorgonHealth : MonoBehaviour
{
    public static DemogorgonHealth Instance;
    public float maxHealth = 100f;
    private float currentHealth;
    public Image healthFill;
    public TextMeshProUGUI scoreText;
    public float deathScore = 10f;


    private void Awake()
    {
        Instance = this;
    }
    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float dmg)
    {
        currentHealth -= dmg;
        UpdateHealthUI();

        if (currentHealth <= 0)
        {
            Die();
            ScoreTxt();
        }
    }
    
    void UpdateHealthUI()
    {
        if (healthFill != null)
            healthFill.fillAmount = currentHealth / maxHealth;
    }

    void Die()
    {
        Destroy(gameObject);
    }

    public string ScoreTxt()
    {
        scoreText.text = $"HellFire Points: {deathScore}";
        return  scoreText.text;
    }
}