using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Voice = ExitGames.Client.Photon.Voice;

class PhotonVoiceRecorderShort : PhotonVoiceRecorder
{
    protected override Voice.LocalVoice createLocalVoice(Voice.VoiceInfo voiceInfo, Voice.IBufferReader<float> reader)
    {
        return PhotonVoiceNetwork.CreateLocalVoiceShort(voiceInfo, reader);
    }
    // Reads float buffers. Converts them to short and pushes to LocalVoiceAudioShort
    class BufferReaderPushAdapterAsyncPoolFloatToShort : Voice.BufferReaderPushAdapterBase<float>
    {
        float[] buffer;
        public BufferReaderPushAdapterAsyncPoolFloatToShort(Voice.LocalVoice localVoice, Voice.IBufferReader<float> reader) : base(reader)
        {
            buffer = new float[((Voice.LocalVoiceFramed<short>)localVoice).FrameSize];
        }

        public override void Service(Voice.LocalVoice localVoice)
        {
            var v = ((Voice.LocalVoiceFramed<short>)localVoice);
            short[] buf = v.PushDataBufferPool.AcquireOrCreate();
            while (this.reader.Read(buffer))
            {
                for (int i = 0; i < buf.Length; i++)
                {
                    buf[i] = (short)(buffer[i] * (float)short.MaxValue);
                }
                v.PushDataAsync(buf);
                buf = v.PushDataBufferPool.AcquireOrCreate();
            }
            // release unused buffer
            v.PushDataBufferPool.Release(buf, buf.Length);
        }
    }

}
