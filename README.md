# VRTK-PUN-Voice-Demo

A small Unity demo project using VRTK for object interaction and VR controller handling,
PhotonNetwork for allowing up to 4 people to join the same scene together and DFVoice so
those people can actually talk to each other.

![Demo Scene](vrtk-pun-voice-demo.png)
## Configuration

To be able to use this example project you'll have to set it up first:

 1. Open the project in Unity.
 2. In the menu bar click on `Window > Photon Unity Networking > Highlight Server Settings`.
 3. In the `Inspector` window change the `AppId` field to your own one [you got from Photon](https://www.photonengine.com/en-US/Dashboard).
 4. Open the `Main` scene found in the `Scenes` folder in the `Project` window.
 5. Hit `Play`!
 
## Testing locally

To test locally without another computer you can create a standalone Non-VR build:

 1. In the menu bar click on `File > Build Settings...`
 2. Click the `Build` button, choose a destination folder and name your executable.
 3. In the Explorer window that popped up right-click on the `.exe` and select `Create shortcut`
 4. Right-click on the newly created shortcut and select `Properties`
 5. In the `Target` field at the very end just after `.exe` type a space and `-novr`
 6. Click `Ok`
 7. Run the executable by double-clicking the shortcut
 8. Hit `Play` in the Unity Editor (or run the `.exe` instead of the shortcut)

## Command line arguments

 - `-novr` : start the program without VR support, will use the monitor plus mouse and keyboard
 - `-sdk:<NAME>` : force the program to use the specified SDK. Valid values for `<NAME>` are:
    - `SteamVR`
    - `Oculus`
    - `Simulator`

## Using networked interactable objects

If you want to add networking to your VRTK interactable objects then you just need to follow a couple of simple steps. This assumes you already have a project that has both the `VRTK` and `Photon Unity Networking` assets (you can find them on the Unity Asset store) and that networking has already been set up. So for each object that has a `VRTK_Interactable_Object` script you do the following:

 - Add a `PhotonView` script
 - Add a `NetworkGrabManager` script
 - Add a `NetworkInterpolatingTransform` script (leave the deafult options)
 - Drag the last two scripts to the `Observed Components` of the `PhotonView`
 - For now you also must make sure that you are not using the `VRTK_ChildOfControllerGrabAttach` script. That limitation will be removed in a future update
 
## Thanks

 - @thestonefox for VRTK
 - @bddckr for allowing me to steal some things (including most of this README!) from
 [his test project ](https://github.com/bddckr/VRTK-PUN-NetworkTest)
 
