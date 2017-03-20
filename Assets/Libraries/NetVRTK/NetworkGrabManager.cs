namespace NetVRTK {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using VRTK;
    using NetBase;

    [RequireComponent(typeof(VRTK_InteractableObject)), RequireComponent(typeof(PhotonView))]
    public class NetworkGrabManager : NetworkBehaviour {
        private int grabOwner;
        private int prevGrabOwner;

        public override bool HasChanged() {
            return prevGrabOwner != grabOwner;
        }

        public override void Serialize(PhotonStream stream, PhotonMessageInfo info) {
            stream.Serialize(ref grabOwner);
        }

        public override void Retain() {
            prevGrabOwner = grabOwner;
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
