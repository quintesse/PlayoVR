namespace NetBase {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class NetworkParentManager : NetworkBehaviour {
        private NetworkReference parentNetRef;
        private NetworkReference prevParentNetRef;

        public NetworkReference currentParent {
            get {
                return parentNetRef;
            }
        }

        public override void Obtain() {
            parentNetRef = NetworkReference.FromTransform(transform.parent);
        }

        public override bool HasChanged() {
            return parentNetRef != prevParentNetRef;
        }

        public override void Serialize(PhotonStream stream, PhotonMessageInfo info) {
            stream.Serialize(ref parentNetRef.parentHandleId);
            stream.Serialize(ref parentNetRef.pathFromParent);
            //if (HasChanged() || stream.isReading) {
            //    Debug.Log("PARENT " + (stream.isWriting ? "Sent " : "Recvd ") + this + " --> " + parentNetRef);
            //}
        }

        public override void Retain() {
            prevParentNetRef = parentNetRef;
        }

        public override void Apply() {
            var actualNor = NetworkReference.FromTransform(transform.parent);
            if (actualNor != parentNetRef) {
                //Debug.Log("Reparenting from " + actualNor + " to " + parentNetRef);
                GameObject newParent = parentNetRef.FindObject();
                //Debug.Log("New parent " + newParent);
                transform.parent = newParent != null ? newParent.transform : null;
            }
            base.Apply();
        }
    }
}
