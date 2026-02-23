using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DemogorgonHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;
    public Image healthFill;
    public TextMeshProUGUI scoreText;
    public int deathScore = 10;

    
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
        UIManager.Instance.EnemyDie(deathScore);
    }
    
}