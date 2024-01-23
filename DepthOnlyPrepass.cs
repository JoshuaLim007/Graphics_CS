using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    internal class DepthOnlyPrepass : RenderPass
    {
        public DepthOnlyPrepass() : base(RenderQueue.AfterOpaques, 0)
        {
        }

        public override void Execute(in CommandBuffer cmd, in RenderTexture frameBuffer)
        {

        }
    }
}
