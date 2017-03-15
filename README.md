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
 2. Click `Player Settings...`
 3. Uncheck `Other Settings -> Virtual Reality Supported`
 4. In the `Build Settings` click the `Build` button, choose a destination folder and name your executable.
 5. Run the executable you just built
 6. Hit Play in the Editor!

## Thanks

 - @thestonefox for VRTK
 - @bddckr for allowing me to steal some things (including most of this README!) from
 [his test project ](https://github.com/bddckr/VRTK-PUN-NetworkTest)
 
