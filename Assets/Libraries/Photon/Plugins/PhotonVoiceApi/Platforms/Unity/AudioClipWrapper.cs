using UnityEngine;
using System.Collections;

// Wraps UnityEngine.AudioClip with Voice.IAudioStream interface.
// Used for playing back audio clips via Photon Voice.
public class AudioClipWrapper : ExitGames.Client.Photon.Voice.IAudioReader<float>
{
    private AudioClip audioClip;
    private int readPos;
    private float startTime;    
    public bool Loop { get; set; }

    public AudioClipWrapper(AudioClip audioClip)
    {
        this.audioClip = audioClip;
        startTime = Time.time;
    }
    private bool playing = true;
    public bool Read(float[] buffer)
    {
        if (!playing)
        {
            return false;
        }

        var playerPos = (int)((Time.time - startTime) * audioClip.frequency);
        var bufferSamplesCount = buffer.Length / audioClip.channels;
        if (playerPos > readPos + bufferSamplesCount)
        {
            this.audioClip.GetData(buffer, readPos);
            readPos += bufferSamplesCount;
            
            if (readPos >= audioClip.samples)
            {
                if (this.Loop)
                {
                    readPos = 0;
                    startTime = Time.time;
                }
                else
                {
                    playing = false;
                }
            }

            return true;
        }
        else
        {
            return false;
        }
    }
    public int SamplingRate { get { return this.audioClip.frequency; } }
    public int Channels { get { return this.audioClip.channels; } }

    public void Dispose() 
    {

    }
}
