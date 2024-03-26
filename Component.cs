using JLGraphics.Utility.GuiAttributes;
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
        internal static Component Clone(Component other)
        {
            var clone = (Component)other.MemberwiseClone();
            clone.OnClone();
            return clone;
        }
        protected virtual void OnClone() { }
        public Component() : base("null")
        {
        }
        internal void OnAddComponentEvent(Entity entity, params object[] args)
        {
            entityReference = entity;
            Name = entity.Name + "_Component_" + GetType().Name;
            CallCreate(this, args);
        }

        Entity entityReference;
        public Entity Entity {
            get
            {
                AssertNull();
                return entityReference;
            } 
        }
        public virtual void OnGuiChange() { }
        public Transform Transform => Entity.Transform;
        bool _enabled = true;
        /// <summary>
        /// Enabled/Disables component
        /// </summary>
        [Gui("Enable/Disable")]
        public bool Enabled { get { return _enabled; } set { AssertNull(); _enabled = value; } }

        bool IComponentEvent.IsActiveAndEnabled()
        {
            AssertNull();
            return IsActiveAndEnabled;
        }
        bool GetAllEnabled()
        {
            AssertNull();
            bool enabled = Enabled & Entity.Enabled;
            Entity parent = Entity.Parent;
            while (parent)
            {
                enabled &= parent.Enabled;
                parent = parent.Parent;
            }
            return enabled;
        }
        /// <summary>
        /// Returns true if the component and entity is enabled.
        /// Returns false if the component or entity is disabled.
        /// </summary>
        public bool IsActiveAndEnabled => GetAllEnabled();
    }
}
