using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;

public class InputTest : MonoBehaviour
{
    public InputActionProperty testAction;
    public TextMeshProUGUI valueText;
    public TextMeshProUGUI buttonPressedText;
    public TextMeshProUGUI interactionStateText;

    [Header("XR Settings")]
    [Tooltip("Enable XR-specific features. If false, only shows input values.")]
    public bool enableXRFeatures = true;

    private XRInteractionManager interactionManager;
    private bool isXRAvailable = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (valueText == null || buttonPressedText == null)
        {
            Debug.LogWarning("UI Text components not assigned!");
        }

        // Enable input action
        if (testAction.action != null)
        {
            testAction.action.Enable();
        }

        // Check for XR features
        if (enableXRFeatures)
        {
            // Find XRInteractionManager in scene
            interactionManager = FindFirstObjectByType<XRInteractionManager>();
            if (interactionManager != null)
            {
                isXRAvailable = true;
                Debug.Log("XR features enabled and XRInteractionManager found.");
            }
            else
            {
                Debug.LogWarning("XR features enabled but XRInteractionManager not found in scene. Running in non-XR mode.");
                if (interactionStateText != null)
                {
                    interactionStateText.text = "XR Mode: Disabled (No XRInteractionManager found)";
                }
            }
        }
        else
        {
            Debug.Log("Running in non-XR mode.");
            if (interactionStateText != null)
            {
                interactionStateText.text = "XR Mode: Disabled";
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Read and display input action value
        float value = testAction.action.ReadValue<float>();
        if (valueText != null)
        {
            valueText.text = "Input Action Value: " + value.ToString("F2");
        }

        // Check if button is pressed
        bool buttonPressed = testAction.action.IsPressed();
        if (buttonPressedText != null)
        {
            buttonPressedText.text = "Is Button Pressed: " + buttonPressed.ToString();
        }

        // Display XR Interaction Manager states only if XR is available
        if (isXRAvailable && interactionStateText != null && interactionManager != null)
        {
            string interactionInfo = GetInteractionState();
            interactionStateText.text = interactionInfo;
        }
    }

    string GetInteractionState()
    {
        string info = "XR Interaction State\n";

        // Get registered interactors
        List<IXRInteractor> interactors = new List<IXRInteractor>();
        interactionManager.GetRegisteredInteractors(interactors);
        info += $"Registered Interactors: {interactors.Count}\n";

        // Get registered interactables
        List<IXRInteractable> interactables = new List<IXRInteractable>();
        interactionManager.GetRegisteredInteractables(interactables);
        info += $"Registered Interactables: {interactables.Count}\n";

        // Get registered interaction groups
        List<IXRInteractionGroup> groups = new List<IXRInteractionGroup>();
        interactionManager.GetRegisteredInteractionGroups(groups);
        info += $"Interaction Groups: {groups.Count}\n";

        // Display last focused interactable
        if (interactionManager.lastFocused != null)
        {
            var focusedObject = interactionManager.lastFocused as MonoBehaviour;
            info += $"Last Focused: {focusedObject?.gameObject.name ?? "Unknown"}\n";
        }
        else
        {
            info += "Last Focused: None\n";
        }

        return info;
    }

    void OnDestroy()
    {
        // Disable input action when destroyed
        if (testAction.action != null)
        {
            testAction.action.Disable();
        }
    }
}
