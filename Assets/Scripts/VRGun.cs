using UnityEngine;
using UnityEngine.InputSystem;

public class VRGun : MonoBehaviour
{
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 40f;
    [SerializeField] private AudioSource shotSound;

    [SerializeField] private InputActionReference shootAction;

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