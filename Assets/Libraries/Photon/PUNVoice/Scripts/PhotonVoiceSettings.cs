using UnityEngine;
using ExitGames.Client.Photon.Voice;

/// <summary>
/// Collection of Photon Voice settings. Add single instance to the scene and set values in inspector.
/// Will be created automatically on application start otherwise.
/// </summary>
[DisallowMultipleComponent]
public class PhotonVoiceSettings : MonoBehaviour
{

    /// Follow PUN room status (connect, join, rejoin).
    public bool AutoConnect = true;             // set in inspector

    /// Disconnect voice client as PUN client dosconnects.
    public bool AutoDisconnect = false;         // set in inspector

    /// Start recording and transmit as soon as joined to the room.
    public bool AutoTransmit = true;            // set in inspector

    /// Outgoing audio stream sampling rate (applied per every recoder instance).
    public POpusCodec.Enums.SamplingRate SamplingRate = POpusCodec.Enums.SamplingRate.Sampling24000;     // set in inspector

    /// Outgoing audio stream encoder delay (buffer size in terms of time; applied per every recoder instance).
    public OpusCodec.FrameDuration FrameDuration = OpusCodec.FrameDuration.Frame20ms;   // set in inspector

    /// Outgoing audio stream bitrate (applied per every recoder instance).
    public int Bitrate = 30000;               // set in inspector

    /// Enable voice detection (applied per every recoder instance).
    public bool VoiceDetection = false;                 // set in inspector

    /// Voice detection threshold (applied per every recoder instance).
    public float VoiceDetectionThreshold = 0.01f;       // set in inspector

    /// Remote audio stream playback delay to compensate packets latency variations (applied per every speaker instance). Try 100 - 200 if sound is choppy.
    public int PlayDelayMs = 200;                       // set in inspector

    /// PS4 user ID of the local user: pass the userID of the PS4 controller that is used by the local user.
    /// This value is used by Photon Voice when sending output to the headphones of as PS4 controller.
    /// If you don't provide a user ID, then Photon Voice uses the user ID of the user at index 0 in the list of local users
    /// and in case that multiple controllers are attached, the audio output might be sent to the headphones of a different controller then intended.
#if UNITY_PS4
    public int PS4UserID = 0;                       // set from your games code
#endif

    public enum MicAudioSourceType
    {
        Unity,
        Photon
    }

    /// Default microphone type;
    public MicAudioSourceType MicrophoneType;
    /// Lost frames simulation ratio.
    public int DebugLostPercent = 0;                    // set in inspector

    /// Log debug info.
    public bool DebugInfo = false;                    // set in inspector

    private static PhotonVoiceSettings instance;
    private static object instanceLock = new object();

    /// <summary>
    /// Get current settings.
    /// </summary>
    public static PhotonVoiceSettings Instance
    {
        get
        {
            lock (instanceLock)
            {
                if (instance == null)
                {
                    PhotonVoiceSettings candidate = FindObjectOfType<PhotonVoiceSettings>();
                    if (candidate != null)
                    {
                        instance = candidate;
                    }
                    else
                    {
                        instance = PhotonVoiceNetwork.instance.gameObject.AddComponent<PhotonVoiceSettings>();
                    }
                }
                return instance;
            }
        }
        private set
        {
            lock (instanceLock)
            {
                if (instance != null && value != null)
                {
                    if (instance.GetInstanceID() != value.GetInstanceID())
                    {
                        Debug.LogErrorFormat(
                            "PUNVoice: Destroying a duplicate instance of PhotonVoiceSettings as only one is allowed.");
                        Destroy(value);
                        return;
                    }
                    return;
                }
                instance = value;
            }
        }
    }

    // for settings put in scene in editor
    private void Awake()
    {
        Instance = this;
    }
}