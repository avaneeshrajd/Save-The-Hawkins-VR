using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Rifle that always sits in one hand, aligned with that controller.
/// - Starts in the hand it is parented to in the scene (left controller).
/// - Grabbing it with the other hand moves it to that hand with the same aligned pose.
/// - Releasing grip never drops it or leaves it misaligned; it stays in the last hand.
/// - The trigger of the hand holding the gun fires.
/// </summary>
public class VRGun : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 40f;
    [SerializeField] private AudioSource shotSound;
    [Tooltip("Fallback fire input, only used if no XR interactor is found for the holding hand.")]
    [SerializeField] private InputActionReference shootAction;
    [Tooltip("Grab interactable on the rifle mesh. Found in children if empty.")]
    [SerializeField] private XRGrabInteractable grabInteractable;

    [Header("Ammo")]
    [SerializeField] private int magazineSize = 30;
    [SerializeField] private float reloadTime = 1.2f;
    [SerializeField] private float rapidFireRate = 10f;

    public static VRGun Instance { get; private set; }
    /// <summary>Raised when the gun moves to another hand (sword follows the free hand).</summary>
    public event Action HandChanged;
    public Transform CurrentHand => currentHand;
    public XRNode HandNode => IsLeft(currentInteractor, currentHand) ? XRNode.LeftHand : XRNode.RightHand;
    public Transform FirePoint => firePoint;

    private int ammo;
    private bool reloading;
    private float reloadEnd, rapidFireUntil, nextAutoShot, muzzleDownTime;
    private Light muzzleLight;
    private GameObject muzzleFlash;
    private float muzzleOffAt;
    private TextMeshPro hud;
    private Transform leftHand, rightHand;

    // Gun root pose relative to the controller, captured from the authored scene setup
    // (barrel along controller forward, gun up along controller up).
    private Vector3 holdLocalPosition;
    private Quaternion holdLocalRotation;

    // Mesh/grab object pose relative to the gun root, so it can be restored after XRI moves it.
    private Transform grabTransform;
    private Vector3 grabLocalPosition;
    private Quaternion grabLocalRotation;
    private Vector3 grabLocalScale;

    private Collider[] gunColliders;
    private Transform currentHand;
    private XRBaseInputInteractor currentInteractor;
    private IXRSelectInteractor pendingInteractor;
    private bool fallbackBound;

    void Awake()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponentInChildren<XRGrabInteractable>(true);

        holdLocalPosition = transform.localPosition;
        holdLocalRotation = transform.localRotation;
        currentHand = transform.parent;

        if (grabInteractable != null)
        {
            grabTransform = grabInteractable.transform;
            grabLocalPosition = grabTransform.localPosition;
            grabLocalRotation = grabTransform.localRotation;
            grabLocalScale = grabTransform.localScale;

            grabInteractable.throwOnDetach = false;
            grabInteractable.retainTransformParent = true;
        }

        gunColliders = GetComponentsInChildren<Collider>(true);
    }

    void Start()
    {
        Instance = this;
        ammo = magazineSize;
        FindHands();
        CreateMuzzleFx();
        CreateHud();
        currentInteractor = FindInteractorForHand(currentHand);
        if (currentInteractor == null)
            BindFallbackShoot();
    }

    void OnEnable()
    {
        if (grabInteractable != null)
            grabInteractable.selectEntered.AddListener(OnGrabbed);

        // Undo the hide from OnDisable; Update puts the mesh back under the gun root.
        if (grabTransform != null && !grabTransform.gameObject.activeSelf)
            grabTransform.gameObject.SetActive(true);
    }

    void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            // Releases the grab if the gun is hidden (pause/settings) while held.
            if (grabInteractable.isSelected && grabInteractable.interactionManager != null)
                grabInteractable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grabInteractable);

            // XRI moves the mesh to the scene root while grabbed. If it is still there,
            // hide it so it does not float in the world while the gun root is hidden.
            if (grabTransform != null && grabTransform.parent != transform)
                grabTransform.gameObject.SetActive(false);
        }
        pendingInteractor = null;
        UnbindFallbackShoot();
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        // Hierarchy changes are not safe inside the XRI callback, so handle it next frame.
        pendingInteractor = args.interactorObject;
    }

    void Update()
    {
        if (pendingInteractor != null)
        {
            IXRSelectInteractor interactor = pendingInteractor;
            pendingInteractor = null;
            MoveToHand(interactor);
        }
        else if (grabTransform != null && !grabInteractable.isSelected && grabTransform.parent != transform)
        {
            // Safety net: the grab ended some other way, so put the mesh back in place.
            RestoreGrabTransform();
        }

        if (muzzleFlash != null && muzzleFlash.activeSelf && Time.time >= muzzleOffAt)
        {
            muzzleFlash.SetActive(false);
            muzzleLight.enabled = false;
        }

        UpdateReload();

        bool rapid = Time.time < rapidFireUntil;
        if (currentInteractor != null && currentInteractor.isActiveAndEnabled)
        {
            if (currentInteractor.activateInput.ReadWasPerformedThisFrame())
            {
                Fire();
                nextAutoShot = Time.time + 1f / rapidFireRate;
            }
            else if (rapid && currentInteractor.activateInput.ReadIsPerformed() && Time.time >= nextAutoShot)
            {
                Fire();
                nextAutoShot = Time.time + 1f / rapidFireRate;
            }
        }
        UpdateHud(rapid);
    }

    // ---------- Ammo / reload ----------

    void UpdateReload()
    {
        if (reloading)
        {
            if (Time.time >= reloadEnd)
            {
                reloading = false;
                ammo = magazineSize;
                GameFx.Play3D(GameFx.EmptyClick, firePoint.position, 1f, 0.6f);
                GameFx.Haptic(HandNode, 0.6f, 0.08f);
            }
            return;
        }
        if (ammo >= magazineSize) return;

        // Reload: press A / X on the gun hand, or point the gun at the ground for a moment.
        UnityEngine.XR.InputDevice dev = InputDevices.GetDeviceAtXRNode(HandNode);
        bool button = dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool pressed) && pressed;
        muzzleDownTime = firePoint.forward.y < -0.75f ? muzzleDownTime + Time.deltaTime : 0f;
        if (button || muzzleDownTime > 0.35f) StartReload();
    }

    void StartReload()
    {
        if (reloading) return;
        reloading = true;
        reloadEnd = Time.time + reloadTime;
        muzzleDownTime = 0f;
        GameFx.Play3D(GameFx.EmptyClick, firePoint.position, 1f, 0.8f);
        GameFx.Haptic(HandNode, 0.3f, 0.05f);
    }

    public void ActivateRapidFire(float seconds)
    {
        rapidFireUntil = Time.time + seconds;
        ammo = magazineSize;
        reloading = false;
    }

    // ---------- FX / HUD ----------

    void CreateMuzzleFx()
    {
        muzzleLight = GameFx.CreateLight(firePoint, Vector3.zero, new Color(1f, 0.75f, 0.35f), 8f, 5f);
        muzzleLight.enabled = false;
        muzzleFlash = GameFx.GlowPrimitive(PrimitiveType.Sphere, new Color(1f, 0.85f, 0.4f), Vector3.one, firePoint);
        muzzleFlash.transform.localScale = Vector3.one * (0.08f / Mathf.Max(0.0001f, firePoint.lossyScale.x));
        muzzleFlash.SetActive(false);
    }

    void CreateHud()
    {
        hud = GameHUD.CreateText("Gun HUD", 0.5f, Color.white, new Vector2(0.6f, 0.25f), transform);
        float s = Mathf.Max(0.0001f, transform.lossyScale.x);
        hud.transform.localScale = Vector3.one / s;
        hud.transform.localPosition = new Vector3(0f, 0.11f, -0.05f) / s;
        hud.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
    }

    void UpdateHud(bool rapid)
    {
        if (hud == null) return;
        if (currentHand != null && Camera.main != null)
        {
            // Float above the back of the gun, facing the eyes, so it never clips into the rifle.
            hud.transform.position = currentHand.position + Vector3.up * 0.2f - currentHand.forward * 0.08f;
            GameFx.Billboard(hud.transform, Camera.main.transform);
        }
        string ammoText = rapid ? $"<color=#FFD91A>RAPID {Mathf.CeilToInt(rapidFireUntil - Time.time)}s</color>"
            : reloading ? "<color=#FFD91A>RELOADING...</color>"
            : ammo == 0 ? "<color=#FF4040>RELOAD! (A/X or aim down)</color>"
            : $"{ammo}/{magazineSize}";
        int mult = ScoreSystem.Multiplier;
        string combo = mult > 1 ? $"\n<color=#FF6AD5>COMBO x{mult}</color>" : "";
        hud.text = ammoText + combo;
    }

    void FindHands()
    {
        foreach (var i in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform c = GetControllerTransform(i.transform);
            if (i.handedness == InteractorHandedness.Left) leftHand = c;
            else if (i.handedness == InteractorHandedness.Right) rightHand = c;
        }
    }

    public IEnumerable<Transform> HandTransforms() { yield return leftHand; yield return rightHand; }

    /// <summary>The controller not holding the gun.</summary>
    public Transform FreeHand => currentHand == leftHand ? rightHand : leftHand;
    public XRNode FreeHandNode => HandNode == XRNode.LeftHand ? XRNode.RightHand : XRNode.LeftHand;

    bool IsLeft(XRBaseInputInteractor interactor, Transform hand)
    {
        if (interactor != null && interactor.handedness != InteractorHandedness.None)
            return interactor.handedness == InteractorHandedness.Left;
        return hand != null && hand == leftHand;
    }

    void MoveToHand(IXRSelectInteractor interactor)
    {
        // End the XRI grab. The gun is held by parenting, so releasing grip changes nothing.
        if (grabInteractable.isSelected && grabInteractable.interactionManager != null)
            grabInteractable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grabInteractable);

        RestoreGrabTransform();

        Transform hand = GetControllerTransform(interactor.transform);
        transform.SetParent(hand, false);
        transform.localPosition = holdLocalPosition;
        transform.localRotation = holdLocalRotation;

        bool changed = currentHand != hand;
        currentHand = hand;
        currentInteractor = interactor as XRBaseInputInteractor;
        if (changed) HandChanged?.Invoke();
        if (currentInteractor == null)
            currentInteractor = FindInteractorForHand(hand);
    }

    void RestoreGrabTransform()
    {
        if (grabTransform == null) return;
        grabTransform.SetParent(transform, false);
        grabTransform.localPosition = grabLocalPosition;
        grabTransform.localRotation = grabLocalRotation;
        grabTransform.localScale = grabLocalScale;
    }

    // The tracked controller object (has TrackedPoseDriver), not the interactor child,
    // so the pose matches the authored left-hand setup for both hands.
    static Transform GetControllerTransform(Transform interactorTransform)
    {
        var poseDriver = interactorTransform.GetComponentInParent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
        if (poseDriver != null)
            return poseDriver.transform;
        return interactorTransform.parent != null ? interactorTransform.parent : interactorTransform;
    }

    static XRBaseInputInteractor FindInteractorForHand(Transform hand)
    {
        if (hand == null) return null;

        XRBaseInputInteractor best = null;
        foreach (var interactor in hand.GetComponentsInChildren<XRBaseInputInteractor>(true))
        {
            if (interactor is NearFarInteractor)
                return interactor;
            if (best == null)
                best = interactor;
        }
        return best;
    }

    void BindFallbackShoot()
    {
        if (shootAction == null || fallbackBound) return;
        shootAction.action.performed += OnFallbackShoot;
        shootAction.action.Enable();
        fallbackBound = true;
    }

    void UnbindFallbackShoot()
    {
        if (!fallbackBound) return;
        shootAction.action.performed -= OnFallbackShoot;
        fallbackBound = false;
    }

    void OnFallbackShoot(InputAction.CallbackContext ctx)
    {
        Fire();
    }

    void Fire()
    {
        if (reloading) return;
        if (ammo <= 0 && Time.time >= rapidFireUntil)
        {
            GameFx.Play3D(GameFx.EmptyClick, firePoint.position, 1f, 1f);
            return;
        }
        if (Time.time >= rapidFireUntil) ammo--;

        if (shotSound != null)
            shotSound.Play();
        muzzleFlash.SetActive(true);
        muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
        muzzleLight.enabled = true;
        muzzleOffAt = Time.time + 0.05f;
        GameFx.Haptic(HandNode, 0.45f, 0.05f);
        Shoot();
    }

    void Shoot()
    {
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        // Bullets must not bounce off the rifle's own collider (that sent shots off target).
        Collider bulletCol = bullet.GetComponent<Collider>();
        if (bulletCol != null)
        {
            foreach (Collider gunCol in gunColliders)
            {
                if (gunCol != null)
                    Physics.IgnoreCollision(bulletCol, gunCol);
            }
        }

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        rb.linearVelocity = firePoint.forward * bulletSpeed;
    }
}
