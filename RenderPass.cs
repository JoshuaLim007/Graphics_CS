using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public enum RenderQueue
    {
        Prepass = 0,
        AfterOpaques = 2000,
        AfterTransparents = 3000,
        AfterPostProcessing = 4000,
    }
    public class CommandBuffer
    {
        Queue<Action> actions = new Queue<Action>();
        public void Blit(FrameBuffer src, FrameBuffer dst, bool restoreSrc, Shader shader = null)
        {
            actions.Enqueue(() => { Graphics.Blit(src, dst, restoreSrc, shader); });
        }
        public void Add(Action action)
        {
            actions.Enqueue(action);
        }
        internal void Invoke()
        {
            Action action;
            actions.TryDequeue(out action);
            while (action != null)
            {
                action.Invoke();
                actions.TryDequeue(out action);
            }
        }
    }
    public abstract class RenderPass : IComparable<RenderPass>, IDisposable
    {
        public RenderPass(RenderQueue queue, int queueOffset)
        {
            Queue = (int)queue + queueOffset;
        }
        public int Queue { get; set; }
        public virtual void FrameSetup() { }
        public abstract void Execute(in CommandBuffer cmd, in FrameBuffer frameBuffer);
        public virtual void FrameCleanup() { }
        public int CompareTo(RenderPass? other)
        {
            if(other == null)
            {
                return 0;
            }
            if(this.Queue < other.Queue)
            {
                return -1;
            }
            else
            {
                return 1;
            }
        }
        public virtual void Dispose() { }
    }
}
