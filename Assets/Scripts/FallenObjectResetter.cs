using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallenObjectResetter : Photon.MonoBehaviour {
    [Tooltip("The Y value at which the object will be considered to have fallen off the world")]
    public int MaxDepth = -100;

    private Vector3 positionBackup;
    private Quaternion rotationBackup;

	void Awake() {
        positionBackup = transform.position;
        rotationBackup = transform.rotation;
	}
	
	void Update () {
		if (photonView.isMine && transform.position.y < MaxDepth) {
            var rb = GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) {
                rb.isKinematic = true;
                transform.position = positionBackup;
                transform.rotation = rotationBackup;
                rb.position = positionBackup;
                rb.rotation = rotationBackup;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = false;
            } else {
                transform.position = positionBackup;
                transform.rotation = rotationBackup;
            }
        }
    }
}
