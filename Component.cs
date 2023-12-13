using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public abstract class Component : IComponentEvent
    {
        public Entity Entity { get; internal set; }
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
