namespace NetBase {
    using System;
    using System.Collections;
    using UnityEngine;
    using UnityEngine.SceneManagement;

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
            if (name == null) {
                return parent;
            }
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

    public struct NetworkReference {
        public int parentHandleId;
        public string pathFromParent;

        public static NetworkReference INVALID {
            get {
                return FromIdAndPath(0, null);
            }
        }

        public GameObject FindObject() {
            MonoBehaviour parentScript = FindNetworkReferenceParent(parentHandleId);
            Transform parent = (parentScript != null) ? parentScript.transform : null;
            Transform child = NetUtils.Find(parent, pathFromParent);
            return (child != null) ? child.gameObject : null;
        }

        public T FindComponent<T>() {
            GameObject go = FindObject();
            if (go != null) {
                T comp = go.GetComponent<T>();
                if (comp != null) {
                    return comp;
                }
            }
            return default(T);
        }

        public T[] FindComponents<T>() {
            GameObject go = FindObject();
            if (go != null) {
                T[] comps = go.GetComponents<T>();
                if (comps != null) {
                    return comps;
                }
            }
            return default(T[]);
        }

        public bool IsPhotonView {
            get {
                return parentHandleId > 0;
            }
        }

        public bool IsNetworkAttachment {
            get {
                return parentHandleId < 0;
            }
        }

        public PhotonView GetPhotonView() {
            if (parentHandleId > 0) {
                return PhotonView.Find(parentHandleId);
            } else {
                return null;
            }
        }

        public NetworkAttachment GetNetworkAttachment() {
            if (parentHandleId < 0) {
                return NetworkAttachment.Find(parentHandleId);
            } else {
                return null;
            }
        }

        public bool IsChild {
            get {
                return pathFromParent != null;
            }
        }

        public static bool operator ==(NetworkReference nref1, NetworkReference nref2) {
            if (ReferenceEquals(nref1, nref2)) {
                return true;
            }
            if (ReferenceEquals(nref1, null)) {
                return false;
            }
            if (ReferenceEquals(nref2, null)) {
                return false;
            }
            return nref1.Equals(nref2);
        }

        public static bool operator !=(NetworkReference nref1, NetworkReference nref2) {
            return !(nref1 == nref2);
        }

        public bool Equals(NetworkReference other) {
            if (ReferenceEquals(null, other)) {
                return false;
            }
            if (ReferenceEquals(this, other)) {
                return true;
            }
            return parentHandleId == other.parentHandleId
                   && pathFromParent == other.pathFromParent;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) {
                return false;
            }
            if (ReferenceEquals(this, obj)) {
                return true;
            }
            return obj.GetType() == GetType() && Equals((NetworkReference)obj);
        }

        public override int GetHashCode() {
            unchecked {
                int hash = 17;
                hash = hash * 23 + parentHandleId.GetHashCode();
                if (pathFromParent != null) {
                    hash = hash * 23 + pathFromParent.GetHashCode();
                }
                return hash;
            }
        }

        public override string ToString() {
            if (parentHandleId != 0) {
                return pathFromParent + "@" + parentHandleId + "[" + FindNetworkReferenceParent(parentHandleId) + "]";
            } else if (pathFromParent != null) {
                return pathFromParent;
            } else {
                return "INVALID";
            }
        }

        public static NetworkReference FromIdAndPath(int parentHandleId, string pathFromParent) {
            NetworkReference nref;
            nref.parentHandleId = parentHandleId;
            nref.pathFromParent = pathFromParent;
            return nref;
        }

        public static NetworkReference FromObject(GameObject obj) {
            return FromTransform(obj != null ? obj.transform : null);
        }

        public static NetworkReference FromTransform(Transform transform) {
            NetworkReference nref;
            if (transform != null) {
                var handle = GetNetworkHandle(transform);
                nref.parentHandleId = GetNetworkHandleId(handle);
                nref.pathFromParent = GetNetworkHandlePath(transform, handle);
            } else {
                nref.parentHandleId = 0;
                nref.pathFromParent = null;
            }
            return nref;
        }

        private static MonoBehaviour GetNetworkHandle(Transform obj) {
            NetworkAttachment na = obj.GetComponentInParent<NetworkAttachment>();
            if (na != null) {
                return na;
            }
            PhotonView pv = obj.GetComponentInParent<PhotonView>();
            if (pv != null) {
                return pv;
            }
            PhotonViewLink pvl = obj.GetComponentInParent<PhotonViewLink>();
            if (pvl != null) {
                return pvl;
            }
            return null;
        }

        private static int GetNetworkHandleId(MonoBehaviour script) {
            if (script != null) {
                if (script is NetworkAttachment) {
                    NetworkAttachment na = (NetworkAttachment)script;
                    return -na.id;
                }
                if (script is PhotonView) {
                    PhotonView pv = (PhotonView)script;
                    return pv.viewID;
                }
                if (script is PhotonViewLink) {
                    PhotonViewLink pvl = (PhotonViewLink)script;
                    return pvl.linkedView.viewID;
                }
            }
            return 0;
        }

        private static string GetNetworkHandlePath(Transform obj, MonoBehaviour script) {
            if (script != null) {
                if (script.transform == obj) {
                    return null;
                }
                if (script is NetworkAttachment) {
                    NetworkAttachment na = (NetworkAttachment)script;
                    return NetUtils.RelPath(obj.parent, na.transform);
                }
                if (script is PhotonView) {
                    PhotonView pv = (PhotonView)script;
                    return NetUtils.RelPath(obj.parent, pv.transform);
                }
                if (script is PhotonViewLink) {
                    PhotonViewLink pvl = (PhotonViewLink)script;
                    return null; // TODO see if we can return some path here
                }
            }
            return NetUtils.GetPath(obj);
        }

        private static MonoBehaviour FindNetworkReferenceParent(int parentHandleId) {
            MonoBehaviour parent;
            if (parentHandleId > 0) {
                PhotonView pv = PhotonView.Find(parentHandleId);
                parent = pv;
            } else if (parentHandleId < 0) {
                NetworkAttachment na = NetworkAttachment.Find(parentHandleId);
                parent = na;
            } else {
                parent = null;
            }
            return parent;
        }
    }
}