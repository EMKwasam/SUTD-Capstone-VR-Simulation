using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TMPro;

[RequireComponent(typeof(Collider))]
public class ColliderPickupJoystick : MonoBehaviour
{
    [SerializeField] private Transform holdPoint;
    [SerializeField] private LayerMask pickupLayers = ~0;
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private GrabObjectManager grabObjectManager;
    [SerializeField] private TextMeshProUGUI pickupStatusText;
    [SerializeField] private string outOfRangeMessage = "Move tool to grabbing distance";
    [SerializeField] private string inRangeMessage = "press button to grab lesion";
    [SerializeField] private string pressTriggerToCutMessage = "Press trigger to cut";
    [SerializeField] private string grabbedMessage = "move tool to end zone";
    [SerializeField] private Animator toolAnimator;
    [SerializeField] private string preGrabTriggerName = "Grab";
    [SerializeField] private string grabbedTriggerName = "Grabbed";
    [SerializeField] private float preGrabDuration = 0.35f;
    [SerializeField] private bool lockToolControlsDuringPreGrab = true;
    [SerializeField] private JoystickToolMovement joystickToolMovement;

    private readonly List<Rigidbody> candidateBodies = new List<Rigidbody>();
    private Rigidbody heldRigidbody;
    private bool isHoldingObject;
    private bool heldUsedGravity;
    private RigidbodyInterpolation heldInterpolation;
    private Collider triggerCollider;
    private bool isPreGrabAnimating;
    private Rigidbody pendingGrabBody;
    private Quaternion lastHoldPointRotation;
    private bool hasLastHoldPointRotation;
    private PickupUiState currentUiState = (PickupUiState)(-1);

    public bool IsInputLocked => lockToolControlsDuringPreGrab && joystickToolMovement != null && joystickToolMovement.IsMovementLocked;

    private enum PickupUiState
    {
        OutOfRange,
        InRange,
        AwaitingCut,
        Grabbed
    }

    private void Awake()
    {
        if (toolAnimator == null)
        {
            toolAnimator = GetComponentInChildren<Animator>();
        }

        if (grabObjectManager == null)
        {
            grabObjectManager = GetComponent<GrabObjectManager>();
            if (enableDebugLogs && grabObjectManager != null)
            {
                Debug.Log("[ColliderPickup] Auto-assigned GrabObjectManager from the same GameObject.", this);
            }
        }

        if (joystickToolMovement == null)
        {
            joystickToolMovement = GetComponent<JoystickToolMovement>();
        }

        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
            if (enableDebugLogs)
            {
                Debug.Log($"[ColliderPickup] Set collider '{triggerCollider.name}' to trigger.", this);
            }
        }

        UpdatePickupStatusText();
    }

    private void Start()
    {
        UpdatePickupStatusText();
    }

    private void Update()
    {
        HandleExternallyReleasedObject();
        CleanupCandidates();
        UpdatePickupStatusText();

        var mouse = Mouse.current;
        var joystick = Joystick.current;
        if (joystick == null)
        {
            return;
        }

        if (WasButtonPressedThisFrame(joystick, "button3"))
        {
            StartPreGrabSequence();
        }

        if (WasButtonPressedThisFrame(joystick, "button2"))
        {
            ConfirmGrabAfterPreGrab();
        }

        if (joystick.trigger != null && joystick.trigger.wasPressedThisFrame)
        {
            SetToolMovementLocked(false);
            if (enableDebugLogs)
            {
                Debug.Log("[ColliderPickup] Trigger pressed. Tool movement unlocked.", this);
            }
        }
    }

    private void FixedUpdate()
    {
        if (heldRigidbody == null || holdPoint == null)
        {
            return;
        }

        heldRigidbody.MovePosition(holdPoint.position);

        Quaternion currentHoldRotation = holdPoint.rotation;
        if (!hasLastHoldPointRotation)
        {
            lastHoldPointRotation = currentHoldRotation;
            hasLastHoldPointRotation = true;
            return;
        }

        Quaternion rotationDelta = currentHoldRotation * Quaternion.Inverse(lastHoldPointRotation);
        if (Quaternion.Angle(Quaternion.identity, rotationDelta) > 0.001f)
        {
            heldRigidbody.MoveRotation(rotationDelta * heldRigidbody.rotation);
        }

        lastHoldPointRotation = currentHoldRotation;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsLayerAllowed(other.gameObject.layer))
        {
            return;
        }

        Rigidbody body = other.attachedRigidbody;
        if (body == null || candidateBodies.Contains(body))
        {
            return;
        }

        candidateBodies.Add(body);
        if (enableDebugLogs)
        {
            Debug.Log($"[ColliderPickup] In range: {body.name}", body);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Rigidbody body = other.attachedRigidbody;
        if (body == null)
        {
            return;
        }

        if (candidateBodies.Remove(body) && enableDebugLogs)
        {
            Debug.Log($"[ColliderPickup] Out of range: {body.name}", body);
        }

        UpdatePickupStatusText();
    }

    private void StartPreGrabSequence()
    {
        if (heldRigidbody != null)
        {
            return;
        }

        if (isPreGrabAnimating)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[ColliderPickup] Pre-grab is already in progress.", this);
            }
            return;
        }

        if (pendingGrabBody != null)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[ColliderPickup] Pre-grab complete. Press button2 to switch to grabbed model.", this);
            }
            return;
        }

        if (holdPoint == null)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[ColliderPickup] Cannot pick up: holdPoint is not assigned.", this);
            }
            return;
        }

        CleanupCandidates();
        if (!TryGetClosestCandidate(out Rigidbody closestBody))
        {
            if (enableDebugLogs)
            {
                Debug.Log("[ColliderPickup] No grabbable rigidbody inside trigger.", this);
            }
            return;
        }

        SetToolMovementLocked(true);
        StartCoroutine(PlayPreGrabSequence(closestBody));
    }

    private IEnumerator PlayPreGrabSequence(Rigidbody targetBody)
    {
        isPreGrabAnimating = true;
        pendingGrabBody = targetBody;

        if (toolAnimator != null && !string.IsNullOrEmpty(preGrabTriggerName))
        {
            toolAnimator.ResetTrigger(preGrabTriggerName);
            toolAnimator.SetTrigger(preGrabTriggerName);
        }

        float delay = Mathf.Max(0f, preGrabDuration);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        isPreGrabAnimating = false;

        if (targetBody == null)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[ColliderPickup] Pre-grab completed but target no longer exists.", this);
            }
            pendingGrabBody = null;
            SetToolMovementLocked(false);
            yield break;
        }

        if (enableDebugLogs)
        {
            Debug.Log("[ColliderPickup] Pre-grab complete. Press button2 to grab.", this);
        }
    }

    private void ConfirmGrabAfterPreGrab()
    {
        if (heldRigidbody != null)
        {
            return;
        }

        if (isPreGrabAnimating)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[ColliderPickup] Wait for pre-grab animation to finish before pressing button2.", this);
            }
            return;
        }

        if (pendingGrabBody == null)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[ColliderPickup] No pre-grab target. Press button3 first.", this);
            }
            return;
        }

        if (toolAnimator != null && !string.IsNullOrEmpty(grabbedTriggerName))
        {
            toolAnimator.ResetTrigger(grabbedTriggerName);
            toolAnimator.SetTrigger(grabbedTriggerName);
        }

        BeginHold(pendingGrabBody);
        pendingGrabBody = null;
    }

    private bool TryGetClosestCandidate(out Rigidbody closestBody)
    {
        closestBody = null;
        float closestDistanceSq = float.MaxValue;
        Vector3 origin = holdPoint.position;

        for (int i = 0; i < candidateBodies.Count; i++)
        {
            Rigidbody body = candidateBodies[i];
            if (body == null)
            {
                continue;
            }

            float distanceSq = (body.worldCenterOfMass - origin).sqrMagnitude;
            if (distanceSq < closestDistanceSq)
            {
                closestDistanceSq = distanceSq;
                closestBody = body;
            }
        }

        return closestBody != null;
    }

    private void BeginHold(Rigidbody targetBody)
    {
        heldRigidbody = targetBody;
        isHoldingObject = true;
        if (holdPoint != null)
        {
            lastHoldPointRotation = holdPoint.rotation;
            hasLastHoldPointRotation = true;
        }
        else
        {
            hasLastHoldPointRotation = false;
        }
        heldUsedGravity = heldRigidbody.useGravity;
        heldInterpolation = heldRigidbody.interpolation;

        heldRigidbody.useGravity = false;
        heldRigidbody.interpolation = RigidbodyInterpolation.None;

        if (!heldRigidbody.isKinematic)
        {
            heldRigidbody.linearVelocity = Vector3.zero;
            heldRigidbody.angularVelocity = Vector3.zero;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[ColliderPickup] Picked up: {heldRigidbody.name}", heldRigidbody);
        }

        if (grabObjectManager != null)
        {
            grabObjectManager.OnObjectGrabbed();
        }

        UpdatePickupStatusText();
    }

    private void ReleaseHeldObject()
    {
        if (heldRigidbody == null)
        {
            if (isHoldingObject)
            {
                CompleteRelease(null, true);
            }
            return;
        }

        heldRigidbody.useGravity = heldUsedGravity;
        heldRigidbody.interpolation = heldInterpolation;
        CompleteRelease(heldRigidbody.name, false);
    }

    private void HandleExternallyReleasedObject()
    {
        if (!isHoldingObject)
        {
            return;
        }

        if (heldRigidbody != null)
        {
            return;
        }

        CompleteRelease(null, true);
    }

    private void CompleteRelease(string releasedName, bool externalRelease)
    {
        heldRigidbody = null;
        isHoldingObject = false;
        hasLastHoldPointRotation = false;
        pendingGrabBody = null;
        isPreGrabAnimating = false;
        SetToolMovementLocked(false);

        if (enableDebugLogs)
        {
            string nameToLog = string.IsNullOrEmpty(releasedName) ? "<destroyed object>" : releasedName;
            string releaseType = externalRelease ? "Released externally" : "Released";
            Debug.Log($"[ColliderPickup] {releaseType}: {nameToLog}", this);
        }

        if (grabObjectManager != null)
        {
            grabObjectManager.OnObjectReleased();
        }

        UpdatePickupStatusText();
    }

    private bool IsLayerAllowed(int layer)
    {
        return (pickupLayers.value & (1 << layer)) != 0;
    }

    private bool WasButtonPressedThisFrame(Joystick joystick, string buttonName)
    {
        if (joystick == null || string.IsNullOrEmpty(buttonName))
        {
            return false;
        }

        ButtonControl buttonControl = joystick.TryGetChildControl<ButtonControl>(buttonName);
        return buttonControl != null && buttonControl.wasPressedThisFrame;
    }

    private void SetToolMovementLocked(bool shouldLock)
    {
        if (!lockToolControlsDuringPreGrab)
        {
            return;
        }

        if (joystickToolMovement == null)
        {
            return;
        }

        joystickToolMovement.SetMovementLocked(shouldLock);
    }

    private void CleanupCandidates()
    {
        candidateBodies.RemoveAll(body => body == null);
    }

    private void UpdatePickupStatusText()
    {
        if (pickupStatusText == null)
        {
            return;
        }

        PickupUiState nextState;
        if (heldRigidbody != null || isHoldingObject)
        {
            nextState = IsInputLocked ? PickupUiState.AwaitingCut : PickupUiState.Grabbed;
        }
        else if (candidateBodies.Count > 0)
        {
            nextState = PickupUiState.InRange;
        }
        else
        {
            nextState = PickupUiState.OutOfRange;
        }

        if (nextState == currentUiState)
        {
            return;
        }

        currentUiState = nextState;
        switch (currentUiState)
        {
            case PickupUiState.AwaitingCut:
                pickupStatusText.text = pressTriggerToCutMessage;
                break;
            case PickupUiState.Grabbed:
                pickupStatusText.text = grabbedMessage;
                break;
            case PickupUiState.InRange:
                pickupStatusText.text = inRangeMessage;
                break;
            default:
                pickupStatusText.text = outOfRangeMessage;
                break;
        }
    }
}