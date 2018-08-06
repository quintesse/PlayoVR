using UnityEngine;
using System.Collections;
using NetBase;

public class Bullet : MonoBehaviour {
    public AudioClip hitSolidSound;
    public AudioClip hitSoftSound;

    private double timeCreated;

    void Start() {
        // Add velocity to the bullet
        GetComponent<Rigidbody>().velocity = transform.forward * 12;
        timeCreated = PhotonNetwork.time;
    }

    void Update() {
        if (PhotonNetwork.isMasterClient && PhotonNetwork.time - timeCreated > 1) {
            PhotonNetwork.Destroy(gameObject);
        }
    }

    void OnCollisionEnter(Collision collision) {
        var hit = collision.gameObject;

        // Very stupid check to see if we're hitting a gun
        if (hit.GetComponent<Gun>() != null) {
            return;
        }

        if (PhotonNetwork.isMasterClient) {
            PhotonNetwork.Destroy(gameObject);
            NetworkAudio.SendPlayClipAtPoint(hitSolidSound, transform.position, 1.0f);
        }
    }
}
