using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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


        internal static List<Entity> AllEntities { get; } = new List<Entity>();
        internal static void AddEntityToWorld(Entity entity)
        {
            AllEntities.Add(entity);
        }
        internal static List<Renderer> AllRenderers { get; } = new List<Renderer>();


        internal static List<IUpdate> AllUpdates { get; } = new List<IUpdate>();
        internal static List<IFixedUpdate> AllFixedUpdates { get; } = new List<IFixedUpdate>();
        internal static List<IStart> AllStarts { get; } = new List<IStart>();
        internal static List<IOnRender> AllOnRenders { get; } = new List<IOnRender>();



        private List<Component> m_components = new List<Component>();
        private void Init(Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Transform = new Transform(parent, position, rotation, scale);
            AddComponent(Transform);
            AllEntities.Add(this);
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
        public T AddComponent<T>(T instance) where T : Component
        {
            m_components.Add(instance);
            if(typeof(IUpdate).IsAssignableFrom(typeof(T)))
            {
                AllUpdates.Add((IUpdate)instance);
            }
            if (typeof(IFixedUpdate).IsAssignableFrom(typeof(T)))
            {
                AllFixedUpdates.Add((IFixedUpdate)instance);
            }
            if (typeof(IStart).IsAssignableFrom(typeof(T)))
            {
                AllStarts.Add((IStart)instance);
            }
            if (typeof(IOnRender).IsAssignableFrom(typeof(T)))
            {
                AllOnRenders.Add((IOnRender)instance);
            }
            if (typeof(T) == typeof(Renderer))
            {
                AllRenderers.Add(instance as Renderer);
            }
            instance.Entity = this;
            return instance;
        }
        public void RemoveComponent<T>(T instance) where T : Component
        {
            m_components.Remove(instance);
        }
        public bool HasComponent<T>(T instance) where T : Component
        {
            for (int i = 0; i < m_components.Count; i++)
            {
                if (m_components[i] == instance)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
