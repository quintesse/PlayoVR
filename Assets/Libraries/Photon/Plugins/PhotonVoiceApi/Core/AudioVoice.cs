using System.Collections;
using System;
using System.Collections.Generic;

namespace ExitGames.Client.Photon.Voice
{
    public interface ILocalVoiceAudio
    {        
        /// <summary>Use to enable or disable voice detector and set its parameters.</summary>
        AudioUtil.IVoiceDetector VoiceDetector { get; }

        /// <summary>
        /// Level meter utility.
        /// </summary>
        AudioUtil.ILevelMeter LevelMeter { get; }

        /// <summary>If true, voice detector calibration is in progress.</summary>
        bool VoiceDetectorCalibrating { get; }

        /// <summary>Trigger voice detector calibration process.
        /// While calibrating, keep silence. Voice detector sets threshold basing on measured backgroud noise level.
        /// </summary>
        /// <param name="durationMs">Duration of calibration in milliseconds.</param>
        void VoiceDetectorCalibrate(int durationMs);
    }

    public static class LocalVoiceAudio
    {
        public static LocalVoiceAudioDummy Dummy = new LocalVoiceAudioDummy();
        public static LocalVoiceAudio<T> Create<T>(VoiceClient voiceClient, byte voiceId, IEncoder encoder, VoiceInfo voiceInfo, int channelId)
        {
            if (typeof(T) == typeof(float))
            {
                if (encoder == null || encoder is IEncoderDataFlow<float>)
                {
                    return new LocalVoiceAudioFloat(voiceClient, encoder as IEncoderDataFlow<float>, voiceId, voiceInfo, channelId) as LocalVoiceAudio<T>;
                }
                else
                    throw new Exception("[PV] CreateLocalVoice: encoder for IFrameStream<float> is not IBufferEncoder<float>: " + encoder.GetType());
            }
            else if (typeof(T) == typeof(short))
            {
                if (encoder == null || encoder is IEncoderDataFlow<short>)
                    return new LocalVoiceAudioShort(voiceClient, encoder as IEncoderDataFlow<short>, voiceId, voiceInfo, channelId) as LocalVoiceAudio<T>;
                else
                    throw new Exception("[PV] CreateLocalVoice: encoder for IFrameStream<short> is not IBufferEncoder<short>: " + encoder.GetType());
            }
            else
            {
                throw new UnsupportedSampleTypeException(typeof(T));
            }
        }
    }

    abstract public class LocalVoiceAudio<T> : LocalVoiceFramed<T>, ILocalVoiceAudio
    {
        public virtual AudioUtil.IVoiceDetector VoiceDetector { get { return voiceDetector; } }
        protected AudioUtil.VoiceDetector<T> voiceDetector;
        protected AudioUtil.VoiceDetectorCalibration<T> voiceDetectorCalibration;
        public virtual AudioUtil.ILevelMeter LevelMeter { get { return levelMeter; } }
        protected AudioUtil.LevelMeter<T> levelMeter;

        /// <summary>Trigger voice detector calibration process.
        /// While calibrating, keep silence. Voice detector sets threshold basing on measured backgroud noise level.
        /// </summary>
        /// <param name="durationMs">Duration of calibration in milliseconds.</param>
        public void VoiceDetectorCalibrate(int durationMs)
        {
            voiceDetectorCalibration.VoiceDetectorCalibrate(durationMs);
        }

        public bool VoiceDetectorCalibrating { get { return voiceDetectorCalibration.VoiceDetectorCalibrating; } }

        protected int channels;
        protected int sourceSamplingRateHz;
        protected bool resampleSource;

        public override bool IsTransmitting
        {
            get { return this.Transmit && (!this.VoiceDetector.On || this.VoiceDetector.Detected); }
        }

        internal LocalVoiceAudio(VoiceClient voiceClient, IEncoderDataFlow<T> encoder, byte id, VoiceInfo voiceInfo, int channelId)
            : base(voiceClient, encoder, id, voiceInfo, channelId,
                  voiceInfo.SamplingRate != 0 ? voiceInfo.FrameSize * voiceInfo.SourceSamplingRate / voiceInfo.SamplingRate : voiceInfo.FrameSize
                  )
        {
            if (this.encoder == null)
            {
                this.encoder = voiceClient.CreateEncoder(voiceInfo, this);
            }
            this.channels = voiceInfo.Channels;
            this.sourceSamplingRateHz = voiceInfo.SourceSamplingRate;
            if (this.sourceSamplingRateHz != voiceInfo.SamplingRate)
            {
                this.resampleSource = true;
                this.voiceClient.frontend.LogWarning("[PV] Local voice #" + this.id + " audio source frequency " + this.sourceSamplingRateHz + " and encoder sampling rate " + voiceInfo.SamplingRate + " do not match. Resampling will occur before encoding.");                
            }
        }
        protected void initBuiltinProcessors()
        {
            if (this.resampleSource)
            {
                AddPostProcessor(new AudioUtil.Resampler<T>(this.info.FrameSize, channels));
            }
            this.voiceDetectorCalibration = new AudioUtil.VoiceDetectorCalibration<T>(voiceDetector, levelMeter, this.info.SamplingRate * (int)this.channels);
            AddPostProcessor(levelMeter, voiceDetectorCalibration, voiceDetector); // level meter and calibration should be processed even if no signal detected            
        }
    }
    public class LocalVoiceAudioDummy : LocalVoice, ILocalVoiceAudio
    {
        private AudioUtil.VoiceDetectorDummy voiceDetector;
        private AudioUtil.LevelMetterDummy levelMeter;

        public AudioUtil.IVoiceDetector VoiceDetector { get { return voiceDetector; } }
        public AudioUtil.ILevelMeter LevelMeter { get { return levelMeter; } }
        public bool VoiceDetectorCalibrating { get { return false; } }

        public void VoiceDetectorCalibrate(int durationMs) { }
        public LocalVoiceAudioDummy()
        {
            voiceDetector = new AudioUtil.VoiceDetectorDummy();
            levelMeter = new AudioUtil.LevelMetterDummy();
        }
    }

    public class LocalVoiceAudioFloat : LocalVoiceAudio<float>
    {
        internal LocalVoiceAudioFloat(VoiceClient voiceClient, IEncoderDataFlow<float> encoder, byte id, VoiceInfo voiceInfo, int channelId)
            : base(voiceClient, encoder, id, voiceInfo, channelId)
        {
            // these 2 processors go after resampler
            this.levelMeter = new AudioUtil.LevelMeterFloat(this.info.SamplingRate, this.info.Channels);
            this.voiceDetector = new AudioUtil.VoiceDetectorFloat(this.info.SamplingRate, this.info.Channels);            
            initBuiltinProcessors();
        }
    }

    public class LocalVoiceAudioShort : LocalVoiceAudio<short>
    {
        internal LocalVoiceAudioShort(VoiceClient voiceClient, IEncoderDataFlow<short> encoder, byte id, VoiceInfo voiceInfo, int channelId)
            : base(voiceClient, encoder, id, voiceInfo, channelId)
        {
            // these 2 processors go after resampler
            this.levelMeter = new AudioUtil.LevelMeterShort(this.info.SamplingRate, this.info.Channels); //1/2 sec
            this.voiceDetector = new AudioUtil.VoiceDetectorShort(this.info.SamplingRate, this.info.Channels);
            initBuiltinProcessors();
        }
    }

    /// <summary>
    /// Audio parameters and data conversion utilities.
    /// </summary>
    public static class AudioUtil
    {

        public static void Resample<T>(T[] src, T[] dst, int dstCount, int channels)
        {
            //TODO: Low-pass filter
            for (int i = 0; i < dstCount; i += channels)
            {
                var interp = (i * src.Length / dstCount);
                for (int ch = 0; ch < channels; ch++)
                {
                    dst[i + ch] = src[interp + ch];
                }
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

            public void ResetAccumAvgPeakAmp() { this.accumAvgPeakAmpSum = 0; this.accumAvgPeakAmpCount = 0; }

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

            /// <summary>Keep detected state during this time after signal level dropped below threshold.</summary>
            int ActivityDelayMs { get; set; }
        }

        public class VoiceDetectorCalibration<T> : LocalVoiceFramed<T>.IProcessor
        {
            IVoiceDetector voiceDetector;
            ILevelMeter levelMeter;
            int samplesPerSec;
            public bool VoiceDetectorCalibrating { get { return voiceDetectorCalibrateCount > 0; } }
            protected int voiceDetectorCalibrateCount;

            public VoiceDetectorCalibration(IVoiceDetector voiceDetector, ILevelMeter levelMeter, int samplesPerSec)
            {
                this.samplesPerSec = samplesPerSec;
                this.voiceDetector = voiceDetector;
                this.levelMeter = levelMeter;
            }
            public void VoiceDetectorCalibrate(int durationMs)
            {
                this.voiceDetectorCalibrateCount = samplesPerSec * durationMs / 1000;
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
        }
        /// <summary>
        /// Simple voice activity detector triggered by signal level.
        /// </summary>
        abstract public class VoiceDetector<T> : LocalVoiceFramed<T>.IProcessor, IVoiceDetector
        {
            public bool On { get; set; }
            public float Threshold { get; set; }
            public bool Detected { get; protected set; }
            public int ActivityDelayMs
            {
                get { return this.activityDelay; }
                set
                {
                    this.activityDelay = value;
                    this.activityDelayValuesCount = value * valuesCountPerSec / 1000;
                }
            }

            protected int activityDelay;
            protected int autoSilenceCounter = 0;
            protected int valuesCountPerSec;
            protected int activityDelayValuesCount;

            internal VoiceDetector(int samplingRate, int numChannels)
            {
                this.valuesCountPerSec = samplingRate * numChannels;
                this.Threshold = 0.01f;
                this.ActivityDelayMs = 500;
            }

            public abstract T[] Process(T[] buf);

            public void Dispose()
            {
            }
        }

        public class VoiceDetectorFloat : VoiceDetector<float>
        {
            public VoiceDetectorFloat(int samplingRate, int numChannels) : base(samplingRate, numChannels) { }
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
            internal VoiceDetectorShort(int samplingRate, int numChannels) : base(samplingRate, numChannels) { }
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
    }
}