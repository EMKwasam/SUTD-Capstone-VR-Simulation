using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class JoystickDebug : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;

    void Update()
    {
        var joystick = Joystick.current;
        if (joystick == null)
        {
            text.text = "No joystick detected.";
            return;
        }

        // Read all available joystick inputs
        Vector2 stick = joystick.stick.ReadValue();
        float trigger = joystick.trigger.ReadValue();

        // Build display string with all controls
        string displayText = $"Joystick: {joystick.displayName}\n";
        displayText += $"Stick: X={stick.x:0.00}, Y={stick.y:0.00}\n";
        displayText += $"Trigger: {trigger:0.00}\n";
        displayText += $"\nAll Controls:\n";

        // List all controls available on this joystick
        foreach (var control in joystick.allControls)
        {
            string controlName = control.name;
            string controlPath = control.path;
            
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

