using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ColliderPickupJoystickSwitchKeyboardTest : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform holdPoint;
    [SerializeField] private GrabObjectManager grabObjectManager;
    [SerializeField] private Animator toolAnimator;
    [SerializeField] private TextMeshProUGUI pickupStatusText;
    [SerializeField] private JoystickToolMovement joystickToolMovement;

    [Header("Filtering")]
    [SerializeField] private LayerMask pickupLayers = ~0;

    [Header("Test Input")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private KeyCode switchOnKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode switchOffKey = KeyCode.Alpha0;
    [SerializeField] private KeyCode triggerKey = KeyCode.Space;
    [SerializeField] private bool switchStartsOn;

    [Header("Animation")]
    [SerializeField] private string preGrabTriggerName = "Grab";
    [SerializeField] private string grabbedTriggerName = "Grabbed";
    [SerializeField] private float preGrabDuration = 0.35f;
    [SerializeField] private bool lockToolControlsDuringPreGrab = true;

    [Header("UI Messages")]
    [SerializeField] private string outOfRangeMessage = "Move tool to grabbing distance";
    [SerializeField] private string inRangeMessage = "press button to grab lesion";
    [SerializeField] private string pressTriggerToCutMessage = "Press trigger to cut";
    [SerializeField] private string grabbedMessage = "move tool to end zone";

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
    private bool isSwitchOn;
    private Coroutine activeSwitchRoutine;

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
                Debug.Log("[ColliderPickupSwitchKeyboardTest] Auto-assigned GrabObjectManager from the same GameObject.", this);
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
                Debug.Log($"[ColliderPickupSwitchKeyboardTest] Set collider '{triggerCollider.name}' to trigger.", this);
            }
        }

        UpdatePickupStatusText();
    }

    private void Start()
    {
        SetSwitchState(switchStartsOn, true);
        UpdatePickupStatusText();
    }

    private void Update()
    {
        HandleExternallyReleasedObject();
        CleanupCandidates();

        if (Input.GetKeyDown(switchOnKey))
        {
            SetSwitchState(true, false);
        }

        if (Input.GetKeyDown(switchOffKey))
        {
            SetSwitchState(false, false);
        }

        if (Input.GetKeyDown(triggerKey))
        {
            SetToolMovementLocked(false);
            if (enableDebugLogs)
            {
                Debug.Log("[ColliderPickupSwitchKeyboardTest] Trigger key pressed. Tool movement unlocked.", this);
            }
        }

        UpdatePickupStatusText();
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
            Debug.Log($"[ColliderPickupSwitchKeyboardTest] In range: {body.name}", body);
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
            Debug.Log($"[ColliderPickupSwitchKeyboardTest] Out of range: {body.name}", body);
        }

        UpdatePickupStatusText();
    }

    private void SetSwitchState(bool desiredState, bool forceImmediateSync)
    {
        if (!forceImmediateSync && desiredState == isSwitchOn)
        {
            return;
        }

        isSwitchOn = desiredState;

        if (isSwitchOn)
        {
            HandleSwitchTurnedOn();
        }
        else
        {
            HandleSwitchTurnedOff();
        }
    }

    private void HandleSwitchTurnedOn()
    {
        if (enableDebugLogs)
        {
            Debug.Log("[ColliderPickupSwitchKeyboardTest] Switch turned on.", this);
        }

        CancelActiveSwitchRoutine();
        pendingGrabBody = null;
        ApplyImmediateSwitchVisualState(true);
        SetToolMovementLocked(true);
        StartSwitchOnSequence();
    }

    private void HandleSwitchTurnedOff()
    {
        if (enableDebugLogs)
        {
            Debug.Log("[ColliderPickupSwitchKeyboardTest] Switch turned off.", this);
        }

        CancelActiveSwitchRoutine();

        if (heldRigidbody != null || isHoldingObject)
        {
            ReleaseHeldObject();
        }
        else
        {
            CompleteRelease(null, false);
        }

        ApplyImmediateSwitchVisualState(false);
        SetToolMovementLocked(false);
    }

    private void ApplyImmediateSwitchVisualState(bool switchOn)
    {
        if (grabObjectManager == null)
        {
            return;
        }

        if (switchOn)
        {
            grabObjectManager.OnObjectGrabbed();
        }
        else
        {
            grabObjectManager.OnObjectReleased();
        }
    }

    private void StartSwitchOnSequence()
    {
        if (holdPoint == null)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[ColliderPickupSwitchKeyboardTest] Cannot pick up: holdPoint is not assigned.", this);
            }

            return;
        }

        CleanupCandidates();
        TryGetClosestCandidate(out Rigidbody closestBody);

        if (activeSwitchRoutine != null)
        {
            StopCoroutine(activeSwitchRoutine);
        }

        activeSwitchRoutine = StartCoroutine(PlaySwitchOnSequence(closestBody));
    }

    private IEnumerator PlaySwitchOnSequence(Rigidbody targetBody)
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
        activeSwitchRoutine = null;

        if (!isSwitchOn)
        {
            pendingGrabBody = null;
            yield break;
        }

        if (toolAnimator != null && !string.IsNullOrEmpty(grabbedTriggerName))
        {
            toolAnimator.ResetTrigger(grabbedTriggerName);
            toolAnimator.SetTrigger(grabbedTriggerName);
        }

        if (targetBody == null)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[ColliderPickupSwitchKeyboardTest] No grabbable rigidbody available. Showing grabbed pose without attaching an object.", this);
            }

            pendingGrabBody = null;
            UpdatePickupStatusText();
            yield break;
        }

        if (heldRigidbody != null || isHoldingObject)
        {
            pendingGrabBody = null;
            yield break;
        }

        BeginHold(targetBody);
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
            Debug.Log($"[ColliderPickupSwitchKeyboardTest] Picked up: {heldRigidbody.name}", heldRigidbody);
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

        if (!heldRigidbody.isKinematic)
        {
            heldRigidbody.linearVelocity = Vector3.zero;
            heldRigidbody.angularVelocity = Vector3.zero;
        }

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
        activeSwitchRoutine = null;

        if (enableDebugLogs)
        {
            string nameToLog = string.IsNullOrEmpty(releasedName) ? "<destroyed object>" : releasedName;
            string releaseType = externalRelease ? "Released externally" : "Released";
            Debug.Log($"[ColliderPickupSwitchKeyboardTest] {releaseType}: {nameToLog}", this);
        }

        UpdatePickupStatusText();
    }

    private void CancelActiveSwitchRoutine()
    {
        if (activeSwitchRoutine != null)
        {
            StopCoroutine(activeSwitchRoutine);
            activeSwitchRoutine = null;
        }

        isPreGrabAnimating = false;
        pendingGrabBody = null;
    }

    private bool IsLayerAllowed(int layer)
    {
        return (pickupLayers.value & (1 << layer)) != 0;
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
        if (isSwitchOn || heldRigidbody != null || isHoldingObject)
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