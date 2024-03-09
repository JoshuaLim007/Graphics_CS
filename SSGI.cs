using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public class SSGI : RenderPass
    {
        public SSGI(int queueOffset) : base(RenderQueue.AfterTransparents, queueOffset)
        {
        }

        public override string Name => "SSGI";

        public override void Execute(in FrameBuffer frameBuffer)
        {

        }

        protected override void OnDispose()
        {

        }
    }
}
