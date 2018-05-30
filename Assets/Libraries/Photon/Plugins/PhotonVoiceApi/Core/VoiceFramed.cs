// -----------------------------------------------------------------------
// <copyright file="VoiceFramed.cs" company="Exit Games GmbH">
//   Photon Voice API Framework for Photon - Copyright (C) 2017 Exit Games GmbH
// </copyright>
// <summary>
//   Photon data streaming support.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
#if NETFX_CORE
using Windows.System.Threading;
#endif
namespace ExitGames.Client.Photon.Voice
{
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
        public int Count(int bufLen)
        {
            return (bufLen + framePos) / frame.Length;
        }
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
    public class LocalVoiceFramed : LocalVoice
    {
        // Data flow repacked to frames of this size. May differ from info.FrameSize. Processors should resample in this case.
        public int FrameSize { get; private set; }
        internal LocalVoiceFramed(VoiceClient voiceClient, IEncoder encoder, byte id, VoiceInfo voiceInfo, int channelId, int frameSize)
        : base(voiceClient, encoder, id, voiceInfo, channelId)
        {
            this.FrameSize = frameSize;
        }
    }
    /// <summary>
    /// Consumes data in array buffers of arbitrary length. Repacks them in frames of constant length for further processing and encoding.
    /// </summary>
    /// <param name="voiceInfo">Outgoing stream parameters. Set applicable fields to read them by encoder and by receiving client when voice created.</param>
    /// <param name="channelId">Transport channel specific to frontend. Set to VoiceClient.ChannelAuto to let frontend automatically assign channel.</param>
    /// <param name="encoder">Encoder producing the stream.</param>
    /// <returns>Outgoing stream handler.</returns>
    public class LocalVoiceFramed<T> : LocalVoiceFramed
    {
        Framer<T> framer;
        public interface IProcessor : IDisposable
        {
            T[] Process(T[] buf);
        }
        // Optionally process input data. 
        // Should return arrays exactly of info.FrameSize size or null to skip sending
        internal T[] processFrame(T[] buf)
        {
            lock (this.processors)
            {
                foreach (var p in processors)
                {
                    buf = p.Process(buf);
                    if (buf == null)
                    {
                        break;
                    }
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
            lock (this.processors)
            {
                foreach (var p in processors)
                {
                    this.processors.Add(p);
                }
            }
        }
        int preProcessorsCnt;
        /// <summary>
        /// Adds processors before built-in processors (resampling, level measurement, voice detection and calibration) and everything added with AddPostProcessor.
        /// </summary>
        /// <param name="processors"></param>
        public void AddPreProcessor(params IProcessor[] processors)
        {
            lock (this.processors)
            {
                foreach (var p in processors)
                {
                    this.processors.Insert(preProcessorsCnt++, p);
                }
            }
        }
        /// <summary>
        /// Clears all processors in pipeline including built-in resampling.
        /// User should add at least resampler processor after call.
        /// </summary>
        public void ClearProcessors()
        {
            lock (this.processors)
            {
                this.processors.Clear();
                preProcessorsCnt = 0;
            }
        }
        List<IProcessor> processors = new List<IProcessor>();
        internal LocalVoiceFramed(VoiceClient voiceClient, IEncoder encoder, byte id, VoiceInfo voiceInfo, int channelId, int frameSize)
        : base(voiceClient, encoder, id, voiceInfo, channelId, frameSize)
        {
            this.framer = new Framer<T>(FrameSize);
            pushDataBufferPool = new PrimitiveArrayPool<T>(DATA_POOL_CAPACITY, Name + " Data");
            // Frame source is free to change this
            this.PushDataBufferPool.Init(FrameSize);
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
                PushDataBufferPool.Release(buf);
                framesSkipped++;
                voiceClient.frontend.LogWarning(LogPrefix + ": PushData queue overflow. Frames skipped: " + framesSkipped);
            }
        }
        int framesSkipped;
        bool exitThread = false;
        private void PushDataAsyncThread()
        {
            try
            {
                while (!exitThread)
                {
                    pushDataQueueReady.WaitOne(); // Wait until data is pushed to the queue or Dispose signals.
                    while (true) // Dequeue and process while the queue is not empty
                    {
                        if (exitThread) break; // early exit to save few resources
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
                            PushDataBufferPool.Release(b);
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
                lock (disposeLock)
                {
                    disposed = true;
                }
                PushDataBufferPool.Dispose();
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
                    lock (disposeLock)
                    {
                        if (!disposed)
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
            exitThread = true;
            lock (disposeLock)
            {
                if (!disposed)
                {
                                              // objects used for async push disposed in encode thread 'finally'
                    lock (this.processors)
                    {
                        foreach (var p in processors)
                        {
                            p.Dispose();
                        }
                    }
                    base.Dispose();
                    pushDataQueueReady.Set(); // let worker exit
                }
            }
            base.Dispose();
        }
    }
}
