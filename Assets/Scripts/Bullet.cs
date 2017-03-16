namespace PlaypenVR {
    using UnityEngine;
    using System.Collections;
    using NetVRTK;

    public class Bullet : MonoBehaviour {
        public AudioClip hitSolidSound;
        public AudioClip hitSoftSound;

        void Start() {
            // Add velocity to the bullet
            GetComponent<Rigidbody>().velocity = transform.forward * 12;
            // Destroy the bullet after 1 second
            Destroy(this.gameObject, 1.0f);
        }

        void OnCollisionEnter(Collision collision) {
            var hit = collision.gameObject;

            // Very stupid check to see if we're hitting a gun
            if (hit.GetComponent<Gun>() != null) {
                return;
            }

            Destroy(gameObject);

            if (PhotonNetwork.isMasterClient) {
                NetworkAudio.SendPlayClipAtPoint(hitSolidSound, transform.position, 1.0f);
            }
        }
    }
}