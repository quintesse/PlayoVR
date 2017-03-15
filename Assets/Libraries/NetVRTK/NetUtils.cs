namespace NetVRTK {
    using UnityEngine;
    using System.Collections;

    public class NetUtils {

        public static void EnablePhotonView(Transform trans, bool enable) {
            var comp = trans.gameObject.GetComponent<PhotonView>();
            if (comp != null) {
                comp.enabled = enable;
            }
            foreach (Rigidbody c in trans.gameObject.GetComponentsInChildren(typeof(PhotonView), true)) {
                comp.enabled = enable;
            }
        }

        public static string GetPath(Transform current) {
            if (current.parent == null)
                return "/" + current.name;
            return GetPath(current.parent) + "/" + current.name;
        }

        public static string RelPath(Transform current, Transform parent) {
            string curPath = GetPath(current);
            string parentPath = GetPath(parent);
            if (curPath.StartsWith(parentPath + "/")) {
                return curPath.Substring(parentPath.Length + 1);
            } else {
                return null;
            }
        }
    }
}