using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DemogorgonHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    float currentHealth;
    public Image healthFill;
    public TextMeshProUGUI scoreText;
    private float deathScore = 10f;

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
            scoreText.text = $"HellFire Points: {deathScore}";
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
}