using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

[RequireComponent(typeof(Collider))]
public class ColliderPickup : MonoBehaviour
{
    [SerializeField] private Transform holdPoint;
    [SerializeField] private LayerMask pickupLayers = ~0;
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private GrabObjectManager grabObjectManager;
    [SerializeField] private TextMeshProUGUI pickupStatusText;
    [SerializeField] private string outOfRangeMessage = "Move tool to grabbing distance";
    [SerializeField] private string inRangeMessage = "press button to grab lesion";
    [SerializeField] private string grabbedMessage = "move tool to end zone";
    [SerializeField] private Animator toolAnimator;
    [SerializeField] private string preGrabTriggerName = "Grab";
    [SerializeField] private float preGrabDuration = 0.35f;
    [SerializeField] private bool lockToolControlsDuringPreGrab = true;

    private readonly List<Rigidbody> candidateBodies = new List<Rigidbody>();
    private Rigidbody heldRigidbody;
    private bool isHoldingObject;
    private bool heldUsedGravity;
    private RigidbodyInterpolation heldInterpolation;
    private Collider triggerCollider;
    private bool isPreGrabAnimating;
    private PickupUiState currentUiState = (PickupUiState)(-1);

    public bool IsInputLocked => lockToolControlsDuringPreGrab && isPreGrabAnimating;

    private enum PickupUiState
    {
        OutOfRange,
        InRange,
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
        if (mouse == null)
        {
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (isPreGrabAnimating)
            {
                if (enableDebugLogs)
                {
                    Debug.Log("[ColliderPickup] Ignoring click while pre-grab animation is playing.", this);
                }
                return;
            }

            if (heldRigidbody == null)
            {
                StartPreGrabSequence();
            }
            else
            {
                ReleaseHeldObject();
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
        heldRigidbody.MoveRotation(holdPoint.rotation);
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

        StartCoroutine(PlayPreGrabThenPickup(closestBody));
    }

    private IEnumerator PlayPreGrabThenPickup(Rigidbody targetBody)
    {
        isPreGrabAnimating = true;

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
            yield break;
        }

        if (heldRigidbody != null)
        {
            yield break;
        }

        BeginHold(targetBody);
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