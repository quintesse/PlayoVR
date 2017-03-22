using Photon;
using UnityEngine;

public class JoinLeaveSounds : PunBehaviour {
    public AudioClip joinSound;
    public AudioClip leaveSound;
    private AudioSource source;

    public override void OnPhotonPlayerConnected(PhotonPlayer newPlayer) {
        if (this.joinSound != null) {
            if (this.source == null) this.source = FindObjectOfType<AudioSource>();
            this.source.PlayOneShot(this.joinSound);
        }
    }

    public override void OnPhotonPlayerDisconnected(PhotonPlayer otherPlayer) {
        if (this.leaveSound != null) {
            if (this.source == null) this.source = FindObjectOfType<AudioSource>();
            this.source.PlayOneShot(this.leaveSound);
        }
    }
}