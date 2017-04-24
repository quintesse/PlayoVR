namespace NetBase {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;

    [CustomEditor(typeof(NetworkParentManager))]
    [CanEditMultipleObjects]
    public class NetworkParentManagerEditor : Editor {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            NetworkParentManager npm = (NetworkParentManager)target;
            string txt = npm.currentParent == NetworkReference.INVALID ? "/" : npm.currentParent.ToString();
            EditorGUILayout.LabelField("Parent", txt);
        }
    }
}
