using UnityEngine;
using VRTK;

public sealed class AvatarCameraRigSync : MonoBehaviour {
    public GameObject AvatarHead;
    public GameObject LeftHand;
    public GameObject RightHand;

    private void OnEnable() {
        SetUpTransformFollow(AvatarHead, VRTK_DeviceFinder.Devices.Headset);
        SetUpTransformFollow(LeftHand, VRTK_DeviceFinder.Devices.LeftController);
        SetUpTransformFollow(RightHand, VRTK_DeviceFinder.Devices.RightController);
        SetUpControllerHandLink(LeftHand, VRTK_DeviceFinder.Devices.LeftController);
        SetUpControllerHandLink(RightHand, VRTK_DeviceFinder.Devices.RightController);
    }

    private static void SetUpTransformFollow(GameObject avatarComponent, VRTK_DeviceFinder.Devices device) {
        var photonView = avatarComponent.GetComponent<PhotonView>();
        if (photonView == null) {
            Debug.LogError(string.Format("The network representation '{0}' has no {1} component on it.", avatarComponent.name, typeof(PhotonView).Name));
            return;
        }

        if (!photonView.isMine) {
            return;
        }

        var transformFollow = avatarComponent.AddComponent<VRTK_TransformFollow>();
        transformFollow.gameObjectToFollow = VRTK_DeviceFinder.DeviceTransform(device).gameObject;
        transformFollow.followsScale = false;
    }

    private static void SetUpControllerHandLink(GameObject avatarComponent, VRTK_DeviceFinder.Devices device) {
        var photonView = avatarComponent.GetComponent<PhotonView>();
        if (photonView == null) {
            Debug.LogError(string.Format("The network representation '{0}' has no {1} component on it.", avatarComponent.name, typeof(PhotonView).Name));
            return;
        }

        if (!photonView.isMine) {
            return;
        }

        GameObject controller = VRTK_DeviceFinder.DeviceTransform(device).gameObject;
        GameObject actual = VRTK_DeviceFinder.GetActualController(controller);
        var link = actual.AddComponent<PhotonViewLink>();
        link.linkedView = photonView;
    }
}
