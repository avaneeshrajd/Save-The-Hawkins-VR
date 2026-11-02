using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float lifeTime = 1f;
    // Demogorgon has 100 HP: 34 damage = 3 hits to kill.
    [SerializeField] private float damage = 34f;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter(Collider hit)
    {
        // The enemy collider can sit on a child object, so search up the hierarchy.
        DemogorgonHealth enemy = hit.GetComponentInParent<DemogorgonHealth>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            if (VRGun.Instance != null) GameFx.Haptic(VRGun.Instance.HandNode, 0.25f, 0.03f); // hit confirm
            Destroy(gameObject);
            return;
        }

        BatFlyer bat = hit.GetComponentInParent<BatFlyer>();
        if (bat != null)
        {
            bat.Hit();
            Destroy(gameObject);
            return;
        }

        UpsideDownGate gate = hit.GetComponentInParent<UpsideDownGate>();
        if (gate != null)
        {
            gate.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        Pickup pickup = hit.GetComponentInParent<Pickup>();
        if (pickup != null)
        {
            pickup.Collect();
            Destroy(gameObject);
            return;
        }

        // Ignore other triggers (UI, interaction volumes) so they do not eat bullets.
        if (hit.isTrigger) return;

        Destroy(gameObject);
    }
}
