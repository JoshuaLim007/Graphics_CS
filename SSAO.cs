using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace JLGraphics
{
    public class SSAO : RenderPass
    {
        FrameBuffer SSAORt, blurRT;
        Shader shader;
        Shader blur, comp;
        public float Radius = 15.0f;
        public float Intensity = 1.0f;
        public float DepthRange = 5.0f;
        public int Samples = 32;

        public SSAO() : base(RenderQueue.AfterTransparents, 7)
        {
            var program = new ShaderProgram("SSAO program", "./Shaders/SSAO.frag", "./Shaders/Passthrough.vert");
            program.CompileProgram();
            shader = new Shader("SSAO", program);

            program = new ShaderProgram("SSAO blur", "./Shaders/BoxBlur.frag", "./Shaders/Passthrough.vert");
            program.CompileProgram();
            blur = new Shader("SSAO Blur", program);

            program = new ShaderProgram("SSAO blur", "./Shaders/SSAOComp.frag", "./Shaders/Passthrough.vert");
            program.CompileProgram();
            comp = new Shader("SSAO Comp", program);
        }

        public override string Name => "SSAO";
        int previousWidth = 0;
        int previousHeight = 0;
        public override void Execute(in FrameBuffer frameBuffer)
        {
            if(previousHeight != frameBuffer.Height || previousWidth != frameBuffer.Width)
            {
                previousWidth = frameBuffer.Width;
                previousHeight = frameBuffer.Height;
                if(SSAORt != null)
                {
                    SSAORt.Dispose();
                    blurRT.Dispose();
                }
                SSAORt = FrameBuffer.Copy(frameBuffer, 0.5f);
                blurRT = FrameBuffer.Copy(frameBuffer, 0.5f);
            }
            shader.SetInt(Shader.GetShaderPropertyId("samples"), Samples);
            shader.SetFloat(Shader.GetShaderPropertyId("Radius"), Radius);
            shader.SetFloat(Shader.GetShaderPropertyId("DepthRange"), DepthRange);
            if (Graphics.Instance.Window.KeyboardState.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.T))
            {
                comp.SetFloat(Shader.GetShaderPropertyId("Intensity"), 0);
            }
            else
            {
                comp.SetFloat(Shader.GetShaderPropertyId("Intensity"), Intensity);
            }

            Blit(frameBuffer, SSAORt, shader);
            Blit(SSAORt, blurRT, blur);
            comp.SetTexture(Shader.GetShaderPropertyId("AOTex"), blurRT.TextureAttachments[0]);
            Blit(frameBuffer, frameBuffer, comp);
        }

        protected override void OnDispose()
        {
            SSAORt.Dispose();
            blurRT.Dispose();
            shader.Program.Dispose();
            comp.Program.Dispose();
            blur.Program.Dispose();
            shader = null;
        }
    }
}
