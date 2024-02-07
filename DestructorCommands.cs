using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JLUtility;

namespace JLGraphics
{
    internal sealed class DestructorCommands
    {
        private static readonly Lazy<DestructorCommands> lazy =
            new Lazy<DestructorCommands>(() => new DestructorCommands());
        private DestructorCommands() 
        {
            actions = new List<Action>(100);
        }
        
        List<Action> actions;
        public static DestructorCommands Instance { get { return lazy.Value; } }
        public void QueueAction(Action action)
        {
            actions.Add(action);
        }
        public void ExecuteCommands()
        {
            for (int i = 0; i < actions.Count; i++)
            {
                actions[i].Invoke();
            }
            actions.Clear();
        }
    }
    public abstract class SafeDispose : IDisposable, IName
    {
        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }
            Disposed = true;
            OnDispose();
        }
        protected abstract void OnDispose();
        public bool Disposed { get; private set; } = false;
        public abstract string Name { get; }
        string IName.Name { get; set; }

        ~SafeDispose()
        {
            if(Disposed == false)
            {
                DestructorCommands.Instance.QueueAction(Dispose);
                Debug.Log("Memory has not been freed! Using DestructorCommands to free memory for: " + Name, Debug.Flag.Warning);
            }
        }
    }
}
