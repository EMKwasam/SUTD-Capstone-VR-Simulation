using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

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

        // Read joystick stick
        Vector2 stick = joystick.stick.ReadValue();

        text.text =
            $"Joystick: {joystick.displayName}\n" +
            $"Stick X: {stick.x:0.00}\n" +
            $"Stick Y: {stick.y:0.00}";
    }
}

