using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class KeyboardToolMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private float rotationSpeed = 1f;
    [SerializeField] private float verticalRotationSpeed = 1f;
    [SerializeField] private ColliderPickupKeyboard colliderPickupKeyboard;

    private Rigidbody rb;
    private Vector2 movementInput;
    private float rotationInput;
    private float verticalRotationInput;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (colliderPickupKeyboard == null)
        {
            colliderPickupKeyboard = GetComponent<ColliderPickupKeyboard>();
        }
    }

    private void Update()
    {
        movementInput = Vector2.zero;
        rotationInput = 0f;
        verticalRotationInput = 0f;

        if (colliderPickupKeyboard != null && colliderPickupKeyboard.IsInputLocked)
        {
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.dKey.isPressed)
        {
            movementInput.y += 1f;
        }
        if (keyboard.aKey.isPressed)
        {
            movementInput.y -= 1f;
        }
        if (keyboard.wKey.isPressed)
        {
            movementInput.x -= 1f;
        }
        if (keyboard.sKey.isPressed)
        {
            movementInput.x += 1f;
        }

        bool shiftHeld = keyboard.leftShiftKey.isPressed;
        if (shiftHeld)
        {
            if (keyboard.qKey.isPressed)
            {
                verticalRotationInput -= 1f;
            }
            if (keyboard.eKey.isPressed)
            {
                verticalRotationInput += 1f;
            }
        }
        else
        {
            if (keyboard.qKey.isPressed)
            {
                rotationInput -= 1f;
            }
            if (keyboard.eKey.isPressed)
            {
                rotationInput += 1f;
            }
        }

        movementInput = Vector2.ClampMagnitude(movementInput, 1f);
    }

    private void FixedUpdate()
    {
        Vector3 localRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 localForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 movement = (localRight * movementInput.x + localForward * movementInput.y) * moveSpeed;
        float rotationDelta = rotationInput * rotationSpeed * Time.fixedDeltaTime;
        float verticalRotationDelta = verticalRotationInput * verticalRotationSpeed * Time.fixedDeltaTime;
        Quaternion rotationStep = Quaternion.Euler(verticalRotationDelta, rotationDelta, 0f);

        if (rb.isKinematic)
        {
            Vector3 targetPosition = rb.position + (movement * Time.fixedDeltaTime);
            rb.MovePosition(targetPosition);

            if (rotationInput != 0f || verticalRotationInput != 0f)
            {
                rb.MoveRotation(rb.rotation * rotationStep);
            }

            return;
        }

        float currentYVelocity = rb.linearVelocity.y;

        if (movementInput == Vector2.zero)
        {
            rb.linearVelocity = new Vector3(0f, currentYVelocity, 0f);
        }
        else
        {
            rb.linearVelocity = new Vector3(movement.x, currentYVelocity, movement.z);
        }

        if (rotationInput == 0f && verticalRotationInput == 0f)
        {
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            rb.MoveRotation(rb.rotation * rotationStep);
        }
    }
}