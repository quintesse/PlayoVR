using UnityEngine;
#if !UNITY_EDITOR && UNITY_PS4
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Threading;
    using ExitGames.Client.Photon.Voice;
    using Sony.NP;
#endif

/// <summary>
/// Component representing remote audio stream in local scene. Automatically attached to the PUN object which owner's instance has streaming Recorder attached.
/// </summary>
[RequireComponent(typeof (AudioSource))]
[DisallowMultipleComponent]
[AddComponentMenu("Photon Voice/Photon Voice Speaker")]
//[HelpURL("https://doc.photonengine.com/en-us/voice/current/getting-started/voice-for-pun#the__audio_source__prefab")]
public class PhotonVoiceSpeaker : Photon.MonoBehaviour
{
#if !UNITY_EDITOR && UNITY_PS4
    const string libName = "PhotonVoiceAudioOutputPlugin";
    [DllImport(libName)]
    private static extern IntPtr egpvopen(int userID, uint granularity, bool stereo); // open a new audio output port
    [DllImport(libName)]
    private static extern void egpvclose(IntPtr pVoiceAudio); // close the specified port
    [DllImport(libName)]
    private static extern int egpvplay(IntPtr pVoiceAudio, IntPtr pData); // play the specified audio data on the specified port
    [DllImport(libName)]
    private static extern int egpvgetHandle(IntPtr pVoiceAudio); // returns a negative value if egpvopen has failed or the handle that is passed to the Sony APIs to identify the port if egpvopen has been successful
    [DllImport(libName)]
    private static extern bool egpvgetHeadphonesConnected(IntPtr pVoiceAudio);  // returns true, if headphones are plugged in for the specified output port, false otherwise
#endif



    private AudioStreamPlayer player;
#if !UNITY_EDITOR && UNITY_PS4
    private static IntPtr pPhotonVoiceAudioOutput;
    private static Framer<float> framer;
    private static Dictionary<int, Queue<float[]>> frameBuf;
    public const int FRAME_POOL_CAPACITY = 50;
    public const int GRANULARITY = 256; // The number of samples per channel of the audio data that will be passed to the Sony APIs at one time. The values that can be specified are 256, 512, 768, 1024, 1280, 1536, 1792 and 2048.
    private static ExitGames.Client.Photon.Voice.PrimitiveArrayPool<float> framePool;
    private static Thread playThread;
    private static bool playThreadShouldTerminate;
#endif

    /// <summary>Time when last audio packet was received for the speaker.</summary>
    public long LastRecvTime { get; private set; }

    /// <summary>Is the speaker playing right now.</summary>
    public bool IsPlaying { get { return this.player.IsPlaying; } }

    /// <summary>Smoothed difference between (jittering) stream and (clock-driven) player.</summary>
    public int CurrentBufferLag { get { return this.player.CurrentBufferLag; } }

    /// <summary>Is the speaker linked to the remote voice (info available and streaming is possible).</summary>
    public bool IsVoiceLinked { get { return this.player != null && this.player.IsStarted; } }

    void Awake()
    {
        this.player = new AudioStreamPlayer(GetComponent<AudioSource>(), "PUNVoice: PhotonVoiceSpeaker:", PhotonVoiceSettings.Instance.DebugInfo);
        PhotonVoiceNetwork.LinkSpeakerToRemoteVoice(this);
    }

    // initializes the speaker with remote voice info
    internal void OnVoiceLinked(int frequency, int channels, int frameSamplesPerChannel, int playDelayMs)
    {
#if !UNITY_EDITOR && UNITY_PS4
        if(frameBuf == null)
        {
            if(framePool == null)
            {
                framePool = new ExitGames.Client.Photon.Voice.PrimitiveArrayPool<float>(FRAME_POOL_CAPACITY, "PhotonVoiceSpeaker");
                framePool.Init(frameSamplesPerChannel * channels);
            }

            int userID = PhotonVoiceSettings.Instance.PS4UserID;
            if(userID == 0)
            {
                UserProfiles.LocalUsers localUsers = new UserProfiles.LocalUsers();
                UserProfiles.GetLocalUsers(localUsers);
                userID = localUsers.LocalUsersIds[0].UserId.Id;
            }
            pPhotonVoiceAudioOutput = egpvopen(userID, GRANULARITY, channels > 1);
            framer = new Framer<float>(GRANULARITY * channels);
            playThreadShouldTerminate = false;
            playThread = new Thread(Play);
            playThread.Name = "photon voice audio output thread";
            playThread.IsBackground = true;
            playThread.Start();
            frameBuf = new Dictionary<int, Queue<float[]>>();
        }
        lock(frameBuf)
        {
            frameBuf.Add(GetInstanceID(), new Queue<float[]>());
        }
#endif
        this.player.Start(frequency, channels, frameSamplesPerChannel, playDelayMs);
    }

    internal void OnVoiceUnlinked()
    {
        Cleanup();
    }

    void Update()
    {
        this.player.Update();
    }

    void OnDestroy()
    {
        PhotonVoiceNetwork.UnlinkSpeakerFromRemoteVoice(this);
        Cleanup();
    }

    void OnApplicationQuit()
    {
        Cleanup();
    }

    void Cleanup()
    {
#if !UNITY_EDITOR && UNITY_PS4
        if(frameBuf == null)
            return;
        lock(frameBuf)
        {
            frameBuf.Remove(GetInstanceID());
            if(frameBuf.Count > 0)
                return;
            frameBuf = null;
        }
        playThreadShouldTerminate = true;
        framePool = null;
        egpvclose(pPhotonVoiceAudioOutput);
        pPhotonVoiceAudioOutput = IntPtr.Zero;
#endif
        this.player.Stop();
    }

    internal void OnAudioFrame(float[] frame)
    {
        // Set last time we got something
        this.LastRecvTime = System.DateTime.Now.Ticks;

#if !UNITY_EDITOR && UNITY_PS4
        bool headphonesConnected = egpvgetHeadphonesConnected(pPhotonVoiceAudioOutput);
        if(headphonesConnected)
        {
            float[] frameCopy = framePool.AcquireOrCreate();
            Array.Copy(frame, frameCopy, frame.Length);
            lock(frameBuf)
            {
                Queue<float[]> instanceFrameBuf;
                frameBuf.TryGetValue(GetInstanceID(), out instanceFrameBuf);
                if(instanceFrameBuf != null)
                    instanceFrameBuf.Enqueue(frameCopy);
            }
        }
        else
#endif
            this.player.OnAudioFrame(frame);
    }

#if !UNITY_EDITOR && UNITY_PS4
    internal static void Play()
    {
        while(!playThreadShouldTerminate)
        {
            if(frameBuf != null)
            {
                float[] frameMix = null;
                lock(frameBuf)
                {
                    foreach(KeyValuePair<int, Queue<float[]>> it in frameBuf)
                    {
                        if(it.Value.Count > 0)
                        {
                            float[] frame = it.Value.Dequeue();
                            if(frameMix == null)
                            {
                                frameMix = framePool.AcquireOrCreate();
                                for(int i=0; i<frame.Length; ++i)
                                    frameMix[i] = frame[i];
                            }
                            for(int i=0; i<frame.Length; ++i)
                                frameMix[i] += frame[i];
                            framePool.Release(frame);
                        }
                    }
                }
                if(frameMix != null)
                {
                    foreach(var newFrame in framer.Frame(frameMix))
                    {
                        unsafe
                        {
                            fixed(float* pArray = newFrame)
                                egpvplay(pPhotonVoiceAudioOutput, new IntPtr(pArray));
                        }
                    }
                    framePool.Release(frameMix);
                }
            }
        }
    }
#endif
}
