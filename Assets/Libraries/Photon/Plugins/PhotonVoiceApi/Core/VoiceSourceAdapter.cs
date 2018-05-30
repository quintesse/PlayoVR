// -----------------------------------------------------------------------
// <copyright file="VoiceSourceAdapter.cs" company="Exit Games GmbH">
//   Photon Voice API Framework for Photon - Copyright (C) 2017 Exit Games GmbH
// </copyright>
// <summary>
//   Photon data streaming support.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------
using System;
namespace ExitGames.Client.Photon.Voice
{
    // Adapts IBufferReader.Read to LocalVoice.PushData
    // should be used on typed LocalVoice of same T type
    public abstract class BufferReaderPushAdapterBase<T> : IServiceable
    {
        protected IDataReader<T> reader;
        public abstract void Service(LocalVoice localVoice);
        public BufferReaderPushAdapterBase(IDataReader<T> reader)
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
        public BufferReaderPushAdapter(LocalVoice localVoice, IDataReader<T> reader) : base(reader)
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
        public BufferReaderPushAdapterAsyncPool(LocalVoice localVoice, IDataReader<T> reader) : base(reader) { }
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
        public BufferReaderPushAdapterAsyncPoolCopy(LocalVoice localVoice, IDataReader<T> reader) : base(reader)
        {
            buffer = new T[((LocalVoiceFramed)localVoice).FrameSize];
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
    public class BufferReaderPushAdapterAsyncPoolFloatToShort : Voice.BufferReaderPushAdapterBase<float>
    {
        float[] buffer;
        public BufferReaderPushAdapterAsyncPoolFloatToShort(Voice.LocalVoice localVoice, Voice.IDataReader<float> reader) : base(reader)
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
