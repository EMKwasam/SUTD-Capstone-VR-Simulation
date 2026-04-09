using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[RequireComponent(typeof(Collider))]
public class ColliderPickupJoystickSwitchNoAnim : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform holdPoint;
    [SerializeField] private GrabObjectManager grabObjectManager;
    [SerializeField] private TextMeshProUGUI pickupStatusText;
    [SerializeField] private JoystickToolMovement joystickToolMovement;
    [SerializeField] private UdpTrackedPoseReceiver trackedPoseReceiver;

    [Header("Filtering")]
    [SerializeField] private LayerMask pickupLayers = ~0;

    [Header("Input")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private string switchControlName = "trigger";
    [SerializeField] private string movementReleaseControlName = "button2";
    [SerializeField] private float switchOnThreshold = 0.5f;

    [Header("UI Messages")]
    [SerializeField] private string outOfRangeMessage = "Move tool to grabbing distance";
    [SerializeField] private string inRangeMessage = "Press button to grab lesion";
    [SerializeField] private string pressTriggerToCutMessage = "Press trigger to cut";
    [SerializeField] private string grabbedMessage = "Move tool to end zone to extract the lesion";
    [SerializeField] private string noLesionGrabbedMessage = "No Lesion Grabbed! Unlock tool and try again!";

    private readonly List<Rigidbody> candidateBodies = new List<Rigidbody>();

    private Rigidbody heldRigidbody;
    private bool isHoldingObject;
    private bool heldUsedGravity;
    private RigidbodyInterpolation heldInterpolation;
    private Quaternion lastHoldPointRotation;
    private bool hasLastHoldPointRotation;
    private PickupUiState currentUiState = (PickupUiState)(-1);
    private bool isSwitchOn;
    private bool hasCachedSwitchState;

    public bool IsInputLocked
    {
        get
        {
            bool joystickLock = joystickToolMovement != null && joystickToolMovement.IsMovementLocked;
            bool trackedPoseLock = trackedPoseReceiver != null && trackedPoseReceiver.IsExternallyMovementLocked;
            return joystickLock || trackedPoseLock;
        }
    }

    private enum PickupUiState
    {
        OutOfRange,
        InRange,
        NoLesionGrabbed,
        AwaitingCut,
        Grabbed
    }

    private void Awake()
    {
        if (grabObjectManager == null)
        {
            grabObjectManager = GetComponent<GrabObjectManager>();
            if (enableDebugLogs && grabObjectManager != null)
            {
                Debug.Log("[ColliderPickupSwitchNoAnim] Auto-assigned GrabObjectManager from the same GameObject.", this);
            }
        }

        if (joystickToolMovement == null)
        {
            joystickToolMovement = GetComponent<JoystickToolMovement>();
        }

        if (trackedPoseReceiver == null)
        {
            trackedPoseReceiver = FindFirstObjectByType<UdpTrackedPoseReceiver>();
        }

        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
            if (enableDebugLogs)
            {
                Debug.Log($"[ColliderPickupSwitchNoAnim] Set collider '{triggerCollider.name}' to trigger.", this);
            }
        }

        UpdatePickupStatusText();
    }

    private void Start()
    {
        SyncSwitchStateFromInput(true);
    }

    private void Update()
    {
        HandleExternallyReleasedObject();
        CleanupCandidates();
        SyncSwitchStateFromInput(false);
        UpdatePickupStatusText();

        Joystick joystick = Joystick.current;
        if (joystick == null)
        {
            return;
        }

        if (WasButtonPressedThisFrame(joystick, movementReleaseControlName))
        {
            if (heldRigidbody != null || isHoldingObject)
            {
                SetToolMovementLocked(false);
                if (enableDebugLogs)
                {
                    Debug.Log("[ColliderPickupSwitchNoAnim] Trigger pressed. Tool movement unlocked.", this);
                }
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
            Debug.Log($"[ColliderPickupSwitchNoAnim] In range: {body.name}", body);
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
            Debug.Log($"[ColliderPickupSwitchNoAnim] Out of range: {body.name}", body);
        }
    }

    private void SyncSwitchStateFromInput(bool forceImmediateSync)
    {
        Joystick joystick = Joystick.current;
        if (joystick == null)
        {
            return;
        }

        bool switchNowOn = ReadSwitchState(joystick);
        if (!hasCachedSwitchState)
        {
            hasCachedSwitchState = true;
            isSwitchOn = switchNowOn;

            if (forceImmediateSync)
            {
                ApplyImmediateSwitchVisualState(isSwitchOn);
                SetToolMovementLocked(false);
                UpdatePickupStatusText();
            }

            return;
        }

        if (switchNowOn == isSwitchOn)
        {
            return;
        }

        isSwitchOn = switchNowOn;
        if (isSwitchOn)
        {
            HandleSwitchTurnedOn();
        }
        else
        {
            HandleSwitchTurnedOff();
        }
    }

    private bool ReadSwitchState(Joystick joystick)
    {
        if (joystick == null || string.IsNullOrEmpty(switchControlName))
        {
            return false;
        }

        if (string.Equals(switchControlName, "trigger", System.StringComparison.OrdinalIgnoreCase))
        {
            return joystick.trigger != null && joystick.trigger.ReadValue() >= switchOnThreshold;
        }

        ButtonControl switchControl = joystick.TryGetChildControl<ButtonControl>(switchControlName);
        if (switchControl == null)
        {
            return false;
        }

        return switchControl.ReadValue() >= switchOnThreshold;
    }

    private bool WasButtonPressedThisFrame(Joystick joystick, string controlName)
    {
        if (joystick == null || string.IsNullOrEmpty(controlName))
        {
            return false;
        }

        if (string.Equals(controlName, "trigger", System.StringComparison.OrdinalIgnoreCase))
        {
            return joystick.trigger != null && joystick.trigger.wasPressedThisFrame;
        }

        ButtonControl buttonControl = joystick.TryGetChildControl<ButtonControl>(controlName);
        return buttonControl != null && buttonControl.wasPressedThisFrame;
    }

    private void HandleSwitchTurnedOn()
    {
        if (enableDebugLogs)
        {
            Debug.Log("[ColliderPickupSwitchNoAnim] Switch turned on.", this);
        }

        ApplyImmediateSwitchVisualState(true);
        StartSwitchOnSequence();
    }

    private void HandleSwitchTurnedOff()
    {
        if (enableDebugLogs)
        {
            Debug.Log("[ColliderPickupSwitchNoAnim] Switch turned off.", this);
        }

        if (heldRigidbody != null || isHoldingObject)
        {
            ReleaseHeldObject();
        }
        else
        {
            CompleteRelease(null, false);
        }

        ApplyImmediateSwitchVisualState(false);
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
                Debug.Log("[ColliderPickupSwitchNoAnim] Cannot pick up: holdPoint is not assigned.", this);
            }

            return;
        }

        CleanupCandidates();
        if (!TryGetClosestCandidate(out Rigidbody closestBody))
        {
            if (enableDebugLogs)
            {
                Debug.Log("[ColliderPickupSwitchNoAnim] No grabbable rigidbody inside trigger.", this);
            }

            SetToolMovementLocked(false);
            return;
        }

        BeginHold(closestBody);
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

        SetToolMovementLocked(true);

        if (!heldRigidbody.isKinematic)
        {
            heldRigidbody.linearVelocity = Vector3.zero;
            heldRigidbody.angularVelocity = Vector3.zero;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[ColliderPickupSwitchNoAnim] Picked up: {heldRigidbody.name}", heldRigidbody);
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
        SetToolMovementLocked(false);

        if (enableDebugLogs)
        {
            string nameToLog = string.IsNullOrEmpty(releasedName) ? "<destroyed object>" : releasedName;
            string releaseType = externalRelease ? "Released externally" : "Released";
            Debug.Log($"[ColliderPickupSwitchNoAnim] {releaseType}: {nameToLog}", this);
        }

        UpdatePickupStatusText();
    }

    private bool IsLayerAllowed(int layer)
    {
        return (pickupLayers.value & (1 << layer)) != 0;
    }

    private void SetToolMovementLocked(bool shouldLock)
    {
        if (joystickToolMovement != null)
        {
            joystickToolMovement.SetMovementLocked(shouldLock);
        }

        if (trackedPoseReceiver != null)
        {
            trackedPoseReceiver.SetMovementLocked(shouldLock);
        }
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
        bool isObjectHeld = heldRigidbody != null || isHoldingObject;
        if (isSwitchOn && !isObjectHeld)
        {
            nextState = PickupUiState.NoLesionGrabbed;
        }
        else if (isObjectHeld)
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
            case PickupUiState.NoLesionGrabbed:
                pickupStatusText.text = noLesionGrabbedMessage;
                break;
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
