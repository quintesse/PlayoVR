using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRTK;

public class SetupPlayerAvatar : Photon.MonoBehaviour {

	void Awake () {
        if (!photonView.isMine) {
            return;
        }
        // Move the camera rig to where the player was spawned
        VRTK_SDKManager sdk = VRTK_SDKManager.instance;
        sdk.actualBoundaries.transform.position = transform.position;
        sdk.actualBoundaries.transform.rotation = transform.rotation;
    }
}
