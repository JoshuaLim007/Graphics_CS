using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public class MotionblurPass : RenderPass
    {
        public override string Name => "Motion Blur";
        public int Samples { get; set; } = 16;
        public float Strength { get; set; } = 1.0f;

        FrameBuffer FrameBuffer;
        Shader motionBlur;

        public MotionblurPass(RenderQueue queue = RenderQueue.AfterPostProcessing, int queueOffset = 0) : base(queue, queueOffset)
        {
            var program = new ShaderProgram("motion blur program", "./Shaders/MotionBlur.frag", "./Shaders/Passthrough.vert");
            program.CompileProgram();
            motionBlur = new Shader("motion blur", program);
            motionBlur.DepthTest = false;
        }

        public override void Execute(in FrameBuffer frameBuffer)
        {
            if(!FrameBuffer.AlikeResolution(frameBuffer, FrameBuffer))
            {
                FrameBuffer = FrameBuffer.Copy(frameBuffer, 1.0f);// new FrameBuffer(frameBuffer.Width, frameBuffer.Height, false, TFP.Default);
            }
            motionBlur.SetInt(Shader.GetShaderPropertyId("samples"), Samples);
            motionBlur.SetFloat(Shader.GetShaderPropertyId("strength"), Strength);
            motionBlur.SetFloat(Shader.GetShaderPropertyId("scale"), 60.0f * Time.UnscaledDeltaTime);
            Blit(frameBuffer, FrameBuffer);
            Blit(FrameBuffer, frameBuffer, motionBlur);
        }

        protected override void OnDispose()
        {
            FrameBuffer.Dispose();
            motionBlur.Program.Dispose();
        }
    }
}
