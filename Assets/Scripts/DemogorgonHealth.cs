using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DemogorgonHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private Image healthFill;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private int deathScore = 10;

    
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