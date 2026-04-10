# Microkiap XR training Simulation

This is the repositroy containing the Unity assets and scripts for MicroKiap's XR training Simulation. It uses a specific scene file called Surgery (VR + Controller) located at `Assets/Scenes/Surgery (VR + Controller).unity`. Additionally, a bespoke controller is needed. To learn how to set up the controller, please visit the link here
https://github.com/LeanLabel/Depth_Camera_Controller/tree/cleanup

## What You Need
- Unity 6.3 Editor or later (Version in use is 6000.3.10f1)
- A configured controller/joystick input source. See link above for set up
- A tracked tool feed (via UDP tracking receiver setup in scene).

## Set up
- Pull the files from Github and open them in Unity editor.
- In the Heirarchy view, select the object called "XR controller"
- Choose the desired Listening Adddress/ UDP port to match controller configurations. If default configurations are used no changes are necessary
- Start the Depth Camera Controller code
- Run the scene in editor


## Controls (MicroKiap controller + Keyboard)
- Side Switch (Called `trigger` in Unity): toggles tool between release/grab workflow.
- Handle (Called `Button2` in Unity): Simulates cutting by unlocking tool movement after it is grabbed by MicroKiap.
- Move MicroKiap by moving the controller physically to approach lesions. Once the lesion is grabbed move to the extraction zone to complete the extraction
- Press the `R` key on the keyboard on the to reload the scene/ reset position of MicroKiap


## Basic Workflow
1. Start scene by pressing the play button in Unity Editor
2. Follow instructions on scene to extract the lesion
3. Stop the simulation by pressing the stop button in Unity Editor
