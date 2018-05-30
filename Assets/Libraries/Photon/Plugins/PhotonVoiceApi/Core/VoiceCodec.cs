// -----------------------------------------------------------------------
// <copyright file="VoiceCodec.cs" company="Exit Games GmbH">
//   Photon Voice API Framework for Photon - Copyright (C) 2017 Exit Games GmbH
// </copyright>
// <summary>
//   Photon data streaming support.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------
//#define PHOTON_VOICE_VIDEO_ENABLE
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace ExitGames.Client.Photon.Voice
{   
    public interface IEncoder : IDisposable
    {
    }
    public interface IEncoderDataFlow<T> : IEncoder
    {
    }
    // Returns compressed data instantly
    public interface IEncoderDataFlowDirect<T> : IEncoderDataFlow<T>
    {
        ArraySegment<byte> EncodeAndGetOutput(T[] buf);
    }
    // Returns compressed image instantly
    public interface IEncoderNativeImageDirect : IEncoder
    {
        IEnumerable<ArraySegment<byte>> EncodeAndGetOutput(IntPtr[] buf, int width, int height, int[] stride, ImageFormat imageFormat, Rotation rotation, Flip flip);
    }
    // Returns compressed data in the call independent from Encode or does not need Encode call at all (produces output on its own)
    public interface IEncoderQueued : IEncoder
    {
        IEnumerable<ArraySegment<byte>> GetOutput();
    }
    public interface IDecoder : IDisposable
    {
        void Open(VoiceInfo info);
    }
    // Returns decoded data instantly
    public interface IDecoderDirect : IDecoder
    {
        byte[] DecodeToByte(byte[] buf);
        float[] DecodeToFloat(byte[] buf);
        short[] DecodeToShort(byte[] buf);
    }
    // Returns output in separate method or callback or does not produce output at all
    public interface IDecoderQueued : IDecoder
    {
        // Called also for every missing frame with buf = null
        void Decode(byte[] buf);
    }
    public delegate void OnImageOutputNative(IntPtr buf, int width, int height, int stride);
    public interface IDecoderQueuedOutputImageNative : IDecoderQueued
    {
        ImageFormat OutputImageFormat { get; set; }
        Flip OutputImageFlip { get; set; }
        // if provided, decoder writes output to it 
        Func<int, int, IntPtr> OutputImageBufferGetter { get; set; }
        OnImageOutputNative OnOutputImage { get; set; }
    }
    class UnsupportedSampleTypeException : Exception
    {
        public UnsupportedSampleTypeException(Type t) : base("[PV] unsupported sample type: " + t) { }
    }
    class UnsupportedCodecException : Exception
    {
        public UnsupportedCodecException(Codec codec, LocalVoice voice) : base("[PV] unsupported codec: " + codec + " at voice " + voice.GetType()) { }
    }
    public enum Codec // Transmitted in voice info. Do not change values.
    {
        AudioOpus = 11
#if PHOTON_VOICE_VIDEO_ENABLE
        , VideoVP8 = 21
#endif
    }
    public enum ImageFormat
    {
        I420, // native vpx (no format conversion before encodong)                        
        YV12, // native vpx (no format conversion before encodong)
        Android420,
        RGBA,
        ABGR,
        BGRA,
        ARGB,
    }
    public enum Rotation
    {
        Rotate0 = 0,      // No rotation.
        Rotate90 = 90,    // Rotate 90 degrees clockwise.
        Rotate180 = 180,  // Rotate 180 degrees.
        Rotate270 = 270,  // Rotate 270 degrees clockwise.
    }
    public enum Flip
    {
        None,
        Vertical,
        Horizontal
    }
    public class ImageBufferInfo
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int[] Stride { get; private set; }
        public ImageFormat Format { get; private set; }
        public Rotation Rotation { get; set; }
        public Flip Flip { get; set; }
        public ImageBufferInfo(int width, int height, int[] stride, ImageFormat format)
        {
            Width = width;
            Height = height;
            Stride = stride;
            Format = format;
        }
    }
    public class ImageBufferNative
    {
        public ImageBufferNative(ImageBufferInfo info)
        {
            Info = info;
        }
        public ImageBufferInfo Info { get; protected set; }
        public IntPtr[] Planes { get; protected set; }
        // Release resources for dispose or reuse.
        public virtual void Release() { }
        public virtual void Dispose() { }
    }
    // Allocates native buffers for planes
    // Supports releasing to image pool with allocation reuse
    public class ImageBufferNativeAlloc : ImageBufferNative, IDisposable
    {
        ImageBufferNativePool<ImageBufferNativeAlloc> pool;
        public ImageBufferNativeAlloc(ImageBufferNativePool<ImageBufferNativeAlloc> pool, ImageBufferInfo info) : base(info)
        {
            this.pool = pool;
            Planes = new IntPtr[info.Stride.Length];
            for (int i = 0; i < info.Stride.Length; i++)
            {
                Planes[i] = System.Runtime.InteropServices.Marshal.AllocHGlobal(info.Stride[i] * info.Height);
            }
        }
        public override void Release()
        {
            if (pool != null)
            {
                pool.Release(this);
            }
        }
        public override void Dispose()
        {
            for (int i = 0; i < Info.Stride.Length; i++)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(Planes[i]);
            }
        }
    }
    // Acquires byte[] plane via GHandle. Optimized for single plane images.
    // Supports releasing to image pool after freeing GHandle (object itself reused only)
    public class ImageBufferNativeGCHandleSinglePlane : ImageBufferNative, IDisposable
    {
        ImageBufferNativePool<ImageBufferNativeGCHandleSinglePlane> pool;
        GCHandle planeHandle;
        public ImageBufferNativeGCHandleSinglePlane(ImageBufferNativePool<ImageBufferNativeGCHandleSinglePlane> pool, ImageBufferInfo info) : base(info)
        {
            if (info.Stride.Length != 1)
            {
                throw new Exception("ImageBufferNativeGCHandleSinglePlane wrong plane count " + info.Stride.Length);
            }
            this.pool = pool;
            Planes = new IntPtr[1];
        }
        public void PinPlane(byte[] plane)
        {
            planeHandle = GCHandle.Alloc(plane, GCHandleType.Pinned);
            Planes[0] = planeHandle.AddrOfPinnedObject();
        }
        public override void Release()
        {
            planeHandle.Free();
            if (pool != null)
            {
                pool.Release(this);
            }
        }
        public override void Dispose()
        {
        }
    }
    internal static class VoiceCodec
    {
        internal static IEncoder CreateDefaultEncoder(VoiceInfo info, LocalVoice localVoice)
        {
            switch (info.Codec)
            {
                case Codec.AudioOpus:
                    return OpusCodec.EncoderFactory.Create(info, localVoice);
#if PHOTON_VOICE_VIDEO_ENABLE
                case Codec.VideoVP8:
                    return new VPxCodec.Encoder(info);
#endif
                default:
                    throw new UnsupportedCodecException(info.Codec, localVoice);
            }
        }
        internal static IDecoder CreateDefaultDecoder(int channelId, int playerId, byte voiceId, VoiceInfo info)
        {
            switch (info.Codec)
            {
                case Codec.AudioOpus:
                    return new OpusCodec.Decoder();
#if PHOTON_VOICE_VIDEO_ENABLE
                case Codec.VideoVP8:
                    return new VPxCodec.Decoder();
#endif
                default:
                    return null;
            }
        }
    }
}
