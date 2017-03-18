namespace NetVRTK {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using VRTK;

    [RequireComponent(typeof(VRTK_InteractableObject)), RequireComponent(typeof(PhotonView))]
    public class NetworkGrabManager : Photon.MonoBehaviour {
        private int grabOwner;
        private int prevGrabOwner;

        void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info) {
            if (stream.isWriting) {
                if (HasChanged()) {
                    stream.Serialize(ref grabOwner);
                }
            } else {
                stream.Serialize(ref grabOwner);
            }
            Retain();
        }

        private bool HasChanged() {
            return prevGrabOwner != grabOwner;
        }

        private PhotonView GetAvatarHandView() {
            if (transform.parent != null) {
                PhotonView pv = GetComponentInParent<PhotonView>();
                return pv;
            }
            return null;
        }

        private void InitState(int ownerId) {
            grabOwner = ownerId;
        }

        private void Retain() {
            prevGrabOwner = grabOwner;
        }

        void Awake() {
            VRTK_InteractableObject obj = GetComponent<VRTK_InteractableObject>();
            obj.InteractableObjectGrabbed += new InteractableObjectEventHandler(HandleGrab);
            obj.InteractableObjectUngrabbed += new InteractableObjectEventHandler(HandleUngrab);
            InitState(photonView.ownerId);
            Retain();
        }

        private void HandleGrab(object sender, InteractableObjectEventArgs e) {
            photonView.TransferOwnership(PhotonNetwork.player);
            InitState(PhotonNetwork.player.ID);
        }

        private void HandleUngrab(object sender, InteractableObjectEventArgs e) {
            photonView.TransferOwnership(PhotonNetwork.player);
            InitState(0);
        }
    }
}
