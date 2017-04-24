namespace NetVRTK {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;
    using NetBase;

    [CustomEditor(typeof(NetworkSnapManager))]
    [CanEditMultipleObjects]
    public class NetworkSnapManagerEditor : Editor {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            NetworkSnapManager nsm = (NetworkSnapManager)target;
            string txt = nsm.currentDropZone == NetworkReference.INVALID ? "Not Snapped" : nsm.currentDropZone.ToString();
            EditorGUILayout.LabelField("Snapped To", txt);
        }
    }
}
