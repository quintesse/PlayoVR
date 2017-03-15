namespace NetVRTK {
    using UnityEngine;
    using System.Collections;

    [RequireComponent(typeof(PhotonView))]
    public class NetworkAudio : Photon.PunBehaviour {
        public const int CLIP_BULLET_HIT_SOLID = 1;
        public const int CLIP_BULLET_HIT_SOFT = 2;

        public AudioClip hitSolidSound; // 1
        public AudioClip hitPlayerSound; // 2

        private static NetworkAudio netAudio;

        // Get the component for sending sounds over the network
        private static NetworkAudio instance() {
            if (netAudio == null) {
                netAudio = GameObject.Find("/Scripts").GetComponent<NetworkAudio>();
            }
            return netAudio;
        }

        // Play a sound both locally and for all connected players
        public static void SendPlayClipAtPoint(int clip, Vector3 position, float volume) {
            instance().photonView.RPC("PlayClipAtPoint", PhotonTargets.All, clip, position, volume);
        }

        [PunRPC]
        private void PlayClipAtPoint(int clip, Vector3 position, float volume) {
            AudioSource.PlayClipAtPoint(getClip(clip), position, volume);
        }

        private AudioClip getClip(int clip) {
            switch (clip) {
                case 1:
                    return hitSolidSound;
                case 2:
                    return hitPlayerSound;
            }
            Debug.Log("Somebody trying to play an unknown clip : " + clip);
            return null;
        }
    }
}