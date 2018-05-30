using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Voice = ExitGames.Client.Photon.Voice;

class DelayProcessor : UnityEngine.MonoBehaviour
{
    // Message sent by PhotonVoiceRecorder
    void PhotonVoiceCreated(PhotonVoiceRecorder.PhotonVoiceCreatedParams p)
    {
		if (p.Voice is Voice.LocalVoiceAudioFloat) 
		{
    	    ((Voice.LocalVoiceAudioFloat)p.Voice).AddPreProcessor(new ProcessorFloat(p.AudioSource.SamplingRate / 2, 0.3f));
            UnityEngine.Debug.Log("DelayProcessor: ProcessorFloat added to local voice pipeline");
        }
        else if (p.Voice is Voice.LocalVoiceAudioShort)
        {
            ((Voice.LocalVoiceAudioShort)p.Voice).AddPreProcessor(new ProcessorShort(p.AudioSource.SamplingRate / 2, 0.3f));
            UnityEngine.Debug.Log("DelayProcessor: ProcessorShort added to local voice pipeline");
        }
        else
        {
            UnityEngine.Debug.LogError("DelayProcessor: Only float and short voices are supported. Trying to add processor to " + p.Voice.GetType());
        }
    }

    /*
    // Building processing pipeline from the scratch.
    // Gives full control on processing order and resampler implementation.
    void PhotonVoiceCreated(PhotonVoiceRecorder.VoiceCreatedParams p)
    {        
        p.Voice.ClearProcessors();

        // Custom processor
        p.Voice.AddPostProcessor(new Processor(40000, 0.3f));

        // Resampling is mandatory if frequencies differ.
        if (p.Info.SamplingRate != p.Info.SourceSamplingRate)
        {
            p.Voice.AddPostProcessor(new Voice.AudioUtil.Resampler<float>(p.Info.FrameSize, p.Info.Channels));
        }

        // Optional. If added before resampling, use SourceSamplingRate instead of SamplingRate
        var levelMeter = new Voice.AudioUtil.LevelMeterFloat(p.Info.SamplingRate, p.Info.Channels);
        var voiceDetector = new Voice.AudioUtil.VoiceDetectorFloat(p.Info.SamplingRate, p.Info.Channels);        
        var voiceDetectorCalibration = new Voice.AudioUtil.VoiceDetectorCalibration<float>(voiceDetector, levelMeter, p.Info.SamplingRate, p.Info.Channels);
        p.Voice.AddPostProcessor(levelMeter, voiceDetectorCalibration, voiceDetector); // level meter and calibration should be processed even if no signal detected

        // Audio voice properties exposing built-in processor will not work after clear. Use refs to created processors.
        voiceDetector.Threshold = 0.05f;
        voiceDetector.On = true;
        
    }
    //*/
    abstract class Processor<T> : Voice.LocalVoiceAudio<T>.IProcessor
    {
        float factor;
        T[] prevBuf;
        int prevBufPosWrite;
        int prevBufPosRead;
        public Processor(int delaySamples, float factor)
        {
            prevBuf = new T[delaySamples * 2];
            prevBufPosRead = delaySamples;
            this.factor = factor;
        }
        abstract protected void mix(float factor, T[] buf, T[] prevBuf, ref int prevBufPosRead);
        public T[] Process(T[] buf)
        {
            mix(factor, buf, prevBuf, ref prevBufPosRead);            

            if (buf.Length > prevBuf.Length - prevBufPosWrite)
            {
                Array.Copy(buf, 0, prevBuf, prevBufPosWrite, prevBuf.Length - prevBufPosWrite);
                var newPos = buf.Length - (prevBuf.Length - prevBufPosWrite);
                Array.Copy(buf, prevBuf.Length - prevBufPosWrite, prevBuf, 0, newPos);
                prevBufPosWrite = newPos;
            }
            else
            {
                Array.Copy(buf, 0, prevBuf, prevBufPosWrite, buf.Length);
                prevBufPosWrite += buf.Length;
            }

            return buf;
        }

        public void Dispose()
        {
        }
    }

    class ProcessorFloat : Processor<float>
    {
        public ProcessorFloat(int delaySamples, float factor) : base(delaySamples, factor)
        {            
        }

        protected override void mix(float factor, float[] buf, float[] prevBuf, ref int prevBufPosRead)        
        {
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] += factor * prevBuf[prevBufPosRead++ % prevBuf.Length];
            }
        }
    }

    class ProcessorShort : Processor<short>
    {
        public ProcessorShort(int delaySamples, float factor) : base(delaySamples, factor)
        {
        }
        protected override void mix(float factor, short[] buf, short[] prevBuf, ref int prevBufPosRead)
        {
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] += (short)(buf[i] + factor * prevBuf[prevBufPosRead++ % prevBuf.Length]);
            }
        }
    }
}
