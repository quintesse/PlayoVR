namespace PlayoVR {
    using UnityEngine;

    public class SpeakerIconManager : MonoBehaviour {
        [Tooltip("The sprite that will be shown when the associated player is not talking")]
        public Sprite NotTalking;
        [Tooltip("The sprite that will be shown when the associated player is talking")]
        public Sprite Talking;
        [Tooltip("The time in seconds to switch back from Talking to Not Talking")]
        public int SwitchBackDelay = 2;
        [Tooltip("The time in seconds to remove the icon completely. Set to -1 to always keep showing the icon")]
        public int InactiveDelay = 3;
        [Tooltip("The voice to listen to")]
        public PhotonVoiceSpeaker Speaker;

        private SpriteRenderer spriteRenderer;
        private float lastTimeTalking;

        void Awake() {
            if (Talking == null) {
                Debug.LogError("SpeakerIconManager is missing a reference to the talking state sprite!");
            }
            if (Speaker == null) {
                Debug.LogError("SpeakerIconManager is missing a reference to the speaker to listen to!");
            }
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) {
                Debug.LogError("SpeakerIconManager requires a SpriteRenderer on the same GameObject!");
            }
            if (InactiveDelay == -1) {
                spriteRenderer.sprite = NotTalking;
            }
        }

        void Update() {
            if (Speaker.IsPlaying) {
                spriteRenderer.sprite = Talking;
                lastTimeTalking = Time.time;
            } else {
                if ((Time.time - lastTimeTalking) > SwitchBackDelay) {
                    if (InactiveDelay != -1 && (Time.time - lastTimeTalking) > (SwitchBackDelay + InactiveDelay)) {
                        spriteRenderer.sprite = null;
                    } else {
                        spriteRenderer.sprite = NotTalking;
                    }
                }
            }
        }
    }
}
