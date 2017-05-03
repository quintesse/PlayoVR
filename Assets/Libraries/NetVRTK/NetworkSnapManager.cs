namespace NetVRTK {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using VRTK;
    using NetBase;

    [RequireComponent(typeof(VRTK_InteractableObject)), RequireComponent(typeof(PhotonView))]
    public class NetworkSnapManager : NetworkBehaviour {
        private VRTK_InteractableObject io;
        private VRTK_SnapDropZone dropZone;
        private VRTK_SnapDropZone prevDropZone;
        private NetworkReference dropZoneNetRef;

        public NetworkReference currentDropZone {
            get {
                return dropZoneNetRef;
            }
        }

        public override bool HasChanged() {
            return prevDropZone != dropZone;
        }

        public override void Serialize(PhotonStream stream, PhotonMessageInfo info) {
            stream.Serialize(ref dropZoneNetRef.parentHandleId);
            stream.Serialize(ref dropZoneNetRef.pathFromParent);
            //if (HasChanged() || stream.isReading) {
            //    Debug.Log("DROP " + (stream.isWriting ? "Sent " : "Recvd ") + this + " --> " + dropZoneNetRef);
            //}
        }

        public override void Retain() {
            prevDropZone = dropZone;
        }

        public override void Apply() {
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

        void Awake() {
            io = GetComponent<VRTK_InteractableObject>();
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
    }
}
