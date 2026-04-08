using UnityEngine;
using UnityEngine.InputSystem;

public class MousePickup : MonoBehaviour
{
    [SerializeField] private Transform holdPoint;
    [SerializeField] private Transform pickupOrigin;
    [SerializeField] private float pickupDistance = 4f;
    [SerializeField] private LayerMask pickupLayers = ~0;
    [SerializeField] private bool showDebugRay = true;
    [SerializeField] private bool enableDebugLogs = true;

    private Rigidbody heldRigidbody;
    private Rigidbody inRangeRigidbody;
    private bool heldUsedGravity;
    private RigidbodyInterpolation heldInterpolation;

    private void Awake()
    {
        if (pickupOrigin == null)
        {
            pickupOrigin = holdPoint != null ? holdPoint : transform;
        }

        if (enableDebugLogs)
        {
            string originName = pickupOrigin != null ? pickupOrigin.name : "None";
            string holdName = holdPoint != null ? holdPoint.name : "None";
            Debug.Log($"[MousePickup] Initialized on {name}. Origin: {originName}, HoldPoint: {holdName}, Distance: {pickupDistance}", this);
        }
    }

    private void Update()
    {
        if (showDebugRay)
        {
            Transform origin = pickupOrigin != null ? pickupOrigin : holdPoint;
            if (origin != null)
            {
                Debug.DrawRay(origin.position, origin.forward * pickupDistance, Color.cyan);
            }
        }

        var mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        TrackGrabRange();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            TryPickup();
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            ReleaseHeldObject();
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugRay)
        {
            return;
        }

        Transform origin = pickupOrigin != null ? pickupOrigin : holdPoint;
        if (origin == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(origin.position, origin.forward * pickupDistance);
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

    private void TrackGrabRange()
    {
        if (pickupOrigin == null)
        {
            inRangeRigidbody = null;
            return;
        }

        Rigidbody currentInRange = null;
        Ray ray = new Ray(pickupOrigin.position, pickupOrigin.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, pickupDistance, pickupLayers, QueryTriggerInteraction.Ignore))
        {
            currentInRange = hit.rigidbody != null ? hit.rigidbody : hit.collider.attachedRigidbody;
        }

        if (currentInRange != null && currentInRange != inRangeRigidbody)
        {
            Debug.Log($"Grabbable object entered range: {currentInRange.name}", currentInRange);
        }

        if (enableDebugLogs && currentInRange == null && inRangeRigidbody != null)
        {
            Debug.Log($"[MousePickup] Grabbable object left range: {inRangeRigidbody.name}", inRangeRigidbody);
        }

        inRangeRigidbody = currentInRange;
    }

    private void TryPickup()
    {
        if (heldRigidbody != null || pickupOrigin == null || holdPoint == null)
        {
            if (enableDebugLogs)
            {
                string reason = heldRigidbody != null
                    ? "already holding an object"
                    : (pickupOrigin == null ? "pickupOrigin is null" : "holdPoint is null");
                Debug.Log($"[MousePickup] Pickup blocked: {reason}", this);
            }
            return;
        }

        Ray ray = new Ray(pickupOrigin.position, pickupOrigin.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, pickupDistance, pickupLayers, QueryTriggerInteraction.Ignore))
        {
            if (enableDebugLogs)
            {
                Debug.Log("[MousePickup] Pickup raycast hit nothing in range.", this);
            }
            return;
        }

        Rigidbody targetRigidbody = hit.rigidbody != null ? hit.rigidbody : hit.collider.attachedRigidbody;
        if (targetRigidbody == null)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[MousePickup] Hit '{hit.collider.name}' but it has no Rigidbody.", hit.collider);
            }
            return;
        }

        heldRigidbody = targetRigidbody;
        heldUsedGravity = heldRigidbody.useGravity;
        heldInterpolation = heldRigidbody.interpolation;

        heldRigidbody.useGravity = false;
        heldRigidbody.linearVelocity = Vector3.zero;
        heldRigidbody.angularVelocity = Vector3.zero;
        heldRigidbody.interpolation = RigidbodyInterpolation.None;

        if (enableDebugLogs)
        {
            Debug.Log($"[MousePickup] Picked up: {heldRigidbody.name}", heldRigidbody);
        }
    }

    private void ReleaseHeldObject()
    {
        if (heldRigidbody == null)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[MousePickup] Release requested, but nothing is currently held.", this);
            }
            return;
        }

        string releasedName = heldRigidbody.name;
        heldRigidbody.useGravity = heldUsedGravity;
        heldRigidbody.interpolation = heldInterpolation;
        heldRigidbody = null;

        if (enableDebugLogs)
        {
            Debug.Log($"[MousePickup] Released: {releasedName}", this);
        }
    }
}