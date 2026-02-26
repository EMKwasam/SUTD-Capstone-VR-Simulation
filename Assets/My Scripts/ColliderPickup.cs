using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class ColliderPickup : MonoBehaviour
{
    [SerializeField] private Transform holdPoint;
    [SerializeField] private LayerMask pickupLayers = ~0;
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private GrabObjectManager grabObjectManager;

    private readonly List<Rigidbody> candidateBodies = new List<Rigidbody>();
    private Rigidbody heldRigidbody;
    private bool isHoldingObject;
    private bool heldUsedGravity;
    private RigidbodyInterpolation heldInterpolation;
    private Collider triggerCollider;

    private void Awake()
    {
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
    }

    private void Update()
    {
        HandleExternallyReleasedObject();

        var mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (heldRigidbody == null)
            {
                TryPickupClosest();
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
    }

    private void TryPickupClosest()
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
        if (candidateBodies.Count == 0)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[ColliderPickup] No grabbable rigidbody inside trigger.", this);
            }
            return;
        }

        Rigidbody closestBody = null;
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

        if (closestBody == null)
        {
            return;
        }

        heldRigidbody = closestBody;
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
    }

    private bool IsLayerAllowed(int layer)
    {
        return (pickupLayers.value & (1 << layer)) != 0;
    }

    private void CleanupCandidates()
    {
        candidateBodies.RemoveAll(body => body == null);
    }
}