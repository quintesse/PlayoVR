namespace PlaypenVR {
    using UnityEngine;
    using System.Collections;

    public class Billboard : MonoBehaviour {

        void Update() {
            if (Camera.main != null) {
                transform.LookAt(Camera.main.transform);
            }
        }
    }
}