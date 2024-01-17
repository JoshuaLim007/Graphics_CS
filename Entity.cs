using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace JLGraphics
{
    public enum StaticFlags
    {
        None = 0,
        StaticDraw = 0b1,
    }
    public interface IComponentEvent {
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
    
    public sealed class Entity
    {
        public StaticFlags StaticFlag { get; set; } = StaticFlags.None;
        public string Name { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public Transform Transform { get; private set; } = null;

        public static Entity FindObjectByName(string name)
        {
            for (int i = 0; i < GlobalInstance<Entity>.Values.Count; i++)
            {
                if (GlobalInstance<Entity>.Values[i].Name == name)
                {
                    return GlobalInstance<Entity>.Values[i];
                }
            }
            return null;
        }
        public static Entity FindObjectOfType<T>() where T: Component
        {
            for (int i = 0; i < GlobalInstance<Entity>.Values.Count; i++)
            {
                if (GlobalInstance<Entity>.Values[i].HasComponent<T>())
                {
                    return GlobalInstance<Entity>.Values[i];
                }
            }
            return null;
        }

        private List<Component> m_components = new List<Component>();
        private void Init(Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Transform = new Transform(parent, position, rotation, scale);
            AddComponent(Transform);
            GlobalInstance<Entity>.Values.Add(this);
        }
        public Entity()
        {
            Init(null, Vector3.Zero, Quaternion.Identity, Vector3.One);
        }
        public Entity(Transform parent)
        {
            Init(parent, Vector3.Zero, Quaternion.Identity, Vector3.One);
        }
        public Entity(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Init(null, position, rotation, scale);
        }
        public Entity(Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Init(parent, position, rotation, scale);
        }


        public List<T> GetComponents<T>() where T : Component
        {
            List<T> list = new List<T>();
            for (int i = 0; i < m_components.Count; i++)
            {
                if (m_components[i].GetType() == typeof(T))
                {
                    list.Add((T)m_components[i]);
                }
            }
            return list;
        }
        public T GetComponent<T>() where T : Component
        {
            for (int i = 0; i < m_components.Count; i++)
            {
                if (m_components[i].GetType() == typeof(T))
                {
                    return (T)m_components[i];
                }
            }
            return null;
        }
        public Entity AddComponent<T>(T instance) where T : Component
        {
            m_components.Add(instance);
            if(typeof(IUpdate).IsAssignableFrom(typeof(T)))
            {
                //AllUpdates.Add((IUpdate)instance);
                GlobalInstance<IUpdate>.Values.Add(instance as IUpdate);
            }
            if (typeof(IFixedUpdate).IsAssignableFrom(typeof(T)))
            {
                //AllFixedUpdates.Add((IFixedUpdate)instance);
                GlobalInstance<IFixedUpdate>.Values.Add(instance as IFixedUpdate);
            }
            if (typeof(IStart).IsAssignableFrom(typeof(T)))
            {
                //StartQueue.Add((IStart)instance);
                GlobalInstance<IStart>.Values.Add(instance as IStart);
            }
            if (typeof(IOnRender).IsAssignableFrom(typeof(T)))
            {
                //AllOnRenders.Add((IOnRender)instance);
                GlobalInstance<IOnRender>.Values.Add(instance as IOnRender);
            }
            if (typeof(T) == typeof(Renderer))
            {
                GlobalInstance<Renderer>.Values.Add(instance as Renderer);
                //AllRenderers.Add(instance as Renderer);
            }
            instance.Entity = this;
            return this;
        }
        public bool HasComponent<T>() where T : Component
        {
            for (int i = 0; i < m_components.Count; i++)
            {
                if (m_components[i].GetType() == typeof(T))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
