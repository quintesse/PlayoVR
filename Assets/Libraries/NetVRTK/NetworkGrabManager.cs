namespace NetVRTK {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using VRTK;
    using NetBase;
    using System;
    using Hashtable = ExitGames.Client.Photon.Hashtable;

    [RequireComponent(typeof(VRTK_InteractableObject))]
    public class NetworkGrabManager : NetworkBehaviour {
        public PhotonView[] ownAdditionalPhotonviews;

        private int grabOwner;

        private VRTK_InteractableObject io;
        private NetworkReference nref;

        public int currentGrabOwner {
            get {
                return grabOwner;
            }
        }

        void Awake() {
            io = GetComponent<VRTK_InteractableObject>();
            nref = NetworkReference.FromObject(this.gameObject);
            propKey = PROP_KEY_ID + nref.parentHandleId + "$" + (nref.pathFromParent != null ? nref.pathFromParent : "") + "$";
            var dummy = PropertyEventHandler.Instance;
        }

        void OnEnable() {
            io.InteractableObjectGrabbed += HandleGrab;
            io.InteractableObjectUngrabbed += HandleUngrab;
            if (nref.IsPhotonView) {
                InitState(nref.GetPhotonView().ownerId);
            }
        }

        void OnDisable() {
            io.InteractableObjectGrabbed -= HandleGrab;
            io.InteractableObjectUngrabbed -= HandleUngrab;
        }

        private void HandleGrab(object sender, InteractableObjectEventArgs e) {
            if (nref.IsPhotonView) {
                nref.GetPhotonView().TransferOwnership(PhotonNetwork.player);
            }
            foreach (PhotonView pv in ownAdditionalPhotonviews) {
                pv.TransferOwnership(PhotonNetwork.player);
            }
            InitState(PhotonNetwork.player.ID);
            SendState();
        }

        private void HandleUngrab(object sender, InteractableObjectEventArgs e) {
            InitState(0);
            SendState();
        }

        private void InitState(int ownerId) {
            grabOwner = ownerId;
            io.isGrabbable = (grabOwner == 0);
        }

        //
        // Syncing states
        //

        private string propKey;

        public const string PROP_KEY_ID = "ngm$";

        protected override string PropKey {
            get {
                return propKey;
            }
        }

        private void SendState() {
            Hashtable content = new Hashtable();
            content.Add("go", grabOwner);
            SetProperties(content);
        }

        protected override void RecvState(Hashtable content) {
            InitState((int)content["go"]);
        }
    }
}
