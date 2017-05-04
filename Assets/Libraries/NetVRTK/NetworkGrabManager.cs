namespace NetVRTK {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using VRTK;
    using NetBase;

    [RequireComponent(typeof(VRTK_InteractableObject)), RequireComponent(typeof(PhotonView))]
    public class NetworkGrabManager : NetworkBehaviour {
        public PhotonView[] ownAdditionalPhotonviews;

        private int grabOwner;
        private int prevGrabOwner;

        private VRTK_InteractableObject io;

        public int currentGrabOwner {
            get {
                return grabOwner;
            }
        }

        public override bool HasChanged() {
            return prevGrabOwner != grabOwner;
        }

        public override void Serialize(PhotonStream stream, PhotonMessageInfo info) {
            stream.Serialize(ref grabOwner);
            //if (HasChanged() || stream.isReading) {
            //    Debug.Log("GRAB " + (stream.isWriting ? "Sent " : "Recvd ") + this + " --> " + grabOwner);
            //}
        }

        public override void Retain() {
            prevGrabOwner = grabOwner;
        }

        public override void Apply() {
            io.isGrabbable = (grabOwner == 0);
        }

        private void InitState(int ownerId) {
            grabOwner = ownerId;
        }

        void Awake() {
            io = GetComponent<VRTK_InteractableObject>();
        }

        void OnEnable() {
            io.InteractableObjectGrabbed += HandleGrab;
            io.InteractableObjectUngrabbed += HandleUngrab;
            InitState(photonView.ownerId);
            Retain();
        }

        void OnDisable() {
            io.InteractableObjectGrabbed -= HandleGrab;
            io.InteractableObjectUngrabbed -= HandleUngrab;
        }

        private void HandleGrab(object sender, InteractableObjectEventArgs e) {
            photonView.TransferOwnership(PhotonNetwork.player);
            foreach (PhotonView pv in ownAdditionalPhotonviews) {
                pv.TransferOwnership(PhotonNetwork.player);
            }
            InitState(PhotonNetwork.player.ID);
        }

        private void HandleUngrab(object sender, InteractableObjectEventArgs e) {
            //photonView.TransferOwnership(PhotonNetwork.player);
            //foreach (PhotonView pv in ownAdditionalPhotonviews) {
            //    pv.TransferOwnership(PhotonNetwork.player);
            //}
            InitState(0);
        }
    }
}
