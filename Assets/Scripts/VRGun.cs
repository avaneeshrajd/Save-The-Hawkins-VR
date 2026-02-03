using UnityEngine;
using UnityEngine.InputSystem;

public class VRGun : MonoBehaviour
{
    public Transform firePoint;
    public GameObject bulletPrefab;
    public float fireRate = 0.2f;

    [Header("INPUT")]
    public InputActionReference shootAction;

    float _nextFire;

    void OnEnable()
    {
        if (shootAction != null)
            shootAction.action.Enable();
    }

    void OnDisable()
    {
        if (shootAction != null)
            shootAction.action.Disable();
    }

    void Update()
    {
        if (shootAction !=null && shootAction.action.IsPressed())
        {
            Shoot();
        }
    }

    void Shoot()
    {
        if (Time.time < _nextFire) return;

        _nextFire = Time.time + fireRate;
        Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        Debug.Log("SHOT FIRED");
    }
}