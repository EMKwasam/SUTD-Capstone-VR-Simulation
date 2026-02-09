using UnityEngine;
using UnityEngine.InputSystem;

public class DeviceDebugger : MonoBehaviour
{
    void Start()
    {
        Debug.Log("=== ALL INPUT DEVICES ===");
        
        foreach (var device in InputSystem.devices)
        {
            Debug.Log($"\n--- Device: {device.displayName} ---");
            Debug.Log($"Layout: {device.layout}");
            Debug.Log($"Interface: {device.description.interfaceName}");
            Debug.Log($"Full Descriptor: {device.description}");
            Debug.Log($"Device Type: {device.GetType().Name}");
        }
        
        Debug.Log("\n=== END DEVICES ===");
    }

    void Update()
    {
        foreach (var device in InputSystem.devices)
        {
            if (device.description.interfaceName.Contains("Bluetooth") || 
                device.displayName.Contains("Bluetooth"))
            {
                Debug.Log($"Bluetooth Device: {device.displayName}");
                Debug.Log($"Layout: {device.layout}");
                Debug.Log($"Descriptor: {device.description}");
                
                // Try to read all children/controls
                var controls = device.allControls;
                foreach (var control in controls)
                {
                    Debug.Log($"  - Control: {control.path} ({control.GetType().Name})");
                }
            }
        }
    }

}
