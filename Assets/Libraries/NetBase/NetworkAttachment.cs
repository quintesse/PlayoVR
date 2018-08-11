namespace NetBase {
    using System.Collections.Generic;
    using UnityEngine;

    public class NetworkAttachment : MonoBehaviour {
        public int id;

        private static Dictionary<int, NetworkAttachment> ids = new Dictionary<int, NetworkAttachment>();

        public static NetworkAttachment Find(int id) {
            return ids.ContainsKey(id) ? ids[id] : null;
        }

        void Awake() {
            if (!ids.ContainsKey(id)) {
                ids.Add(id, this);
            } else {
                Debug.LogError("Duplicate ID, NetworkAttachment IDs must be unique!");
            }
        }
    }
}
