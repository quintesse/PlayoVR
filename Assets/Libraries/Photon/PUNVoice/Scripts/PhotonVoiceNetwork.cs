using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using UnityEngine;
using Voice = ExitGames.Client.Photon.Voice;
using LoadBalancing = ExitGames.Client.Photon.LoadBalancing;

using VoiceIdPair = System.Collections.Generic.KeyValuePair<int, byte>;

/// <summary>
/// The singleton encapsulating Photon Voice client.
/// Accessed via class static methods.
/// </summary>
/// <remarks>
/// Extends MonoBehaviour and attached to an object in scene to call Photon Voice Client Service() in Update().
/// </remarks>
[DisallowMultipleComponent]
public class PhotonVoiceNetwork : MonoBehaviour
{
    private static PhotonVoiceNetwork _instance;
    private static GameObject _singleton;
    private static object instanceLock = new object();
    private static bool destroyed = false;

    internal static PhotonVoiceNetwork instance
    {
        get
        {
            return getInstance();
        }
    }

	internal static PhotonVoiceNetwork getInstance()
	{
		lock (instanceLock)
		{
			if (destroyed)
			{
				return null;
			}
			if (_instance == null)
			{
				_singleton = new GameObject();
				_instance = _singleton.AddComponent<PhotonVoiceNetwork>();
				_singleton.name = "PhotonVoiceNetworkSingleton";

				DontDestroyOnLoad(_singleton);
			}
			return _instance;
		}
	}

    /// <summary>
    /// Defines how many seconds Voice Unity Client keeps the connection, after Unity's OnApplicationPause(true) call. Default: 60 seconds.
    /// </summary>
    /// <remarks>
    /// It's best practice to disconnect inactive apps/connections after a while but to also allow users to take calls, etc..
    /// We think a reasonable backgroung timeout is 60 seconds.
    ///
    /// To handle the timeout, implement: OnDisconnectedFromPhoton(), as usual.
    /// Your application will "notice" the background disconnect when it becomes active again (running the Update() loop).
    ///
    /// If you need to separate this case from others, you need to track if the app was in the background
    /// (there is no special callback).
    ///
    /// A value below 0.1 seconds will disable this timeout (careful: connections can be kept indefinitely).
    ///
    ///
    /// Info:
    /// Voice Unity Client is running a "fallback thread" to send ACKs to the server, even when Unity is not calling Update() regularly.
    /// This helps keeping the connection while loading scenes and assets and when the app is in the background.
    ///
    /// Note:
    /// Some platforms (e.g. iOS) don't allow to keep a connection while the app is in background.
    /// In those cases, this value does not change anything, the app immediately loses connection in background.
    ///
    /// Unity's OnApplicationPause() callback is broken in some exports (Android) of some Unity versions.
    /// Make sure OnApplicationPause() gets the callbacks you'd expect on the platform you target!
    /// Check PhotonHandler.OnApplicationPause(bool pause), to see the implementation.
    /// </remarks>
    public static float BackgroundTimeout = 60.0f;

    void OnDestroy()
    {
        if (this != _instance)
        {
            return;
        }

        destroyed = true;
        this.client.Dispose();
    }

    internal UnityVoiceFrontend client;
    PhotonVoiceNetwork()
    {
        client = new UnityVoiceFrontend(ConnectionProtocol.Udp);
    }

    [RuntimeInitializeOnLoadMethod]
    public static void RuntimeInitializeOnLoad()
    {
        getInstance();
    }

    public void Awake()
    {
		if (Microphone.devices.Length < 1)
        {
            Debug.LogError("PUNVoice: No microphone device found");
        }
	}
	
    /// <summary>
    /// Connects Voice client to a Master Server of region specified in settings, using the Name Server to find the IP.
    /// </summary>
    /// <returns>If the operation could be sent. If false, no operation was sent.</returns>
    public static bool Connect()
    {
        instance.client.AppId = PhotonNetwork.PhotonServerSettings.VoiceAppID;
        instance.client.AppVersion = PhotonNetwork.gameVersion;

        if (PhotonNetwork.PhotonServerSettings.HostType == ServerSettings.HostingOption.SelfHosted)
        {
            string voiceMasterAddress = string.Format("{0}:{1}",PhotonNetwork.PhotonServerSettings.ServerAddress,
                PhotonNetwork.PhotonServerSettings.VoiceServerPort);
            Debug.LogFormat("PUNVoice: connecting to master {0}", voiceMasterAddress);
  
            return instance.client.Connect(voiceMasterAddress, null, null, null, null);
        }
        else {
            Debug.LogFormat("PUNVoice: connecting to region {0}", PhotonNetwork.networkingPeer.CloudRegion.ToString());
            return instance.client.ConnectToRegionMaster(PhotonNetwork.networkingPeer.CloudRegion.ToString());
        }
    }

    /// <summary>Disconnects voice client from any server.</summary>
    public static void Disconnect()
    {
        instance.client.Disconnect();
    }

    /// <summary>Returns underlying Photon LoadBalancing client.</summary>
    public static UnityVoiceFrontend Client { get { return instance.client; } }
    /// <summary>Returns underlying Photon Voice client.</summary>
    public static Voice.VoiceClient VoiceClient { get { return instance.client.VoiceClient; } }

    /// <summary>Returns Photon Voice client state.</summary>
    public static LoadBalancing.ClientState ClientState { get { return instance.client.State; } }

    /// <summary>Room name currently joined by Photon Voice client or empty string.</summary>
    public static string CurrentRoomName { get { return instance.client.CurrentRoom == null ? "" : instance.client.CurrentRoom.Name; } }

    private static string microphoneDevice = null;

    /// <summary>Global microphone device setting. Applied to all PhotonVoiceRecorders owned by client during initialization in Start().
    /// If changed while joined PUN room, rejoin (recorders recreation) required.
    /// </summary>
    /// <remarks>
    /// Use PhotonVoiceRecorder.MicrophoneDevice to set device per recorder.
    /// </remarks>    
    public static string MicrophoneDevice
    {
        get { return microphoneDevice; }
        set
        {
            if (value != null && !Microphone.devices.Contains(value))
            {
                Debug.LogError("PUNVoice: " + value + " is not a valid microphone device");
                return;
            }

            microphoneDevice = value;
            if (PhotonVoiceSettings.Instance.DebugInfo)
            {
                Debug.LogFormat("PUNVoice: Setting global microphone device to {0}", microphoneDevice);
            }
            foreach (var r in FindObjectsOfType<PhotonVoiceRecorder>())
            {
                if (r.photonView.isMine)
                {
                    if (r.MicrophoneDevice == null)
                    {
                        // update mic device
                        r.MicrophoneDevice = null;
                    }
                }
            }
        }
    }

    protected void OnEnable()
    {
        if (this != _instance)
        {
            return;
        }

        Application.RequestUserAuthorization(UserAuthorization.Microphone);        
    }

    protected void OnApplicationQuit()
    {
        if (this != _instance)
        {
            return;
        }

        client.Disconnect();
        client.Dispose();
    }

    protected void Update()
    {
        if (this != _instance)
        {
            return;
        }

        // to be able change percentage in runtime
        this.client.VoiceClient.DebugLostPercent = PhotonVoiceSettings.Instance.DebugLostPercent;

        client.Service();
    }

    /// <summary>
    /// Creates new local voice (outgoing audio stream).
    /// </summary>
    /// <param name="voiceInfo">Outgoing audio stream parameters (should be set according to Opus encoder restrictions).</param>
    /// <param name="serviceableFactory">Optional factory called after LovalVoice<T> initialized to create ILocalVoiceServiceable instance attached to the LocalVoice"/>.</param>
    /// <returns>Outgoing stream handler.</returns>
    /// <remarks>
    /// audioStream.SamplingRate and voiceInfo.SamplingRate may do not match. Automatic resampling will occur in this case.
    /// </remarks>
    public static Voice.LocalVoice CreateLocalVoice(Voice.VoiceInfo voiceInfo, Voice.IBufferReader<float> reader)
    {
        var localVoice = instance.client.VoiceClient.CreateLocalVoiceAudio<float>(voiceInfo);
        localVoice.LocalUserServiceable = new Voice.BufferReaderPushAdapterAsyncPool<float>(localVoice, reader);
        return localVoice;
    }

    // Reads float buffers. Converts them to short and pushes to LocalVoiceAudioShort
    class BufferReaderPushAdapterAsyncPoolFloatToShort : Voice.BufferReaderPushAdapterBase<float>
    {
        float[] buffer;
        public BufferReaderPushAdapterAsyncPoolFloatToShort(Voice.LocalVoice localVoice, Voice.IBufferReader<float> reader) : base(reader)
        {
            buffer = new float[((Voice.LocalVoiceFramed<short>)localVoice).FrameSize];
        }

        public override void Service(Voice.LocalVoice localVoice)
        {
            var v = ((Voice.LocalVoiceFramed<short>)localVoice);
            short[] buf = v.PushDataBufferPool.AcquireOrCreate();
            while (this.reader.Read(buffer))
            {
                for (int i = 0;i < buf.Length; i++)
                {
                    buf[i] = (short)(buffer[i]*(float)short.MaxValue);
                }
                v.PushDataAsync(buf);
                buf = v.PushDataBufferPool.AcquireOrCreate();
            }
            // release unused buffer
            v.PushDataBufferPool.Release(buf, buf.Length);
        }
    }

    public static Voice.LocalVoice CreateLocalVoiceShort(Voice.VoiceInfo voiceInfo, Voice.IBufferReader<float> reader)
    {
        var localVoice = instance.client.VoiceClient.CreateLocalVoiceAudio<short>(voiceInfo);
        localVoice.LocalUserServiceable = new BufferReaderPushAdapterAsyncPoolFloatToShort(localVoice, reader);
        return localVoice;
    }
    // PUN room joined
    void OnJoinedRoom()
    {
        if (this != _instance)
        {
            return;
        }

        // voice room check
        switch (this.client.State)
        {
            case LoadBalancing.ClientState.Joined:
                if (PhotonVoiceSettings.Instance.AutoConnect)
                {
                    // trigger rejoin to the (possible other) room                    
                    this.client.OpLeaveRoom();
                }
                break;
            default:
                if (PhotonVoiceSettings.Instance.AutoConnect)
                {
                    // trigger reconnect to the (possible other) room                    
                    this.client.Reconnect();
                }
                break;
        }
    }

    void OnLeftRoom()
    {
        if (this != _instance)
        {
            return;
        }

        if (PhotonVoiceSettings.Instance.AutoDisconnect)
        {
            client.Disconnect();
        }
    }

    void OnDisconnectedFromPhoton()
    {
        if (this != _instance)
        {
            return;
        }

        if (PhotonVoiceSettings.Instance.AutoDisconnect)
        {
            client.Disconnect();
        }
    }

    internal static void LinkSpeakerToRemoteVoice(PhotonVoiceSpeaker speaker)
    {
        instance.client.LinkSpeakerToRemoteVoice(speaker);
    }

    internal static void UnlinkSpeakerFromRemoteVoice(PhotonVoiceSpeaker speaker)
    {
        // can be called from OnDestroy, check if still exists
        if (!destroyed)
        {
            instance.client.UnlinkSpeakerFromRemoteVoice(speaker);
        }
    }
}

public class UnityVoiceFrontend : Voice.LoadBalancingFrontend
{
    // or could be dict of dicts like VoiceClient.remoteVoices counterpart
    private Dictionary<VoiceIdPair, PhotonVoiceSpeaker> voiceSpeakers = new Dictionary<VoiceIdPair, PhotonVoiceSpeaker>();

    // let user code set actions which we occupy; call them in our actions
    public Action<int, byte, Voice.VoiceInfo> OnRemoteVoiceInfoAction { get; set; }
    public Action<int, byte> OnRemoteVoiceRemoveAction { get; set; }
    public Action<int, byte, float[]> OnAudioFrameAction { get; set; }

    new public Action<LoadBalancing.ClientState> OnStateChangeAction { get; set; }
    new public Action<OperationResponse> OnOpResponseAction { get; set; }

    internal UnityVoiceFrontend(ConnectionProtocol connetProtocol) : base(connetProtocol)
    {
        this.voiceClient.OnRemoteVoiceInfoAction += OnRemoteVoiceInfo;
        this.AutoJoinLobby = false;
        base.OnStateChangeAction += OnStateChange;
        base.OnOpResponseAction += OnOpResponse;

        this.loadBalancingPeer.DebugOut = DebugLevel.INFO;
    }

    private bool reconnect = false;

    /// <summary>
    /// Trigger reconnect to the (possible other) room.
    /// </summary>
    /// /// <remarks>
    /// When connecting manually, call this to switch Voice client to new voice room if PUN room changed.
    /// </remarks>
    public void Reconnect()
    {
        if (this.State == LoadBalancing.ClientState.Disconnected || this.State == LoadBalancing.ClientState.PeerCreated)
        {
            PhotonVoiceNetwork.Connect();
        }
        else
        {
            this.reconnect = true;
            this.Disconnect();
        }
    }

    public override void DebugReturn(DebugLevel level, string message)
    {
        message = string.Format("PUNVoice: {0}", message);
        if (level == DebugLevel.ERROR)
        {
            Debug.LogError(message);
        }
        else if (level == DebugLevel.WARNING)
        {
            Debug.LogWarning(message);
        }
        else if (level == DebugLevel.INFO && PhotonNetwork.logLevel >= PhotonLogLevel.Informational)
        {
            Debug.Log(message);
        }
        else if (level == DebugLevel.ALL && PhotonNetwork.logLevel == PhotonLogLevel.Full)
        {
            Debug.Log(message);
        }
    }

    public void OnOpResponse(OperationResponse resp)
    {
        if (this.OnOpResponseAction != null) this.OnOpResponseAction(resp);
    }

    private void linkVoice(int playerId, byte voiceId, Voice.VoiceInfo voiceInfo, PhotonVoiceSpeaker speaker)
    {
        speaker.OnVoiceLinked(voiceInfo.SamplingRate, voiceInfo.Channels, voiceInfo.FrameDurationSamples, PhotonVoiceSettings.Instance.PlayDelayMs);
        var key = new VoiceIdPair(playerId, voiceId);
        PhotonVoiceSpeaker oldSpeaker;
        if (this.voiceSpeakers.TryGetValue(key, out oldSpeaker))
        {
            if (oldSpeaker == speaker)
            {
                return;
            }
            else 
            {
                Debug.LogFormat("PUNVoice: Player {0} voice #{1} speaker replaced.", playerId, voiceId);                
            }
        }
        else 
        {
            Debug.LogFormat("PUNVoice: Player {0} voice #{1} speaker created.", playerId, voiceId);
        }
        this.voiceSpeakers[key] = speaker;
    }

    public void OnRemoteVoiceInfo(int channelId, int playerId, byte voiceId, Voice.VoiceInfo voiceInfo, ref Voice.RemoteVoiceOptions options)
    {
        options.OnDecodedFrameFloatAction += (frame) => OnAudioFrame(playerId, voiceId, frame);
        options.OnRemoteVoiceRemoveAction += () => OnRemoteVoiceRemove(playerId, voiceId);

        var key = new VoiceIdPair(playerId, voiceId);
        if (this.voiceSpeakers.ContainsKey(key))
        {
            Debug.LogWarningFormat("PUNVoice: Info duplicate for voice #{0} of player {1}", voiceId, playerId);
        }

        PhotonVoiceSpeaker speaker = null;
        PhotonVoiceSpeaker[] speakers = GameObject.FindObjectsOfType<PhotonVoiceSpeaker>();
        foreach (var s in speakers)
        {
            if (s.photonView.viewID == (int)voiceInfo.UserData)
            {
                speaker = s;
                break;
            }
        }

        if (speaker == null)
        {
            //            Debug.LogWarning("PUNVoice: No PhotonVoiceMic found for info of voice #" + voiceId + " of player " + playerId);
        }
        else
        {
            this.linkVoice(playerId, voiceId, voiceInfo, speaker);
        }

        // do not expose options to user code
        if (this.OnRemoteVoiceInfoAction != null) this.OnRemoteVoiceInfoAction(playerId, voiceId, voiceInfo);

    }

    // Try to link new PUN object with Speaker attached to remote voice.
    // If remote voice is not available yet, will link as soon as remote voice info arrives in OnRemoteVoiceInfo.
    public void LinkSpeakerToRemoteVoice(PhotonVoiceSpeaker speaker)
    {
        foreach (var i in this.VoiceClient.RemoteVoiceInfos)
        {
            if (speaker.photonView.viewID == (int)i.Info.UserData)
            {
                this.linkVoice(i.PlayerId, i.VoiceId, i.Info, speaker);
            }
        }
    }

    public void OnRemoteVoiceRemove(int playerId, byte voiceId)
    {
        var key = new VoiceIdPair(playerId, voiceId);
        if (!this.unlinkSpeaker(key))
        {
            Debug.LogWarningFormat("PUNVoice: Voice #{0} of player {1} not found.", voiceId, playerId);
        }
        else
        {
            Debug.LogFormat("PUNVoice: Player {0} voice # {1} speaker unlinked.", playerId, voiceId);
        }

        if (this.OnRemoteVoiceRemoveAction != null) this.OnRemoteVoiceRemoveAction(playerId, voiceId);
    }

    private bool unlinkSpeaker(VoiceIdPair key)
    {
        PhotonVoiceSpeaker s;
        if (this.voiceSpeakers.TryGetValue(key, out s))
        {
            s.OnVoiceUnlinked();
        }
        return this.voiceSpeakers.Remove(key);
    }

    // Unlinks destroyed PUN object from remote voice (if not done yet in OnRemoteVoiceRemove)
    public void UnlinkSpeakerFromRemoteVoice(PhotonVoiceSpeaker speaker)
    {
        var toRemove = new List<VoiceIdPair>();
        foreach (var s in this.voiceSpeakers)
        {
            if (s.Value == speaker)
            {
                toRemove.Add(s.Key);
                Debug.LogFormat("PUNVoice: Player {0} voice # {1} speaker unlinked.", s.Key.Key, s.Key.Value);
            }
        }
        foreach (var k in toRemove)
        {
            this.unlinkSpeaker(k);
        }
    }

    public void OnAudioFrame(int playerId, byte voiceId, float[] frame)
    {
        PhotonVoiceSpeaker voiceSpeaker = null;
        if (this.voiceSpeakers.TryGetValue(new VoiceIdPair(playerId, voiceId), out voiceSpeaker))
        {
            voiceSpeaker.OnAudioFrame(frame);
        }
        else
        {
            Debug.LogWarningFormat("PUNVoice: Audio Frame event for not existing speaker for voice #{0} of player {1}.",  
                voiceId, playerId);
        }

        if (this.OnAudioFrameAction != null) this.OnAudioFrameAction(playerId, voiceId, frame);
    }

    public void OnStateChange(LoadBalancing.ClientState state)
    {
        if (PhotonNetwork.logLevel >= PhotonLogLevel.Informational)
        {
            Debug.LogFormat("PUNVoice: Voice Client state: {0}", state);
        }
        switch (state)
        {
            case LoadBalancing.ClientState.ConnectedToMasterserver:
                if (PhotonNetwork.inRoom)
                {
                    this.OpJoinOrCreateRoom(string.Format("{0}_voice_", PhotonNetwork.room.Name), 
                        new LoadBalancing.RoomOptions() { IsVisible = false }, null);
                }
                else
                {
                    Debug.LogWarning("PUNVoice: PUN client is not in room yet. Disconnecting voice client.");
                    this.Disconnect();
                }
                break;
            case LoadBalancing.ClientState.Disconnected: // triggered by PUN room join or network failure                
                if (this.reconnect)
                {
                    PhotonVoiceNetwork.Connect();
                }
                this.reconnect = false;
                break;
        }

        if (this.OnStateChangeAction != null) this.OnStateChangeAction(state);
    }
}
