// -----------------------------------------------------------------------
// <copyright file="Voice.cs" company="Exit Games GmbH">
//   Photon Voice API Framework for Photon - Copyright (C) 2015 Exit Games GmbH
// </copyright>
// <summary>
//   Photon data streaming support.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

//#define PHOTON_VOICE_VIDEO_ENABLE

using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
#if NETFX_CORE
using Windows.System.Threading;
#endif

namespace ExitGames.Client.Photon.Voice
{
    /// <summary>
    /// Interface used by Frame Stream LocalVoice's to get source data.
    /// Implement it in class wrapping platform-specific data source.
    /// Only byte, float and short types supported.
    /// </summary>
    public interface IBufferReader<T> : IDisposable
    {
        /// <summary>
        /// Fill full given frame buffer with source unconmressed data or return false if not enough such data.
        /// </summary>
        bool Read(T[] buffer);
    }

    /// <summary>
    /// Used to make LocalVoice call user's code in its Service()
    /// </summary>
    public interface IServiceable : IDisposable
    {
        void Service(LocalVoice localVoice);
    }

    // Adapts IBufferReader.Read to LocalVoice.PushData
    // should be used on typed LocalVoice of same T type
    public abstract class BufferReaderPushAdapterBase<T> : IServiceable
    {
        protected IBufferReader<T> reader;
        public abstract void Service(LocalVoice localVoice);
        public BufferReaderPushAdapterBase(IBufferReader<T> reader)
        {
            this.reader = reader;
        }
        public void Dispose()
        {
            this.reader.Dispose();
        }
    }
    public class BufferReaderPushAdapter<T> : BufferReaderPushAdapterBase<T>
    {
        protected T[] buffer;
        public BufferReaderPushAdapter(LocalVoice localVoice, IBufferReader<T> reader) : base(reader)
        {
            // any buffer will work but only of localVoice.SourceFrameSize avoids additional processing
            buffer = new T[((LocalVoiceFramed<T>)localVoice).FrameSize];
        }

        public override void Service(LocalVoice localVoice)
        {
            while (this.reader.Read(this.buffer))
            {
                ((LocalVoiceFramed<T>)localVoice).PushData(this.buffer);
            }
        }
    }

    // Acquires buffer from pool before each Read, releases buffer after last Read (Acquire/Release overhead)
    public class BufferReaderPushAdapterAsyncPool<T> : BufferReaderPushAdapterBase<T>
    {
        public BufferReaderPushAdapterAsyncPool(LocalVoice localVoice, IBufferReader<T> reader) : base(reader) { }

        public override void Service(LocalVoice localVoice)
        {
            var v = ((LocalVoiceFramed<T>)localVoice);
            T[] buf = v.PushDataBufferPool.AcquireOrCreate();
            while (this.reader.Read(buf))
            {
                v.PushDataAsync(buf);
                buf = v.PushDataBufferPool.AcquireOrCreate();
            }
            // release unused buffer
            v.PushDataBufferPool.Release(buf, buf.Length);
        }
    }

    // Reads data to preallocated buffer, copies it to buffer from pool before pushing (copy overhead)
    public class BufferReaderPushAdapterAsyncPoolCopy<T> : BufferReaderPushAdapterBase<T>
    {
        protected T[] buffer;
        public BufferReaderPushAdapterAsyncPoolCopy(LocalVoice localVoice, IBufferReader<T> reader) : base(reader)
        {
            buffer = new T[((LocalVoiceFramed<T>)localVoice).FrameSize];
        }

        public override void Service(LocalVoice localVoice)
        {
            while (this.reader.Read(buffer))
            {
                var v = ((LocalVoiceFramed<T>)localVoice);
                var buf = v.PushDataBufferPool.AcquireOrCreate();
                Array.Copy(buffer, buf, buffer.Length);
                v.PushDataAsync(buf);
            }
        }
    }

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
    /// <summary>Describes stream properties.</summary>
    public struct VoiceInfo
    {
        /// <summary>
        /// Helper for Opus stream info creation.
        /// </summary>
        /// <param name="samplingRate">Audio sampling rate.</param>
        /// <param name="sourceSamplingRate">Source audio sampling rate (to be resampled to SamplingRate).</param>
        /// <param name="channels">Number of channels.</param>
        /// <param name="frameDurationUs">Uncompressed frame (audio packet) size in microseconds.</param>
        /// <param name="bitrate">Stream bitrate.</param>
        /// <param name="userdata">Optional user data. Should be serializable by Photon.</param>
        /// <returns>VoiceInfo instance.</returns>
        static public VoiceInfo CreateAudioOpus(POpusCodec.Enums.SamplingRate samplingRate, int sourceSamplingRate, int channels, OpusCodec.FrameDuration frameDurationUs, int bitrate, object userdata = null)
        {
            return new VoiceInfo()
            {
                Codec = Codec.AudioOpus,
                SamplingRate = (int)samplingRate,
                SourceSamplingRate = sourceSamplingRate,
                Channels = channels,
                FrameDurationUs = (int)frameDurationUs,
                Bitrate = bitrate,
                UserData = userdata
            };
        }
#if PHOTON_VOICE_VIDEO_ENABLE
        /// <summary>
        /// Helper for VP8 stream info creation.
        /// </summary>
        /// <param name="bitrate">Stream bitrate.</param>
        /// <param name="width">Streamed video width. If 0, width and height of video source used (no rescaling).</param>
        /// <param name="heigth">Streamed video height. If -1, aspect ratio preserved during rescaling.</param>
        /// <param name="userdata">Optional user data. Should be serializable by Photon.</param>        
        /// <returns>VoiceInfo instance.</returns>
        static public VoiceInfo CreateVideoVP8(int bitrate, int width = 0, int heigth = -1, object userdata = null)
        {
            return new VoiceInfo()
            {
                Codec = Codec.VideoVP8,
                Bitrate = bitrate,
                Width = width,
                Height = heigth,
                UserData = userdata,
            };
        }
#endif
        public override string ToString()
        {
            return "c=" + Codec + " f=" + SamplingRate + " ch=" + Channels + " d=" + FrameDurationUs + " s=" + FrameSize + " b=" + Bitrate + " w=" + Width + " h=" + Height + " ud=" + UserData;
        }

        internal static VoiceInfo CreateFromEventPayload(Dictionary<byte, object> h)
        {
            var i = new VoiceInfo();
            i.SamplingRate = (int)h[(byte)EventParam.SamplingRate];
            i.Channels = (int)h[(byte)EventParam.Channels];
            i.FrameDurationUs = (int)h[(byte)EventParam.FrameDurationUs];
            i.Bitrate = (int)h[(byte)EventParam.Bitrate];
            i.UserData = h[(byte)EventParam.UserData];
            i.Codec = (Codec)h[(byte)EventParam.Codec];

            return i;
        }
        public Codec Codec { get; set; }
        /// <summary>Audio sampling rate (frequency).</summary>
        public int SamplingRate { get; set; }
        /// <summary>Source audio sampling rate (to be resampled to SamplingRate).</summary>
        public int SourceSamplingRate { get; set; }
        /// <summary>Number of channels.</summary>
        public int Channels { get; set; }
        /// <summary>Uncompressed frame (audio packet) size in microseconds.</summary>
        public int FrameDurationUs { get; set; }
        /// <summary>Compression quality in terms of bits per second.</summary>
        public int Bitrate { get; set; }
        /// <summary>Optional user data. Should be serializable by Photon.</summary>
        public object UserData { get; set; }

        /// <summary>Uncompressed frame (data packet) size in samples.</summary>
        public int FrameDurationSamples { get { return (int)(this.SamplingRate * (long)this.FrameDurationUs / 1000000); } }
        /// <summary>Uncompressed frame (data packet) size in samples.</summary>
        public int FrameSize { get { return this.FrameDurationSamples * this.Channels; } }
        /// <summary>Video width (optional).</summary>
        public int Width { get; set; }
        /// <summary>Video height (optional)</summary>
        public int Height { get; set; }
    }

    /// <summary>Helper to provide remote voices infos via Client.RemoteVoiceInfos iterator.</summary>
    public class RemoteVoiceInfo
    {
        internal RemoteVoiceInfo(int channelId, int playerId, byte voiceId, VoiceInfo info, object localUserObject)
        {
            this.ChannelId = channelId;
            this.PlayerId = playerId;
            this.VoiceId = voiceId;
            this.Info = info;
            this.LocalUserObject = localUserObject;
        }
        /// <summary>Remote voice info.</summary>
        public VoiceInfo Info { get; private set; }
        /// <summary>Id of channel used for transmission.</summary>
        public int ChannelId { get; private set; }
        /// <summary>Player Id of voice owner.</summary>
        public int PlayerId { get; private set; }
        /// <summary>Voice id unique in the room.</summary>
        public byte VoiceId { get; private set; }
        /// <summary>Object set by user when remote voice created.</summary>
        public object LocalUserObject { get; private set; }
    }

    enum EventSubcode : byte
    {
        VoiceInfo = 1,
        VoiceRemove = 2,
        Frame = 3,
    }

    enum EventParam : byte
    {
        VoiceId = 1,
        SamplingRate = 2,
        Channels = 3,
        FrameDurationUs = 4,
        Bitrate = 5,
        UserData = 10,
        EventNumber = 11,
        Codec = 12,
    }

    /// <summary>
    /// Represents outgoing data stream. Compresses data provided via IAudioStream and broadcasts it to all players in the room.
    /// </summary>
    public class LocalVoice : IDisposable
    {
        public const int DATA_POOL_CAPACITY = 50; // TODO: may depend on data type and properties, set for average audio stream
        /// <summary>If AudioGroup != 0, voice's data is sent only to clients listening to this group.</summary>
        /// <see cref="LoadBalancingFrontend.ChangeAudioGroups(byte[], byte[])"/>
        public byte Group { get; set; }

        /// <summary>If true, stream data broadcasted.</summary>
        public bool Transmit { get; set; }

        /// <summary>Returns true if stream broadcasts.</summary>
        public virtual bool IsTransmitting { get { return this.Transmit; } }
        /// <summary>Sent frames counter.</summary>
        public int FramesSent { get; private set; }

        /// <summary>Sent frames bytes counter.</summary>
        public int FramesSentBytes { get; private set; }

        /// <summary>Send data reliable.</summary>
        public bool Reliable { get; set; }

        /// <summary>Optional user object attached to LocalVoice.</summary>
        public object LocalUserObject { get; set; }

        /// <summary>Optional user object attached to LocalVoice. its Service() will be called at each VoiceClient.Service() call and Dispose() on LocalVoice removal.</summary>
        public IServiceable LocalUserServiceable { get; set; }

        /// <summary>
        /// If true, outgoing stream routed back to client via server same way as for remote client's streams.
        /// Can be swithed any time. OnRemoteVoiceInfoAction and OnRemoteVoiceRemoveAction are triggered if required.
        /// This functionality availability depends on frontend.
        /// </summary>
        public bool DebugEchoMode
        {
            get { return debugEchoMode; }
            set
            {
                if (debugEchoMode != value)
                {
                    debugEchoMode = value;
                    voiceClient.frontend.SetDebugEchoMode(this);
                }
            }
        }
        bool debugEchoMode;

#region nonpublic

        internal VoiceInfo info;
        protected IEncoder encoder;
        internal byte id;
        internal int channelId;
        internal byte evNumber = 0; // sequence used by receivers to detect loss. will overflow.
        protected VoiceClient voiceClient;

        volatile protected bool disposed;

        internal LocalVoice() // for dummy voices
        {
        }

        internal LocalVoice(VoiceClient voiceClient, IEncoder encoder, byte id, VoiceInfo voiceInfo, int channelId)
        {
            this.encoder = encoder;
            this.Transmit = true;
            this.info = voiceInfo;
            this.channelId = channelId;
            this.voiceClient = voiceClient;
            this.id = id;
        }

        internal string Name { get { return "Local v#" + id + " ch#" + voiceClient.channelStr(channelId); } }
        internal string LogPrefix { get { return "[PV] " + Name; } }

        internal virtual void service()
        {
            if (this.voiceClient.frontend.IsChannelJoined(this.channelId) && this.Transmit)
            {
                if (encoder is IEncoderQueued)
                {
                    foreach (var x in ((IEncoderQueued)encoder).GetOutput())
                    {
                        sendFrame(x);
                    }
                }
            }
            if (LocalUserServiceable != null)
            {
                LocalUserServiceable.Service(this);
            }
        }

        internal void sendFrame(ArraySegment<byte> compressed)
        {
            this.FramesSent++;
            this.FramesSentBytes += compressed.Count;

            object compressedObj = compressed;

            if (!this.voiceClient.frontend.SupportsArraySegmentSerialization)
            {
                // convert to byte[] for hosts not supporting ArraySegment
                var compressedBytes = new byte[compressed.Count];
                Buffer.BlockCopy(compressed.Array, compressed.Offset, compressedBytes, 0, compressed.Count);
                compressedObj = compressedBytes;
            }
            object[] content = new object[] { this.id, evNumber, compressedObj };
            this.voiceClient.frontend.SendFrame(content, this.channelId, this);
            this.eventTimestamps[evNumber] = Environment.TickCount;
            evNumber++;
        }

        internal Dictionary<byte, int> eventTimestamps = new Dictionary<byte, int>();
#endregion

        public void RemoveSelf()
        {
            this.voiceClient.RemoveLocalVoice(this);
        }

        public virtual void Dispose()
        {
            if (!disposed)
            {
                if (LocalUserServiceable != null)
                {
                    LocalUserServiceable.Dispose();
                }

                if (this.encoder != null)
                {
                    this.encoder.Dispose();
                }
                disposed = true;
            }
        }
    }

#if PHOTON_VOICE_VIDEO_ENABLE
    public class LocalVoiceVideo : LocalVoice
    {
        internal LocalVoiceVideo(VoiceClient voiceClient, IEncoder encoder, byte id, VoiceInfo voiceInfo, int channelId) : base(voiceClient, encoder, id, voiceInfo, channelId)
        {
            if (this.encoder == null)
            {
                this.encoder = voiceClient.CreateEncoder(voiceInfo, this);
            }
        }

        bool imageEncodeThreadStarted;
        Queue<ImageBufferNative> pushImageQueue = new Queue<ImageBufferNative>();
        AutoResetEvent pushImageQueueReady = new AutoResetEvent(false);
        public int PushImageQueueCount { get { return pushImageQueue.Count; } }
        public void PushImageAsync(ImageBufferNative buf)
        {
            if (disposed) return;

            if (!imageEncodeThreadStarted)
            {
                voiceClient.frontend.LogInfo(LogPrefix + ": Starting image encode thread");
#if NETFX_CORE
                ThreadPool.RunAsync((x) =>
                {
                    PushImageAsyncThread();
                });
#else
                var t = new Thread(PushImageAsyncThread);
                t.Name = LogPrefix + " image encode";
                t.Start();
#endif
                imageEncodeThreadStarted = true;
            }

            lock (pushImageQueue)
            {
                pushImageQueue.Enqueue(buf);
            }
            pushImageQueueReady.Set();
        }

        private void PushImageAsyncThread()
        {
            try
            {
                while (true)
                {
                    pushImageQueueReady.WaitOne(); // Wait until data is pushed to the queue or Dispose signals.
                    if (disposed) break;

                    while (true) // Dequeue and process while the queue is not empty.
                    {
                        ImageBufferNative b = null;
                        lock (pushImageQueue)
                        {
                            if (pushImageQueue.Count > 0)
                            {
                                b = pushImageQueue.Dequeue();
                            }
                        }

                        if (b != null)
                        {
                            PushImage(b.Planes, b.Info.Width, b.Info.Height, b.Info.Stride, b.Info.Format, b.Info.Rotation, b.Info.Flip);
                            b.Release();
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                voiceClient.frontend.LogError(LogPrefix + ": Exception in encode thread: " + e);
                throw e;
            }
            finally
            {
                lock (pushImageQueue)
                {
                    while (pushImageQueue.Count > 0)
                    {
                        pushImageQueue.Dequeue().Dispose();
                    }
                }

#if NETFX_CORE
                pushImageQueueReady.Dispose();                
#else
                pushImageQueueReady.Close();
#endif

                voiceClient.frontend.LogInfo(LogPrefix + ": Exiting image encode thread");
            }
        }


        public void PushImage(IntPtr[] buf, int width, int height, int[] stride, ImageFormat imageFormat, Rotation rotation = Rotation.Rotate0, Flip flip = Flip.None)
        {
            if (this.voiceClient.frontend.IsChannelJoined(this.channelId) && this.Transmit)
            {
                if (this.encoder is IEncoderNativeImageDirect)
                {
                    foreach (var compressed in ((IEncoderNativeImageDirect)this.encoder).EncodeAndGetOutput(buf, width, height, stride, imageFormat, rotation, flip))
                    {
                        if (compressed.Count != 0)
                        {
                            sendFrame(compressed);
                        }
                    }
                }
                else
                {
                    throw new Exception(LogPrefix + ": PushImage() called on encoder of unsupported type " + (this.encoder == null ? "null" : this.encoder.GetType().ToString()));
                }
            }
        }

        public override void Dispose()
        {
            if (!disposed)
            {
                base.Dispose();
                pushImageQueueReady.Set(); // let worker exit
            }
        }
    }
#endif
    public class Framer<T>
    {
        T[] frame;
        public Framer(int frameSize)
        {
            this.frame = new T[frameSize];
            var x = new T[1];
            if (x[0] is byte)
                this.sizeofT = sizeof(byte);
            else if (x[0] is short)
                this.sizeofT = sizeof(short);
            else if (x[0] is float)
                this.sizeofT = sizeof(float);
            else
                throw new Exception("Input data type is not supported: " + x[0].GetType());

        }
        int sizeofT;
        int framePos = 0;

        public IEnumerable<T[]> Frame(T[] buf)
        {
            var s = frame.Length;
            // quick return in trivial case
            if (s == buf.Length && framePos == 0)
            {
                yield return buf;
            }
            else
            {
                var bufPos = 0;

                while (bufPos + s - framePos <= buf.Length)
                {
                    var l = s - framePos;
                    Buffer.BlockCopy(buf, bufPos * sizeofT, frame, framePos * sizeofT, l * sizeofT);
                    //Console.WriteLine("=== Y {0} {1} -> {2} {3} ", bufPos, bufPos + l, sourceFramePos, sourceFramePos + l);
                    bufPos += l;
                    framePos = 0;

                    yield return this.frame;
                }
                if (bufPos != buf.Length)
                {
                    var l = buf.Length - bufPos;
                    Buffer.BlockCopy(buf, bufPos * sizeofT, frame, framePos * sizeofT, l * sizeofT);
                    //Console.WriteLine("=== L {0} {1} -> {2} {3} ", bufPos, bufPos + l, sourceFramePos, sourceFramePos + l);
                    framePos += l;
                }
            }
        }
    }
    /// <summary>
    /// Consumes data in array buffers of arbitrary length. Repacks them in frames of constant length for further processing and encoding.
    /// </summary>
    /// <param name="voiceInfo">Outgoing stream parameters. Set applicable fields to read them by encoder and by receiving client when voice created.</param>
    /// <param name="channelId">Transport channel specific to frontend. Set to VoiceClient.ChannelAuto to let frontend automatically assign channel.</param>
    /// <param name="encoder">Encoder producing the stream.</param>
    /// <returns>Outgoing stream handler.</returns>
    public class LocalVoiceFramed<T> : LocalVoice
    {
        Framer<T> framer;
        public interface IProcessor : IDisposable
        {
            T[] Process(T[] buf);
        }

        // Data flow repacked to frames of this size. May differ from info.FrameSize. Processors should resample in this case.
        public int FrameSize { get; private set; }
        // Optionally process input data. 
        // Should return arrays exactly of info.FrameSize size or null to skip sending
        internal T[] processFrame(T[] buf)
        {
            foreach (var p in processors)
            {
                buf = p.Process(buf);
                if (buf == null)
                {
                    break;
                }
            }
            return buf;
        }
        /// <summary>
        /// Adds processors after built-in processors (resampling, level measurement, voice detection and calibration) and everything added with AddPreProcessor.
        /// </summary>
        /// <param name="processors"></param>
        public void AddPostProcessor(params IProcessor[] processors)
        {
            foreach (var p in processors)
            {
                this.processors.Add(p);
            }
        }
        int preProcessorsCnt;

        /// <summary>
        /// Adds processors before built-in processors (resampling, level measurement, voice detection and calibration) and everything added with AddPostProcessor.
        /// </summary>
        /// <param name="processors"></param>
        public void AddPreProcessor(params IProcessor[] processors)
        {
            foreach (var p in processors)
            {
                this.processors.Insert(preProcessorsCnt++, p);
            }
        }
        /// <summary>
        /// Clears all processors in pipeline including built-in resampling.
        /// User should add at least resampler processor after call.
        /// </summary>
        public void ClearProcessors()
        {
            this.processors.Clear();
            preProcessorsCnt = 0;
        }
        List<IProcessor> processors = new List<IProcessor>();
        internal LocalVoiceFramed(VoiceClient voiceClient, IEncoder encoder, byte id, VoiceInfo voiceInfo, int channelId, int frameSize)
        : base(voiceClient, encoder, id, voiceInfo, channelId)
        {
            this.FrameSize = frameSize;
            this.framer = new Framer<T>(FrameSize);

            pushDataBufferPool = new PrimitiveArrayPool<T>(DATA_POOL_CAPACITY, Name + " Data");
            this.pushDataBufferPool.Init(FrameSize);
        }

        bool dataEncodeThreadStarted;
        Queue<T[]> pushDataQueue = new Queue<T[]>();
        AutoResetEvent pushDataQueueReady = new AutoResetEvent(false);
        // Work only if buffers of SourceFrameSize size pushed via PushDataAsync
        PrimitiveArrayPool<T> pushDataBufferPool;
        public PrimitiveArrayPool<T> PushDataBufferPool { get { return pushDataBufferPool; } }

        public bool PushDataAsyncReady { get { lock (pushDataQueue) return pushDataQueue.Count < DATA_POOL_CAPACITY - 1; } } // 1 slot for buffer currently processed and not contained either by pool or queue
        // Accepts array of arbitrary size. Automatically splits or aggregates input to buffers of sourceFrameBuffer's length
        // Expects buf content preserved until PushData call in worker thread. Releases buffer to PushDataBufferPool then.
        public void PushDataAsync(T[] buf)
        {
            if (disposed) return;

            if (!dataEncodeThreadStarted)
            {
                voiceClient.frontend.LogInfo(LogPrefix + ": Starting data encode thread");
#if NETFX_CORE
                ThreadPool.RunAsync((x) =>
                {
                    PushDataAsyncThread();
                });
#else
                var t = new Thread(PushDataAsyncThread);
                t.Start();
                t.Name = LogPrefix + " data encode";
#endif
                dataEncodeThreadStarted = true;
            }

            // Caller should check this asap in general case if packet production is expensive.
            // This is not the case For lightweight audio stream. Also overflow does not happen for audio stream normally.
            // Make sure that queue is not too large even if caller missed the check.
            if (this.PushDataAsyncReady)
            {
                lock (pushDataQueue)
                {
                    pushDataQueue.Enqueue(buf);
                }
                pushDataQueueReady.Set();
            }
            else
            {
                pushDataBufferPool.Release(buf);
            }
        }

        private void PushDataAsyncThread()
        {
            try
            {
                while (true)
                {
                    pushDataQueueReady.WaitOne(); // Wait until data is pushed to the queue or Dispose signals.
                    if (disposed) break;

                    while (true) // Dequeue and process while the queue is not empty
                    {
                        T[] b = null;
                        lock (pushDataQueue)
                        {
                            if (pushDataQueue.Count > 0)
                            {
                                b = pushDataQueue.Dequeue();
                            }
                        }
                        if (b != null)
                        {
                            PushData(b);
                            pushDataBufferPool.Release(b);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                voiceClient.frontend.LogError(LogPrefix + ": Exception in encode thread: " + e);
                throw e;
            }
            finally
            {
                pushDataBufferPool.Dispose();

#if NETFX_CORE
                pushDataQueueReady.Dispose();
#else
                pushDataQueueReady.Close();
#endif

                voiceClient.frontend.LogInfo(LogPrefix + ": Exiting data encode thread");
            }
        }


        // Accepts array of arbitrary size. Automatically splits or aggregates input to buffers of sourceFrameBuffer's length
        public void PushData(T[] buf)
        {
            if (this.voiceClient.frontend.IsChannelJoined(this.channelId) && this.Transmit)
            {
                if (this.encoder is IEncoderDataFlowDirect<T>)
                {
                    foreach (var framed in framer.Frame(buf))
                    {
                        var processed = processFrame(framed);
                        if (processed != null)
                        {
                            sendFrame(((IEncoderDataFlowDirect<T>)this.encoder).EncodeAndGetOutput(processed));
                        }
                    }
                }
                else
                {
                    throw new Exception(LogPrefix + ": PushData(T[]) called on encoder of unsupported type " + (this.encoder == null ? "null" : this.encoder.GetType().ToString()));
                }
            }
        }

        public override void Dispose()
        {
            if (!disposed)
            {
                this.pushDataBufferPool.Dispose(); // disposed in encoder thread also
                foreach (var p in processors)
                {
                    p.Dispose();
                }
                base.Dispose();
                pushDataQueueReady.Set(); // let worker exit
            }
        }
    }

#region nonpublic
    public struct RemoteVoiceOptions
    {
        /// <summary>
        /// Register a method to be called when new data frame received. Use it to get uncomressed data as byte[].
        /// Metod parameters: (int channelId, int playerId, byte voiceId, byte[] frame, object localUserObject)
        /// </summary>
        public Action<byte[]> OnDecodedFrameByteAction { get; set; }
        /// <summary>
        /// Register a method to be called when new data frame received. Use it to get uncomressed data as float[].
        /// </summary>
        public Action<float[]> OnDecodedFrameFloatAction { get; set; }
        /// <summary>
        /// Register a method to be called when new data frame received. Use it to get uncomressed data as short[].
        /// Metod parameters: (int channelId, int playerId, byte voiceId, short[] frame)
        /// </summary>
        public Action<short[]> OnDecodedFrameShortAction { get; set; }
        /// <summary>
        /// Register a method to be called when remote voice removed.
        /// </summary>
        public Action OnRemoteVoiceRemoveAction { get; set; }

        /// <summary>User object (e.g. audio pleayer) attached to remote voice instance for easy access.</summary>
        public object LocalUserObject { get; set; }

        /// <summary>Remote voice data decoder. Use to set decoder options or override it with user decoder.</summary>
        public IDecoder Decoder { get; set; }
    }
    internal class RemoteVoice : IDisposable
    {
        // Client.RemoteVoiceInfos support
        internal VoiceInfo Info { get; private set; }
        internal RemoteVoiceOptions options;
        private int channelId;
        private int playerId;
        private byte voiceId;
        bool disposed;
        internal RemoteVoice(VoiceClient client, RemoteVoiceOptions options, int channelId, int playerId, byte voiceId, VoiceInfo info, byte lastEventNumber)
        {
            this.options = options;
            this.voiceClient = client;
            this.channelId = channelId;
            this.playerId = playerId;
            this.voiceId = voiceId;
            this.Info = info;
            this.lastEvNumber = lastEventNumber;
#if NETFX_CORE
            ThreadPool.RunAsync((x) =>
            {
                decodeThread();
            });
#else
            var t = new Thread(decodeThread);
            t.Name = LogPrefix + " decode";
            t.Start();
#endif
            if (this.options.Decoder != null)
            {
                this.options.Decoder.Open(info);
            }
        }

        protected string Name { get { return "Remote v#" + voiceId + " ch#" + voiceClient.channelStr(channelId) + " p#" + playerId; } }
        protected string LogPrefix { get { return "[PV] " + Name; } }

        internal byte lastEvNumber = 0;
        private VoiceClient voiceClient;

        internal void receiveBytes(byte[] receivedBytes, byte evNumber)
        {
            // receive-gap detection and compensation
            if (evNumber != this.lastEvNumber) // skip check for 1st event 
            {
                int missing = VoiceUtil.byteDiff(evNumber, this.lastEvNumber);
                if (missing != 0)
                {

                    this.voiceClient.frontend.LogDebug(LogPrefix + " evNumer: " + evNumber + " playerVoice.lastEvNumber: " + this.lastEvNumber + " missing: " + missing + " r/b " + receivedBytes.Length);
                }

                this.lastEvNumber = evNumber;

                // restoring missing frames
                receiveNullFrames(missing);

                this.voiceClient.FramesLost += missing;
            }
            this.receiveFrame(receivedBytes);
        }

        Queue<byte[]> frameQueue = new Queue<byte[]>();
        AutoResetEvent frameQueueReady = new AutoResetEvent(false);

        void receiveFrame(byte[] frame)
        {
            if (disposed) return;

            lock (frameQueue)
            {
                frameQueue.Enqueue(frame);
                frameQueueReady.Set();
            }
        }

        void receiveNullFrames(int count)
        {
            lock (frameQueue)
            {
                for (int i = 0; i < count; i++)
                {
                    frameQueue.Enqueue(null);
                    frameQueueReady.Set();
                }
            }
        }
        void decodeThread()
        {
            voiceClient.frontend.LogInfo(LogPrefix + ": Starting decode thread");
            try
            {
                while (true)
                {
                    frameQueueReady.WaitOne(); // Wait until data is pushed to the queue or Dispose signals.
                    if (disposed) break;

                    while (true) // Dequeue and process while the queue is not empty
                    {
                        byte[] f = null;
                        bool ok = false;
                        lock (frameQueue)
                        {
                            if (frameQueue.Count > 0)
                            {
                                ok = true;
                                f = frameQueue.Dequeue();
                            }
                        }
                        if (ok)
                        {
                            decodeFrame(f);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                voiceClient.frontend.LogError(LogPrefix + ": Exception in decode thread: " + e);
                throw e;
            }
            finally
            {

#if NETFX_CORE
                frameQueueReady.Dispose();
#else
                frameQueueReady.Close();
#endif

                voiceClient.frontend.LogInfo(LogPrefix + ": Exiting decode thread");
            }
        }
        void decodeFrame(byte[] frame)
        {
            if (this.options.Decoder != null)
            {
                if (this.options.Decoder is IDecoderDirect)
                {
                    if (this.options.OnDecodedFrameByteAction != null)
                    {
                        var decodedSamples = this.decodeStreamToByte(frame);
                        this.options.OnDecodedFrameByteAction(decodedSamples);
                    }
                    if (this.options.OnDecodedFrameShortAction != null)
                    {
                        var decodedSamples = this.decodeStreamToShort(frame);
                        this.options.OnDecodedFrameShortAction(decodedSamples);
                    }
                    if (this.options.OnDecodedFrameFloatAction != null)
                    {
                        var decodedSamples = this.decodeStreamToFloat(frame);
                        this.options.OnDecodedFrameFloatAction(decodedSamples);
                    }
                }
                else
                {
                    ((IDecoderQueued)this.options.Decoder).Decode(frame);
                }
            }
        }

        internal byte[] decodeStreamToByte(byte[] buffer)
        {
            byte[] res;
            if (buffer == null)
            {
                res = ((IDecoderDirect)this.options.Decoder).DecodeToByte(null);
                this.voiceClient.frontend.LogDebug(LogPrefix + " lost packet decoded length: " + res.Length);
            }
            else
            {
                res = ((IDecoderDirect)this.options.Decoder).DecodeToByte(buffer);
            }
            return res;
        }
        internal short[] decodeStreamToShort(byte[] buffer)
        {
            short[] res;
            if (buffer == null)
            {
                res = ((IDecoderDirect)this.options.Decoder).DecodeToShort(null);
                this.voiceClient.frontend.LogDebug(LogPrefix + " lost packet decoded length: " + res.Length);
            }
            else
            {
                res = ((IDecoderDirect)this.options.Decoder).DecodeToShort(buffer);
            }
            return res;
        }

        internal float[] decodeStreamToFloat(byte[] buffer)
        {
            float[] res;
            if (buffer == null)
            {
                res = ((IDecoderDirect)this.options.Decoder).DecodeToFloat(null);
                this.voiceClient.frontend.LogDebug(LogPrefix + " lost packet decoded length: " + res.Length);
            }
            else
            {
                res = ((IDecoderDirect)this.options.Decoder).DecodeToFloat(buffer);
            }
            return res;
        }

        internal void removeAndDispose()
        {
            if (options.OnRemoteVoiceRemoveAction != null)
            {
                options.OnRemoteVoiceRemoveAction();
            }
            Dispose();
        }

        public void Dispose()
        {
            if (!disposed)
            {
                if (this.options.Decoder != null)
                {
                    this.options.Decoder.Dispose();
                }
                disposed = true;
                frameQueueReady.Set(); // let worker exit
            }
        }
    }

#endregion

    interface IVoiceFrontend
    {
        void LogError(string fmt, params object[] args);
        void LogWarning(string fmt, params object[] args);
        void LogInfo(string fmt, params object[] args);
        void LogDebug(string fmt, params object[] args);

        int AssignChannel(VoiceInfo v);
        bool IsChannelJoined(int channelId);
        void SendVoicesInfo(IEnumerable<LocalVoice> voices, int channelId, int targetPlayerId);
        void SendVoiceRemove(LocalVoice voice, int channelId, int targetPlayerId);
        void SendFrame(object content, int channelId, LocalVoice localVoice);
        string ChannelIdStr(int channelId);
        string PlayerIdStr(int playerId);
        bool SupportsArraySegmentSerialization { get; }
        void SetDebugEchoMode(LocalVoice v);
    }

    /// <summary>
    /// Base class for Voice clients implamantations
    /// </summary>        
    public class VoiceClient : IDisposable
    {
        internal IVoiceFrontend frontend;

        /// <summary>Lost frames counter.</summary>
        public int FramesLost { get; internal set; }

        /// <summary>Received frames counter.</summary>
        public int FramesReceived { get; private set; }

        /// <summary>Sent frames counter.</summary>
        public int FramesSent { get { int x = 0; foreach (var v in this.localVoices) { x += v.Value.FramesSent; } return x; } }

        /// <summary>Sent frames bytes counter.</summary>
        public int FramesSentBytes { get { int x = 0; foreach (var v in this.localVoices) { x += v.Value.FramesSentBytes; } return x; } }

        /// <summary>Average time required voice packet to return to sender.</summary>
        public int RoundTripTime { get; private set; }

        /// <summary>Average round trip time variation.</summary>
        public int RoundTripTimeVariance { get; private set; }

        /// <summary>Do not log warning when duplicate info received.</summary>
        public bool SuppressInfoDuplicateWarning { get; set; }

        /// <summary>Remote voice info event delegate.</summary>        
        public delegate void RemoteVoiceInfoDelegate(int channelId, int playerId, byte voiceId, VoiceInfo voiceInfo, ref RemoteVoiceOptions options);

        /// <summary>
        /// Register a method to be called when remote voice info arrived (after join or new new remote voice creation).
        /// Metod parameters: (int channelId, int playerId, byte voiceId, VoiceInfo voiceInfo, ref RemoteVoiceOptions options);
        /// </summary>
        public RemoteVoiceInfoDelegate OnRemoteVoiceInfoAction { get; set; }

        /// <summary>Lost frames simulation ratio.</summary>
        public int DebugLostPercent { get; set; }

        private int prevRtt = 0;
        /// <summary>Iterates through copy of all local voices list.</summary>
        public IEnumerable<LocalVoice> LocalVoices
        {
            get
            {
                var res = new LocalVoice[this.localVoices.Count];
                this.localVoices.Values.CopyTo(res, 0);
                return res;
            }
        }

        /// <summary>Iterates through copy of all local voices list of given channel.</summary>
        public IEnumerable<LocalVoice> LocalVoicesInChannel(int channelId)
        {
            List<LocalVoice> channelVoices;
            if (this.localVoicesPerChannel.TryGetValue(channelId, out channelVoices))
            {
                var res = new LocalVoice[channelVoices.Count];
                channelVoices.CopyTo(res, 0);
                return res;
            }
            else
            {
                return new LocalVoice[0];
            }
        }

        /// <summary>Iterates through all remote voices infos.</summary>
        public IEnumerable<RemoteVoiceInfo> RemoteVoiceInfos
        {
            get
            {
                foreach (var channelVoices in this.remoteVoices)
                {
                    foreach (var playerVoices in channelVoices.Value)
                    {
                        foreach (var voice in playerVoices.Value)
                        {
                            yield return new RemoteVoiceInfo(channelVoices.Key, playerVoices.Key, voice.Key, voice.Value.Info, voice.Value.options.LocalUserObject);
                        }
                    }
                }
            }
        }

        /// <summary>Iterates through all local objects set by user in remote voices.</summary>
        public IEnumerable<object> RemoteVoiceLocalUserObjects
        {
            get
            {
                foreach (var channelVoices in this.remoteVoices)
                {
                    foreach (var playerVoices in channelVoices.Value)
                    {
                        foreach (var voice in playerVoices.Value)
                        {
                            yield return voice.Value.options.LocalUserObject;
                        }
                    }
                }
            }
        }
        /// <summary>Creates Client instance</summary>
        internal VoiceClient(IVoiceFrontend frontend)
        {
            this.frontend = frontend;
        }

        /// <summary>
        /// This method dispatches all available incoming commands and then sends this client's outgoing commands.
        /// Call this method regularly (2..20 times a second).
        /// </summary>
        public void Service()
        {
            foreach (var v in localVoices)
            {
                v.Value.service();
            }
        }

        private LocalVoice createLocalVoice(VoiceInfo voiceInfo, int channelId, IEncoder encoder, Func<byte, int, LocalVoice> voiceFactory)
        {
            if (channelId == ChannelAuto)
            {
                channelId = this.frontend.AssignChannel(voiceInfo);
            }
            var newId = getNewVoiceId();
            if (newId != 0)
            {
                LocalVoice v = voiceFactory(newId, channelId);
                if (v != null)
                {
                    addVoice(newId, channelId, v);
                    this.frontend.LogInfo(v.LogPrefix + " added enc: " + v.info.ToString());
                    return v;
                }
            }

            return null;
        }
        public const int ChannelAuto = -1; // any number not used as channel id in frontends
        /// <summary>
        /// Creates basic outgoing stream w/o data processing support. Provided encoder should generate output data stream.
        /// </summary>
        /// <param name="voiceInfo">Outgoing stream parameters. Set applicable fields to read them by encoder and by receiving client when voice created.</param>
        /// <param name="channelId">Transport channel specific to frontend. Set to VoiceClient.ChannelAuto to let frontend automatically assign channel.</param>
        /// <param name="encoder">Encoder producing the stream.</param>
        /// <returns>Outgoing stream handler.</returns>
        public LocalVoice CreateLocalVoice(VoiceInfo voiceInfo, int channelId = ChannelAuto, IEncoder encoder = null)
        {
            return (LocalVoice)createLocalVoice(voiceInfo, channelId, encoder, (vId, chId) => new LocalVoice(this, encoder, vId, voiceInfo, chId));
        }

        /// <summary>
        /// Creates outgoing stream consuming sequence of values passed in array buffers of arbitrary length which repacked in frames of constant length for further processing and encoding.
        /// </summary>
        /// <typeparam name="T">Type of data consumed by outgoing stream (element type of array buffers).</typeparam>
        /// <param name="voiceInfo">Outgoing stream parameters. Set applicable fields to read them by encoder and by receiving client when voice created.</param>
        /// <param name="channelId">Transport channel specific to frontend. Set to VoiceClient.ChannelAuto to let frontend automatically assign channel.</param>
        /// <param name="encoder">Encoder compressing data stream in pipeline.</param>
        /// <returns>Outgoing stream handler.</returns>
        public LocalVoiceFramed<T> CreateLocalVoiceFramed<T>(VoiceInfo voiceInfo, int frameSize, int channelId = ChannelAuto, IEncoderDataFlow<T> encoder = null)
        {
            return (LocalVoiceFramed<T>)createLocalVoice(voiceInfo, channelId, encoder, (vId, chId) => new LocalVoiceFramed<T>(this, encoder, vId, voiceInfo, chId, frameSize));
        }

        /// <summary>
        /// Creates outgoing audio stream. Adds audio specific features (e.g. resampling, level meter) to processing pipeline and to returning stream handler.
        /// </summary>
        /// <typeparam name="T">Element type of audio array buffers.</typeparam>
        /// <param name="voiceInfo">Outgoing audio stream parameters. Set applicable fields to read them by encoder and by receiving client when voice created.</param>
        /// <param name="channelId">Transport channel specific to frontend. Set to VoiceClient.ChannelAuto to let frontend automatically assign channel.</param>
        /// <param name="encoder">Audio encoder. Set to null to use default Opus encoder.</param>
        /// <returns>Outgoing stream handler.</returns>
        /// <remarks>
        /// voiceInfo.sourceSamplingRate and voiceInfo.SamplingRate may do not match. Automatic resampling will occur in this case.
        /// </remarks>
        public LocalVoiceAudio<T> CreateLocalVoiceAudio<T>(VoiceInfo voiceInfo, int channelId = ChannelAuto, IEncoderDataFlow<T> encoder = null)
        {
            return (LocalVoiceAudio<T>)createLocalVoice(voiceInfo, channelId, encoder, (vId, chId) => LocalVoiceAudio.Create<T>(this, vId, encoder, voiceInfo, chId));
        }

#if PHOTON_VOICE_VIDEO_ENABLE
        /// <summary>
        /// Creates outgoing video stream consuming sequence of image buffers.
        /// </summary>
        /// <param name="voiceInfo">Outgoing stream parameters. Set applicable fields to read them by encoder and by receiving client when voice created.</param>
        /// <param name="channelId">Transport channel specific to frontend. Set to VoiceClient.ChannelAuto to let frontend automatically assign channel.</param>
        /// <param name="encoder">Encoder compressing video data. Set to null to use default VP8 implementation.</param>
        /// <returns>Outgoing stream handler.</returns>
        public LocalVoiceVideo CreateLocalVoiceVideo(VoiceInfo voiceInfo, int channelId = ChannelAuto, IEncoder encoder = null)
        {
            return (LocalVoiceVideo)createLocalVoice(voiceInfo, channelId, encoder, (vId, chId) => new LocalVoiceVideo(this, encoder, vId, voiceInfo, chId));
        }
#endif

        private byte getNewVoiceId()
        {
            // id assigned starting from 1 and up to 255

            byte newId = 0; // non-zero if successfully assigned
            if (voiceIdCnt == 255)
            {
                // try to reuse id
                var ids = new bool[256];
                foreach (var v in localVoices)
                {
                    ids[v.Value.id] = true;
                }
                // ids[0] is not used
                for (byte id = 1; id != 0 /* < 256 */ ; id++)
                {
                    if (!ids[id])
                    {
                        newId = id;
                        break;
                    }
                }
            }
            else
            {
                voiceIdCnt++;
                newId = voiceIdCnt;
            }
            return newId;
        }

        void addVoice(byte newId, int channelId, LocalVoice v)
        {
            localVoices[newId] = v;

            List<LocalVoice> voiceList;
            if (!localVoicesPerChannel.TryGetValue(channelId, out voiceList))
            {
                voiceList = new List<LocalVoice>();
                localVoicesPerChannel[channelId] = voiceList;
            }
            voiceList.Add(v);

            if (this.frontend.IsChannelJoined(channelId))
            {
                this.frontend.SendVoicesInfo(new List<LocalVoice>() { v }, channelId, 0); // broadcast if joined
            }
            v.Group = this.GlobalGroup;
        }
        /// <summary>
        /// Removes local voice (outgoing data stream).
        /// <param name="voice">Handler of outgoing stream to be removed.</param>
        /// </summary>
        internal void RemoveLocalVoice(LocalVoice voice)
        {
            this.localVoices.Remove(voice.id);

            this.localVoicesPerChannel[voice.channelId].Remove(voice);
            if (this.frontend.IsChannelJoined(voice.channelId))
            {
                this.frontend.SendVoiceRemove(voice, voice.channelId, 0);
            }

            voice.Dispose();
            this.frontend.LogInfo(voice.LogPrefix + " removed");
        }

        internal void sendVoicesInfo(int targetPlayerId)
        {
            foreach (var ch in this.localVoicesPerChannel.Keys)
            {
                sendChannelVoicesInfo(ch, targetPlayerId);
            }
        }

        internal void sendChannelVoicesInfo(int channelId, int targetPlayerId)
        {
            if (this.frontend.IsChannelJoined(channelId))
            {
                List<LocalVoice> voiceList;
                if (this.localVoicesPerChannel.TryGetValue(channelId, out voiceList))
                {
                    this.frontend.SendVoicesInfo(voiceList, channelId, targetPlayerId);
                }
            }
        }

        internal void onVoiceEvent(object content0, int channelId, int playerId, int localPlayerId)
        {
            object[] content = (object[])content0;
            if ((byte)content[0] == (byte)0)
            {
                switch ((byte)content[1])
                {
                    case (byte)EventSubcode.VoiceInfo:
                        this.onVoiceInfo(channelId, playerId, content[2]);
                        break;
                    case (byte)EventSubcode.VoiceRemove:
                        this.onVoiceRemove(channelId, playerId, content[2]);
                        break;
                    default:
                        this.frontend.LogError("[PV] Unknown sevent subcode " + content[1]);
                        break;
                }
            }
            else
            {
                byte voiceId = (byte)content[0];
                byte evNumber = (byte)content[1];
                byte[] receivedBytes = (byte[])content[2];
                if (playerId == localPlayerId)
                {
                    LocalVoice voice;
                    if (this.localVoices.TryGetValue(voiceId, out voice))
                    {
                        int sendTime;
                        if (voice.eventTimestamps.TryGetValue(evNumber, out sendTime))
                        {
                            int rtt = Environment.TickCount - sendTime;
                            int rttvar = rtt - prevRtt;
                            prevRtt = rtt;
                            if (rttvar < 0) rttvar = -rttvar;
                            this.RoundTripTimeVariance = (rttvar + RoundTripTimeVariance * 19) / 20;
                            this.RoundTripTime = (rtt + RoundTripTime * 19) / 20;
                        }
                    }
                    //internal Dictionary<byte, DateTime> localEventTimestamps = new Dictionary<byte, DateTime>();
                }
                this.onFrame(channelId, playerId, voiceId, evNumber, receivedBytes);
            }
        }

        internal byte GlobalGroup
        {
            get { return this.globalGroup; }
            set
            {
                this.globalGroup = value;
                foreach (var v in this.localVoices)
                {
                    v.Value.Group = this.globalGroup;
                }
            }
        }

#region nonpublic

        private byte globalGroup;
        private byte voiceIdCnt = 0;

        private Dictionary<byte, LocalVoice> localVoices = new Dictionary<byte, LocalVoice>();
        private Dictionary<int, List<LocalVoice>> localVoicesPerChannel = new Dictionary<int, List<LocalVoice>>();
        // channel id -> player id -> voice id -> voice
        private Dictionary<int, Dictionary<int, Dictionary<byte, RemoteVoice>>> remoteVoices = new Dictionary<int, Dictionary<int, Dictionary<byte, RemoteVoice>>>();

        internal object[] buildVoicesInfo(IEnumerable<LocalVoice> voicesToSend, bool logInfo)
        {
            object[] infos = new object[voicesToSend.Count()];

            object[] content = new object[] { (byte)0, EventSubcode.VoiceInfo, infos };
            int i = 0;
            foreach (var v in voicesToSend)
            {
                infos[i] = new Dictionary<byte, object>() {
                    { (byte)EventParam.VoiceId, v.id },
                    { (byte)EventParam.Codec, v.info.Codec },
                    { (byte)EventParam.SamplingRate, v.info.SamplingRate },
                    { (byte)EventParam.Channels, v.info.Channels },
                    { (byte)EventParam.FrameDurationUs, v.info.FrameDurationUs },
                    { (byte)EventParam.Bitrate, v.info.Bitrate },
                    { (byte)EventParam.UserData, v.info.UserData },
                    { (byte)EventParam.EventNumber, v.evNumber }

                };
                i++;

                if (logInfo)
                {
                    this.frontend.LogInfo(v.LogPrefix + " Sending info: " + v.info.ToString() + " ev=" + v.evNumber);
                }
            }
            return content;
        }

        internal object[] buildVoiceRemoveMessage(LocalVoice v)
        {
            byte[] ids = new byte[] { v.id };

            object[] content = new object[] { (byte)0, EventSubcode.VoiceRemove, ids };

            this.frontend.LogInfo(v.LogPrefix + " remove sent");

            return content;
        }

        internal void clearRemoteVoices()
        {
            foreach (var channelVoices in remoteVoices)
            {
                foreach (var playerVoices in channelVoices.Value)
                {
                    foreach (var voice in playerVoices.Value)
                    {
                        voice.Value.removeAndDispose();
                    }
                }
            }
            remoteVoices.Clear();
            this.frontend.LogInfo("[PV] Remote voices cleared");
        }

        internal void clearRemoteVoicesInChannel(int channelId)
        {
            Dictionary<int, Dictionary<byte, RemoteVoice>> channelVoices = null;
            if (this.remoteVoices.TryGetValue(channelId, out channelVoices))
            {

                foreach (var playerVoices in channelVoices)
                {
                    foreach (var voice in playerVoices.Value)
                    {
                        voice.Value.removeAndDispose();
                    }
                }
                this.remoteVoices.Remove(channelId);
            }
            this.frontend.LogInfo("[PV] Remote voices for channel " + this.channelStr(channelId) + " cleared");
        }
        private void onVoiceInfo(int channelId, int playerId, object payload)
        {
            Dictionary<int, Dictionary<byte, RemoteVoice>> channelVoices = null;
            if (!this.remoteVoices.TryGetValue(channelId, out channelVoices))
            {
                channelVoices = new Dictionary<int, Dictionary<byte, RemoteVoice>>();
                this.remoteVoices[channelId] = channelVoices;
            }
            Dictionary<byte, RemoteVoice> playerVoices = null;

            if (!channelVoices.TryGetValue(playerId, out playerVoices))
            {
                playerVoices = new Dictionary<byte, RemoteVoice>();
                channelVoices[playerId] = playerVoices;
            }

            foreach (var el in (object[])payload)
            {
                var h = (Dictionary<byte, Object>)el;
                var voiceId = (byte)h[(byte)EventParam.VoiceId];
                if (!playerVoices.ContainsKey(voiceId))
                {

                    var eventNumber = (byte)h[(byte)EventParam.EventNumber];

                    var info = VoiceInfo.CreateFromEventPayload(h);
                    this.frontend.LogInfo("[PV] ch#" + this.channelStr(channelId) + " p#" + this.playerStr(playerId) + " v#" + voiceId + " Info received: " + info.ToString() + " ev=" + eventNumber);

                    // create default decoder                   
                    RemoteVoiceOptions options = new RemoteVoiceOptions();
                    // create default decoder
                    options.Decoder = this.CreateDecoder(channelId, playerId, voiceId, info);
                    if (this.OnRemoteVoiceInfoAction != null)
                    {
                        this.OnRemoteVoiceInfoAction(channelId, playerId, voiceId, info, ref options);
                    }
                    playerVoices[voiceId] = new RemoteVoice(this, options, channelId, playerId, voiceId, info, eventNumber);
                }
                else
                {
                    if (!this.SuppressInfoDuplicateWarning)
                    {
                        this.frontend.LogWarning("[PV] Info duplicate for voice #" + voiceId + " of player " + this.playerStr(playerId) + " at channel " + this.channelStr(channelId));
                    }
                }
            }
        }

        private void onVoiceRemove(int channelId, int playerId, object payload)
        {
            var voiceIds = (byte[])payload;
            Dictionary<int, Dictionary<byte, RemoteVoice>> channelVoices = null;
            if (this.remoteVoices.TryGetValue(channelId, out channelVoices))
            {
                Dictionary<byte, RemoteVoice> playerVoices = null;
                if (channelVoices.TryGetValue(playerId, out playerVoices))
                {
                    foreach (var voiceId in voiceIds)
                    {
                        RemoteVoice voice;
                        if (playerVoices.TryGetValue(voiceId, out voice))
                        {
                            playerVoices.Remove(voiceId);
                            this.frontend.LogInfo("[PV] Remote voice #" + voiceId + " of player " + this.playerStr(playerId) + " at channel " + this.channelStr(channelId) + " removed");
                            voice.removeAndDispose();
                        }
                        else
                        {
                            this.frontend.LogWarning("[PV] Remote voice #" + voiceId + " of player " + this.playerStr(playerId) + " at channel " + this.channelStr(channelId) + " not found when trying to remove");
                        }
                    }
                }
                else
                {
                    this.frontend.LogWarning("[PV] Remote voice list of player " + this.playerStr(playerId) + " at channel " + this.channelStr(channelId) + " not found when trying to remove voice(s)");
                }
            }
            else
            {
                this.frontend.LogWarning("[PV] Remote voice list of channel " + this.channelStr(channelId) + " not found when trying to remove voice(s)");
            }
        }

        Random rnd = new Random();
        private void onFrame(int channelId, int playerId, byte voiceId, byte evNumber, byte[] receivedBytes)
        {

            if (this.DebugLostPercent > 0 && rnd.Next(100) < this.DebugLostPercent)
            {
                this.frontend.LogWarning("[PV] Debug Lost Sim: 1 packet dropped");
                return;
            }

            FramesReceived++;

            Dictionary<int, Dictionary<byte, RemoteVoice>> channelVoices = null;
            if (this.remoteVoices.TryGetValue(channelId, out channelVoices))
            {
                Dictionary<byte, RemoteVoice> playerVoices = null;
                if (channelVoices.TryGetValue(playerId, out playerVoices))
                {

                    RemoteVoice voice = null;
                    if (playerVoices.TryGetValue(voiceId, out voice))
                    {
                        voice.receiveBytes(receivedBytes, evNumber);
                    }
                    else
                    {
                        this.frontend.LogWarning("[PV] Frame event for not inited voice #" + voiceId + " of player " + this.playerStr(playerId) + " at channel " + this.channelStr(channelId));
                    }
                }
                else
                {
                    this.frontend.LogWarning("[PV] Frame event for voice #" + voiceId + " of not inited player " + this.playerStr(playerId) + " at channel " + this.channelStr(channelId));
                }
            }
            else
            {
                this.frontend.LogWarning("[PV] Frame event for voice #" + voiceId + " of not inited channel " + this.channelStr(channelId));
            }
        }

        internal bool removePlayerVoices(int playerId)
        {
            foreach (var ch in this.localVoicesPerChannel.Keys)
            {
                removePlayerVoices(ch, playerId);
            }
            return true;
        }

        internal bool removePlayerVoices(int channelId, int playerId)
        {
            Dictionary<int, Dictionary<byte, RemoteVoice>> channelVoices = null;
            if (this.remoteVoices.TryGetValue(channelId, out channelVoices))
            {
                Dictionary<byte, RemoteVoice> playerVoices = null;
                if (channelVoices.TryGetValue(playerId, out playerVoices))
                {
                    channelVoices.Remove(playerId);

                    foreach (var v in playerVoices)
                    {
                        v.Value.removeAndDispose();
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        internal IEncoder CreateEncoder(VoiceInfo info, LocalVoice localVoice)
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

        internal IDecoder CreateDecoder(int channelId, int playerId, byte voiceId, VoiceInfo info)
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

        internal string channelStr(int channelId)
        {
            var str = this.frontend.ChannelIdStr(channelId);
            if (str != null)
            {
                return channelId + "(" + str + ")";
            }
            else
            {
                return channelId.ToString();
            }
        }

        internal string playerStr(int playerId)
        {
            var str = this.frontend.PlayerIdStr(playerId);
            if (str != null)
            {
                return playerId + "(" + str + ")";
            }
            else
            {
                return playerId.ToString();
            }
        }
        //public string ToStringFull()
        //{
        //    return string.Format("Photon.Voice.Client, local: {0}, remote: {1}",  localVoices.Count, remoteVoices.Count);
        //}

#endregion

        public void Dispose()
        {
            foreach (var v in this.localVoices)
            {
                v.Value.Dispose();
            }
            foreach (var channelVoices in this.remoteVoices)
            {
                foreach (var playerVoices in channelVoices.Value)
                {
                    foreach (var voice in playerVoices.Value)
                    {
                        voice.Value.Dispose();
                    }
                }
            }
        }
    }
    public static class VoiceUtil
    {
        internal static byte byteDiff(byte latest, byte last)
        {
            return (byte)(latest - (last + 1));
        }
    }
}