using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public interface IComponentEvent : IGlobalScope
    {
        public bool IsActiveAndEnabled();
    }
    public interface IUpdate : IComponentEvent
    {
        public void Update();
    }
    public interface IFixedUpdate : IComponentEvent
    {
        public void FixedUpdate();
    }
    public interface IStart : IComponentEvent
    {
        public void Start();
    }
    public interface IOnRender : IComponentEvent
    {
        public void OnRender(Camera camera);
    }

    public abstract class Object : IGlobalScope
    {
        static int count = 0;
        static Stack<int> previousDestroyedObject = new Stack<int>();
        int mId = 0;
        public int InstanceID => mId;
        private bool Null => mId == 0;
        protected virtual void OnCreate(params object[] args) { }
        internal void CallCreate(params object[] args)
        {
            if (previousDestroyedObject.Count != 0)
            {
                mId = previousDestroyedObject.Pop();
            }
            else
            {
                count++;
                mId = count;
            }

            if (typeof(IUpdate).IsAssignableFrom(GetType()))
            {
                //AllUpdates.Add((IUpdate)instance);
                InternalGlobalScope<IUpdate>.Values.Add(this as IUpdate);
            }
            if (typeof(IFixedUpdate).IsAssignableFrom(GetType()))
            {
                //AllFixedUpdates.Add((IFixedUpdate)instance);
                InternalGlobalScope<IFixedUpdate>.Values.Add(this as IFixedUpdate);
            }
            if (typeof(IStart).IsAssignableFrom(GetType()))
            {
                //StartQueue.Add((IStart)instance);
                InternalGlobalScope<IStart>.Values.Add(this as IStart);
            }
            if (typeof(IOnRender).IsAssignableFrom(GetType()))
            {
                //AllOnRenders.Add((IOnRender)instance);
                InternalGlobalScope<IOnRender>.Values.Add(this as IOnRender);
            }
            OnCreate(args);
        }
        internal void CallDestroy()
        {
            previousDestroyedObject.Push(mId);
            mId = 0;

            if (typeof(IUpdate).IsAssignableFrom(GetType()))
            {
                InternalGlobalScope<IUpdate>.Values.Remove(this as IUpdate);
            }
            if (typeof(IFixedUpdate).IsAssignableFrom(GetType()))
            {
                InternalGlobalScope<IFixedUpdate>.Values.Remove(this as IFixedUpdate);
            }
            if (typeof(IStart).IsAssignableFrom(GetType()))
            {
                InternalGlobalScope<IStart>.Values.Remove(this as IStart);
            }
            if (typeof(IOnRender).IsAssignableFrom(GetType()))
            {
                InternalGlobalScope<IOnRender>.Values.Remove(this as IOnRender);
            }
            InternalOnImmediateDestroy();
            OnDestroy();
        }
        protected virtual void InternalOnImmediateDestroy() { }
        public virtual void OnDestroy() { }
        public static bool operator ==(Object a, Object b)
        {
            if (a is null && b is not null)
            {
                if (b.Null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else if (b is null && a is not null)
            {
                if (a.Null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else if (b is null && a is null)
            {
                return true;
            }

            return ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b)
        {
            return !(a == b);
        }
        public override bool Equals(object? obj)
        {
            if (obj is null && Null)
            {
                return true;
            }
            else if (obj is null)
            {
                return false;
            }
            else
            {
                return Null == ((Object)obj).Null;
            }
        }
        public static implicit operator bool(Object a) => a.Null;
        public override int GetHashCode()
        {
            return mId;
        }
    }
    public abstract class NamedObject : Object, IName
    {
        public NamedObject(string name)
        {
            Name = name;
        }
        public string Name { get; set; }
    }
    public interface IName 
    {
        public string Name { get; set; }
    }
}
