#if (UNITY_IOS && !UNITY_EDITOR) || __IOS__
#define DLL_IMPORT_INTERNAL
#endif

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Voice = ExitGames.Client.Photon.Voice;

// Experimental.
// Does not work.
public class SpeexDSP : MonoBehaviour
{
    class Logger : Voice.ILogger
    {
        public void LogError(string fmt, params object[] args) { Debug.LogErrorFormat(fmt, args); }
        public void LogWarning(string fmt, params object[] args) { Debug.LogWarningFormat(fmt, args); }
        public void LogInfo(string fmt, params object[] args) { Debug.LogFormat(fmt, args); }
        public void LogDebug(string fmt, params object[] args) { Debug.LogFormat(fmt, args); }
    }

    [Header("Platforms")]
    public bool Windows = true;
    public bool Mac = true;
    // Native libs are not available
    //public bool Android = false;
    //public bool IOS = false;

    [Header("Effects"), Tooltip("Acoustic Echo Cancellation")]
    public bool AEC = true;
    public int AECPlaybackDelayMs = 100; // pc: 100, mac: 50
    public int AECFilterLengthMs = 200;

    public bool Denoise = true;

    [Tooltip("Automatic Gain Control")]
    public bool AGC = true;
    [Range(1.0f, 32768.0f)]
    public float AGCLevel = 20000.0f;

    public int AECPlayDelayCurrentFrames;

    public bool AECLatencyDetect;

    public Voice.SpeexProcessor Processor { get { return proc; } }
    
    Voice.SpeexProcessor proc;

    // Set in Awake depending on platform and user settings. Do not init component if false.
    public bool Active { get; private set; }

    public void ResetAEC()
    {
        if (proc != null)
        {
            proc.ResetAEC();
        }
    }

    private void Awake()
    {
        switch (Application.platform)
        {
            case RuntimePlatform.WindowsEditor:
            case RuntimePlatform.WindowsPlayer:
                Active = Windows;
                break;
            case RuntimePlatform.OSXEditor:
            case RuntimePlatform.OSXPlayer:
                Active = Mac;
                break;
            // Native libs are not available
            //case RuntimePlatform.Android:
            //    Active = Android;
            //    break;
            //case RuntimePlatform.IPhonePlayer:
            //    Active = IOS;
            //    break;
        }
    }

    private void Start()
    {
    }

    bool prevAEC = false;
    private void UpdateProcProps()
    {
        if (proc != null)
        {
            if (prevAEC != AEC)
            {
                setOutputListener(AEC);
                prevAEC = AEC;
            }
            proc.AEC = AEC;
            proc.AECFilterLengthMs = AECFilterLengthMs;
            proc.AECPlaybackDelayMs = AECPlaybackDelayMs;
            proc.Denoise = Denoise;
            proc.AGC = AGC;
            proc.AGCLevel = AGCLevel;
            AECPlayDelayCurrentFrames = proc.AECurrentPlayDelayFrames;
            if (proc.AECLatencyDetect != AECLatencyDetect)
            {
                if (AECLatencyDetect)
                {
                    aecLDState = 0;
                    if (aecLDPing == null)
                    {
                        InitAECLatencyDetect();
                    }
                }
                proc.AECLatencyDetect = AECLatencyDetect;
            }
        }
    }

    private void setOutputListener(bool set)
    {
        var audioListener = FindObjectOfType<AudioListener>();
        if (audioListener != null)
        {
            var ac = audioListener.gameObject.GetComponent<AudioOutCapture>();
            if (ac != null)
            {
                ac.OnAudioFrame -= proc.OnAudioOutFrame;
            }
            if (set)
            {
                if (ac == null)
                {
                    ac = audioListener.gameObject.AddComponent<AudioOutCapture>();
                }

                ac.OnAudioFrame += proc.OnAudioOutFrame;
            }
        }
    }

    private void Update()
    {
        UpdateProcProps();
        if (proc != null)
        {
            if (Time.frameCount % 10 == 0)
            {
                proc.PrintInfo();
            }

            if (AECLatencyDetect)
            {
                var t = Time.time;                
                // relax for 1 sec
                if (aecLDState == 0)
                {
                    aecLDState = 1;
                    aecLDStart = t;
                }
                var since = t - aecLDStart;
                // calibrate for 2 sec
                if (aecLDState == 1 && since > 1.0f)
                {
                    aecLDState = 2;
                    proc.AECLatecnyDetectCaliberate();
                }
                // ping 
                if (aecLDState == 2 && since > 4.0f)
                {
                    aecLDState = 3;
                    aecLDPing.Play();
                }
                // reset
                if (aecLDState == 3 && since > 5.0f)
                {
                    aecLDState = 0;
                }
            }
        }
    }

    void InitAECLatencyDetect()
    {
        int lenMs = 200;
        var freq = AudioSettings.outputSampleRate;
        aecLDPing = gameObject.AddComponent<AudioSource>();
        aecLDPing.clip = AudioClip.Create("AEC ping", freq * lenMs / 1000, 2, freq, false);
        aecLDPing.loop = false;
        var d = new float[aecLDPing.clip.frequency * 2];
        //for (int i = 0; i < 20; i++)
        //{
        //    d[2 * i] = d[2 * i + 1] = 1.0f;
        //}
        for (int i = 0; i < aecLDPing.clip.frequency; i++)
        {
            d[2 * i] = d[2 * i + 1] = (float)System.Math.Sin(i / 4.0f);
        }
        aecLDPing.clip.SetData(d, 0);
    }

    // latency detect
    int aecLDState;
    float aecLDStart;
    AudioSource aecLDPing;

    // Message sent by PhotonVoiceRecorder
    void PhotonVoiceCreated(Voice.LocalVoice localVoice)
    {
        if (!Active)
        {
            return;
        }
        if (localVoice.Info.Channels != 1)
        {
            throw new Exception("SpeexProcessor: only mono audio signals supported.");
        }
        if (!(localVoice is Voice.LocalVoiceAudioShort))
        {
            throw new Exception("SpeexProcessor: only short audio voice supported (Set PhotonVoiceRecorder.TypeConvert option).");
        }
        var v = (Voice.LocalVoiceAudioShort)localVoice;

        // can't access the AudioSettings properties in InitAEC if it's called from not main thread
        var playChannelCount = new Dictionary<AudioSpeakerMode, int>() {
            {AudioSpeakerMode.Raw, 0},
            {AudioSpeakerMode.Mono, 1},
            {AudioSpeakerMode.Stereo, 2},
            {AudioSpeakerMode.Quad, 4},
            {AudioSpeakerMode.Surround, 5},
            {AudioSpeakerMode.Mode5point1, 6},
            {AudioSpeakerMode.Mode7point1, 8},
            {AudioSpeakerMode.Prologic, 0},
        }[AudioSettings.speakerMode];
        int playBufSize;
        int playBufNum;
        AudioSettings.GetDSPBufferSize(out playBufSize, out playBufNum);
        proc = new Voice.SpeexProcessor(new Logger(), () => (long)(AudioSettings.dspTime * 1000),v.FrameSize, localVoice.Info.SourceSamplingRate, localVoice.Info.Channels, AudioSettings.outputSampleRate, playChannelCount, playBufSize);        
        v.AddPreProcessor(proc);
        UpdateProcProps();
        Debug.Log("SpeexDSP initialized.");
    }

    void PhotonVoiceRemoved()
    {
        reset();
    }

    private void OnDestroy()
    {
        reset();
    }

    private void reset()
    {
        if (proc != null)
        {
            setOutputListener(false);
            proc.Dispose();
            proc = null;
        }
    }
}
