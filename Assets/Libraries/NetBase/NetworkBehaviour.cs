namespace NetBase {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public abstract class NetworkBehaviour : Photon.MonoBehaviour {
        [Tooltip("Only send updates if any of the tracked values have changed")]
        public bool onChangeOnly = false;

        protected void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info) {
            if (stream.isWriting) {
                Obtain(); // Obtain the current state of the object
                // FIXME PhotonNetwork doesn't make it at all easy to try to only send data when needed
                // so for now we completely disable change detection :(
                if (true /*!onChangeOnly || HasChanged()*/) { // Determine if we should send it
                    Serialize(stream, info); // Send the new state
                    Retain(); // Remember the state for later
                }
            } else {
                Serialize(stream, info); // Receive the new state
                Apply(); // Apply the new state to the object
            }
        }

        /// <summary>
        /// Gets called to obtain the current state of the object.
        /// If the state can be read directly from the object's state
        /// when writing to the stream it won't be necessary to
        /// implement this.
        /// </summary>
        public virtual void Obtain() {
        }

        /// <summary>
        /// Gets called to determine if the object's state has changed
        /// with respect to the previous state. Return `true` is it has
        /// or if you don't want to bother checking for changes. Return
        /// false if the state is the same and no update needs to be sent.
        /// </summary>
        /// <returns>Boolean indicating the object's state has changed</returns>
        public abstract bool HasChanged();

        /// <summary>
        /// Gets called to read/write the object's state to/from the Photon stream
        /// </summary>
        public abstract void Serialize(PhotonStream stream, PhotonMessageInfo info);

        /// <summary>
        /// Gets called to store a copy of the current state so it can be
        /// used to check for changes in the state. If no change checks
        /// are being done it won't be necessary to implement this.
        /// </summary>
        public virtual void Retain() {
        }

        /// <summary>
        /// Gets called to apply the newly received state to the object.
        /// If the state was applied directly to the object's state when
        /// reading from the stream it won't be necessary to implement this.
        /// </summary>
        public virtual void Apply() {
        }

    }
}
