using System.Linq;
using UnityEngine;
using Voice = ExitGames.Client.Photon.Voice;

/// <summary>
/// Component representing outgoing audio stream in scene. Should be attached to prefab with PhotonView attached.
/// </summary>
[RequireComponent(typeof(PhotonVoiceSpeaker))]
[DisallowMultipleComponent]
[AddComponentMenu("Photon Voice/Photon Voice Recorder")]
[HelpURL("https://doc.photonengine.com/en-us/voice/current/getting-started/voice-for-pun#the__audio_source__prefab")]
public class PhotonVoiceRecorder : Photon.MonoBehaviour
{
    private Voice.LocalVoice voice = Voice.LocalVoiceAudio.Dummy;

    protected Voice.ILocalVoiceAudio voiceAudio { get { return (Voice.ILocalVoiceAudio)this.voice; } }

    private string microphoneDevice = null;

    /// <summary>
    /// Set aidio clip in instector for playing back instead of microphone signal streaming.
    /// </summary>
    public AudioClip AudioClip;

    /// <summary>
    /// Loop playback for audio clip sources.
    /// </summary>
    public bool LoopAudioClip = true;

    /// <summary>
    /// Returns voice activity detector for recorder's audio stream.
    /// </summary>
    public Voice.AudioUtil.IVoiceDetector VoiceDetector
    {
        get { return this.photonView.isMine ? this.voiceAudio.VoiceDetector : null; }
    }

    /// <summary>
    /// Set or get microphone device used for streaming.
    /// </summary>
    /// <remarks>
    /// If null, global PhotonVoiceNetwork.MicrophoneDevice is used.
    /// </remarks>    
    public string MicrophoneDevice
    {
        get { return this.microphoneDevice; }
        set
        {
            if (value != null && !Microphone.devices.Contains(value))
            {
                Debug.LogError("PUNVoice: " + value + " is not a valid microphone device");
                return;
            }

            this.microphoneDevice = value;

            // update local voice's mic audio source
            if (this.voice != Voice.LocalVoiceAudio.Dummy && AudioClip == null)
            {
                var pvs = PhotonVoiceSettings.Instance;

                Application.RequestUserAuthorization(UserAuthorization.Microphone);

                var micDev = this.MicrophoneDevice != null ? this.MicrophoneDevice : PhotonVoiceNetwork.MicrophoneDevice;
                if (PhotonVoiceSettings.Instance.DebugInfo)
                {
                    Debug.LogFormat("PUNVoice: Setting recorder's microphone device to {0}", micDev);
                }
                // first remove voice and stop mic, then create new mic wrapper
                this.voice.RemoveSelf();
                var mic = new MicWrapper(micDev, (int)pvs.SamplingRate);

                var debugEchoMode = this.DebugEchoMode;
                this.DebugEchoMode = false;

                Voice.VoiceInfo voiceInfo = Voice.VoiceInfo.CreateAudioOpus(pvs.SamplingRate, mic.SourceSamplingRate, mic.Channels, pvs.FrameDuration, pvs.Bitrate, photonView.viewID);
                var prevVoice = this.voice;
                this.voice = createLocalVoice(voiceInfo, mic);
                this.voice.Group = prevVoice.Group;
                this.voice.Transmit = prevVoice.Transmit;
                this.voiceAudio.VoiceDetector.On = voiceAudio.VoiceDetector.On;
                this.voiceAudio.VoiceDetector.Threshold = voiceAudio.VoiceDetector.Threshold;
                sendVoiceCreatedMessage(voiceInfo);

                this.DebugEchoMode = debugEchoMode;
            }
        }
    }

    /// <summary>If AudioGroup != 0, recorders's audio data is sent only to clients listening to this group.</summary>
    /// <see PhotonVoiceNetwork.Client.ChangeAudioGroups/>
    public byte AudioGroup
    {
        get { return voice.Group; }
        set { voice.Group = value; }
    }

    /// <summary>Returns true if audio stream broadcasts.</summary>
    public bool IsTransmitting
    {
        get { return voice.IsTransmitting; }
    }

    /// <summary>
    /// Level meter utility.
    /// </summary>
    public Voice.AudioUtil.ILevelMeter LevelMeter
    {
        get { return voiceAudio.LevelMeter; }
    }

    // give user a chance to change MicrophoneDevice in Awake()
    void Start()
    {
        if (photonView.isMine)
        {
            var pvs = PhotonVoiceSettings.Instance;

            Application.RequestUserAuthorization(UserAuthorization.Microphone);
            // put required sample rate into audio source and encoder - both adjust it if needed
            Voice.IBufferReader<float> audioStream;
            int channels = 0;
            int sourceSamplingRate = 0;
            if (AudioClip == null)
            {
                if (Microphone.devices.Length < 1)
                {
                    // Error already logged in PhotonVoiceNetwork.Awake()
                    return;
                }

                var micDev = this.MicrophoneDevice != null ? this.MicrophoneDevice : PhotonVoiceNetwork.MicrophoneDevice;
                if (PhotonVoiceSettings.Instance.DebugInfo)
                {
                    Debug.LogFormat("PUNVoice: Setting recorder's microphone device to {0}", micDev);
                }
                var mic = new MicWrapper(micDev, (int)pvs.SamplingRate);
                sourceSamplingRate = mic.SourceSamplingRate;
                channels = mic.Channels;
                audioStream = mic;
            }
            else
            {
                audioStream = new AudioClipWrapper(AudioClip);
                sourceSamplingRate = AudioClip.frequency;
                channels = AudioClip.channels;
                if (this.LoopAudioClip)
                {
                    ((AudioClipWrapper)audioStream).Loop = true;
                }
            }

            Voice.VoiceInfo voiceInfo = Voice.VoiceInfo.CreateAudioOpus(pvs.SamplingRate, sourceSamplingRate, channels, pvs.FrameDuration, pvs.Bitrate, photonView.viewID);
            this.voice = createLocalVoice(voiceInfo, audioStream);
            this.VoiceDetector.On = PhotonVoiceSettings.Instance.VoiceDetection;
            this.VoiceDetector.Threshold = PhotonVoiceSettings.Instance.VoiceDetectionThreshold;            
            if (this.voice != Voice.LocalVoiceAudio.Dummy)
            {
                this.voice.Transmit = PhotonVoiceSettings.Instance.AutoTransmit;
            }
            else if (PhotonVoiceSettings.Instance.AutoTransmit)
            {
                Debug.LogWarning("PUNVoice: Cannot Transmit.");
            }
            sendVoiceCreatedMessage(voiceInfo);
        }
    }

    protected virtual Voice.LocalVoice createLocalVoice(Voice.VoiceInfo voiceInfo, Voice.IBufferReader<float> reader)
    {
        return PhotonVoiceNetwork.CreateLocalVoice(voiceInfo, reader);
    }
    protected virtual void sendVoiceCreatedMessage(Voice.VoiceInfo voiceInfo)
    {
        gameObject.SendMessage("VoiceCreated", new VoiceCreatedParams(this.voice, voiceInfo), SendMessageOptions.DontRequireReceiver);
    }

    public struct VoiceCreatedParams
    {
        public Voice.LocalVoice Voice;
        public Voice.VoiceInfo Info;
        public VoiceCreatedParams(Voice.LocalVoice voice, Voice.VoiceInfo info)
        {
            Voice = voice;
            Info = info;
        }
    }
    void OnDestroy()
    {
        if (this.voice != Voice.LocalVoiceAudio.Dummy) // photonView.isMine does not work
        {
            this.voice.RemoveSelf();
        }
    }

    void OnEnable()
    {
        Application.RequestUserAuthorization(UserAuthorization.Microphone);
    }

    /// <summary>If true, stream data broadcasted unconditionally.</summary>        
    public bool Transmit { get { return voice.Transmit; } set { voice.Transmit = value; } }

    /// <summary>If true, voice detection enabled.</summary>
    public bool Detect { get { return voiceAudio.VoiceDetector.On; } set { voiceAudio.VoiceDetector.On = value; } }

    /// <summary>If true, outgoing stream routed back to client via server same way as for remote client's streams.</summary>
    public bool DebugEchoMode { get { return voice.DebugEchoMode; } set { voice.DebugEchoMode = value; } }
    /// <summary>Trigger voice detector calibration process.
    /// While calibrating, keep silence. Voice detector sets threshold basing on measured backgroud noise level.
    /// </summary>
    /// <param name="durationMs">Duration of calibration in milliseconds.</param>
    public void VoiceDetectorCalibrate(int durationMs)
    {
        if (photonView.isMine)
        {
            this.voiceAudio.VoiceDetectorCalibrate(durationMs);
        }
    }

    /// <summary>If true, voice detector calibration is in progress.</summary>
    public bool VoiceDetectorCalibrating { get { return voiceAudio.VoiceDetectorCalibrating; } }

    private string tostr<T>(T[] x, int lim = 10)
    {
        System.Text.StringBuilder b = new System.Text.StringBuilder();
        for (var i = 0; i < (x.Length < lim ? x.Length : lim); i++)
        {
            b.Append("-");
            b.Append(x[i]);
        }
        return b.ToString();
    }

    public string ToStringFull()
    {
        int minFreq = 0;
        int maxFreq = 0;
        Microphone.GetDeviceCaps(MicrophoneDevice, out minFreq, out maxFreq);
        return string.Format("Mic '{0}': {1}..{2} Hz", MicrophoneDevice, minFreq, maxFreq);
    }

}