using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;

namespace JLGraphics
{
    public class PostProcessPass : RenderPass
    {
        public float FogDensity = .0025f;
        public Vector3 FogColor = new Vector3(1, 1, 1);
        public bool Tonemapping = true;
        public bool GammaCorrection = true;

        Shader shader = null;
        ShaderProgram PostProcessShader = null;
        FrameBuffer postProcessTexture = null;
        public PostProcessPass() : base(RenderQueue.AfterTransparents, 9)
        {
            PostProcessShader = new ShaderProgram("PostProcess", "./Shaders/PostProcess.frag", "./Shaders/Passthrough.vert");
            if (Graphics.Instance.GetFileTracker(out var ft))
            {
                ft.AddFileObject(PostProcessShader.FragFile);
            }
            PostProcessShader.CompileProgram();
            shader = new Shader("Postprocessing material", PostProcessShader);
        }

        public override string Name => "Post process effects pass";

        public override void Execute(in FrameBuffer frameBuffer)
        {
            if(postProcessTexture == null)
            {
                postProcessTexture = new FrameBuffer(frameBuffer.Width, frameBuffer.Height, false, new TFP() { internalFormat = PixelInternalFormat.Rgb16f, pixelFormat = PixelFormat.Rgb });
            }
            Shader.SetGlobalFloat(Shader.GetShaderPropertyId("FogDensity"), FogDensity);
            Shader.SetGlobalVector3(Shader.GetShaderPropertyId("FogColor"), FogColor);
            shader.SetBool(Shader.GetShaderPropertyId("Tonemapping"), Tonemapping);
            shader.SetBool(Shader.GetShaderPropertyId("GammaCorrection"), GammaCorrection);
            if (!FrameBuffer.AlikeResolution(frameBuffer, postProcessTexture))
            {
                postProcessTexture.Dispose();
                postProcessTexture = new FrameBuffer(frameBuffer.Width, frameBuffer.Height, false, new TFP { internalFormat = PixelInternalFormat.Rgb16f, pixelFormat = PixelFormat.Rgb });
            }
            Blit(frameBuffer, postProcessTexture, shader);
            Blit(postProcessTexture, frameBuffer);
        }

        protected override void OnDispose()
        {
            PostProcessShader.Dispose();
            postProcessTexture.Dispose();
        }
    }
}
