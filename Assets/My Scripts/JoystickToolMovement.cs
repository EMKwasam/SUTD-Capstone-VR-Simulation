using UnityEngine;
using UnityEngine.InputSystem;

public class JoystickToolMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField, Range(0f, 1f)] private float fullDeflectionThreshold = 0.95f;
    [SerializeField] private bool enableDebugLogging = true;
    [SerializeField] private float debugLogInterval = 0.5f; // Log every 0.5 seconds

    private Vector2 joystickInput;
    private Rigidbody rb;
    private bool joystickDetectedLastFrame = false;
    private float lastDebugLogTime = 0f;
    private bool isMovementLocked;

    public bool IsMovementLocked => isMovementLocked;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        if (rb == null)
        {
            Debug.LogError("Rigidbody component not found on " + gameObject.name);
        }

        // Check for joystick at start
        if (Joystick.current != null)
        {
            var joystick = Joystick.current;
            Debug.Log($"Joystick detected: {joystick.displayName}");
            Debug.Log($"Joystick description: {joystick.description}");
            Debug.Log($"Joystick layout: {joystick.layout}");
            Debug.Log($"Joystick device ID: {joystick.deviceId}");
            
            // List available controls
            Debug.Log($"Total controls on joystick: {joystick.allControls.Count}");
            Debug.Log("Available controls:");
            foreach (var control in joystick.allControls)
            {
                Debug.Log($"  - {control.name} (path: {control.path})");
            }
            
            joystickDetectedLastFrame = true;
        }
        else
        {
            Debug.LogWarning("No joystick detected at start. Connect a joystick to use this script.");
        }
    }

    private void Update()
    {
        // Read joystick input from the Input System
        joystickInput = ReadJoystickInput();
    }

    private void FixedUpdate()
    {
        // Move the object based on joystick input
        MoveObject();
    }

    private Vector2 ReadJoystickInput()
    {
        Vector2 input = Vector2.zero;
        var joystick = Joystick.current;

        // Check if joystick is connected
        if (joystick == null)
        {
            // Log only when joystick state changes
            if (joystickDetectedLastFrame)
            {
                Debug.LogWarning("Joystick disconnected!");
                joystickDetectedLastFrame = false;
            }
            return input;
        }

        // Log only when joystick is newly detected
        if (!joystickDetectedLastFrame)
        {
            Debug.Log($"Joystick connected: {joystick.displayName}");
            joystickDetectedLastFrame = true;
        }

        // Read stick input directly from joystick.
        Vector2 rawInput = joystick.stick.ReadValue();
        input = new Vector2(
            GetFullDeflectionAxis(rawInput.x),
            GetFullDeflectionAxis(rawInput.y)
        );

        // Enhanced debugging with time-based throttling
        if (enableDebugLogging && Time.time - lastDebugLogTime >= debugLogInterval)
        {
            lastDebugLogTime = Time.time;
            
            Debug.Log($"=== Joystick Debug (t={Time.time:F2}s) ===");
            Debug.Log($"Raw Stick Value: X={rawInput.x:F3}, Y={rawInput.y:F3}, Magnitude={rawInput.magnitude:F3}");
            Debug.Log($"Full-deflection filtered value: X={input.x:F3}, Y={input.y:F3}");
            
            // Check if stick control is actually available and working
            if (joystick.stick == null)
            {
                Debug.LogError("Joystick.stick control is NULL!");
            }
            else
            {
                Debug.Log($"Stick control path: {joystick.stick.path}");
                Debug.Log($"Stick control is actuated: {joystick.stick.IsActuated()}");
            }
            
            // Try reading all axis controls individually
            Debug.Log("Individual axis values:");
            foreach (var control in joystick.allControls)
            {
                if (control is UnityEngine.InputSystem.Controls.AxisControl axisControl)
                {
                    float axisValue = axisControl.ReadValue();
                    if (Mathf.Abs(axisValue) > 0.01f) // Only log non-zero axes
                    {
                        Debug.Log($"  {control.name} = {axisValue:F3}");
                    }
                }
            }
        }

        return input;
    }

    private float GetFullDeflectionAxis(float value)
    {
        // Treat near-full deflection as full to handle real joystick noise.
        if (value >= fullDeflectionThreshold)
        {
            return 1f;
        }

        if (value <= -fullDeflectionThreshold)
        {
            return -1f;
        }

        return 0f;
    }

    private void MoveObject()
    {
        if (rb == null)
        {
            return;
        }

        if (isMovementLocked)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        if (joystickInput == Vector2.zero)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        // Requested mapping:
        // - Forward when stick Y is positive, backward when Y is negative
        // - Left when stick X is positive, right when X is negative
        Vector3 movement = new Vector3(joystickInput.x, 0, joystickInput.y) * moveSpeed;

        // Apply movement to rigidbody
        rb.linearVelocity = new Vector3(movement.x, rb.linearVelocity.y, movement.z);
    }

    // Adjust movement speed at runtime
    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
    }

    public void SetMovementLocked(bool locked)
    {
        isMovementLocked = locked;
        if (isMovementLocked && rb != null)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }
}
