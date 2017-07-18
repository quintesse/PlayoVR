using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Voice = ExitGames.Client.Photon.Voice;

class DelayProcessor : UnityEngine.MonoBehaviour
{
    // Message sent by PhotonVoiceRecorder
    void VoiceCreated(PhotonVoiceRecorder.VoiceCreatedParams p)
    {
        ((Voice.LocalVoiceAudioFloat)p.Voice).AddPreProcessor(new Processor(40000, 0.3f));
    }

    /*
    // Building processing pipeline from the scratch.
    // Gives full control on processing order and resampler implementation.
    void VoiceCreated(PhotonVoiceRecorder.VoiceCreatedParams p)
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
        var voiceDetectorCalibration = new Voice.AudioUtil.VoiceDetectorCalibration<float>(voiceDetector, levelMeter, p.Info.SamplingRate * p.Info.Channels);
        p.Voice.AddPostProcessor(levelMeter, voiceDetectorCalibration, voiceDetector); // level meter and calibration should be processed even if no signal detected

        // Audio voice properties exposing built-in processor will not work after clear. Use refs to created processors.
        voiceDetector.Threshold = 0.05f;
        voiceDetector.On = true;
        
    }
    //*/
    class Processor : Voice.LocalVoiceAudioFloat.IProcessor
    {
        float factor;
        float[] prevBuf;
        int prevBufPosWrite;
        int prevBufPosRead;
        public Processor(int size, float factor)
        {
            prevBuf = new float[size];
            prevBufPosRead = size / 2;
            this.factor = factor;
        }
        public float[] Process(float[] buf)
        {

            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] += factor * prevBuf[prevBufPosRead++ % prevBuf.Length];
            }

            //var len = buf.Length;
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
}
