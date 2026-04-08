# Surgery (VR + Controller) - Operator Quick Guide

## Purpose
This scene simulates lesion extraction with a tracked surgical tool in VR. The operator moves the tool, grabs lesions, and delivers them to the end zone for removal.

Scene file: `Assets/Scenes/Surgery (VR + Controller).unity`

## What You Need
- A build or Unity Play Mode running this scene.
- A configured controller/joystick input source.
- A tracked tool feed (via UDP tracking receiver setup in scene).

## Controls (Operator View)
- Switch (`button2`): toggles tool between release/grab workflow.
- Trigger: unlocks tool movement when lock is active.
- Move tool physically/through tracker to approach lesions and move to extraction zone.

Note: In the current project configuration for this scene, switch state interpretation is custom.

## Basic Workflow
1. Start scene and verify lesion counter is visible.
2. Move tool close to a lesion.
3. Toggle switch to engage grab sequence.
4. If lesion is grabbed, move to end zone and extract.
5. If message says no lesion grabbed, unlock tool and retry.
6. Repeat until lesion counter reaches zero.

## On-screen Messages (What They Mean)
- Move tool to grabbing distance: no valid lesion in range.
- Press button to grab lesion: lesion is in range and ready.
- No Lesion Grabbed! Unlock tool and try again!: switch is engaged but nothing attached.
- Press trigger to cut: movement lock state is active.
- Move tool to end zone to extract the lesion: lesion is attached and ready for extraction.

## Troubleshooting (Quick)
- Tool does not move: check tracker feed and target object assignment.
- Cannot grab lesion: confirm lesion is in range and on pickup layers.
- Counter not updating: verify lesion tag is `Lesion` and end zone trigger is active.
- Stuck after toggling: press trigger to unlock, then retry approach and grab.

## Known Limits (Operator-facing)
- Grabbing depends on trigger overlap and nearest rigidbody candidate.
- Extraction requires correct lesion tagging and end zone trigger contact.
- Input mapping may vary by controller profile.
