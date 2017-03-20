namespace NetBase {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class NetworkBehaviourBundle : Photon.MonoBehaviour {
        public List<NetworkBehaviour> observedBehaviours;

        protected void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info) {
            if (stream.isWriting) {
                // Obtain the current state of the observed objects and
                // determine if any of them have changes to send
                bool changed = false;
                foreach (NetworkBehaviour nb in observedBehaviours) {
                    nb.Obtain();
                    changed = changed || nb.HasChanged();
                }

                if (changed) { // Determine if we should send it
                    // Send the new state for each of the observed objects and
                    // have them remember their state for later
                    foreach (NetworkBehaviour nb in observedBehaviours) {
                        nb.Serialize(stream, info);
                        nb.Retain();
                    }
                }
            } else {
                // Receive the new state for each of the observed objects and
                // apply that state to the objects
                foreach (NetworkBehaviour nb in observedBehaviours) {
                    nb.Serialize(stream, info);
                    nb.Apply();
                }
            }
        }

    }
}
