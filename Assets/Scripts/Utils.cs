namespace PlayoVR {
    using UnityEngine;
    using System;
    using NetBase;

    public class Utils {

        static public bool IsHeadless() {
            return SystemInfo.graphicsDeviceID == 0;
        }

        static public bool IsNoVr() {
            return HasStartupFlag("novr");
            //return !VRSettings.enabled || HasStartupFlag("novr");
        }

        static public bool HasStartupFlag(String flag) {
            return Array.IndexOf(Environment.GetCommandLineArgs(), "-" + flag) >= 0;
        }

        static public bool HasStartupOption(String option) {
            foreach (string arg in Environment.GetCommandLineArgs()) {
                if (arg.StartsWith("-" + option + ":")) {
                    return true;
                }
            }
            return false;
        }

        static public string GetStartupOption(String option) {
            foreach (string arg in Environment.GetCommandLineArgs()) {
                if (arg.StartsWith("-" + option + ":")) {
                    return arg.Substring(option.Length + 2);
                }
            }
            return null;
        }

        public static void SetTag(Transform trans, string tag) {
            trans.gameObject.tag = tag;
            if (trans.childCount > 0) {
                foreach (Transform t in trans) {
                    SetTag(t, tag);
                }
            }
        }

        public static void EnableRenderer(Transform trans, bool enable) {
            var comp = trans.gameObject.GetComponent<Renderer>();
            if (comp != null) {
                comp.enabled = enable;
            }
            foreach (Renderer c in trans.gameObject.GetComponentsInChildren(typeof(Renderer), true)) {
                c.enabled = enable;
            }
        }

        public static void EnableCollider(Transform trans, bool enable) {
            var comp = trans.gameObject.GetComponent<Collider>();
            if (comp != null) {
                comp.enabled = enable;
            }
            foreach (Collider c in trans.gameObject.GetComponentsInChildren(typeof(Collider), true)) {
                c.enabled = enable;
            }
        }

        public static void EnableKinematic(Transform trans, bool enable) {
            var comp = trans.gameObject.GetComponent<Rigidbody>();
            if (comp != null) {
                comp.isKinematic = enable;
            }
            foreach (Rigidbody c in trans.gameObject.GetComponentsInChildren(typeof(Rigidbody), true)) {
                c.isKinematic = enable;
            }
        }

        public static Transform Reparent(Transform child, Transform parent) {
            Transform old = child.parent;
            child.position = parent.position;
            child.rotation = parent.rotation;
            child.parent = parent;
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return old;
        }

        public static void DebugTree(Transform t) {
            Debug.Log(NetUtils.GetPath(t));
            for (int i = 0; i < t.childCount; i++) {
                Transform t2 = t.GetChild(i);
                DebugTree(t2);
            }
        }

    }
}
