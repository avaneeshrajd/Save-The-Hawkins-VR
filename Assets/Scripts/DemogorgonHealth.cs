using UnityEngine;
using UnityEngine.UI;

public class DemogorgonHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    float currentHealth;
    public Image healthFill;

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
    }
}