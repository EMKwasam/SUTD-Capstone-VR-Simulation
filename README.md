# Microkiap XR training Simulation

This is the repositroy containing the Unity assets and scripts for MicroKiap's XR training Simulation. It uses a specific scene file called Surgery (VR + Controller) located at `Assets/Scenes/Surgery (VR + Controller).unity`. Additionally, a bespoke controller is needed. To learn how to set up the controller, please visit the link here
https://github.com/LeanLabel/Depth_Camera_Controller/tree/cleanup

## What You Need
- Unity 6.3 Editor or later (Version in use is 6000.3.10f1)
- A configured controller/joystick input source. See link above for set up
- A tracked tool feed (via UDP tracking receiver setup in scene).

#Set up
- Pull the files from Github and open them in Unity editor.
- In the Heirarchy view, select the object called "XR controller"
- Choose the desired Listening Adddress/ UDP port to match controller configurations. If default configurations are used no changes are necessary
- Start the Depth Camera Controller code
- Run the scene in editor


## Controls (Operator View)
- Switch (`button2`): toggles tool between release/grab workflow.
- Trigger: unlocks tool movement when lock is active.
- Move tool physically/through tracker to approach lesions and move to extraction zone.
- Press "R" to reload the scene

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
