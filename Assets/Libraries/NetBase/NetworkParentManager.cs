namespace NetBase {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using Hashtable = ExitGames.Client.Photon.Hashtable;

    public class NetworkParentManager : EventBehaviour {
        private NetworkReference parentNetRef;
        private NetworkReference nref;

        public NetworkReference currentParent {
            get {
                return parentNetRef;
            }
        }

        void Awake() {
            nref = NetworkReference.FromObject(this.gameObject);
            var dummy = EventHandler.Instance;
        }

        void OnEnable() {
            parentNetRef = NetworkReference.FromTransform(transform.parent);
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

        private void RecvState(Hashtable content) {
            int parentId = (int)content["pid"];
            string path = (string)content["pth"];
            NetworkReference nref = NetworkReference.FromIdAndPath(parentId, path);
            InitState(nref);
            ApplyState();
        }

        private void SendState() {
            Hashtable content = new Hashtable();
            content.Add("pid", parentNetRef.parentHandleId);
            content.Add("pth", parentNetRef.pathFromParent);
            RaiseEvent(content);
        }

        //
        // Event handling
        //

        const byte EVENT_CODE = EVENT_CODE_BASE + 0;

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
                    EventBehaviour.HandleOnEvent<NetworkParentManager>((Hashtable)content, senderid);
                }
            }
        }

        protected override void OnEvent(Hashtable content, int senderid) {
            RecvState(content);
            Debug.Log("RVD PARENT: " + content.ToString());
        }

        protected override void RaiseEvent(Hashtable content) {
            RaiseEvent(EVENT_CODE, nref, content);
            Debug.Log("SNT PARENT: " + content.ToString());
        }
    }
}

