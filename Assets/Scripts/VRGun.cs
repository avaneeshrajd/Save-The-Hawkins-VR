using UnityEngine;
using UnityEngine.InputSystem;

public class VRGun : MonoBehaviour
{
    public Transform firePoint;
    public GameObject bulletPrefab;
    public float bulletSpeed = 40f;
    public AudioSource shotSound;

    public InputActionReference shootAction;

    void OnEnable()
    {
        shootAction.action.performed += OnShoot;
        shootAction.action.Enable();
    }

    void OnDisable()
    {
        shootAction.action.performed -= OnShoot;
        shootAction.action.Disable();
    }

    void OnShoot(InputAction.CallbackContext ctx)
    {
        Shoot();
        shotSound.Play();
    }

    void Shoot()
    {
        Debug.Log("SHOOT EVENT RECEIVED");

        GameObject bullet = Instantiate(
            bulletPrefab,
            firePoint.position,
            firePoint.rotation
        );

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        rb.linearVelocity = firePoint.forward * bulletSpeed;
    }
    
}