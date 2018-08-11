namespace PlayoVR {
    using UnityEngine;
    using VRTK;
    using NetBase;

    public class AvatarHMDSyncAction : Photon.PunBehaviour {
        [Tooltip("The avatar's head to sync with the HMD. If empty, a child named 'Head' will be used.")]
        public GameObject AvatarHead;
        [Tooltip("The avatar's left hand to sync with the left controller. If empty, a child named 'Left Hand' will be used.")]
        public GameObject LeftHand;
        [Tooltip("The avatar's right hand to sync with the right controller. If empty, a child named 'Right Hand' will be used.")]
        public GameObject RightHand;
        [Tooltip("The avatar's top point to sync with the HMD. If empty, a child named 'Top' will be used.")]
        public GameObject AvatarTop;

        private Transform headsetTransform;
        private Transform leftHandTransform;
        private Transform rightHandTransform;
        private Transform playAreaTransform;

        void Awake() {
            VRTK_SDKManager.instance.AddBehaviourToToggleOnLoadedSetupChange(this);
        }

        void OnDestroy() {
            VRTK_SDKManager.instance.RemoveBehaviourToToggleOnLoadedSetupChange(this);
        }

        protected virtual void OnEnable() {
            AvatarTop = AvatarTop != null ? AvatarTop : NetUtils.Find(gameObject, "Top");
            AvatarHead = AvatarHead != null ? AvatarHead : NetUtils.Find(gameObject, "Head");
            LeftHand = LeftHand != null ? LeftHand : NetUtils.Find(gameObject, "Left Hand");
            RightHand = RightHand != null ? RightHand : NetUtils.Find(gameObject, "Right Hand");
            headsetTransform = VRTK_DeviceFinder.DeviceTransform(VRTK_DeviceFinder.Devices.Headset).gameObject.transform;
            leftHandTransform = VRTK_DeviceFinder.DeviceTransform(VRTK_DeviceFinder.Devices.LeftController).gameObject.transform;
            rightHandTransform = VRTK_DeviceFinder.DeviceTransform(VRTK_DeviceFinder.Devices.RightController).gameObject.transform;
            playAreaTransform = VRTK_DeviceFinder.PlayAreaTransform();
            Camera.onPreRender += OnCamPreRender;
        }

        protected virtual void OnDisable() {
            Camera.onPreRender -= OnCamPreRender;
        }

        protected virtual void OnCamPreRender(Camera cam) {
            if (cam.gameObject.transform == VRTK_SDK_Bridge.GetHeadsetCamera()) {
                Action();
            }
        }

        protected virtual void Action() {
            if (photonView.isMine) {
                // The avatar follows the position of the HMD projected down to the play area floor
                FollowTransform(gameObject, headsetTransform, playAreaTransform, playAreaTransform);
                // The avatar's head exactly follows the position and rotation of the HMD
                FollowTransform(AvatarHead, headsetTransform, headsetTransform, headsetTransform);
                // The avatar's left hand exactly follows the position and rotation of the left VR controller
                FollowTransform(LeftHand, leftHandTransform, leftHandTransform, leftHandTransform);
                // The avatar's right hand exactly follows the position and rotation of the right VR controller
                FollowTransform(RightHand, rightHandTransform, rightHandTransform, rightHandTransform);
                // The avatar's "top" follows the position of the HMD but not its rotation
                // This is useful for putting labels/icons that need to be floating above an avatar's head
                FollowTransform(AvatarTop, headsetTransform, headsetTransform, null);
            } else {
                // The avatar's "top" follows the position of the Head but not its rotation
                // This is useful for putting labels/icons that need to be floating above an avatar's head
                FollowTransform(AvatarTop, AvatarHead.transform, AvatarHead.transform, null);
            }
        }

        private static void FollowTransform(GameObject avatarComponent, Transform followXZ, Transform followY, Transform followRotation) {
            if (avatarComponent != null) {
                Vector3 pos = new Vector3(followXZ.position.x, followY.position.y, followXZ.position.z);
                avatarComponent.transform.position = pos;
                if (followRotation != null) {
                    avatarComponent.transform.rotation = followRotation.rotation;
                }
            }
        }
    }
}
