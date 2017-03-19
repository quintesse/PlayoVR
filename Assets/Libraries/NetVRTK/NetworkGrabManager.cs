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

        protected override bool HasChanged() {
            return prevGrabOwner != grabOwner;
        }

        protected override void Serialize(PhotonStream stream, PhotonMessageInfo info) {
            Debug.Log("Ser NGM " + stream.isWriting + " " + NetUtils.GetPath(transform));
            stream.Serialize(ref grabOwner);
        }

        protected override void Retain() {
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
