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
        public bool Vignette = true;
        public float VignetteStrength = 0.5f;

        Shader shader = null;
        ShaderProgram PostProcessShader = null;
        FrameBuffer postProcessTexture = null;
        public PostProcessPass(int queueOffset) : base(RenderQueue.AfterTransparents, queueOffset)
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
                postProcessTexture = new FrameBuffer(frameBuffer.Width, frameBuffer.Height, false, new TFP() { internalFormat = PixelInternalFormat.Rgb8});
            }
            Shader.SetGlobalFloat(Shader.GetShaderPropertyId("FogDensity"), FogDensity);
            Shader.SetGlobalVector3(Shader.GetShaderPropertyId("FogColor"), FogColor);
            shader.SetBool(Shader.GetShaderPropertyId("Tonemapping"), Tonemapping);
            shader.SetBool(Shader.GetShaderPropertyId("GammaCorrection"), GammaCorrection);
            shader.SetBool(Shader.GetShaderPropertyId("Vignette"), Vignette);
            shader.SetFloat(Shader.GetShaderPropertyId("VignetteStrength"), VignetteStrength);
            
            if (!FrameBuffer.AlikeResolution(frameBuffer, postProcessTexture))
            {
                postProcessTexture.Dispose();
                postProcessTexture = new FrameBuffer(frameBuffer.Width, frameBuffer.Height, false, new TFP { internalFormat = PixelInternalFormat.Rgb8});
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
