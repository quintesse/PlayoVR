#if UNITY_IOS || (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX)
#if (UNITY_IPHONE && !UNITY_EDITOR) || __IOS__
#define DLL_IMPORT_INTERNAL
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using UnityEngine;
using Voice = ExitGames.Client.Photon.Voice;

public class MonoPInvokeCallbackAttribute : System.Attribute
{
	private Type type;
	public MonoPInvokeCallbackAttribute( Type t ) { type = t; }
}

public class AppleAudioInPusher : Voice.IAudioPusher<float>
{	
#if DLL_IMPORT_INTERNAL
	const string lib_name = "__Internal";
#else
const string lib_name = "AudioIn";
#endif
	[DllImport(lib_name)]
	private static extern IntPtr Photon_Audio_In_CreatePusher(int instanceID, int deviceID, Action<int, IntPtr, int> pushCallback);
	[DllImport(lib_name)]
	private static extern void Photon_Audio_In_Destroy(IntPtr handler);

	private delegate void CallbackDelegate(int instanceID, IntPtr buf, int len);

	public AppleAudioInPusher(int deviceID) {
		this.deviceID = deviceID;
	}

	private int deviceID;
	// IL2CPP does not support marshaling delegates that point to instance methods to native code.
	// Using static method and per instance table.
	static int instanceCnt;
	private static Dictionary<int, AppleAudioInPusher> instancePerHandle = new Dictionary<int, AppleAudioInPusher>();
	[MonoPInvokeCallbackAttribute(typeof(CallbackDelegate))]
	private static void nativePushCallback(int instanceID, IntPtr buf, int len)
	{
		AppleAudioInPusher instance;
		if (instancePerHandle.TryGetValue (instanceID, out instance)) 
		{
			instance.push(buf, len);
		}
	}

	IntPtr handle;
    Action<float[]> pushCallback;
    Voice.LocalVoiceFramed<float> localVoice;

    // Supposed to be called once at voice initialization.
    // Otherwise recreate native object (instead of adding 'set callback' method to native interface)
    public void SetCallback(Action<float[]> callback, Voice.LocalVoice localVoice)
    {
        if (handle != IntPtr.Zero)
        {
            Dispose();
        }

        this.pushCallback = callback;
        this.localVoice = (Voice.LocalVoiceFramed<float>)localVoice;
		handle = Photon_Audio_In_CreatePusher(instanceCnt, deviceID, nativePushCallback);
        instancePerHandle.Add(instanceCnt++, this);
    }
	private void push(IntPtr buf, int len)
	{
		if (this.localVoice.PushDataBufferPool.Info != len)
		{
            // Set pool buffers size
            this.localVoice.PushDataBufferPool.Init(len);
		}
        var bufManaged = this.localVoice.PushDataBufferPool.AcquireOrCreate();
        Marshal.Copy(buf, bufManaged, 0, len);
		pushCallback(bufManaged);
	}

    public int Channels { get { return 1; } }


    public int SamplingRate { get { return 44100; } }

	
    public void Dispose()
    {
		Photon_Audio_In_Destroy(handle);
		// TODO: Remove this from instancePerHandle
    }
}
#endif