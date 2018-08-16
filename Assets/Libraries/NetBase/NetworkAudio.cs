namespace NetBase {
    using UnityEngine;

    [RequireComponent(typeof(PhotonView))]
    public abstract class NetworkAudio : Photon.PunBehaviour {

        private static NetworkAudio instance;

        void Awake() {
            instance = this;
        }

        // Play a sound both locally and for all connected players
        public static void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume, PhotonTargets targets = PhotonTargets.All) {
            if (instance != null) {
                int clipId = instance.GetClipId(clip);
                if (clipId >= 0) {
                    PlayClipAtPoint(clipId, position, volume, targets);
                } else {
                    Debug.Log("AudioClip wasn't found: " + clip.name);
                }
            }
        }

        // Play a sound both locally and for all connected players
        public static void PlayClipAtPoint(string clipName, Vector3 position, float volume, PhotonTargets targets = PhotonTargets.All) {
            if (instance != null) {
                int clipId = instance.GetClipId(clipName);
                if (clipId >= 0) {
                    PlayClipAtPoint(clipId, position, volume, targets);
                } else {
                    Debug.Log("AudioClip wasn't found: " + clipName);
                }
            }
        }

        // Play a sound both locally and for all connected players
        public static void PlayClipAtPoint(int clipId, Vector3 position, float volume, PhotonTargets targets = PhotonTargets.All) {
            if (instance != null) {
                instance.photonView.RPC("NetPlayClipAtPoint", targets, clipId, position, volume);
            }
        }

        [PunRPC]
        protected void NetPlayClipAtPoint(int clipId, Vector3 position, float volume) {
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