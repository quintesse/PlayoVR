using System.Collections;
using System;
using System.Collections.Generic;
namespace ExitGames.Client.Photon.Voice
{
    public interface IAudioSource : IDisposable
    {
        int SamplingRate { get; }
        int Channels { get; }
    }
    
	public interface IAudioReader<T> : IDataReader<T>, IAudioSource
    {
    }
	public  interface IAudioPusher<T> : IAudioSource
	{
        // Assigns callback during voice initialization
        // localVoice may be optionally used by IAudioPusher<T>
        void SetCallback(Action<T[]> callback, LocalVoice localVoice);
	}
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
                this.encoder = VoiceCodec.CreateDefaultEncoder(voiceInfo, this);
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
            this.voiceDetectorCalibration = new AudioUtil.VoiceDetectorCalibration<T>(voiceDetector, levelMeter, this.info.SamplingRate, (int)this.channels);
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
}
