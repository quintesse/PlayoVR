using System.Collections;
using POpusCodec.Enums;
using POpusCodec;
using System;
using System.Collections.Generic;

namespace ExitGames.Client.Photon.Voice
{
    public class OpusCodec
    {
        public enum FrameDuration
        {
            Frame2dot5ms = 2500,
            Frame5ms = 5000,
            Frame10ms = 10000,
            Frame20ms = 20000,
            Frame40ms = 40000,
            Frame60ms = 60000
        }

        public static class EncoderFactory
        {
            public static IEncoder Create(VoiceInfo i, LocalVoice localVoice)
            {
                if (localVoice.GetType() == typeof(LocalVoiceAudioFloat))
                    return new EncoderFloat(i);
                else if (localVoice.GetType() == typeof(LocalVoiceAudioShort))
                    return new EncoderShort(i);
                else
                    throw new UnsupportedCodecException(i.Codec, localVoice);
            }
        }
        abstract public class Encoder<T> : IEncoderDataFlowDirect<T>
        {        
            protected OpusEncoder encoder;
            protected bool disposed;
            protected Encoder(VoiceInfo i)
            {
                encoder = new OpusEncoder((SamplingRate)i.SamplingRate, (Channels)i.Channels, i.Bitrate, OpusApplicationType.Voip, (Delay)(i.FrameDurationUs * 2 / 1000));
            }

            public void Dispose()
            {
                lock (this)
                {                    
                    encoder.Dispose();
                    disposed = true;
                }
            }

            public abstract ArraySegment<byte> EncodeAndGetOutput(T[] buf);
        }

        public class EncoderFloat : Encoder<float>
        {
            private static readonly ArraySegment<byte> EmptyBuffer = new ArraySegment<byte>(new byte[] { });
            internal EncoderFloat(VoiceInfo i) : base(i) { }
            public override ArraySegment<byte> EncodeAndGetOutput(float[] buf)
            {
                lock (this)
                {
                    if (disposed) return EmptyBuffer;
                    else return encoder.Encode(buf);
                }
            }
        }
        public class EncoderShort : Encoder<short>
        {
            private static readonly ArraySegment<byte> EmptyBuffer = new ArraySegment<byte>(new byte[] { });
            internal EncoderShort(VoiceInfo i) : base(i) { }
            public override ArraySegment<byte> EncodeAndGetOutput(short[] buf)
            {
                lock (this)
                {
                    if (disposed) return EmptyBuffer;
                    else return encoder.Encode(buf);
                }
            }
        }

        public class Decoder : IDecoderDirect
        {
            OpusDecoder decoder;
            public void Open(VoiceInfo i)
            {
                decoder = new OpusDecoder((SamplingRate)i.SamplingRate, (Channels)i.Channels);
            }

            public byte[] DecodeToByte(byte[] buf)
            {
                throw new NotImplementedException();
            }

            public float[] DecodeToFloat(byte[] buf)
            {
                return decoder.DecodePacketFloat(buf);
            }

            public short[] DecodeToShort(byte[] buf)
            {
                return decoder.DecodePacketShort(buf);
            }

            public void Dispose()
            {
                decoder.Dispose();
            }
        }

        public class Util
        {
            internal static int bestEncoderSampleRate(int f)
            {
                int diff = int.MaxValue;
                int res = (int)SamplingRate.Sampling48000;
                foreach (var x in Enum.GetValues(typeof(SamplingRate)))
                {
                    var d = Math.Abs((int)x - f);
                    if (d < diff)
                    {
                        diff = d;
                        res = (int)x;
                    }
                }
                return res;
            }
        }
    }
}