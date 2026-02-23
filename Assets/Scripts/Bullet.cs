using UnityEngine;

public class Bullet : MonoBehaviour
{
    private float _lifeTime = 1f;
    private int _damage = 5;
    
    void Start()
    {
        Destroy(gameObject, _lifeTime);
    }

    void OnTriggerEnter(Collider hit)
    {
        if (hit.gameObject.CompareTag("Enemy"))
        {
            DemogorgonHealth enemy = hit.gameObject.GetComponent<DemogorgonHealth>();
            if (enemy != null)
                enemy.TakeDamage(_damage);
        }

        Destroy(gameObject);
    }
}