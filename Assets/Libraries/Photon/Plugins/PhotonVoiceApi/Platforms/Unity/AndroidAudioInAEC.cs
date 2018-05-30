using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Voice = ExitGames.Client.Photon.Voice;

public class AndroidAudioInAEC : Voice.IAudioPusher<short>
{
    class DataCallback : AndroidJavaProxy
    {
        Action<short[]> callback;
        IntPtr javaBuf;
        public DataCallback(Action<short[]> callback, IntPtr javaBuf) : base("com.exitgames.photon.audioinaec.AudioInAEC$DataCallback")
        {
            this.callback = callback;
            this.javaBuf = javaBuf;
        }
        public void OnData()
        {
            //TODO: copy to LocalVoiceFramed.PushDataBufferPool element instead
            var buf = AndroidJNI.FromShortArray(javaBuf);
            cntFrame++;
            cntShort += buf.Length;
            this.callback(buf);
        }
        public void OnStop()
        {
            AndroidJNI.DeleteGlobalRef(javaBuf);
        }
        int cntFrame;
        int cntShort;

    }

    AndroidJavaObject audioIn;
    IntPtr javaBuf;
    public AndroidAudioInAEC()
    {

    }

    // Supposed to be called once at voice initialization.
    // Otherwise recreate native object (instead of adding 'set callback' method to java interface)
    public void SetCallback(Action<short[]> callback, Voice.LocalVoice localVoice)
    {
        if (audioIn != null)
        {
            Dispose();
        }
        var voiceFrameSize = ((Voice.LocalVoiceFramed)localVoice).FrameSize;
        // setting to voice FrameSize lets to avoid framing procedure
        javaBuf = AndroidJNI.NewGlobalRef(AndroidJNI.NewShortArray(voiceFrameSize));

        this.callback = new DataCallback(callback, javaBuf);
        audioIn = new AndroidJavaObject("com.exitgames.photon.audioinaec.AudioInAEC");
        bool aecAvailable = audioIn.Call<bool>("AECIsAvailable");
        int minBufSize = audioIn.Call<int>("GetMinBufferSize", SamplingRate, Channels);
        Debug.LogFormat("AndroidAudioInAEC: AndroidJavaObject created: aecAvailable: {0}, minBufSize: {1}", aecAvailable, minBufSize);

        AndroidJavaClass app = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject activity = app.GetStatic<AndroidJavaObject>("currentActivity");
        // Set buffer IntPtr reference separately via pure jni call, pass other values and start capture via AndroidJavaObject helper

        var meth = AndroidJNI.GetMethodID(audioIn.GetRawClass(), "SetBuffer", "([S)Z");
        bool ok = AndroidJNI.CallBooleanMethod(audioIn.GetRawObject(), meth, new jvalue[] { new jvalue() { l = javaBuf } });
        if (ok) {
            ok = audioIn.Call<bool>("Start", activity, this.callback, SamplingRate, Channels, minBufSize * 4, aecAvailable);
        }

        Debug.LogFormat("AndroidAudioInAEC: AndroidJavaObject started: {0}, buffer size: {1}, sampling rate: {2}, channels: {3}, record buffer size: {4}, aec: {5}", ok, voiceFrameSize, SamplingRate, Channels, minBufSize * 4, aecAvailable);
    }
       
    DataCallback callback;

    public int Channels { get { return 1; } }

    public int SamplingRate { get { return 44100; } }

    public void Dispose()
    {
        audioIn.Call<bool>("Stop");        
    }
}
