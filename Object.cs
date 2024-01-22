using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
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
        protected virtual void SetArgs(params object[] args) { }

        private bool Null = false;
        internal void CallCreate(params object[] args)
        {
            Null = false;
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
            SetArgs(args);
        }
        internal void CallDestroy()
        {
            Null = true;
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
            InternalImmediateDestroy();
        }
        internal virtual void InternalImmediateDestroy() { }
        public static bool operator == (Object a, Object b)
        {
            if(a is null && b is not null)
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
            else if(b is null && a is null)
            {
                return true;
            }

            return ReferenceEquals(a, b);
        }
        public static bool operator != (Object a, Object b)
        {
            return !(a == b);
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
