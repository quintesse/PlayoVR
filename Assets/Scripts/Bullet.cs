namespace PlaypenVR {
    using UnityEngine;
    using System.Collections;

    public class Bullet : MonoBehaviour {

        void Start() {
            // Destroy the bullet after 2 seconds
            Destroy(this.gameObject, 1.0f);
        }

        void OnCollisionEnter(Collision collision) {
            var hit = collision.gameObject;

            // Very stupid check to see if we're hitting a gun
            if (hit.GetComponent<Gun>() != null) {
                return;
            }

            Destroy(gameObject);
        }
    }
}