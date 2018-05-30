using System.Collections;
using System;
using System.Collections.Generic;
namespace ExitGames.Client.Photon.Voice
{
    public static class AudioUtil
    {
        public static void Resample<T>(T[] src, T[] dst, int dstCount, int channels)
        {
            if (channels == 1)
            {
                for (int i = 0; i < dstCount; i++)
                {
                    dst[i] = src[i * src.Length / dstCount];
                }
            }
            else if (channels == 2)
            {
                for (int i = 0; i < dstCount / 2; i++)
                {
                    var srcI = i * src.Length / dstCount;
                    var dstCh0I = i * 2;
                    var srcCh0I = srcI * 2;
                    dst[dstCh0I++] = src[srcCh0I++];
                    dst[dstCh0I] = src[srcCh0I];
                }
            }
            else
            {
                for (int i = 0; i < dstCount / channels; i++)
                {
                    var srcI = i * src.Length / dstCount;
                    var dstCh0I = i * channels;
                    var srcCh0I = srcI * channels;
                    for (int ch = 0; ch < channels; ch++)
                    {
                        dst[dstCh0I++] = src[srcCh0I++];
                    }
                }
            }
        }
        public static void ResampleAndConvert(short[] src, float[] dst, int dstCount, int channels)
        {
            if (channels == 1)
            {
                for (int i = 0; i < dstCount; i++)
                {
                    dst[i] = src[i * src.Length / dstCount] * (float)short.MaxValue;
                }
            }
            else if (channels == 2)
            {
                for (int i = 0; i < dstCount / 2; i++)
                {
                    var srcI = i * src.Length / dstCount;
                    var dstCh0I = i * 2;
                    var srcCh0I = srcI * 2;
                    dst[dstCh0I++] = src[srcCh0I++] * (float)short.MaxValue;
                    dst[dstCh0I] = src[srcCh0I] * (float)short.MaxValue;
                }
            }
            else
            {
                for (int i = 0; i < dstCount / channels; i++)
                {
                    var srcI = i * src.Length / dstCount;
                    var dstCh0I = i * channels;
                    var srcCh0I = srcI * channels;
                    for (int ch = 0; ch < channels; ch++)
                    {
                        dst[dstCh0I++] = src[srcCh0I++] * (float)short.MaxValue;
                    }
                }
            }
        }
        public static void ResampleAndConvert(float[] src, short[] dst, int dstCount, int channels)
        {
            if (channels == 1)
            {
                for (int i = 0; i < dstCount; i++)
                {
                    dst[i] = (short)(src[i * src.Length / dstCount] * (float)short.MaxValue);
                }
            }
            else if (channels == 2)
            {
                for (int i = 0; i < dstCount / 2; i++)
                {
                    var srcI = i * src.Length / dstCount;
                    var dstCh0I = i * 2;
                    var srcCh0I = srcI * 2;
                    dst[dstCh0I++] = (short)(src[srcCh0I++] * (float)short.MaxValue);
                    dst[dstCh0I] = (short)(src[srcCh0I] * (float)short.MaxValue);
                }
            }
            else
            {
                for (int i = 0; i < dstCount / channels; i++)
                {
                    var srcI = i * src.Length / dstCount;
                    var dstCh0I = i * channels;
                    var srcCh0I = srcI * channels;
                    for (int ch = 0; ch < channels; ch++)
                    {
                        dst[dstCh0I++] = (short)(src[srcCh0I++] * (float)short.MaxValue);
                    }
                }
            }
        }
        public static void Convert(float[] src, short[] dst, int dstCount)
        {
            for (int i = 0; i < dstCount; i++)
            {
                dst[i] = (short)(src[i] * (float)short.MaxValue);
            }
        }
        public static void Convert(short[] src, float[] dst, int dstCount)
        {
            for (int i = 0; i < dstCount; i++)
            {
                dst[i] = src[i] / (float)short.MaxValue;
            }
        }
        internal static string tostr<T>(T[] x, int lim = 10)
        {
            System.Text.StringBuilder b = new System.Text.StringBuilder();
            for (var i = 0; i < (x.Length < lim ? x.Length : lim); i++)
            {
                b.Append("-");
                b.Append(x[i]);
            }
            return b.ToString();
        }
        public class Resampler<T> : LocalVoiceFramed<T>.IProcessor
        {
            protected T[] frameResampled;
            int channels;
            public Resampler(int dstSize, int channels)
            {
                this.frameResampled = new T[dstSize];
                this.channels = channels;
            }
            public T[] Process(T[] buf)
            {
                AudioUtil.Resample(buf, this.frameResampled, this.frameResampled.Length, channels);
                return this.frameResampled;
            }
            public void Dispose()
            {
            }
        }
        public interface ILevelMeter
        {
            /// <summary>
            /// Average of last values in current 1/2 sec. buffer.
            /// </summary>
            float CurrentAvgAmp { get; }
            /// <summary>
            /// Max of last values in 1/2 sec. buffer as it was at last buffer wrap.
            /// </summary>
            float CurrentPeakAmp
            {
                get;
            }
            /// <summary>
            /// Average of CurrentPeakAmp's since last reset.
            /// </summary>
            float AccumAvgPeakAmp { get; }
            /// <summary>
            /// Reset LevelMeter.AccumAvgPeakAmp.
            /// </summary>
            void ResetAccumAvgPeakAmp();
        }
        public class LevelMetterDummy : ILevelMeter
        {
            public float CurrentAvgAmp { get { return 0; } }
            public float CurrentPeakAmp { get { return 0; } }
            public float AccumAvgPeakAmp { get { return 0; } }
            public void ResetAccumAvgPeakAmp() { }
        }
        /// <summary>
        /// Utility for measurement audio signal parameters.
        /// </summary>
        abstract public class LevelMeter<T> : LocalVoiceFramed<T>.IProcessor, ILevelMeter
        {
            // sum of all values in buffer
            protected float ampSum;
            // max of values from start buffer to current pos
            protected float ampPeak;
            protected int bufferSize;
            protected float[] buffer;
            protected int prevValuesPtr;
            protected float accumAvgPeakAmpSum;
            protected int accumAvgPeakAmpCount;
            internal LevelMeter(int samplingRate, int numChannels)
            {
                this.bufferSize = samplingRate * numChannels / 2; // 1/2 sec
                this.buffer = new float[this.bufferSize];
            }
            public float CurrentAvgAmp { get { return ampSum / this.bufferSize; } }
            public float CurrentPeakAmp
            {
                get;
                protected set;
            }
            public float AccumAvgPeakAmp { get { return this.accumAvgPeakAmpCount == 0 ? 0 : accumAvgPeakAmpSum / this.accumAvgPeakAmpCount; } }
            public void ResetAccumAvgPeakAmp() { this.accumAvgPeakAmpSum = 0; this.accumAvgPeakAmpCount = 0; ampPeak = 0; }
            public abstract T[] Process(T[] buf);
            public void Dispose()
            {
            }
        }
        public class LevelMeterFloat : LevelMeter<float>
        {
            public LevelMeterFloat(int samplingRate, int numChannels) : base(samplingRate, numChannels) { }
            public override float[] Process(float[] buf)
            {
                foreach (var v in buf)
                {
                    var a = v;
                    if (a < 0)
                    {
                        a = -a;
                    }
                    ampSum = ampSum + a - this.buffer[this.prevValuesPtr];
                    this.buffer[this.prevValuesPtr] = a;
                    if (ampPeak < a)
                    {
                        ampPeak = a;
                    }
                    if (this.prevValuesPtr == 0)
                    {
                        CurrentPeakAmp = ampPeak;
                        ampPeak = 0;
                        accumAvgPeakAmpSum += CurrentPeakAmp;
                        accumAvgPeakAmpCount++;
                    }
                    this.prevValuesPtr = (this.prevValuesPtr + 1) % this.bufferSize;
                }
                return buf;
            }
        }
        public class LevelMeterShort : LevelMeter<short>
        {
            public LevelMeterShort(int samplingRate, int numChannels) : base(samplingRate, numChannels) { }
            public override short[] Process(short[] buf)
            {
                foreach (var v in buf)
                {
                    var a = v;
                    if (a < 0)
                    {
                        a = (short)-a;
                    }
                    ampSum = ampSum + a - this.buffer[this.prevValuesPtr];
                    this.buffer[this.prevValuesPtr] = a;
                    if (ampPeak < a)
                    {
                        ampPeak = a;
                    }
                    if (this.prevValuesPtr == 0)
                    {
                        CurrentPeakAmp = ampPeak;
                        ampPeak = 0;
                        accumAvgPeakAmpSum += CurrentPeakAmp;
                        accumAvgPeakAmpCount++;
                    }
                    this.prevValuesPtr = (this.prevValuesPtr + 1) % this.bufferSize;
                }
                return buf;
            }
        }
        public interface IVoiceDetector
        {
            /// <summary>If true, voice detection enabled.</summary>
            bool On { get; set; }
            /// <summary>Voice detected as soon as signal level exceeds threshold.</summary>
            float Threshold { get; set; }
            /// <summary>If true, voice detected.</summary>
            bool Detected { get; }
            /// <summary>Last time when switched to detected state.</summary>
            DateTime DetectedTime { get; }
            /// <summary>Called when switched to detected state.</summary>
            event Action OnDetected;
            /// <summary>Keep detected state during this time after signal level dropped below threshold.</summary>
            int ActivityDelayMs { get; set; }
        }
        public class VoiceDetectorCalibration<T> : LocalVoiceFramed<T>.IProcessor
        {
            IVoiceDetector voiceDetector;
            ILevelMeter levelMeter;
            int valuesPerSec;
            public bool VoiceDetectorCalibrating { get { return voiceDetectorCalibrateCount > 0; } }
            protected int voiceDetectorCalibrateCount;
            public VoiceDetectorCalibration(IVoiceDetector voiceDetector, ILevelMeter levelMeter, int samplingRate, int channels)
            {
                this.valuesPerSec = samplingRate * channels;
                this.voiceDetector = voiceDetector;
                this.levelMeter = levelMeter;
            }
            public void VoiceDetectorCalibrate(int durationMs)
            {
                this.voiceDetectorCalibrateCount = valuesPerSec * durationMs / 1000;
                levelMeter.ResetAccumAvgPeakAmp();
            }
            public T[] Process(T[] buf)
            {
                if (this.voiceDetectorCalibrateCount != 0)
                {
                    this.voiceDetectorCalibrateCount -= buf.Length;
                    if (this.voiceDetectorCalibrateCount <= 0)
                    {
                        this.voiceDetectorCalibrateCount = 0;
                        this.voiceDetector.Threshold = levelMeter.AccumAvgPeakAmp * 2;
                    }
                }
                return buf;
            }
            public void Dispose()
            {
            }
        }
        public class VoiceDetectorDummy : IVoiceDetector
        {
            public bool On { get { return false; } set { } }
            public float Threshold { get { return 0; } set { } }
            public bool Detected { get { return false; } }
            public int ActivityDelayMs { get { return 0; } set { } }
            public DateTime DetectedTime { get; private set; }
            public event Action OnDetected;
        }
        /// <summary>
        /// Simple voice activity detector triggered by signal level.
        /// </summary>
        abstract public class VoiceDetector<T> : LocalVoiceFramed<T>.IProcessor, IVoiceDetector
        {
            public bool On { get; set; }
            public float Threshold { get; set; }
            bool detected;
            public bool Detected
            {
                get { return detected; }
                protected set
                {
                    if (detected != value)
                    {
                        detected = value; DetectedTime = DateTime.Now;
                        if (detected && OnDetected != null) OnDetected();
                    }
                }
            }
            public DateTime DetectedTime { get; private set; }
            public int ActivityDelayMs
            {
                get { return this.activityDelay; }
                set
                {
                    this.activityDelay = value;
                    this.activityDelayValuesCount = value * valuesCountPerSec / 1000;
                }
            }
            public event Action OnDetected;
            protected int activityDelay;
            protected int autoSilenceCounter = 0;
            protected int valuesCountPerSec;
            protected int activityDelayValuesCount;
            internal VoiceDetector(int samplingRate, int numChannels)
            {
                this.valuesCountPerSec = samplingRate * numChannels;
                this.ActivityDelayMs = 500;
                this.On = true;
            }
            public abstract T[] Process(T[] buf);
            public void Dispose()
            {
            }
        }
        public class VoiceDetectorFloat : VoiceDetector<float>
        {
            public VoiceDetectorFloat(int samplingRate, int numChannels) : base(samplingRate, numChannels)
            {
                this.Threshold = 0.01f;
            }
            public override float[] Process(float[] buffer)
            {
                if (this.On)
                {
                    foreach (var s in buffer)
                    {
                        if (s > this.Threshold)
                        {
                            this.Detected = true;
                            this.autoSilenceCounter = 0;
                        }
                        else
                        {
                            this.autoSilenceCounter++;
                        }
                    }
                    if (this.autoSilenceCounter > this.activityDelayValuesCount)
                    {
                        this.Detected = false;
                    }
                    return Detected ? buffer : null;
                }
                else
                {
                    return buffer;
                }
            }
        }
        public class VoiceDetectorShort : VoiceDetector<short>
        {
            internal VoiceDetectorShort(int samplingRate, int numChannels) : base(samplingRate, numChannels)
            {
                this.Threshold = 0.01f * short.MaxValue;
            }
            public override short[] Process(short[] buffer)
            {
                if (this.On)
                {
                    foreach (var s in buffer)
                    {
                        if (s > this.Threshold)
                        {
                            this.Detected = true;
                            this.autoSilenceCounter = 0;
                        }
                        else
                        {
                            this.autoSilenceCounter++;
                        }
                    }
                    if (this.autoSilenceCounter > this.activityDelayValuesCount)
                    {
                        this.Detected = false;
                    }
                    return Detected ? buffer : null;
                }
                else
                {
                    return buffer;
                }
            }
        }
        // encapsulates level meter, voice detector and voice detector calibrator in single instance
        public class VoiceLevelDetectCalibrate<T> : LocalVoiceFramed<T>.IProcessor
        {
            public ILevelMeter Level { get; private set; }
            public IVoiceDetector Detector { get; private set; }
            VoiceDetectorCalibration<T> c;
            public VoiceLevelDetectCalibrate(int samplingRate, int channels)
            {
                var x = new T[1];
                if (x[0] is float)
                {
                    Level = new LevelMeterFloat(samplingRate, channels);
                    Detector = new VoiceDetectorFloat(samplingRate, channels);
                }
                else if (x[0] is short)
                {
                    Level = new LevelMeterShort(samplingRate, channels);
                    Detector = new VoiceDetectorShort(samplingRate, channels);
                }
                else
                {
                    throw new Exception("VoiceLevelDetectCalibrate: type not supported: " + x[0].GetType());
                }
                c = new VoiceDetectorCalibration<T>(Detector, Level, samplingRate, channels);
            }
            public void Calibrate(int durationMs)
            {
                c.VoiceDetectorCalibrate(durationMs);
            }
            public T[] Process(T[] buf)
            {
                buf = (Level as LocalVoiceFramed<T>.IProcessor).Process(buf);
                buf = (c as LocalVoiceFramed<T>.IProcessor).Process(buf);
                buf = (Detector as LocalVoiceFramed<T>.IProcessor).Process(buf);
                return buf;
            }
            public void Dispose()
            {
                (Level as LocalVoiceFramed<T>.IProcessor).Dispose();
                (Detector as LocalVoiceFramed<T>.IProcessor).Dispose();
                c.Dispose();
            }
        }
        // Tone generator. Helpful for debug but does not compile for UWP because of System.Timers.Timer.
        /*
        public class ToneAudioPusher<T> : ExitGames.Client.Photon.Voice.IAudioPusher<T>
        {
            public ToneAudioPusher(int frequency = 440, int bufSize = 1000)
            {
                buf = new T[bufSize];
                k = 2 * Math.PI * 440 / SamplingRate;
            }
            double k;
            System.Timers.Timer timer;
            Action<T[]> callback;
            T[] buf;
            public void SetCallback(Action<T[]> callback, Voice.LocalVoice localVoice)
            {
                if (timer != null)
                {
                    Dispose();
                }
                this.callback = callback;
                timer = new System.Timers.Timer(1000 * buf.Length / SamplingRate);
                // Hook up the Elapsed event for the timer.
                timer.Elapsed += new System.Timers.ElapsedEventHandler(OnTimedEvent);
                timer.Enabled = true;
            }
            private void OnTimedEvent(object source, System.Timers.ElapsedEventArgs e)
            {                
                if (buf is float[])
                {
                    var b = buf as float[];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        b[i] = (float)(System.Math.Sin((cntShort + i) * k) / 2);
                    }
                }
                else if (buf is short[])
                {
                    var b = buf as short[];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        b[i] = (short)(System.Math.Sin((cntShort + i) * k) * short.MaxValue / 2);
                    }
                }
                cntFrame++;
                cntShort += buf.Length;
                this.callback(buf);
            }
            int cntFrame;
            int cntShort;
            public int Channels { get { return 1; } }
            public int SamplingRate { get { return 44100; } }
            public void Dispose()
            {
                timer.Close();
            }
        }
       */
    }
}
