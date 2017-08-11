namespace NetBase {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using Hashtable = ExitGames.Client.Photon.Hashtable;

    public class NetworkParentManager : NetworkBehaviour {
        private NetworkReference parentNetRef;
        private NetworkReference nref;

        public NetworkReference currentParent {
            get {
                return parentNetRef;
            }
        }

        void Awake() {
            nref = NetworkReference.FromTransform(transform);
            parentNetRef = NetworkReference.FromTransform(transform.parent);
            propKey = PROP_KEY_ID + nref.parentHandleId + "$" + (nref.pathFromParent != null ? nref.pathFromParent : "") + "$";
            var dummy = PropertyEventHandler.Instance;
        }

        private void Update() {
            if (PhotonNetwork.room != null) {
                var actualParentNetRef = NetworkReference.FromTransform(transform.parent);
                if (actualParentNetRef != parentNetRef) {
                    InitState(actualParentNetRef);
                    SendState();
                }
            }
        }

        private void InitState(NetworkReference nref) {
            parentNetRef = nref;
        }

        private void ApplyState() {
            var actualNor = NetworkReference.FromTransform(transform.parent);
            if (actualNor != parentNetRef) {
                //Debug.Log("Reparenting from " + actualNor + " to " + parentNetRef);
                GameObject newParent = parentNetRef.FindObject();
                //Debug.Log("New parent " + newParent);
                transform.parent = newParent != null ? newParent.transform : null;
            }
        }

        //
        // Syncing states
        //

        private string propKey;

        public const string PROP_KEY_ID = "npm$";

        protected override string PropKey {
            get {
                return propKey;
            }
        }

        private void SendState() {
            Hashtable content = new Hashtable();
            content.Add("pid", parentNetRef.parentHandleId);
            content.Add("pth", parentNetRef.pathFromParent);
            SetProperties(content);
        }

        protected override void RecvState(Hashtable content) {
            int parentId = (int)content["pid"];
            string path = (string)content["pth"];
            NetworkReference nref = NetworkReference.FromIdAndPath(parentId, path);
            InitState(nref);
            ApplyState();
        }
    }
}

