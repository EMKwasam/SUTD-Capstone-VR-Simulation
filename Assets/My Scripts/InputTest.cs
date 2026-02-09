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

    private XRInteractionManager interactionManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (valueText == null || buttonPressedText == null || interactionStateText == null)
        {
            Debug.LogWarning("UI Text components not assigned!");
        }

        // Find XRInteractionManager in scene
        interactionManager = FindFirstObjectByType<XRInteractionManager>();
        if (interactionManager == null)
        {
            Debug.LogWarning("XRInteractionManager not found in scene!");
        }
    }

    // Update is called once per frame
    void Update()
    {
        float value = testAction.action.ReadValue<float>();
        if (valueText != null)
        {
            valueText.text = "Input Action Value: " + value.ToString("F2");
        }
        Debug.Log("Input Action Value: " + value);

        bool buttonPressed = testAction.action.IsPressed();
        if (buttonPressedText != null)
        {
            buttonPressedText.text = "Is Button Pressed: " + buttonPressed.ToString();
        }
        Debug.Log("Is Button Pressed: " + buttonPressed);

        // Display XR Interaction Manager states
        if (interactionStateText != null && interactionManager != null)
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
}
