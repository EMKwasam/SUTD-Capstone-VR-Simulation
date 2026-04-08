using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class ColliderPickupMouse : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform holdPoint;
    [SerializeField] private GrabObjectManager grabObjectManager;
    [SerializeField] private Animator toolAnimator;
    [SerializeField] private TextMeshProUGUI pickupStatusText;

    [Header("Filtering")]
    [SerializeField] private LayerMask pickupLayers = ~0;

    [Header("Input")]
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Animation")]
    [SerializeField] private string preGrabTriggerName = "Grab";
    [SerializeField] private string grabbedTriggerName = "Grabbed";
    [SerializeField] private float preGrabDuration = 0.35f;

    [Header("UI Messages")]
    [SerializeField] private string outOfRangeMessage = "Move tool to grabbing distance";
    [SerializeField] private string inRangeMessage = "Left click to pre-grab";
    [SerializeField] private string awaitingConfirmMessage = "Right click to confirm grab";
    [SerializeField] private string grabbedMessage = "Left click to release";

    private readonly List<Rigidbody> candidateBodies = new List<Rigidbody>();

    private Rigidbody heldRigidbody;
    private bool isHoldingObject;
    private bool heldUsedGravity;
    private RigidbodyInterpolation heldInterpolation;

    private Rigidbody pendingGrabBody;
    private bool isPreGrabAnimating;

    private Quaternion lastHoldPointRotation;
    private bool hasLastHoldPointRotation;

    private Collider triggerCollider;
    private PickupUiState currentUiState = (PickupUiState)(-1);

    private enum PickupUiState
    {
        OutOfRange,
        InRange,
        AwaitingConfirm,
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
                Debug.Log("[ColliderPickupMouse] Auto-assigned GrabObjectManager from the same GameObject.", this);
            }
        }

        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
            if (enableDebugLogs)
            {
                Debug.Log($"[ColliderPickupMouse] Set collider '{triggerCollider.name}' to trigger.", this);
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

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (heldRigidbody != null)
            {
                ReleaseHeldObject();
            }
            else if (pendingGrabBody != null && !isPreGrabAnimating)
            {
                ConfirmGrabAfterPreGrab();
            }
            else
            {
                StartPreGrabSequence();
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
            Debug.Log($"[ColliderPickupMouse] In range: {body.name}", body);
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
            Debug.Log($"[ColliderPickupMouse] Out of range: {body.name}", body);
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
                Debug.Log("[ColliderPickupMouse] Pre-grab is already in progress.", this);
            }
            return;
        }

        if (pendingGrabBody != null)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[ColliderPickupMouse] Pre-grab complete. Right click to confirm.", this);
            }
            return;
        }

        if (holdPoint == null)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[ColliderPickupMouse] Cannot pick up: holdPoint is not assigned.", this);
            }
            return;
        }

        CleanupCandidates();
        if (!TryGetClosestCandidate(out Rigidbody closestBody))
        {
            if (enableDebugLogs)
            {
                Debug.Log("[ColliderPickupMouse] No grabbable rigidbody inside trigger.", this);
            }
            return;
        }

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
                Debug.Log("[ColliderPickupMouse] Pre-grab completed but target no longer exists.", this);
            }

            pendingGrabBody = null;
            yield break;
        }

        if (enableDebugLogs)
        {
            Debug.Log("[ColliderPickupMouse] Pre-grab complete. Right click to grab.", this);
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
                Debug.Log("[ColliderPickupMouse] Wait for pre-grab animation to finish before clicking again.", this);
            }
            return;
        }

        if (pendingGrabBody == null)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[ColliderPickupMouse] No pre-grab target. Click once to pre-grab first.", this);
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
            Debug.Log($"[ColliderPickupMouse] Picked up: {heldRigidbody.name}", heldRigidbody);
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

        if (enableDebugLogs)
        {
            string nameToLog = string.IsNullOrEmpty(releasedName) ? "<destroyed object>" : releasedName;
            string releaseType = externalRelease ? "Released externally" : "Released";
            Debug.Log($"[ColliderPickupMouse] {releaseType}: {nameToLog}", this);
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
            nextState = PickupUiState.Grabbed;
        }
        else if (isPreGrabAnimating || pendingGrabBody != null)
        {
            nextState = PickupUiState.AwaitingConfirm;
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
            case PickupUiState.AwaitingConfirm:
                pickupStatusText.text = awaitingConfirmMessage;
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
