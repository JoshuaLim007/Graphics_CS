using OpenTK.Mathematics;
namespace JLGraphics
{
    public enum StaticFlags
    {
        None = 0,
        StaticDraw = 0b1,
    }

    public class Entity : NamedObject
    {
        public StaticFlags StaticFlag { get; set; } = StaticFlags.None;
        public bool Enabled { get; set; } = true;
        public Transform Transform { get; private set; } = null;

        public Entity Parent
        {
            set
            {
                if(value == null)
                {
                    Transform.Parent = null;
                }
                else
                {
                    Transform.Parent = value.Transform;
                }
            }
            get
            {
                if(Transform.Parent != null)
                {
                    return Transform.Parent.Entity;
                }
                return null;
            }
        }
        public Entity[] Children { 
            get
            {
                if(Transform.Childs.Length == 0)
                {
                    return null;
                }

                Entity[] arr = new Entity[Transform.Childs.Length];
                for (int i = 0; i < Transform.Childs.Length; i++)
                {
                    arr[i] = Transform.Childs[i].Entity;
                }
                return arr;
            }
        }

        public static Entity FindObjectByName(string name)
        {
            for (int i = 0; i < InternalGlobalScope<Entity>.Values.Count; i++)
            {
                if (InternalGlobalScope<Entity>.Values[i].Name == name)
                {
                    return InternalGlobalScope<Entity>.Values[i];
                }
            }
            return null;
        }
        public static T FindObjectOfType<T>() where T: Component
        {
            for (int i = 0; i < InternalGlobalScope<Entity>.Values.Count; i++)
            {
                if (InternalGlobalScope<Entity>.Values[i].HasComponent<T>(out var instance))
                {
                    return instance;
                }
            }
            return null;
        }

        private List<Component> m_components = new List<Component>();
        private void Init(Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            AddComponent<Transform>(out var t, parent, position, rotation, scale);
            Transform = t;
            InternalGlobalScope<Entity>.Values.Add(this);
            CallCreate(this, null);
        }
        internal Entity(string Name) : base(Name)
        {
            Init(null, Vector3.Zero, Quaternion.Identity, Vector3.One);
        }
        internal Entity(string Name, Transform parent) : base(Name)
        {
            Init(parent, Vector3.Zero, Quaternion.Identity, Vector3.One);
        }
        internal Entity(string Name, Vector3 position, Quaternion rotation, Vector3 scale) : base(Name)
        {
            Init(null, position, rotation, scale);
        }
        internal Entity(string Name, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale) : base(Name)
        {
            Init(parent, position, rotation, scale);
        }

        public Component[] GetAllComponents()
        {
            return m_components.ToArray();
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
        public Entity AddComponent<T>(params object[] args) where T : Component, new()
        {
            T instance = new T();
            instance.OnAddComponentEvent(this, args);
            m_components.Add(instance);
            return this;
        }
        public Entity AddComponent<T>(out T instance, params object[] args) where T : Component, new()
        {
            instance = new T();
            instance.OnAddComponentEvent(this, args);
            m_components.Add(instance);
            return this;
        }
        public bool HasComponent<T>(out T instance) where T : Component
        {
            for (int i = 0; i < m_components.Count; i++)
            {
                if (m_components[i].GetType() == typeof(T))
                {
                    instance = (T)m_components[i];
                    return true;
                }
            }
            instance = null;
            return false;
        }
        
        public static Entity Clone(Entity entity, Entity parent = null)
        {
            var newEntity = Entity.Create(entity.Name + "_clone");
            newEntity.StaticFlag = entity.StaticFlag;
            newEntity.Enabled = entity.Enabled;
            newEntity.Parent = parent;
            newEntity.Transform.LocalPosition = entity.Transform.LocalPosition;
            newEntity.Transform.LocalRotation = entity.Transform.LocalRotation;
            newEntity.Transform.LocalScale = entity.Transform.LocalScale;

            for (int i = 0; i < entity.m_components.Count; i++)
            {
                if (entity.m_components[i].GetType() == typeof(Transform))
                {
                    continue;
                }

                Component component = Component.Clone(entity.m_components[i]);
                component.OnAddComponentEvent(newEntity, null);
                newEntity.m_components.Add(component);
            }

            if(entity.Children != null)
            {
                for (int i = 0; i < entity.Children.Length; i++)
                {
                    Clone(entity.Children[i], newEntity);
                }
            }

            return newEntity;
        }

        public static void Destroy<T>(ref T toDestroy) where T : Object
        {
            if (typeof(T) == typeof(Component))
            {
                Component? component = toDestroy as Component;
                component?.Entity.m_components.Remove(component);
            }
            if (typeof(T) == typeof(Renderer))
            {
                InternalGlobalScope<Renderer>.Values.Remove(toDestroy as Renderer);
            }
            CallDestroy(toDestroy);
            toDestroy = null;
        }
        public static Entity Create(string Name)
        {
            return new Entity(Name);
        }
        public static Entity Create(string Name, Transform parent)
        {
            return new Entity(Name, parent);
        }
        public static Entity Create(string Name, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            return new Entity(Name, position, rotation, scale);
        }
        public static Entity Create(string Name, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            return new Entity(Name, parent, position, rotation, scale);
        }
        
        protected override void InternalOnImmediateDestroy()
        {
            for (int i = 0; i < m_components.Count; i++)
            {
                CallDestroy(m_components[i]);
            }
            InternalGlobalScope<Entity>.Values.Remove(this);
        }
    }
}
