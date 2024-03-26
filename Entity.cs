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
        StaticFlags staticFlags;
        public StaticFlags StaticFlag {
            get
            {
                AssertNull();
                return staticFlags;
            }
            set
            {
                AssertNull();
                staticFlags = value;
            }
        }

        bool _enabled = true;
        public bool Enabled {
            get
            {
                AssertNull();
                return _enabled;
            }
            set
            {
                AssertNull();
                _enabled = value;
            } 
        } 
        Transform _transform;
        public Transform Transform
        {
            get
            {
                AssertNull();
                return _transform;
            }
        }

        public Entity Parent
        {
            set
            {
                AssertNull();
                if (value is null)
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
                AssertNull();
                if (Transform.Parent != null)
                {
                    return Transform.Parent.Entity;
                }
                return null;
            }
        }
        public Entity[] Children { 
            get
            {
                AssertNull();
                var childs = Transform.Childs;
                if (childs.Length == 0)
                {
                    return null;
                }

                Entity[] arr = new Entity[Transform.Childs.Length];
                for (int i = 0; i < childs.Length; i++)
                {
                    arr[i] = childs[i].Entity;
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
            CallCreate(this, null);
            AddComponent<Transform>(out var t, parent, position, rotation, scale);
            _transform = t;
            InternalGlobalScope<Entity>.Values.Add(this);
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
            AssertNull();
            return m_components.ToArray();
        }
        public List<T> GetComponents<T>() where T : Component
        {
            AssertNull();
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
            AssertNull();
            for (int i = 0; i < m_components.Count; i++)
            {
                if (m_components[i].GetType() == typeof(T))
                {
                    return (T)m_components[i];
                }
            }
            return null;
        }
        public T GetComponentInChild<T>() where T : Component
        {
            AssertNull();
            var res = GetComponent<T>();
            if(res != null)
            {
                return res;
            }
            var childs = Children;
            for (int i = 0; childs != null && i < childs.Length; i++)
            {
                res = childs[i].GetComponentInChild<T>();
                if(res != null)
                {
                    return res;
                }
            }
            return null;
        }
        public Entity AddComponent<T>(params object[] args) where T : Component, new()
        {
            AssertNull();
            T instance = new T();
            instance.OnAddComponentEvent(this, args);
            m_components.Add(instance);
            return this;
        }
        public Entity AddComponent<T>(out T instance, params object[] args) where T : Component, new()
        {
            AssertNull();
            instance = new T();
            instance.OnAddComponentEvent(this, args);
            m_components.Add(instance);
            return this;
        }
        public bool HasComponent<T>(out T instance) where T : Component
        {
            AssertNull();
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
            entity.AssertNull();
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
            var children = entity.Children;
            if (children != null)
            {
                for (int i = 0; i < children.Length; i++)
                {
                    Clone(children[i], newEntity);
                }
            }

            return newEntity;
        }

        public static void Destroy<T>(ref T toDestroy) where T : Object
        {
            toDestroy.AssertNull();
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
            parent.Entity.AssertNull();
            return new Entity(Name, parent);
        }
        public static Entity Create(string Name, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            return new Entity(Name, position, rotation, scale);
        }
        public static Entity Create(string Name, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            parent.Entity.AssertNull();
            return new Entity(Name, parent, position, rotation, scale);
        }
        
        protected override void InternalOnImmediateDestroy()
        {
            Parent = null;
            for (int i = 0; i < m_components.Count; i++)
            {
                CallDestroy(m_components[i]);
            }
            var children = Children;
            for (int i = 0; children != null && i < children.Length; i++)
            {
                var t = children[i];
                Destroy(ref t);
            }
            InternalGlobalScope<Entity>.Values.Remove(this);
        }
    }
}
