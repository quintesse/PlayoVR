namespace NetBase {
    using UnityEngine;
    using UnityEngine.SceneManagement;
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
            if (current == null)
                return null;
            if (current.parent == null)
                return "/" + current.name;
            return GetPath(current.parent) + "/" + current.name;
        }

        public static string RelPath(Transform current, Transform parent) {
            string curPath = GetPath(current);
            string parentPath = GetPath(parent);
            if (curPath != null && parentPath != null && curPath.StartsWith(parentPath + "/")) {
                return curPath.Substring(parentPath.Length + 1);
            } else {
                return null;
            }
        }

        public static GameObject Find(GameObject parent, string name) {
            Transform childTransform = Find(parent != null ? parent.transform : null, name);
            return childTransform != null ? childTransform.gameObject : null;
        }

        public static Transform Find(Transform parent, string name) {
            if (parent != null) {
                return parent.Find(name);
            } else {
                if (name.StartsWith("/")) {
                    for (int i = 0; i < SceneManager.sceneCount; i++) {
                        Scene s = SceneManager.GetSceneAt(i);
                        GameObject childObj = FindInScene(s, name);
                        if (childObj != null) {
                            return childObj.transform;
                        }
                    }
                }
                return null;
            }
        }

        public static GameObject FindInScene(Scene scene, string name) {
            if (name.StartsWith("/")) {
                name = name.Substring(1);
                string childName;
                int p = name.IndexOf("/");
                if (p > 0) {
                    childName = name.Substring(p + 1);
                    name = name.Substring(0, p);
                } else {
                    childName = null;
                }
                GameObject[] roots = scene.GetRootGameObjects();
                foreach (GameObject root in roots) {
                    if (root.name == name) {
                        if (childName != null) {
                            GameObject child = Find(root, childName);
                            if (child != null) {
                                return child;
                            }
                        } else {
                            return root;
                        }
                    }
                }
            }
            return null;
        }
    }
}