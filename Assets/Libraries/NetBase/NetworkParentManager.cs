namespace NetBase {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class NetworkParentManager : NetworkBehaviour {
        private NetworkReference parentNetRef;
        private NetworkReference prevParentNetRef;

        public override void Obtain() {
            parentNetRef = NetUtils.GetObjectNetworkReference(transform.parent);
        }

        public override bool HasChanged() {
            return parentNetRef != prevParentNetRef;
        }

        public override void Serialize(PhotonStream stream, PhotonMessageInfo info) {
            stream.Serialize(ref parentNetRef.parentHandleId);
            stream.Serialize(ref parentNetRef.pathFromParent);
        }

        public override void Retain() {
            prevParentNetRef = parentNetRef;
        }

        public override void Apply() {
            var actualNor = NetUtils.GetObjectNetworkReference(transform.parent);
            if (actualNor != parentNetRef) {
                //Debug.Log("Reparenting from " + actualNor + " to " + parentNetRef);
                GameObject newParent = NetUtils.FindNetworkReferenceObject(ref parentNetRef);
                //Debug.Log("New parent " + newParent);
                transform.parent = newParent != null ? newParent.transform : null;
            }
            base.Apply();
        }
    }
}
