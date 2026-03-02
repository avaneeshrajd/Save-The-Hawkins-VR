using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class VRGun : MonoBehaviour
{
    // [SerializeField] private Transform demogorgon;
    [SerializeField] private Transform target;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 40f;
    [SerializeField] private AudioSource shotSound;
    [SerializeField] private InputActionReference shootAction;

    // private float distance;
    private float fireRange = 20f;


    // private void Update()
    // {
    //     distance = Vector3.Distance(transform.position, demogorgon.position);
    // }

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
        Collider[] hits = Physics.OverlapSphere(
            firePoint.position,
            fireRange
        );

        bool enemyInRange = false;

        foreach (Collider col in hits)
        {
            if (col.CompareTag("Enemy"))
            {
                enemyInRange = true;
                break;
            }
        }

        if (!enemyInRange) return;

        GameObject bullet = Instantiate(
            bulletPrefab,
            firePoint.position,
            firePoint.rotation
        );

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        rb.linearVelocity = firePoint.forward * bulletSpeed;
        }
    }