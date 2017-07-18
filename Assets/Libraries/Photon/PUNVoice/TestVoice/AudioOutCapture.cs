using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
class AudioOutCapture : MonoBehaviour
{
    double sampleRate;
    public event Action<float[], int> OnAudioFrame;
    public double SampleRate { get { return sampleRate; } }
    private void Start()
    {
        sampleRate = AudioSettings.outputSampleRate;
    }
    void OnAudioFilterRead(float[] data, int channels)
    {        
        if (OnAudioFrame != null)
        {
            OnAudioFrame(data, channels);
        }
    }
}
