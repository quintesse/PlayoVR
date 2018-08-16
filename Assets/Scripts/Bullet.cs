namespace PlayoVR {
    using UnityEngine;

    public class Bullet : MonoBehaviour {
        public AudioClip hitSolidSound;
        public AudioClip hitSoftSound;

        private double timeCreated;
        private bool shouldDestroy;

        void Start() {
            // Add velocity to the bullet
            GetComponent<Rigidbody>().velocity = transform.forward * 12;
            timeCreated = Time.time;
            shouldDestroy = false;
        }

        void Update() {
            if (shouldDestroy || Time.time - timeCreated > 1) {
                Destroy(gameObject);
            }
        }

        void OnCollisionEnter(Collision collision) {
            var hit = collision.gameObject;

            // Very stupid check to see if we're hitting a gun
            if (hit.GetComponent<Gun>() != null) {
                return;
            }

            AudioSource.PlayClipAtPoint(hitSolidSound, transform.position, 1.0f);
            shouldDestroy = true;
        }
    }
}
