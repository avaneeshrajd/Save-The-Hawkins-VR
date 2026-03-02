using UnityEngine;

public class Bullet : MonoBehaviour
{
    private float lifeTime = 1f;
    private int damage = 5;
    
    void Start()
    { 
        Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter(Collider hit)
    {
        if (hit.gameObject.CompareTag("Enemy"))
        {
            DemogorgonHealth enemy = hit.gameObject.GetComponent<DemogorgonHealth>();
            if (enemy != null)
                enemy.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}