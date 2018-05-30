using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace ExitGames.Client.Photon.Voice
{
    public abstract class ObjectPool<TType, TInfo> : IDisposable
    {
        protected int capacity;
        protected TInfo info;
        private TType[] freeObj = new TType[0];
        protected int pos;
        protected string name;
        private bool inited;
        abstract protected TType createObject(TInfo info);
        abstract protected void destroyObject(TType obj);
        abstract protected bool infosMatch(TInfo i0, TInfo i1);
        internal string LogPrefix { get { return "[ObjectPool] [" + name + "]"; } }
        public ObjectPool(int capacity, string name)
        {
            this.capacity = capacity;
            this.name = name;
        }
        public ObjectPool(int capacity, string name, TInfo info)
        {
            this.capacity = capacity;
            this.name = name;
            Init(info);
        }
        public void Init(TInfo info)
        {
            lock (this)
            {
                while (pos > 0)
                {
                    destroyObject(freeObj[--pos]);
                }
                this.info = info;
                this.freeObj = new TType[capacity];
                inited = true;
            }
        }
        public TInfo Info
        {
            get { return info; }
        }
        // Creates from the info given in constructor if fails to get from pool.
        public TType AcquireOrCreate()
        {
            lock (this)
            {
                if (pos > 0)
                {
                    return freeObj[--pos];
                }
                if (!inited)
                {
                    throw new Exception(LogPrefix + " not initialized");
                }
            }
            return createObject(this.info);
        }
        // Acquires from pool only if info matches, otherwise creates object from passed info
        public TType AcquireOrCreate(TInfo info)
        {
            // TODO: this.info thread safety
            if (!infosMatch(this.info, info))
            {
                Init(info);
            }
            return AcquireOrCreate();
        }
        // Returns to pool only if info matches
        virtual public bool Release(TType obj, TInfo objInfo)
        {
            // TODO: this.info thread safety
            if (infosMatch(this.info, objInfo))
            {
                lock (this)
                {
                    if (pos < freeObj.Length)
                    {
                        freeObj[pos++] = obj;
                        return true;
                    }
                }
            }
            // destroy if can't reuse
            //UnityEngine.Debug.Log(LogPrefix + " Release(Info) destroy");
            destroyObject(obj);
            // TODO: log warning
            return false;
        }
        virtual public bool Release(TType obj)
        {
            lock (this)
            {
                if (pos < freeObj.Length)
                {
                    freeObj[pos++] = obj;
                    return true;
                }
            }
            // destroy if can't reuse
            //UnityEngine.Debug.Log(LogPrefix + " Release destroy " + pos);
            destroyObject(obj);
            // TODO: log warning
            return false;
        }
        public void Dispose()
        {
            lock (this)
            {
                while (pos > 0)
                {
                    destroyObject(freeObj[--pos]);
                }
                freeObj = new TType[0];
            }
        }
    }
    public class PrimitiveArrayPool<T> : ObjectPool<T[], int>
    {
        public PrimitiveArrayPool(int capacity, string name) : base(capacity, name) { }
        public PrimitiveArrayPool(int capacity, string name, int info) : base(capacity, name, info) { }
        protected override T[] createObject(int info)
        {
            //UnityEngine.Debug.Log(LogPrefix + " Create " + pos);
            return new T[info];
        }
        protected override void destroyObject(T[] obj)
        {
            //UnityEngine.Debug.Log(LogPrefix + " Dispose " + pos + " " + obj.GetHashCode());
        }
        protected override bool infosMatch(int i0, int i1)
        {
            return i0 == i1;
        }
    }
    public class ImageBufferNativePool<T> : ObjectPool<T, ImageBufferInfo> where T : ImageBufferNative
    {
        public delegate T Factory(ImageBufferNativePool<T> pool, ImageBufferInfo info);
        Factory factory;
        public ImageBufferNativePool(int capacity, Factory factory, string name) : base(capacity, name)
        {
            this.factory = factory;
        }
        public ImageBufferNativePool(int capacity, Factory factory, string name, ImageBufferInfo info) : base(capacity, name, info)
        {
            this.factory = factory;
        }
        protected override T createObject(ImageBufferInfo info)
        {
            //UnityEngine.Debug.Log(LogPrefix + " Create " + pos);
            return factory(this, info);
        }
        protected override void destroyObject(T obj)
        {
            //UnityEngine.Debug.Log(LogPrefix + " Dispose " + pos + " " + obj.GetHashCode());
            obj.Dispose();
        }
        // only height and stride compared, other parameters do not affect native buffers and can be simple overwritten
        protected override bool infosMatch(ImageBufferInfo i0, ImageBufferInfo i1)
        {
            if (i0 == null) return i1 == null;
            if (i1 == null) return i0 == null;
            if (i0.Height != i1.Height)
            {
                return false;
            }
            var s0 = i0.Stride;
            var s1 = i1.Stride;
            if (s0.Length != s1.Length)
            {
                return false;
            }
            switch (i0.Stride.Length)
            {
                // most common case are 1 and 3 planes
                case 1:
                    return s0[0] == s1[0];
                case 2:
                    return s0[0] == s1[0] && s0[1] == s1[1];
                case 3:
                    return s0[0] == s1[0] && s0[1] == s1[1] && s0[2] == s1[2];
                default:
                    for (int i = 0; i < s0.Length; i++)
                    {
                        if (s0[i] != s1[i])
                        {
                            return false;
                        }
                    }
                    return true;
            }
        }
    }
}
