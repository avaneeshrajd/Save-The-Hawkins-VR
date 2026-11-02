using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Fires when the player points a controller at this object and presses the trigger.
/// XRI's built-in "Activated" event needs grip (select) held first, so players had to
/// press grip AND trigger together. This only needs hover + trigger.
/// </summary>
[RequireComponent(typeof(XRBaseInteractable))]
public class TriggerActivateInteractable : MonoBehaviour
{
    [Tooltip("Called when trigger is pressed while hovering. If empty, enters the Upside Down level.")]
    [SerializeField] private UnityEvent onTriggerPressed = new UnityEvent();
    [Tooltip("Fire only once (for example, to start a level).")]
    [SerializeField] private bool fireOnce = true;

    private XRBaseInteractable interactable;
    private readonly List<XRBaseInputInteractor> hoveringInteractors = new List<XRBaseInputInteractor>();
    private bool fired;

    void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
    }

    void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
    }

    void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
        hoveringInteractors.Clear();
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (args.interactorObject is XRBaseInputInteractor inputInteractor && !hoveringInteractors.Contains(inputInteractor))
            hoveringInteractors.Add(inputInteractor);
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        if (args.interactorObject is XRBaseInputInteractor inputInteractor)
            hoveringInteractors.Remove(inputInteractor);
    }

    void Update()
    {
        if (fired && fireOnce) return;

        foreach (XRBaseInputInteractor interactor in hoveringInteractors)
        {
            if (interactor != null && interactor.activateInput.ReadWasPerformedThisFrame())
            {
                Fire();
                return;
            }
        }
    }

    void Fire()
    {
        fired = true;

        if (onTriggerPressed.GetPersistentEventCount() > 0)
            onTriggerPressed.Invoke();
        else if (StartGame.Instance != null)
            StartGame.Instance.EnterUpsideDown();
    }
}
