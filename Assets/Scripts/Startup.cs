namespace PlaypenVR {
    using UnityEngine;
    using UnityEngine.VR;
    using System;
    using System.Collections;

    public class Startup : MonoBehaviour {

        static public bool isHeadless() {
            return SystemInfo.graphicsDeviceID == 0;
        }

        static public bool hasStartupFlag(String flag) {
            return Array.IndexOf(Environment.GetCommandLineArgs(), "-" + flag) >= 0;
        }

        static public bool isNoVr() {
            return !VRSettings.enabled || hasStartupFlag("novr");
        }

        void Start() {
            Debug.Log("Connecting to Photon network...");
            PhotonNetwork.ConnectUsingSettings("0.1");
        }
    }
}