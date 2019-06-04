namespace PlayoVR {
    using UnityEngine;
    using VRTK;
    using NetBase;
    using Photon.Pun;

    public class SetupPlayerAvatar : MonoBehaviourPun, IPunInstantiateMagicCallback
    {
        [Tooltip("The avatar's left hand to sync with the left controller. If empty, a child named 'Left Hand' will be used.")]
        public GameObject LeftHand;
        [Tooltip("The avatar's right hand to sync with the right controller. If empty, a child named 'Right Hand' will be used.")]
        public GameObject RightHand;

        public void OnPhotonInstantiate(PhotonMessageInfo info) {
            //Debug.Log("SetupPlayerAvatar.OnPhotonInstantiate has been called");
            string name = photonView.InstantiationData[0].ToString();
            InitPlayer(name);
        }

        private void InitPlayer(string name) {
            // Set player's name
            gameObject.name = name;
            var label = NetUtils.Find(gameObject, "Top/Label");
            if (label != null) {
                if (!photonView.IsMine) {
                    TMPro.TextMeshPro text = label.GetComponent<TMPro.TextMeshPro>();
                    if (text != null) {
                        text.text = name;
                    }
                } else {
                    // We deactivate the label because we don't show a label on our own local avatar
                    label.SetActive(false);
                }
            }
            var icon = NetUtils.Find(gameObject, "Top/Icon");
            if (icon != null && photonView.IsMine) {
                // We deactivate the icon because we don't show a speaker icon on our own local avatar
                icon.SetActive(false);
            }
        }

        void Awake() {
            VRTK_SDKManager.instance.AddBehaviourToToggleOnLoadedSetupChange(this);
        }

        void OnDestroy() {
            VRTK_SDKManager.instance.RemoveBehaviourToToggleOnLoadedSetupChange(this);
        }

        void OnEnable() {
            //Debug.Log("SetupPlayerAvatar.OnEnable has been called");
            if (!photonView.IsMine) {
                return;
            }
            // Move the camera rig to where the player was spawned
            VRTK_SDKManager sdk = VRTK_SDKManager.instance;
            sdk.loadedSetup.actualBoundaries.transform.position = transform.position;
            sdk.loadedSetup.actualBoundaries.transform.rotation = transform.rotation;

            // Add PhotonViewLink objects to the VR controller objects and link them to the avatar's hands
            //Debug.Log("Trying to setup hands");
            LeftHand = LeftHand != null ? LeftHand : NetUtils.Find(gameObject, "Left Hand");
            RightHand = RightHand != null ? RightHand : NetUtils.Find(gameObject, "Right Hand");
            SetUpControllerHandLink(LeftHand, VRTK_DeviceFinder.Devices.LeftController);
            SetUpControllerHandLink(RightHand, VRTK_DeviceFinder.Devices.RightController);
        }

        private static void SetUpControllerHandLink(GameObject avatarComponent, VRTK_DeviceFinder.Devices device) {
            var photonView = avatarComponent.GetComponent<PhotonView>();
            if (photonView == null) {
                Debug.LogError(string.Format("The network representation '{0}' has no {1} component on it.", avatarComponent.name, typeof(PhotonView).Name));
                return;
            }

            GameObject controller = VRTK_DeviceFinder.DeviceTransform(device).gameObject;
            GameObject actual = VRTK_DeviceFinder.GetActualController(controller);
            var link = actual.AddComponent<PhotonViewLink>();
            link.linkedView = photonView;
        }
    }
}
