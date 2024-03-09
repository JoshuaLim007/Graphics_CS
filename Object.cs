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
        static ulong count = 0;
        static Stack<ulong> previousDestroyedObject = new Stack<ulong>();
        ulong mId = 0;
        public ulong InstanceID => mId;
        private bool Null => mId == 0;
        protected virtual void OnCreate(params object[] args) { }
        internal static void CallCreate(Object @object, params object[] args)
        {
            if (previousDestroyedObject.Count != 0)
            {
                @object.mId = previousDestroyedObject.Pop();
            }
            else
            {
                count++;
                @object.mId = count;
            }

            if (typeof(IUpdate).IsAssignableFrom(@object.GetType()))
            {
                //AllUpdates.Add((IUpdate)instance);
                InternalGlobalScope<IUpdate>.Values.Add(@object as IUpdate);
            }
            if (typeof(IFixedUpdate).IsAssignableFrom(@object.GetType()))
            {
                //AllFixedUpdates.Add((IFixedUpdate)instance);
                InternalGlobalScope<IFixedUpdate>.Values.Add(@object as IFixedUpdate);
            }
            if (typeof(IStart).IsAssignableFrom(@object.GetType()))
            {
                //StartQueue.Add((IStart)instance);
                InternalGlobalScope<IStart>.Values.Add(@object as IStart);
            }
            if (typeof(IOnRender).IsAssignableFrom(@object.GetType()))
            {
                //AllOnRenders.Add((IOnRender)instance);
                InternalGlobalScope<IOnRender>.Values.Add(@object as IOnRender);
            }
            @object.OnCreate(args);
        }
        internal static void CallDestroy(Object @object)
        {
            previousDestroyedObject.Push(@object.mId);
            @object.mId = 0;

            if (typeof(IUpdate).IsAssignableFrom(@object.GetType()))
            {
                InternalGlobalScope<IUpdate>.Values.Remove(@object as IUpdate);
            }
            if (typeof(IFixedUpdate).IsAssignableFrom(@object.GetType()))
            {
                InternalGlobalScope<IFixedUpdate>.Values.Remove(@object as IFixedUpdate);
            }
            if (typeof(IStart).IsAssignableFrom(@object.GetType()))
            {
                InternalGlobalScope<IStart>.Values.Remove(@object as IStart);
            }
            if (typeof(IOnRender).IsAssignableFrom(@object.GetType()))
            {
                InternalGlobalScope<IOnRender>.Values.Remove(@object as IOnRender);
            }
            @object.InternalOnImmediateDestroy();
            @object.OnDestroy();
        }

        protected virtual void InternalOnImmediateDestroy() { }
        public virtual void OnDestroy() { }
        public static bool operator ==(Object a, Object b)
        {
            object ao = a;
            object bo = b;

            if (bo == null && ao == null)
            {
                return true;
            }

            if (ao == null && bo != null)
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
            else if (bo == null && ao != null)
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

            return a.InstanceID == b.InstanceID;
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
        public static implicit operator bool(Object a) => a is null ? false : !a.Null;
        public override int GetHashCode()
        {
            return (int)mId;
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
