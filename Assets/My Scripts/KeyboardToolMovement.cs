using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class KeyboardToolMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 300f;
    [SerializeField] private float rotationSpeed = 30f;
    [SerializeField] private float verticalRotationSpeed = 30f;

    private Rigidbody rb;
    private Vector2 movementInput;
    private float rotationInput;
    private float verticalRotationInput;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        movementInput = Vector2.zero;
        rotationInput = 0f;
        verticalRotationInput = 0f;

        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.wKey.isPressed)
        {
            movementInput.y += 1f;
        }
        if (keyboard.sKey.isPressed)
        {
            movementInput.y -= 1f;
        }
        if (keyboard.aKey.isPressed)
        {
            movementInput.x -= 1f;
        }
        if (keyboard.dKey.isPressed)
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

        rb.linearVelocity = new Vector3(movement.x, rb.linearVelocity.y, movement.z);
        rb.MoveRotation(rb.rotation * Quaternion.Euler(verticalRotationDelta, rotationDelta, 0f));
    }
}