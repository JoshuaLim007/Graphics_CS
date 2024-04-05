using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace JLGraphics.RenderPasses
{
    public enum RenderQueue
    {
        Prepass = 0,
        BeforeOpaques = AfterOpaques - 1,
        AfterOpaques = 2000,
        BeforeTransparents = AfterTransparents - 1,
        AfterTransparents = 3000,
        BeforePostProcessing = AfterPostProcessing - 1,
        AfterPostProcessing = 4000,
    }
    public abstract class RenderPass : SafeDispose, IComparable<RenderPass>
    {
        public RenderPass(RenderQueue queue, int queueOffset)
        {
            Queue = (int)queue + queueOffset;
        }
        public static Camera CurrentCamera => CurrentRenderingCamera;
        internal static Camera CurrentRenderingCamera;
        public int Queue { get; set; }
        public virtual void FrameSetup(Camera camera) { }
        public abstract void Execute(in FrameBuffer frameBuffer);
        public virtual void FrameCleanup() { }
        public int CompareTo(RenderPass other)
        {
            if (other == null)
            {
                return 0;
            }
            if (Queue < other.Queue)
            {
                return -1;
            }
            else
            {
                return 1;
            }
        }
        public void Blit(FrameBuffer src, FrameBuffer dst, Shader shader = null)
        {
            Graphics.Instance.Blit(src, dst, false, shader);
        }
        public void BlitThenRestore(FrameBuffer src, FrameBuffer dst, Shader shader = null)
        {
            Graphics.Instance.Blit(src, dst, true, shader);
        }
        public Vector2i GetScaledResolution(int width, int height, float scale)
        {
            return new Vector2i((int)MathF.Max(MathF.Floor(width * scale), 1), (int)MathF.Max(MathF.Floor(height * scale), 1));
        }
    }
}
