# Surgery (VR + Controller) - Detailed Operator and Deployment Guide

## 1. Overview
The `Surgery (VR + Controller)` scene models a lesion extraction workflow using:
- tracked tool motion,
- controller switch-driven grab states,
- end-zone extraction,
- live lesion count UI.

Scene file:
- `Assets/Scenes/Surgery (VR + Controller).unity`

Primary operator outcome:
- Grab lesion -> transport to end zone -> lesion removed -> counter decremented.

## 2. Audience and Use Cases
This document is intended for:
- Operators running simulation sessions.
- Facilitators setting up the scene before training/demo runs.
- Technical staff validating scene behavior.

## 3. Runtime Components Used by This Scene
Custom scripts detected in this scene:
- `ColliderPickupJoystickSwitch`
- `GrabObjectManager`
- `EndZone`
- `GameManager`
- `UdpTrackedPoseReceiver` (class lives in `XRIntegration.cs`)
- `XRToolGrab`

## 4. Dependencies and Setup
## 4.1 Unity and Packages
The project includes XR, Input System, and TextMeshPro dependencies in project packages.

## 4.2 Required Scene Wiring
Before operation, verify:
- Tool object has a collider configured as trigger at runtime.
- `ColliderPickupJoystickSwitch` has references assigned:
  - hold point,
  - animator,
  - status text,
  - grab object manager,
  - optional joystick movement lock component.
- Lesion objects:
  - have tag `Lesion`,
  - have colliders/rigidbodies suitable for pickup.
- End zone:
  - has trigger collider,
  - references a `GameManager`.
- `GameManager`:
  - has lesion count `TextMeshProUGUI` assigned.
- `UdpTrackedPoseReceiver`:
  - has target transform assigned,
  - receives UDP packets on configured address/port.

## 4.3 Input
Current logic uses joystick input via Unity Input System:
- Switch control name default: `button2`
- Trigger control: joystick trigger

Current switch mapping in the script is inverted from common defaults:
- ON when value is below threshold (`0` side)
- OFF when value is above/equal threshold (`1` side)

If your hardware is opposite, adjust `switchOnThreshold` or invert the read logic in script.

## 5. Operator Control Model
## 5.1 Controls
- Switch (`button2`): drives grabbed/released workflow.
- Trigger: unlocks movement if lock is active.

## 5.2 Operator Sequence
1. Start scene and confirm lesion counter appears.
2. Move tool near lesion until in-range guidance appears.
3. Toggle switch to engage grab workflow.
4. If grab succeeds, move to end zone.
5. On end zone trigger, lesion is removed and score updates.
6. Repeat until all lesions are extracted.

## 5.3 Message Interpretation
- `Move tool to grabbing distance`
  - No eligible lesion currently in pickup range.
- `Press button to grab lesion`
  - At least one candidate lesion is in range.
- `No Lesion Grabbed! Unlock tool and try again!`
  - Switch is in ON state, but no lesion attached.
- `Press trigger to cut`
  - Tool is in lock-required state before next action.
- `Move tool to end zone to extract the lesion`
  - Lesion is attached and ready for extraction.

## 6. How Scripts Work Together
## 6.1 Tool Tracking Layer
`UdpTrackedPoseReceiver`:
- Receives UDP pose packets on a background thread.
- Converts source coordinates into Unity space.
- Applies calibration and optional smoothing.
- Writes final pose to tool target transform.

`XRToolGrab`:
- Diagnostic joystick logger for device/control visibility and raw axis values.
- Helps verify hardware input behavior during setup.

## 6.2 Interaction Layer
`ColliderPickupJoystickSwitch`:
- Tracks nearby rigidbody candidates from trigger overlap.
- Reads switch state and trigger.
- On switch ON:
  - applies grabbed visual state,
  - plays pre-grab then grabbed animation,
  - attempts to attach nearest candidate lesion.
- On switch OFF:
  - releases held lesion,
  - applies release visual state.
- Keeps held lesion aligned with hold point.
- Updates operator status text.

`GrabObjectManager`:
- Toggles visual representation for grabbed/released tool states.

## 6.3 Scoring Layer
`EndZone`:
- Trigger area for extraction.
- Detects objects tagged `Lesion` entering the zone.
- Calls score update and destroys lesion root object.

`GameManager`:
- Maintains lesion count display.
- Supports scene reset via keyboard (`R`) when enabled.

## 7. Validation Checklist (Pre-run)
Use this checklist before each operator session.

## 7.1 Scene Integrity
- [ ] Correct scene opened: `Surgery (VR + Controller)`.
- [ ] No missing references in inspector for core scripts.
- [ ] End zone collider is trigger and active.
- [ ] Lesion objects carry `Lesion` tag.

## 7.2 Input and Tracking
- [ ] Joystick/controller detected.
- [ ] `button2` switch transitions are detected.
- [ ] Trigger press unlocks movement.
- [ ] UDP tracking feed is active and tool moves as expected.

## 7.3 Workflow
- [ ] In-range message appears near lesion.
- [ ] Switch ON runs animation and can attach lesion.
- [ ] Lesion follows tool while held.
- [ ] End zone contact removes lesion and decrements counter.

## 8. Troubleshooting Guide
## 8.1 Tool Does Not Move
Possible causes:
- UDP receiver not bound to correct address/port.
- No incoming packets.
- `targetObject` not assigned on receiver.

Actions:
- Verify sender is running.
- Confirm receiver listen settings.
- Confirm target transform assignment.

## 8.2 Cannot Grab Lesions
Possible causes:
- Lesion not in pickup layer.
- Collider overlap not occurring.
- Lesion missing rigidbody.

Actions:
- Confirm lesion layers included in `pickupLayers`.
- Confirm tool collider is trigger.
- Confirm lesion collider + rigidbody setup.

## 8.3 End Zone Does Not Score
Possible causes:
- Lesion tag mismatch.
- End zone trigger not configured.
- Missing `GameManager` reference.

Actions:
- Ensure lesion tag is exactly `Lesion`.
- Ensure end zone collider is trigger.
- Assign/auto-find valid game manager.

## 8.4 Counter Looks Wrong
Possible causes:
- Dynamic lesion spawn/despawn behavior.
- Scene reset or object lifecycle timing.

Actions:
- Re-run checklist.
- Confirm lesion tags are consistent.
- Validate expected lesion count after scene load.

## 8.5 Wrong Switch Direction
Symptom:
- Physical ON/OFF does opposite of expected behavior.

Actions:
- Adjust script threshold and/or inversion logic for switch read.

## 9. Known Limitations
- Candidate selection is nearest rigidbody within trigger overlap.
- Grab success requires proper physics setup on lesion objects.
- Input mapping can differ by hardware profile.
- Tracker latency and packet jitter can affect precision.
- End zone extraction depends on correct lesion tagging and collider routing.

## 10. Recommended Operator SOP
1. Load scene and verify UI counter.
2. Confirm controller detection and switch behavior.
3. Confirm tool tracking movement.
4. Perform one dry-run extraction.
5. Begin session once all checks pass.
