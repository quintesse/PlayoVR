namespace PlayoVR {
    using UnityEngine;

    public class EnableOnWake : MonoBehaviour {
        public GameObject[] objects;

        void Awake() {
            foreach (GameObject obj in objects) {
                obj.SetActive(true);
            }
        }

    }
}
