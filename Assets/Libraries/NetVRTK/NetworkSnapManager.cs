namespace NetVRTK {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using VRTK;
    using NetBase;
    using Hashtable = ExitGames.Client.Photon.Hashtable;

    [RequireComponent(typeof(VRTK_InteractableObject))]
    public class NetworkSnapManager : EventBehaviour {
        private VRTK_InteractableObject io;
        private VRTK_SnapDropZone dropZone;
        private NetworkReference dropZoneNetRef;
        private NetworkReference nref;

        public NetworkReference currentDropZone {
            get {
                return dropZoneNetRef;
            }
        }

        void Awake() {
            io = GetComponent<VRTK_InteractableObject>();
            nref = NetworkReference.FromObject(this.gameObject);
            var dummy = EventHandler.Instance;
        }

        void OnEnable() {
            io.InteractableObjectSnappedToDropZone += HandleSnappedToDropZone;
            io.InteractableObjectUnsnappedFromDropZone += HandleUnsnappedFromDropZone;
            if (io.IsInSnapDropZone()) {
                dropZone = io.GetStoredSnapDropZone();
                dropZoneNetRef = NetworkReference.FromTransform(dropZone.transform);
            } else {
                dropZone = null;
                dropZoneNetRef = NetworkReference.INVALID;
            }
        }

        void OnDisable() {
            io.InteractableObjectSnappedToDropZone -= HandleSnappedToDropZone;
            io.InteractableObjectUnsnappedFromDropZone -= HandleUnsnappedFromDropZone;
        }

        private void HandleSnappedToDropZone(object sender, InteractableObjectEventArgs e) {
            dropZone = io.GetStoredSnapDropZone();
            dropZoneNetRef = NetworkReference.FromTransform(dropZone.transform);
        }

        private void HandleUnsnappedFromDropZone(object sender, InteractableObjectEventArgs e) {
            dropZone = null;
            dropZoneNetRef = NetworkReference.INVALID;
        }

        private void InitState(NetworkReference nref) {
            dropZoneNetRef = nref;
        }

        private void ApplyState() {
            if (dropZoneNetRef == NetworkReference.INVALID) {
                if (io.IsInSnapDropZone()) {
                    dropZone.ForceUnsnap();
                    dropZone = null;
                }
            } else {
                GameObject dzobj = dropZoneNetRef.FindObject();
                if (dzobj != null) {
                    VRTK_SnapDropZone newDropZone = dzobj.GetComponent<VRTK_SnapDropZone>();
                    if (newDropZone != null) {
                        if (newDropZone != dropZone) {
                            if (dropZone != null) {
                                dropZone.ForceUnsnap();
                            }
                            newDropZone.ForceSnap(io.gameObject);
                            dropZone = newDropZone;
                        }
                    } else {
                        Debug.LogError("DropZoneNetRef doesn't have a VRTK_SnapDropZone: " + dropZoneNetRef);
                    }
                } else {
                    Debug.LogError("Couldn't find DropZoneNetRef: " + dropZoneNetRef);
                }
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
            content.Add("pid", dropZoneNetRef.parentHandleId);
            content.Add("pth", dropZoneNetRef.pathFromParent);
            RaiseEvent(content);
        }

        //
        // Event handling
        //

        const byte EVENT_CODE = EVENT_CODE_BASE + 2;

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
                    EventBehaviour.HandleOnEvent<NetworkSnapManager>((Hashtable)content, senderid);
                }
            }
        }

        protected override void OnEvent(Hashtable content, int senderid) {
            RecvState(content);
            Debug.Log("RVD SNAP: " + content.ToString());
        }

        protected override void RaiseEvent(Hashtable content) {
            RaiseEvent(EVENT_CODE, nref, content);
            Debug.Log("SNT SNAP: " + content.ToString());
        }
    }
}
