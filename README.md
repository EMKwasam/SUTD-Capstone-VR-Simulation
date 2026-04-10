# Microkiap XR training Simulation

This is the repositroy containing the Unity assets and scripts for MicroKiap's XR training Simulation. It uses a specific scene file called Surgery (VR + Controller) located at `Assets/Scenes/Surgery (VR + Controller).unity`. Additionally, a bespoke controller is needed. To learn how to set up the controller, please visit the link here
https://github.com/LeanLabel/Depth_Camera_Controller/tree/cleanup

## What You Need
- Unity 6.3 Editor or later (Version in use is 6000.3.10f1)
- A configured controller/joystick input source. See link above for set up
- A tracked tool feed (via UDP tracking receiver setup in scene).

## Set up
1. Pull the files from Github and open them in Unity editor.

<img width="321" height="602" alt="image" src="https://github.com/user-attachments/assets/860ae477-4822-412c-b0ee-79f8e857ab73" />

2. In the project Tab, Select the scene folder

<img width="284" height="207" alt="image" src="https://github.com/user-attachments/assets/4ff2e608-58e3-4f4b-bfb8-6be7f7e030a3" />

3. Double click on the Surgery (VR + Controller) icon to open it in Unity Editor

<img width="475" height="383" alt="image" src="https://github.com/user-attachments/assets/69468d87-dadc-4432-850f-234bfac9a0c5" />

2. In the Heirarchy view, select the object called "XR controller"

  <img width="780" height="601" alt="Photo editing" src="https://github.com/user-attachments/assets/f1bf2731-24a7-42dc-a585-b821988e17ff" />

3. **Optional:** Choose the desired Listening Adddress/ UDP port to match controller configurations. If default configurations are used no changes are necessary
4. Start the Depth Camera Controller code. See Depth Camera Controller Controller Repository for insturctions

<img width="1704" height="160" alt="Photo editing (1)" src="https://github.com/user-attachments/assets/c3fecfb9-40b4-467b-8258-33f63e35a112" />

5. Run the scene in editor by clicking on the start button in editor.

<img width="355" height="84" alt="Photo editing (2)" src="https://github.com/user-attachments/assets/873c4ade-4440-4782-b485-f381f2961713" />

6. To stop the simulation, click on the stop button in editor

## Controls (MicroKiap controller + Keyboard)
- Side Switch (Called `trigger` in Unity): toggles tool between release/grab workflow.
- Handle (Called `Button2` in Unity): Simulates cutting by unlocking tool movement after it is grabbed by MicroKiap.
- Move MicroKiap by moving the controller physically to approach lesions. Once the lesion is grabbed move to the extraction zone to complete the extraction
- Press the `R` key on the keyboard on the to reload the scene/ reset position of MicroKiap


## Basic Workflow
1. Start scene by pressing the play button in Unity Editor
2. Follow instructions on screen to extract the lesion
3. Stop the simulation by pressing the stop button (Located in the same spot as the start button) in Unity Editor
