namespace NetVRTK {
    using UnityEngine;
    using System.Collections;

    [RequireComponent(typeof(PhotonView))]
    public abstract class NetworkAudio : Photon.PunBehaviour {

        private static NetworkAudio instance;

        void Awake() {
            instance = this;
        }

        // Play a sound both locally and for all connected players
        public static void SendPlayClipAtPoint(AudioClip clip, Vector3 position, float volume) {
            if (instance != null) {
                SendPlayClipAtPoint(instance.GetClipId(clip), position, volume);
            }
        }

        // Play a sound both locally and for all connected players
        public static void SendPlayClipAtPoint(string clipName, Vector3 position, float volume) {
            if (instance != null) {
                SendPlayClipAtPoint(instance.GetClipId(clipName), position, volume);
            }
        }

        // Play a sound both locally and for all connected players
        public static void SendPlayClipAtPoint(int clipId, Vector3 position, float volume) {
            instance.photonView.RPC("PlayClipAtPoint", PhotonTargets.All, clipId, position, volume);
        }

        [PunRPC]
        protected void PlayClipAtPoint(int clipId, Vector3 position, float volume) {
            AudioClip clip = GetClip(clipId);
            if (clip != null) {
                AudioSource.PlayClipAtPoint(clip, position, volume);
            }
        }

        // Needs an implementation that turns a reference to an AudioClip into an int
        protected abstract int GetClipId(AudioClip clip);

        // Needs an implementation that turns a name of an AudioClip into an int
        protected abstract int GetClipId(string clipName);

        // Needs an implementation that turns an int into a reference to an AudioClip
        protected abstract AudioClip GetClip(int clipId);
    }
}