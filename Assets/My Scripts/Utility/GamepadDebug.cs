using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class GamepadDebug : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;

    void Update()
    {
        var gamepad = Gamepad.current;
        if (gamepad == null)
        {
            text.text = "No gamepad detected.";
            return;
        }

        // Read all available gamepad inputs
        Vector2 leftStick = gamepad.leftStick.ReadValue();
        Vector2 rightStick = gamepad.rightStick.ReadValue();
        float leftTrigger = gamepad.leftTrigger.ReadValue();
        float rightTrigger = gamepad.rightTrigger.ReadValue();
        Vector2 dpad = gamepad.dpad.ReadValue();

        // Build display string with all controls
        string displayText = $"Gamepad: {gamepad.displayName}\n";
        displayText += $"Left Stick: X={leftStick.x:0.00}, Y={leftStick.y:0.00}\n";
        displayText += $"Right Stick: X={rightStick.x:0.00}, Y={rightStick.y:0.00}\n";
        displayText += $"Left Trigger: {leftTrigger:0.00}\n";
        displayText += $"Right Trigger: {rightTrigger:0.00}\n";
        displayText += $"DPad: X={dpad.x:0.00}, Y={dpad.y:0.00}\n";
        displayText += $"\nAll Controls:\n";

        // List all controls available on this gamepad
        foreach (var control in gamepad.allControls)
        {
            string controlName = control.name;
            
            try
            {
                if (control is AxisControl axisControl)
                {
                    float value = axisControl.ReadValue();
                    displayText += $"{controlName}: {value:0.00}\n";
                }
                else if (control is ButtonControl buttonControl)
                {
                    bool isPressed = buttonControl.isPressed;
                    displayText += $"{controlName}: {(isPressed ? "PRESSED" : "released")}\n";
                }
                else if (control is Vector2Control vec2Control)
                {
                    Vector2 value = vec2Control.ReadValue();
                    displayText += $"{controlName}: ({value.x:0.00}, {value.y:0.00})\n";
                }
            }
            catch
            {
                // Skip controls that can't be read
            }
        }

        text.text = displayText;
    }
}
