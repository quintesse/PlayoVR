namespace NetVRTK {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using VRTK;
    using NetBase;
    using System;
    using Hashtable = ExitGames.Client.Photon.Hashtable;

    [RequireComponent(typeof(VRTK_InteractableObject))]
    public class NetworkGrabManager : EventBehaviour {
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
            var dummy = EventHandler.Instance;
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

        private void RecvState(Hashtable content) {
            InitState((int)content["go"]);
        }

        private void SendState() {
            Hashtable content = new Hashtable();
            content.Add("go", grabOwner);
            RaiseEvent(content);
        }

        //
        // Event handling
        //

        const byte EVENT_CODE = EVENT_CODE_BASE + 1;

        sealed class EventHandler {
            private static readonly EventHandler instance = new EventHandler();

            private EventHandler() {
                PhotonNetwork.OnEventCall += EventHandler.OnEvent;
            }

            public static EventHandler Instance {
                get {
                    return instance;
                }
            }

            public static void OnEvent(byte eventcode, object content, int senderid) {
                if (eventcode == EVENT_CODE) {
                    EventBehaviour.HandleOnEvent<NetworkGrabManager>((Hashtable)content, senderid);
                }
            }
        }

        protected override void OnEvent(Hashtable content, int senderid) {
            RecvState(content);
            Debug.Log("RVD GRAB: " + content.ToString());
        }

        protected override void RaiseEvent(Hashtable content) {
            RaiseEvent(EVENT_CODE, nref, content);
            Debug.Log("SNT GRAB: " + content.ToString());
        }
    }
}
