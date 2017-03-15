using UnityEngine;
using System.Collections;

using DaikonForge.VoIP;
using System;

[RequireComponent(typeof(PhotonView))]
public class LocalVoiceController : VoiceControllerBase {
    private PhotonView photonView;
    private DateTime lastTalking = DateTime.Now.AddMinutes(-1);

    private readonly static RaiseEventOptions eventOptions = new RaiseEventOptions();
    private readonly static byte EVENT_VOICE = 42;

    protected override void Awake() {
        photonView = GetComponent<PhotonView>();

        base.Awake();

        eventOptions.SequenceChannel = 1; // Whatever, just not the default
        PhotonNetwork.OnEventCall += this.OnEventRaised;
    }

    protected override void OnDestroy() {
        PhotonNetwork.OnEventCall -= this.OnEventRaised;
        base.OnDestroy();
    }

    public override bool IsLocal {
        get { return photonView.isMine; }
    }

    protected override void OnAudioDataEncoded(VoicePacketWrapper encodedFrame) {
        byte[] headers = encodedFrame.ObtainHeaders();

        // Option #1: using RPC
        //photonView.RPC("HandleVoice", PhotonTargets.Others, headers, encodedFrame.RawData);

        // Option #2: using events
        byte[][] data = new byte[2][];
        data[0] = headers;
        data[1] = encodedFrame.RawData;
        PhotonNetwork.RaiseEvent(EVENT_VOICE, data, false, eventOptions);
        encodedFrame.ReleaseHeaders();

        lastTalking = DateTime.Now;
    }

    [PunRPC]
    void HandleVoice(byte[] headers, byte[] rawData) {
        if (!GetComponent<AudioSource>().enabled) return;
        VoicePacketWrapper packet = new VoicePacketWrapper(headers, rawData);
        ReceiveAudioData(packet);
    }

    public void OnEventRaised(byte eventCode, object content, int senderID) {
        if (eventCode == EVENT_VOICE) {
            if (!GetComponent<AudioSource>().enabled) return;
            byte[][] data = (byte[][])content;
            VoicePacketWrapper packet = new VoicePacketWrapper(data[0], data[1]);
            ReceiveAudioData(packet);
        }
    }

    public bool isTalking() {
        return DateTime.Now.CompareTo(lastTalking.AddMilliseconds(100)) == -1;
    }
}
