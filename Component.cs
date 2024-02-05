using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Assimp.Metadata;

namespace JLGraphics
{
    public class Component : NamedObject, IComponentEvent
    {
        public Component() : base("null")
        {
        }
        internal void OnAddComponentEvent(Entity entity, params object[] args)
        {
            entityReference = new WeakReference<Entity>(entity);
            base.Name += entity.Name + "_Component_" + GetType().Name;
            CallCreate(this, args);
        }

        WeakReference<Entity> entityReference;
        public Entity Entity {
            get
            {
                entityReference.TryGetTarget(out var entity);
                return entity;
            } 
        }
        public Transform Transform => Entity.Transform;
        /// <summary>
        /// Enabled/Disables component
        /// </summary>
        public bool Enabled { get; set; } = true;

        bool IComponentEvent.IsActiveAndEnabled()
        {
            return Enabled && Entity.Enabled;
        }

        /// <summary>
        /// Returns true if the component and entity is enabled.
        /// Returns false if the component or entity is disabled.
        /// </summary>
        public bool IsActiveAndEnabled => Enabled && Entity.Enabled;
    }
}
