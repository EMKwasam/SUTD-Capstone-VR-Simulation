# My Scripts

This folder contains the custom Unity scripts used for the surgery simulation. The scripts are organized around four main areas: tool interaction, tracking and input, game state, and debugging or legacy support.

## Folder Layout

- `ColliderPickupJoystickSwitch.cs`: Main joystick-driven grab flow for the surgical tool.
- `ColliderPickupJoystickSwitchKeyboardTest.cs`: Keyboard test version of the joystick switch workflow.
- `EndZone.cs`: Detects lesion entry into the extraction zone and removes the lesion.
- `GameManager.cs`: Tracks remaining lesions and updates the on-screen counter.
- `GrabObjectManager.cs`: Toggles visual or object state when the tool is grabbed or released.
- `XRIntegration.cs`: Contains `UdpTrackedPoseReceiver`, the UDP pose receiver used for tracked tool motion.
- `XRToolGrab.cs`: Joystick input diagnostics and control logging.

## Legacy Scripts

The Legacy folder keeps earlier interaction and movement scripts that are no longer the primary path but may still be useful for reference or fallback testing.

- `ColliderPickupJoystick.cs`: Earlier joystick-based pickup flow.
- `ColliderPickupKeyboard.cs`: Keyboard-driven pickup flow.
- `ColliderPickupMouse.cs`: Mouse-driven pickup flow.
- `JoystickToolMovement.cs`: Joystick movement controller for the tool.
- `KeyboardToolMovement.cs`: Keyboard movement controller for the tool.
- `MousePickup.cs`: Mouse interaction helper for picking up objects.

## Utility Scripts

The Utility folder contains debugging and test helpers for input and tracking.

- `DeviceDebugger.cs`: Logs all detected input devices and their controls.
- `GamepadDebug.cs`: Displays live gamepad input values.
- `InputTest.cs`: Tests input action values and XR interaction state.
- `JoystickDebug.cs`: Displays live joystick input values.
- `XRTest.cs`: Contains `UdpTrackedPoseTestSender`, a local UDP pose sender for testing the tracking receiver.

## Script Roles

- Pickup scripts handle candidate detection, grab timing, release behavior, and on-screen status messages.
- Tracking scripts move the tool using UDP pose data and controller input.
- Manager scripts coordinate lesion scoring, object state changes, and extraction behavior.
- Debug scripts help confirm that Unity Input System devices, XR interaction state, and UDP pose packets are working correctly.

## Notes

- Lesion objects are expected to use the Lesion tag.
- The end zone expects a trigger collider.
- Several scripts depend on Unity Input System, TextMeshPro, and XR-related packages.
- The legacy folder is kept for reference and comparison, not as the primary runtime path.