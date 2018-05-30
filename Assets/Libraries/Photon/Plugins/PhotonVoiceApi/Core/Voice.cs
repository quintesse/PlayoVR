// -----------------------------------------------------------------------
// <copyright file="Voice.cs" company="Exit Games GmbH">
//   Photon Voice API Framework for Photon - Copyright (C) 2017 Exit Games GmbH
// </copyright>
// <summary>
//   Photon data streaming support.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------
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
    /// Interface for pulling data, in case this is more appropriate, instead of pushing it. Such data pushed via adapters.
    /// </summary>
    public interface IDataReader<T> : IDisposable
    {
        /// <summary>
        /// Fill full given frame buffer with source unconmressed data or return false if not enough such data.
        /// </summary>
        bool Read(T[] buffer);
    }
    /// <summary>
    /// Used to make LocalVoice call user's code in its Service()
    /// </summary>
    public interface IServiceable
    {
        void Service(LocalVoice localVoice);
    }
    /// <summary>
    /// Represents outgoing data stream.
    /// </summary>
    public class LocalVoice : IDisposable
    {
        public const int DATA_POOL_CAPACITY = 50; // TODO: may depend on data type and properties, set for average audio stream
        /// <summary>If AudioGroup != 0, voice's data is sent only to clients listening to this group.</summary>
        /// <see cref="LoadBalancingFrontend.ChangeAudioGroups(byte[], byte[])"/>
        public byte Group { get; set; }
        /// <summary>Returns Info structure assigned on local voice cration.</summary>
        public VoiceInfo Info { get { return info; } }
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
        /// <summary>Optional user object attached to LocalVoice. its Service() will be called at each VoiceClient.Service() call.</summary>
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
        protected object disposeLock = new object();
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
            this.voiceClient.frontend.SendFrame(compressed, evNumber, id, this.channelId, this);
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
                if (this.encoder != null)
                {
                    this.encoder.Dispose();
                }
                disposed = true;
            }
        }
    }
    public struct RemoteVoiceOptions
    {
        /// <summary>
        /// Register a method to be called when new data frame received. Use it to get uncomressed data as byte[].
        /// </summary>
        public Action<byte[]> OnDecodedFrameByteAction { get; set; }
        /// <summary>
        /// Register a method to be called when new data frame received. Use it to get uncomressed data as float[].
        /// </summary>
        public Action<float[]> OnDecodedFrameFloatAction { get; set; }
        /// <summary>
        /// Register a method to be called when new data frame received. Use it to get uncomressed data as short[].
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
        volatile private bool disposed;
        object disposeLock = new object();
        internal RemoteVoice(VoiceClient client, RemoteVoiceOptions options, int channelId, int playerId, byte voiceId, VoiceInfo info, byte lastEventNumber)
        {            
            this.options = options;
            this.voiceClient = client;
            this.channelId = channelId;
            this.playerId = playerId;
            this.voiceId = voiceId;
            this.Info = info;
            this.lastEvNumber = lastEventNumber;
            if (this.options.Decoder == null) // init fields first for proper logging
            {
                voiceClient.frontend.LogError(LogPrefix + ": decoder is null");
                disposed = true;
                return;
            }
#if NETFX_CORE
            ThreadPool.RunAsync((x) =>
            {
                decodeThread(this.options.Decoder);
            });
#else
            var t = new Thread(() => decodeThread(this.options.Decoder));
            t.Name = LogPrefix + " decode";
            t.Start();
#endif
        }
        protected string Name { get { return "Remote v#" + voiceId + " ch#" + voiceClient.channelStr(channelId) + " p#" + playerId; } }
        protected string LogPrefix { get { return "[PV] " + Name; } }
        internal byte lastEvNumber = 0;
        private VoiceClient voiceClient;
        private static byte byteDiff(byte latest, byte last)
        {
            return (byte)(latest - (last + 1));
        }
        internal void receiveBytes(byte[] receivedBytes, byte evNumber)
        {
            // receive-gap detection and compensation
            if (evNumber != this.lastEvNumber) // skip check for 1st event 
            {
                int missing = byteDiff(evNumber, this.lastEvNumber);
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
            lock (disposeLock) // sync with Dispose and decodeThread 'finally'
            {
                if (disposed) return;
                lock (frameQueue)
                {
                    frameQueue.Enqueue(frame);
                }
                frameQueueReady.Set();
            }
        }
        void receiveNullFrames(int count)
        {
            lock (disposeLock) // sync with Dispose and decodeThread 'finally'
            {
                if (disposed) return;
                lock (frameQueue)
                {
                    for (int i = 0; i < count; i++)
                    {
                        frameQueue.Enqueue(null);
                    }
                }
                frameQueueReady.Set();
            }
        }
        void decodeThread(IDecoder decoder)
        {
            voiceClient.frontend.LogInfo(LogPrefix + ": Starting decode thread");
            try
            {
                decoder.Open(Info);
                while (!disposed)
                {
                    frameQueueReady.WaitOne(); // Wait until data is pushed to the queue or Dispose signals.
                    while (true) // Dequeue and process while the queue is not empty
                    {
                        if (disposed) break; // early exit to save few resources
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
                            decodeFrame(decoder, f);
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
                lock (disposeLock) // sync with receiveFrame/receiveNullFrames
                {
                    disposed = true; // set to disposing state if exiting due to exception
                }
                // cleaning up being sure that fields are not updated anymore
#if NETFX_CORE
                frameQueueReady.Dispose();
#else
                frameQueueReady.Close();
#endif
                lock (frameQueue)
                {
                    frameQueue.Clear();
                }
                decoder.Dispose();
                voiceClient.frontend.LogInfo(LogPrefix + ": Exiting decode thread");
            }
        }
        void decodeFrame(IDecoder decoder, byte[] frame)
        {
            if (decoder is IDecoderDirect)
            {
                if (this.options.OnDecodedFrameByteAction != null)
                {
                    var decodedSamples = this.decodeFrameToByte(frame);
                    this.options.OnDecodedFrameByteAction(decodedSamples);
                }
                if (this.options.OnDecodedFrameShortAction != null)
                {
                    var decodedSamples = this.decodeFrameToShort(frame);
                    this.options.OnDecodedFrameShortAction(decodedSamples);
                }
                if (this.options.OnDecodedFrameFloatAction != null)
                {
                    var decodedSamples = this.decodeFrameToFloat(frame);
                    this.options.OnDecodedFrameFloatAction(decodedSamples);
                }
            }
            else
            {
                ((IDecoderQueued)decoder).Decode(frame);
            }
        }        
        internal byte[] decodeFrameToByte(byte[] buffer)
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
        internal short[] decodeFrameToShort(byte[] buffer)
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
        internal float[] decodeFrameToFloat(byte[] buffer)
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
            lock (disposeLock) // sync with receiveFrame/receiveNullFrames
            {
                if (!disposed)
                {
                    disposed = true;
                    frameQueueReady.Set(); // let decodeThread dispose resporces and exit
                }
            }
        }
    }
}
