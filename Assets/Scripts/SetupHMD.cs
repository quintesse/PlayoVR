namespace PlaypenVR {
    using UnityEngine;
    using System.Collections;
    using VRTK;

    public class SetupHMD : MonoBehaviour {
        public GameObject camVr;
        public GameObject camSim;

        void Awake() {
            camVr.SetActive(false);
            camSim.SetActive(false);
            if (Startup.isHeadless()) {
                Debug.Log("Running headless, no cameras active");
                camVr.SetActive(false);
                camSim.SetActive(false);
            } else {
                if (Startup.isNoVr()) {
                    Debug.Log("Using HMD simluator");
                    setupSimulator();
                    camVr.SetActive(false);
                    camSim.SetActive(true);
                } else {
                    Debug.Log("Using SteamVR");
                    setupSteamVR();
                    camVr.SetActive(true);
                    camSim.SetActive(false);
                }
            }
        }

        private void setupSimulator() {
            var sdk = VRTK_SDKManager.instance;
            sdk.systemSDK = VRTK_SDKManager.SupportedSDKs.Simulator;
            sdk.boundariesSDK = VRTK_SDKManager.SupportedSDKs.Simulator;
            sdk.headsetSDK = VRTK_SDKManager.SupportedSDKs.Simulator;
            sdk.controllerSDK = VRTK_SDKManager.SupportedSDKs.Simulator;
            sdk.actualBoundaries = camSim;
            sdk.actualHeadset = camSim.transform.Find("Camera").gameObject;
            sdk.actualLeftController = camSim.transform.Find("LeftHand").gameObject;
            sdk.actualRightController = camSim.transform.Find("RightHand").gameObject;
            sdk.modelAliasLeftController = sdk.actualLeftController.transform.Find("Hand").gameObject;
            sdk.modelAliasRightController = sdk.actualRightController.transform.Find("Hand").gameObject;
        }

        private void setupSteamVR() {
            var sdk = VRTK_SDKManager.instance;
            sdk.systemSDK = VRTK_SDKManager.SupportedSDKs.SteamVR;
            sdk.boundariesSDK = VRTK_SDKManager.SupportedSDKs.SteamVR;
            sdk.headsetSDK = VRTK_SDKManager.SupportedSDKs.SteamVR;
            sdk.controllerSDK = VRTK_SDKManager.SupportedSDKs.SteamVR;
            sdk.actualBoundaries = camVr;
            sdk.actualHeadset = camVr.transform.Find("Camera (head)/Camera (eye)").gameObject;
            sdk.actualLeftController = camVr.transform.Find("Controller (left)").gameObject;
            sdk.actualRightController = camVr.transform.Find("Controller (right)").gameObject;
            sdk.modelAliasLeftController = sdk.actualLeftController.transform.Find("Model").gameObject;
            sdk.modelAliasRightController = sdk.actualRightController.transform.Find("Model").gameObject;
        }
    }
}