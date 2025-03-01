using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;
using JLUtility;

namespace JLGraphics.RenderPasses
{
    public enum ToneCurve
    {
        //low quality
        Gamma,
        //high quality
        Srgb,
    }
    public class PostProcessPass : RenderPass
    {
        public ToneCurve ToneCurve = ToneCurve.Srgb;
        public float FogDensity = .0025f;
        public Vector3 FogColor = new Vector3(1, 1, 1);
        public bool Tonemapping = true;
        public float Gamma = 2.4f;
        public bool Vignette = true;
        public float VignetteStrength = 0.5f;
        public float Exposure = 1.0f;
        public float BrightnessClamp = 0xffff;
        public float Mosaic = 1.0f;
        public bool FXAA = false;

        Shader shader = null;
        ShaderProgram PostProcessShader = null;
        FrameBuffer postProcessTexture = null;
        public PostProcessPass(int queueOffset) : base(RenderQueue.AfterTransparents, queueOffset)
        {
            PostProcessShader = new ShaderProgram("PostProcess",
                AssetLoader.GetPathToAsset("./Shaders/PostProcess.frag"),
                AssetLoader.GetPathToAsset("./Shaders/Passthrough.vert"));
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
            if (postProcessTexture == null)
            {
                postProcessTexture = new FrameBuffer(frameBuffer.Width, frameBuffer.Height, false, new TFP() { internalFormat = PixelInternalFormat.Rgb8 });
            }
            Shader.SetGlobalFloat(Shader.GetShaderPropertyId("FogDensity"), FogDensity);
            Shader.SetGlobalVector3(Shader.GetShaderPropertyId("FogColor"), FogColor);
            shader.SetBool(Shader.GetShaderPropertyId("Tonemapping"), Tonemapping);
            if(ToneCurve == ToneCurve.Srgb)
            {
                shader.SetBool(Shader.GetShaderPropertyId("GammaCorrection"), false);
                shader.SetBool(Shader.GetShaderPropertyId("Srgb"), true);
            }
            else if(ToneCurve == ToneCurve.Gamma)
            {
                shader.SetBool(Shader.GetShaderPropertyId("GammaCorrection"), true);
                shader.SetBool(Shader.GetShaderPropertyId("Srgb"), false);
            }
            shader.SetFloat(Shader.GetShaderPropertyId("Gamma"), Gamma);
            shader.SetBool(Shader.GetShaderPropertyId("Vignette"), Vignette);
            shader.SetFloat(Shader.GetShaderPropertyId("VignetteStrength"), VignetteStrength);
            shader.SetFloat(Shader.GetShaderPropertyId("Exposure"), Exposure);
            shader.SetFloat(Shader.GetShaderPropertyId("BrightnessClamp"), BrightnessClamp);
            shader.SetFloat(Shader.GetShaderPropertyId("Mosaic"), Mosaic);

            if (!FrameBuffer.AlikeResolution(frameBuffer, postProcessTexture))
            {
                postProcessTexture.Dispose();
                postProcessTexture = new FrameBuffer(frameBuffer.Width, frameBuffer.Height, false, new TFP { internalFormat = PixelInternalFormat.Rgb8 });
            }

            PerfTimer.Start("FXAA");
            var temp = frameBuffer;
            if (FXAA)
                temp = AntiAliasing.ApplyFXAA(frameBuffer);
            PerfTimer.Stop();

            Blit(temp, postProcessTexture, shader);
            Blit(postProcessTexture, frameBuffer);
        }

        protected override void OnDispose()
        {
            PostProcessShader.Dispose();
            postProcessTexture.Dispose();
        }
    }
}
