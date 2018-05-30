using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Voice = ExitGames.Client.Photon.Voice;

/// <summary>
/// Component representing outgoing audio stream in scene. Should be attached to prefab with PhotonView attached.
/// </summary>
[RequireComponent(typeof(PhotonVoiceSpeaker))]
[DisallowMultipleComponent]
[AddComponentMenu("Photon Voice/Photon Voice Recorder")]
//[HelpURL("https://doc.photonengine.com/en-us/voice/current/getting-started/voice-for-pun#the__audio_source__prefab")]
public class PhotonVoiceRecorder : Photon.MonoBehaviour
{
    private Voice.LocalVoice voice = Voice.LocalVoiceAudio.Dummy;

    protected Voice.ILocalVoiceAudio voiceAudio { get { return (Voice.ILocalVoiceAudio)this.voice; } }

    private string microphoneDevice = null;
    private int photonMicrophoneDeviceID = -1;
    private Voice.IAudioSource audioSource;

    public enum AudioSource
    {
        Microphone,
        AudioClip,
        Factory
    }

    public enum MicAudioSourceType
    {
        Settings,
        Unity,
        Photon
    }

    public enum SampleTypeConv
    {
        None,
        Short,
        ShortAuto,
    }

    /// <summary>
    /// Audio data source.
    /// </summary>
    public AudioSource Source;
    public MicAudioSourceType MicrophoneType;

    /// <summary>
    /// Force creation of 'short' pipeline and convert audio data to short for 'float' audio sources
    /// </summary>
    public SampleTypeConv TypeConvert;
    bool forceShort = false;

    /// <summary>
    /// Source audio clip.
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
    /// Set or get Unity microphone device used for streaming.
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

            updateAudioSource();            
        }
    }

    /// <summary>
    /// Set or get photon microphone device used for streaming.
    /// </summary>
    /// <remarks>
    /// If -1, global PhotonVoiceNetwork.PhotonMicrophoneDeviceID is used.
    /// </remarks>    
    public int PhotonMicrophoneDeviceID
    {
        get { return this.photonMicrophoneDeviceID; }
        set
        {
			if (!PhotonVoiceNetwork.PhotonMicrophoneEnumerator.IDIsValid(value))
            {
                Debug.LogError("PUNVoice: " + value + " is not a valid Photon microphone device");
                return;
            }

            this.photonMicrophoneDeviceID = value;

            updateAudioSource();
        }
    }
    private void updateAudioSource() {
        // update local voice's mic audio source
        if (this.voice != Voice.LocalVoiceAudio.Dummy && (Source == AudioSource.Microphone))
        {
            // first remove voice and stop mic, then create new mic wrapper
            this.audioSource.Dispose();
            this.voice.RemoveSelf();
            gameObject.SendMessage("PhotonVoiceRemoved", SendMessageOptions.DontRequireReceiver);

            var debugEchoMode = this.DebugEchoMode;
            this.DebugEchoMode = false;
            var prevVoice = this.voice;

            this.voice = createLocalVoiceAudioAndSource();

            this.voice.Group = prevVoice.Group;
            this.voice.Transmit = prevVoice.Transmit;
            this.voiceAudio.VoiceDetector.On = voiceAudio.VoiceDetector.On;
            this.voiceAudio.VoiceDetector.Threshold = voiceAudio.VoiceDetector.Threshold;
            sendPhotonVoiceCreatedMessage();

            this.DebugEchoMode = debugEchoMode;
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

	IEnumerator Start()
    {
		yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
		if (!Application.HasUserAuthorization(UserAuthorization.Microphone)) 
		{
			yield break;
		}
		
        if (photonView.isMine)
        {
            switch (this.TypeConvert)
            {
                case SampleTypeConv.Short:
                    forceShort = true;
                    Debug.LogFormat("PUNVoice: Type Convertion set to Short. Audio samples will be converted if source samples type differs.");
                    break;
                case SampleTypeConv.ShortAuto:
                    var speex = gameObject.GetComponent<SpeexDSP>();
                    if (speex != null && speex.Active)
                    {
                        if (PhotonVoiceSettings.Instance.DebugInfo)
                        {
                            Debug.LogFormat("PUNVoice: Type Convertion set to ShortAuto. SpeexDSP found. Audio samples will be converted if source samples type differs.");
                        }
                        forceShort = true;
                    }
                    break;
            }
            this.voice = createLocalVoiceAudioAndSource();
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
            sendPhotonVoiceCreatedMessage();
        }
    }

    private Voice.LocalVoice createLocalVoiceAudioAndSource()
    {
        var pvs = PhotonVoiceSettings.Instance;
                
        switch (Source)
        {
            case AudioSource.Microphone:
                {
					Application.RequestUserAuthorization(UserAuthorization.Microphone);
                    if (this.MicrophoneType == MicAudioSourceType.Settings && pvs.MicrophoneType == PhotonVoiceSettings.MicAudioSourceType.Photon ||
                        this.MicrophoneType == MicAudioSourceType.Photon)
                    {
                        var hwMicDev = this.PhotonMicrophoneDeviceID != -1 ? this.PhotonMicrophoneDeviceID : PhotonVoiceNetwork.PhotonMicrophoneDeviceID;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                        if (PhotonVoiceSettings.Instance.DebugInfo)
                        {
                            Debug.LogFormat("PUNVoice: Setting recorder's source to Photon microphone device {0}", hwMicDev);
                        }

                        audioSource = new WindowsAudioInPusher(hwMicDev);
                        if (PhotonVoiceSettings.Instance.DebugInfo)
                        {
                            Debug.LogFormat("PUNVoice: Setting recorder's source to WindowsAudioInPusher");
                        }
                        break;
#elif UNITY_IOS && !UNITY_EDITOR || (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX)
						audioSource = new AppleAudioInPusher(hwMicDev);
                        if (PhotonVoiceSettings.Instance.DebugInfo)
                        {
                            Debug.LogFormat("PUNVoice: Setting recorder's source to AppleAudioInPusher");
                        }
                        break;
#elif UNITY_ANDROID && !UNITY_EDITOR
                        audioSource = new AndroidAudioInAEC();       
                        if (PhotonVoiceSettings.Instance.DebugInfo)
                        {
                            Debug.LogFormat("PUNVoice: Setting recorder's source to AndroidAudioInAEC");
                        }
                        break;
#else
                        Debug.LogFormat("PUNVoice: Photon microphone type is not supported for the current platform. Using Unity microphone.");
#endif
                    }
                    if (Microphone.devices.Length < 1)
                    {
                        // Error already logged in PhotonVoiceNetwork.Start()
                        return Voice.LocalVoiceAudio.Dummy;
                    }
                    var micDev = this.MicrophoneDevice != null ? this.MicrophoneDevice : PhotonVoiceNetwork.MicrophoneDevice;

                    if (PhotonVoiceSettings.Instance.DebugInfo)
                    {
                        Debug.LogFormat("PUNVoice: Setting recorder's source to microphone device {0}", micDev);
                    }
                    // mic can ignore passed sampling rate and set it's own
                    var mic = new MicWrapper(micDev, (int)pvs.SamplingRate);
                    audioSource = mic;
                }
                break;
            case AudioSource.AudioClip:
                {
                    if (AudioClip == null)
                    {
                        Debug.LogErrorFormat("PUNVoice: AudioClip property must be set for AudioClip audio source");
                        return Voice.LocalVoiceAudio.Dummy;
                    }
                    audioSource = new AudioClipWrapper(AudioClip);
                    if (this.LoopAudioClip)
                    {
                        ((AudioClipWrapper)audioSource).Loop = true;
                    }
                }
                break;
            case AudioSource.Factory:
                {
                    if (PhotonVoiceNetwork.AudioSourceFactory == null)
                    {
                        Debug.LogErrorFormat("PUNVoice: PhotonVoiceNetwork.AudioSourceFactory must be specified if PhotonVoiceRecorder.Source set to Factory");
                        return Voice.LocalVoiceAudio.Dummy;
                    }
                    audioSource = PhotonVoiceNetwork.AudioSourceFactory(this);
                }
                break;
            default:
                Debug.LogErrorFormat("PUNVoice: unknown Source value {0}", Source);
                return Voice.LocalVoiceAudio.Dummy;
        }

        Voice.VoiceInfo voiceInfo = Voice.VoiceInfo.CreateAudioOpus(pvs.SamplingRate, audioSource.SamplingRate, audioSource.Channels, pvs.FrameDuration, pvs.Bitrate, photonView.viewID);
        return createLocalVoiceAudio(voiceInfo, audioSource);
    }

    protected virtual Voice.LocalVoice createLocalVoiceAudio(Voice.VoiceInfo voiceInfo, Voice.IAudioSource source)
    {
		if (source is Voice.IAudioPusher<float>)
        {
            if (forceShort)
            {
                throw new NotImplementedException("Voice.IAudioPusher<float> at 'short' voice is not supported currently");
            }
            else
            {
                var localVoice = PhotonVoiceNetwork.VoiceClient.CreateLocalVoiceAudio<float>(voiceInfo);
                ((Voice.IAudioPusher<float>)source).SetCallback(buf => localVoice.PushDataAsync(buf), localVoice);
                return localVoice;
            }
        }
        else if (source is Voice.IAudioPusher<short>)
        {
            var localVoice = PhotonVoiceNetwork.VoiceClient.CreateLocalVoiceAudio<short>(voiceInfo);
            ((Voice.IAudioPusher<short>)source).SetCallback(buf => localVoice.PushDataAsync(buf), localVoice);
            return localVoice;
        }
        else if (source is Voice.IAudioReader<float>)
        {
            if (forceShort)
            {
                if (PhotonVoiceSettings.Instance.DebugInfo)
                {
                    Debug.LogFormat("PUNVoice: Creating local voice with source samples type conversion from float to short.");
                }
                var localVoice = PhotonVoiceNetwork.VoiceClient.CreateLocalVoiceAudio<short>(voiceInfo);
                localVoice.LocalUserServiceable = new Voice.BufferReaderPushAdapterAsyncPoolFloatToShort(localVoice, source as Voice.IAudioReader<float>);
                return localVoice;
            }
            else
            {
                var localVoice = PhotonVoiceNetwork.VoiceClient.CreateLocalVoiceAudio<float>(voiceInfo);
                localVoice.LocalUserServiceable = new Voice.BufferReaderPushAdapterAsyncPool<float>(localVoice, source as Voice.IAudioReader<float>);
                return localVoice;
            }
        }
        else if (source is Voice.IAudioReader<short>)
        {
            var localVoice = PhotonVoiceNetwork.VoiceClient.CreateLocalVoiceAudio<short>(voiceInfo);
            localVoice.LocalUserServiceable = new Voice.BufferReaderPushAdapterAsyncPool<short>(localVoice, source as Voice.IAudioReader<short>);
            return localVoice;
        }
        else
        {
            Debug.LogErrorFormat("PUNVoice: PhotonVoiceRecorder createLocalVoiceAudio does not support Voice.IAudioReader of type {0}", source.GetType());
            return Voice.LocalVoiceAudio.Dummy;
        }
    }

    public class PhotonVoiceCreatedParams
    {
        public Voice.LocalVoice Voice { get; internal set; }
        public Voice.IAudioSource AudioSource { get; internal set; }
    }
    protected virtual void sendPhotonVoiceCreatedMessage()
    {
        gameObject.SendMessage("PhotonVoiceCreated", new PhotonVoiceCreatedParams { Voice = this.voice, AudioSource = this.audioSource}, SendMessageOptions.DontRequireReceiver);
    }

    void OnDestroy()
    {
        if (this.voice != Voice.LocalVoiceAudio.Dummy) // photonView.isMine does not work
        {
            this.voice.RemoveSelf();
            // TODO: IAudioReader allready destroyed?
            if (this.audioSource != null)
            {
                this.audioSource.Dispose();
                this.audioSource = null;
            }

            // no need to send PhotonVoiceRemoved since object is destroyed
        }
    }

    void OnEnable()
    {

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