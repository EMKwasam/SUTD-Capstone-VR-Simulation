using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;

#if UNITY_EDITOR
using UnityEditor;
#endif

[InitializeOnLoad]
public class BluetoothControllerInit
{
    static BluetoothControllerInit()
    {
        // Register layout for generic Bluetooth LE device
        InputSystem.RegisterLayout<GenericBluetoothController>(
            matches: new InputDeviceMatcher()
                .WithInterface("Bluetooth")
                .WithProduct("Bluetooth")
        );
        
        Debug.Log("Bluetooth Controller Layout registered");
    }
}

[InputControlLayout(commonUsages = new[] { "Gamepad" })]
public class GenericBluetoothController : Gamepad
{
}
