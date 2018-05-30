using UnityEngine;
using System.Collections;
using System;

// IAudioReader implementation sample. Provides constant tone signal.
// See also MicWrapper and AudioClipWrapper
// Because of current resamplig algorithm, the tone is distorted if SamplingRate not equals encoder sampling rate.
class ToneAudioReader : ExitGames.Client.Photon.Voice.IAudioReader<float>
{
    public ToneAudioReader()
    {
        k = 2 * Math.PI * 440 / SamplingRate;
    }
    public int Channels { get { return 2; } }

    public int SamplingRate { get { return 24000; } }

    public void Dispose()
    {
    }
    double k;
    long timeSamples;
    public bool Read(float[] buf)
    {
        var bufSamples = buf.Length / Channels;
        var t = (long)(AudioSettings.dspTime * SamplingRate);

        var deltaTimeSamples = t - timeSamples;
        if (Math.Abs(deltaTimeSamples) > SamplingRate / 4) // when started or Read has not been called for a while
        {
            Debug.LogWarningFormat("ToneAudioReader sample time is out: {0} / {1}", timeSamples, t);
            deltaTimeSamples = bufSamples;
            timeSamples = t - bufSamples;
        }

        if (deltaTimeSamples < bufSamples)
        {
            return false;
        }
        else
        {
            int x = 0;
            for (int i = 0; i < bufSamples; i++)
            {
                var v = (float)System.Math.Sin(timeSamples++ * k) * 0.2f;
                for (int j = 0; j < Channels; j++)
                    buf[x++] = v;
            }
            return true;
        }
    }
}

