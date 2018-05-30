#if (UNITY_IOS && !UNITY_EDITOR) || __IOS__
#define DLL_IMPORT_INTERNAL
#endif
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace ExitGames.Client.Photon.Voice
{
    public class SpeexProcessor : SpeexLib, Voice.LocalVoiceAudioShort.IProcessor
    {
        bool _AEC;
        // echo cancellation
        public bool AEC { get { return _AEC; } set { if (_AEC != value) { _AEC = value; playBufQueue.Clear(); } } }
        // Applied on next echo state cretion
        public int AECFilterLengthMs { get; set; }
        int _AECPlaybackDelayMs;
        public int AECPlaybackDelayMs
        {
            get { return _AECPlaybackDelayMs; }
            set { if (_AECPlaybackDelayMs != value) { _AECPlaybackDelayMs = value; InitPlayDelay(value); } }
        }
        public int AECurrentPlayDelayFrames { get { return playBufQueue.Count; } }
        bool _AECLatencyDetect;
        public bool AECLatencyDetect
        {
            get { return _AECLatencyDetect; }
            set
            {
                if (_AECLatencyDetect != value)
                {
                    _AECLatencyDetect = value;
                    if (detectPlay == null) InitLatencyDetect();
                }
            }
        }
        public struct AECLatencyResultType
        {
            public int LatencyMs;
            public int LatencyDelayedMs;
            public bool PlayDetected;
            public bool PlayDelayedDetected;
            public bool RecDetected;
        }
        public AECLatencyResultType AECLatencyResult
        {
            get
            {
                return new AECLatencyResultType()
                {
                    LatencyMs = (int)(detectTimeRec - detectTimePlay),
                    LatencyDelayedMs = (int)(detectTimeRec - detectTimePlayDelayed),
                    PlayDetected = detectPlay != null ? detectPlay.Detector.Detected : false,
                    PlayDelayedDetected = detectPlayCorr != null ? detectPlayCorr.Detector.Detected : false,
                    RecDetected = detectRec != null ? detectRec.Detector.Detected : false
                };
            }
        }
        // Triggers echo state recreation with current filter length
        public void ResetAEC()
        {
            lock (this)
            {
                DestroyEchoState();
            }
        }
        public void AECLatecnyDetectCaliberate()
        {
            detectPlayCorr.Calibrate(2000);
            detectPlay.Calibrate(2000);
            detectRec.Calibrate(2000);
        }
        public bool Denoise
        {
            get { return getBool(SPEEX_PREPROCESS_GET_DENOISE); }
            set { set(SPEEX_PREPROCESS_SET_DENOISE, value); }
        }
        // auto gain conrtol
        public bool AGC
        {
            get { return getBool(SPEEX_PREPROCESS_GET_AGC); }
            set { set(SPEEX_PREPROCESS_SET_AGC, value); }
        }
        public float AGCLevel
        {
            get { return getFloat(SPEEX_PREPROCESS_GET_AGC_LEVEL); }
            set { set(SPEEX_PREPROCESS_SET_AGC_LEVEL, value); }
        }
        
        void set(int param, bool val)
        {
            int i = val ? 1 : 0;
            speex_preprocess_ctl(st, param, ref i);
        }
        void set(int param, float val)
        {
            speex_preprocess_ctl(st, param, ref val);
        }
        bool getBool(int param)
        {
            int i = 0;
            speex_preprocess_ctl(st, param, ref i);
            return i != 0;
        }
        float getFloat(int param)
        {
            float f = 0;
            speex_preprocess_ctl(st, param, ref f);
            return f;
        }
        int frameSamples;
        int samplingRate;
        int channels;
        int playDelayFrames;
        int playDelayMaxFrames;
        IntPtr stEcho;
        IntPtr st;
        bool disposed;
        short[] resultBuf;
        Voice.PrimitiveArrayPool<short> playbackBufPool;
        Queue<short[]> playBufQueue = new Queue<short[]>();
        Voice.Framer<float> playFramer;
        int playSamplingRate;
        int playChannels;
        ILogger logger;
        Func<long> clockMs;
        AudioUtil.VoiceLevelDetectCalibrate<float> detectPlay;
        AudioUtil.VoiceLevelDetectCalibrate<short> detectPlayCorr;
        AudioUtil.VoiceLevelDetectCalibrate<short> detectRec;
        long detectTimePlay;
        long detectTimePlayDelayed;
        long detectTimeRec;
        public SpeexProcessor(ILogger logger, Func<long> clockMs, int frameSize, int samplingRate, int channels, int playSamplingRate, int playChannels, int playBufSize)
        {
            this.clockMs = clockMs == null ? () => DateTime.Now.Millisecond : clockMs;
            this.logger = logger;
            this.frameSamples = frameSize / channels;
            this.samplingRate = samplingRate;
            this.channels = channels;
            this.playSamplingRate = playSamplingRate;
            this.playChannels = playChannels;
            this.resultBuf = new short[frameSize];
            this.st = speex_preprocess_state_init(frameSamples, samplingRate);
            logger.LogInfo("SpeexProcessor state: create sampling rate {0}, frame samples {1}", samplingRate, frameSamples);
        }
        void InitLatencyDetect()
        { 
            this.detectPlay = new AudioUtil.VoiceLevelDetectCalibrate<float>(playSamplingRate, playChannels);
            this.detectPlayCorr = new AudioUtil.VoiceLevelDetectCalibrate<short>(playSamplingRate, playChannels);
            this.detectRec = new AudioUtil.VoiceLevelDetectCalibrate<short>(samplingRate, channels);
            this.detectPlay.Detector.OnDetected += () => detectTimePlay = clockMs();
            this.detectPlayCorr.Detector.OnDetected += () => detectTimePlayDelayed = clockMs();
            this.detectRec.Detector.OnDetected += () => detectTimeRec = clockMs();
        }
        public void InitAEC()
        {
            lock (this)
            {
                if (disposed)
                {
                    return;
                }
                playFramer = new Voice.Framer<float>(frameSamples * playSamplingRate / samplingRate * playChannels);
                InitPlayDelay(AECPlaybackDelayMs);
                int filterLength = samplingRate * AECFilterLengthMs / 1000;
                DestroyEchoState();
                stEcho = speex_echo_state_init_mc(frameSamples, filterLength, channels, playChannels);
                speex_echo_ctl(stEcho, SPEEX_ECHO_SET_SAMPLING_RATE, ref samplingRate);
                speex_preprocess_ctl(st, SPEEX_PREPROCESS_SET_ECHO_STATE, stEcho);
                logger.LogInfo("SpeexProcessor AEC: create sampling rate {0}, frame samples {1}, filter length {2}ms={3}frames, mic channels {4}, out channels {5}, playback delay {6}ms={7}:{8}frames", samplingRate, frameSamples, AECFilterLengthMs, filterLength, channels, playChannels, AECPlaybackDelayMs, playDelayFrames, playDelayMaxFrames);
                logger.LogInfo("SpeexProcessor AEC: output sampling rate {0}", playSamplingRate);
                if (playSamplingRate != samplingRate)
                {
                    logger.LogWarning("SpeexProcessor AEC: output sampling rate {0} != {1} capture sampling rate. For better AEC, set audio source (microphone) and audio output samping rates to the same value.", playSamplingRate, samplingRate);
                }
            }
        }
        void InitPlayDelay(int ms)
        {
            playDelayFrames = ms * samplingRate / frameSamples / 1000;
            playDelayMaxFrames = playDelayFrames * 3;
            playbackBufPool = new Voice.PrimitiveArrayPool<short>(playDelayMaxFrames, "Speex playback pool", frameSamples * playChannels);
        }
        public short[] Process(short[] buf)
        {
            if (disposed) return buf;
            if (_AECLatencyDetect)
            {
                this.detectRec.Process(buf);
            }
            if (AEC)
            {
                // echo state lazy initialization
                if (stEcho == IntPtr.Zero)
                {
                    InitAEC();
                }
                lock (playBufQueue)
                {
                    frameCntRec++;
                    if (playBufQueue.Count > playDelayFrames)
                    {
                        var b = playBufQueue.Dequeue();
                        speex_echo_cancellation(stEcho, buf, b, resultBuf);
                        if (_AECLatencyDetect)
                        {
                            this.detectPlayCorr.Process(b);
                        }
                        playbackBufPool.Release(b);
                        buf = resultBuf;                        
                    }
                    else
                    {
                        logger.LogWarning("SpeexProcessor AEC: playbackBufQueue underrun: {0}", playBufQueue.Count);
                    }
                }
            }
            // the method returns VAD result in theory but VAD does not work in speex currently
            speex_preprocess_run(st, buf);
            return buf;
        }
        public void OnAudioOutFrame(float[] data, int outChannels)
        {
            if (disposed) return;
            lock (this) if (stEcho == IntPtr.Zero) return;
            if (outChannels != playChannels)
            {
                logger.LogError("SpeexProcessor AEC: OnAudioOutFrame channel count {0} != {1} AudioSettings.speakerMode channel count.", outChannels, playChannels);
                return;
            }
            if (_AECLatencyDetect)
            {
                this.detectPlay.Process(data);
            }
            foreach (var playbackBufFloat in playFramer.Frame(data))
            {
                lock (playBufQueue)
                {
                    // make sure that queue does not grow if buffers not consumed in Process
                    if (playBufQueue.Count > playDelayMaxFrames)
                    {
                        logger.LogWarning("SpeexProcessor AEC: playbackBufQueue overrun: {0}", playBufQueue.Count);
                        while (playBufQueue.Count > playDelayMaxFrames)
                        {
                            playbackBufPool.Release(playBufQueue.Dequeue());
                        }
                    }
                }
                var playbackBuf = playbackBufPool.AcquireOrCreate();
                if (playbackBufFloat.Length != playbackBuf.Length)
                {
                    Voice.AudioUtil.ResampleAndConvert(playbackBufFloat, playbackBuf, playbackBuf.Length, outChannels);
                }
                else
                {
                    Voice.AudioUtil.Convert(playbackBufFloat, playbackBuf, playbackBuf.Length);
                }
                lock (playBufQueue)
                {
                    playBufQueue.Enqueue(playbackBuf);
                    frameCntPlay++;
                }
            }
        }
        int frameCntRec;
        int frameCntPlay;
        public void PrintInfo()
        {
            //int size = 0;
            //speex_echo_ctl(st, SPEEX_ECHO_GET_IMPULSE_RESPONSE_SIZE, ref size);
            //logger.LogInfo("SpeexProcessor AEC: response size: {0}", size);
            //logger.LogInfo("===== {0} {1} {2} {3}", frameCntPlay, frameCntRec, frameCntPlay - frameCntRec, playBufQueue.Count);
            //if (_AECLatencyDetect)
            //{
            //    logger.LogInfo("===== {0} {1} {2} {3} {4} {5}", detectPlay.Level.CurrentAvgAmp, detectPlay.Detector.Threshold, detectPlay.Detector.Detected, detectRec.Level.CurrentAvgAmp, detectRec.Detector.Threshold, detectRec.Detector.Detected);
            //}
        }
        void DestroyEchoState()
        {
            if (stEcho != IntPtr.Zero)
            {
                speex_preprocess_ctl(st, SPEEX_PREPROCESS_SET_ECHO_STATE, IntPtr.Zero);
                speex_echo_state_destroy(stEcho);
                stEcho = IntPtr.Zero;
            }
        }
        public void Dispose()
        {
            lock (this)
            {
                if (!disposed)
                {
                    disposed = true;
                    if (st != IntPtr.Zero)
                    {
                        speex_preprocess_state_destroy(st);
                    }
                    DestroyEchoState();
                }
            }
        }
    }
    public class SpeexLib
    {
#if DLL_IMPORT_INTERNAL
        const string lib_name = "__Internal";
#else
        const string lib_name = "libspeexdsp";
#endif
        /** Set preprocessor denoiser state */
        public const int SPEEX_PREPROCESS_SET_DENOISE = 0;
        /** Get preprocessor denoiser state */
        public const int SPEEX_PREPROCESS_GET_DENOISE = 1;
        /** Set preprocessor Automatic Gain Control state */
        public const int SPEEX_PREPROCESS_SET_AGC = 2;
        /** Get preprocessor Automatic Gain Control state */
        public const int SPEEX_PREPROCESS_GET_AGC = 3;
        /** Set preprocessor Voice Activity Detection state */
        public const int SPEEX_PREPROCESS_SET_VAD = 4;
        /** Get preprocessor Voice Activity Detection state */
        public const int SPEEX_PREPROCESS_GET_VAD = 5;
        /** Set preprocessor Automatic Gain Control level (float) */
        public const int SPEEX_PREPROCESS_SET_AGC_LEVEL = 6;
        /** Get preprocessor Automatic Gain Control level (float) */
        public const int SPEEX_PREPROCESS_GET_AGC_LEVEL = 7;
        /** Set preprocessor dereverb state */
        public const int SPEEX_PREPROCESS_SET_DEREVERB = 8;
        /** Get preprocessor dereverb state */
        public const int SPEEX_PREPROCESS_GET_DEREVERB = 9;
        /** Set preprocessor dereverb level */
        public const int SPEEX_PREPROCESS_SET_DEREVERB_LEVEL = 10;
        /** Get preprocessor dereverb level */
        public const int SPEEX_PREPROCESS_GET_DEREVERB_LEVEL = 11;
        /** Set preprocessor dereverb decay */
        public const int SPEEX_PREPROCESS_SET_DEREVERB_DECAY = 12;
        /** Get preprocessor dereverb decay */
        public const int SPEEX_PREPROCESS_GET_DEREVERB_DECAY = 13;
        /** Set probability required for the VAD to go from silence to voice */
        public const int SPEEX_PREPROCESS_SET_PROB_START = 14;
        /** Get probability required for the VAD to go from silence to voice */
        public const int SPEEX_PREPROCESS_GET_PROB_START = 15;
        /** Set probability required for the VAD to stay in the voice state (integer percent) */
        public const int SPEEX_PREPROCESS_SET_PROB_CONTINUE = 16;
        /** Get probability required for the VAD to stay in the voice state (integer percent) */
        public const int SPEEX_PREPROCESS_GET_PROB_CONTINUE = 17;
        /** Set maximum attenuation of the noise in dB (negative number) */
        public const int SPEEX_PREPROCESS_SET_NOISE_SUPPRESS = 18;
        /** Get maximum attenuation of the noise in dB (negative number) */
        public const int SPEEX_PREPROCESS_GET_NOISE_SUPPRESS = 19;
        /** Set maximum attenuation of the residual echo in dB (negative number) */
        public const int SPEEX_PREPROCESS_SET_ECHO_SUPPRESS = 20;
        /** Get maximum attenuation of the residual echo in dB (negative number) */
        public const int SPEEX_PREPROCESS_GET_ECHO_SUPPRESS = 21;
        /** Set maximum attenuation of the residual echo in dB when near end is active (negative number) */
        public const int SPEEX_PREPROCESS_SET_ECHO_SUPPRESS_ACTIVE = 22;
        /** Get maximum attenuation of the residual echo in dB when near end is active (negative number) */
        public const int SPEEX_PREPROCESS_GET_ECHO_SUPPRESS_ACTIVE = 23;
        /** Set the corresponding echo canceller state so that residual echo suppression can be performed (NULL for no residual echo suppression) */
        public const int SPEEX_PREPROCESS_SET_ECHO_STATE = 24;
        /** Get the corresponding echo canceller state */
        public const int SPEEX_PREPROCESS_GET_ECHO_STATE = 25;
        /** Set maximal gain increase in dB/second (int32) */
        public const int SPEEX_PREPROCESS_SET_AGC_INCREMENT = 26;
        /** Get maximal gain increase in dB/second (int32) */
        public const int SPEEX_PREPROCESS_GET_AGC_INCREMENT = 27;
        /** Set maximal gain decrease in dB/second (int32) */
        public const int SPEEX_PREPROCESS_SET_AGC_DECREMENT = 28;
        /** Get maximal gain decrease in dB/second (int32) */
        public const int SPEEX_PREPROCESS_GET_AGC_DECREMENT = 29;
        /** Set maximal gain in dB (int32) */
        public const int SPEEX_PREPROCESS_SET_AGC_MAX_GAIN = 30;
        /** Get maximal gain in dB (int32) */
        public const int SPEEX_PREPROCESS_GET_AGC_MAX_GAIN = 31;
        /*  Can't set loudness */
        /** Get loudness */
        public const int SPEEX_PREPROCESS_GET_AGC_LOUDNESS = 33;
        /*  Can't set gain */
        /** Get current gain (int32 percent) */
        public const int SPEEX_PREPROCESS_GET_AGC_GAIN = 35;
        /*  Can't set spectrum size */
        /** Get spectrum size for power spectrum (int32) */
        public const int SPEEX_PREPROCESS_GET_PSD_SIZE = 37;
        /*  Can't set power spectrum */
        /** Get power spectrum (int32[] of squared values) */
        public const int SPEEX_PREPROCESS_GET_PSD = 39;
        /*  Can't set noise size */
        /** Get spectrum size for noise estimate (int32)  */
        public const int SPEEX_PREPROCESS_GET_NOISE_PSD_SIZE = 41;
        /*  Can't set noise estimate */
        /** Get noise estimate (int32[] of squared values) */
        public const int SPEEX_PREPROCESS_GET_NOISE_PSD = 43;
        /* Can't set speech probability */
        /** Get speech probability in last frame (int32).  */
        public const int SPEEX_PREPROCESS_GET_PROB = 45;
        /** Set preprocessor Automatic Gain Control level (int32) */
        public const int SPEEX_PREPROCESS_SET_AGC_TARGET = 46;
        /** Get preprocessor Automatic Gain Control level (int32) */
        public const int SPEEX_PREPROCESS_GET_AGC_TARGET = 47;
        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr speex_preprocess_state_init(int frame_size, int sampling_rate);
        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void speex_preprocess_state_destroy(IntPtr st);
        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int speex_preprocess_run(IntPtr st, short[] x);
        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int speex_preprocess_ctl(IntPtr st, int request, IntPtr ptr);
        public static int speex_preprocess_ctl(IntPtr st, int request, ref int value)
        {
            // implementation consistent with float method, could use WriteInt32 / ReadInt32 instead
            var ptr = Marshal.AllocCoTaskMem(4);
            Int32[] arr = new Int32[] { value };
            Marshal.Copy(arr, 0, ptr, 1);
            var ret = speex_preprocess_ctl(st, request, ptr);
            Marshal.Copy(ptr, arr, 0, 1);
            value = arr[0];
            Marshal.FreeCoTaskMem(ptr);
            return ret;
        }
        public static int speex_preprocess_ctl(IntPtr st, int request, ref float value)
        {
            var ptr = Marshal.AllocCoTaskMem(4);
            float[] arr = new float[] { value };
            Marshal.Copy(arr, 0, ptr, 1);
            var ret = speex_preprocess_ctl(st, request, ptr);
            Marshal.Copy(ptr, arr, 0, 1);
            Marshal.FreeCoTaskMem(ptr);
            return ret;
        }
        // echo
        /** Obtain frame size used by the AEC */
        public const int SPEEX_ECHO_GET_FRAME_SIZE = 3;
        /** Set sampling rate */
        public const int SPEEX_ECHO_SET_SAMPLING_RATE = 24;
        /** Get sampling rate */
        public const int SPEEX_ECHO_GET_SAMPLING_RATE = 25;
        /* Can't set window sizes */
        /** Get size of impulse response (int32) */
        public const int SPEEX_ECHO_GET_IMPULSE_RESPONSE_SIZE = 27;
        /* Can't set window content */
        /** Get impulse response (int32[]) */
        public const int SPEEX_ECHO_GET_IMPULSE_RESPONSE = 29;
        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr speex_echo_state_init(int frame_size, int filter_length);
        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr speex_echo_state_init_mc(int frame_size, int filter_length, int nb_mic, int nb_speakers);
        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void speex_echo_state_destroy(IntPtr st);
        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void speex_echo_cancellation(IntPtr st, short[] rec, short[] play, short[] outBuf);
        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void speex_echo_capture(IntPtr st, short[] rec, short[] outBuf);
        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void speex_echo_playback(IntPtr st, short[] play);
        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void speex_echo_state_reset(IntPtr st);
        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int speex_echo_ctl(IntPtr st, int request, IntPtr ptr);
        public static int speex_echo_ctl(IntPtr st, int request, ref int value)
        {            
            // implementation consistent with float method, could use WriteInt32 / ReadInt32 instead
            var ptr = Marshal.AllocCoTaskMem(4);
            Int32[] arr = new Int32[] { value };
            Marshal.Copy(arr, 0, ptr, 1);
            var ret = speex_echo_ctl(st, request, ptr);
            Marshal.Copy(ptr, arr, 0, 1);
            value = arr[0];
            Marshal.FreeCoTaskMem(ptr);
            return ret;
        }
        public static int speex_echo_ctl(IntPtr st, int request, ref float value)
        {
            var ptr = Marshal.AllocCoTaskMem(4);
            float[] arr = new float[] { value };
            Marshal.Copy(arr, 0, ptr, 1);
            var ret = speex_echo_ctl(st, request, ptr);
            Marshal.Copy(ptr, arr, 0, 1);
            value = arr[0];
            Marshal.FreeCoTaskMem(ptr);
            return ret;
        }
    }
}
