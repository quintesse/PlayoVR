namespace PlayoVR {
    using UnityEngine;

    public class Billboard : MonoBehaviour {
        [Tooltip("By default the Billboard will rotate the object towards the Camera. Set to true to make it look away instead.")]
        public bool LookAway = false;

        void Update() {
            if (Camera.main != null) {
                Vector3 forward;
                if (LookAway) {
                    forward = transform.position - Camera.main.transform.position;
                } else {
                    forward = Camera.main.transform.position - transform.position;
                }
                transform.rotation = Quaternion.LookRotation(forward);
            }
        }
    }
}
