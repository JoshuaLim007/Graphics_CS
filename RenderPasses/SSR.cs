using JLUtility;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics.RenderPasses
{
    public class SSR : RenderPass
    {
        Shader shader;
        public SSR(int queueOffset) : base(RenderQueue.AfterTransparents, queueOffset)
        {
            var shaderProgram = new ShaderProgram("SSGI",
                AssetLoader.GetPathToAsset("./Shaders/SSR.frag"),
                AssetLoader.GetPathToAsset("./Shaders/Passthrough.vert"));
            shaderProgram.CompileProgram();
            shader = new Shader("SSGI", shaderProgram);
        }

        public override string Name => "SSR Render Pass";
        FrameBuffer initialPass;//, denoisePass;
        public int SamplesPerPixel { get; set; } = 1;
        public int Steps { get; set; } = 64;
        public override void Execute(in FrameBuffer frameBuffer)
        {
            PerfTimer.Start("SSR");
            float scale = 0.5f;
            if (!FrameBuffer.AlikeResolution(initialPass, frameBuffer, scale))
            {
                initialPass?.Dispose();

                var res = GetScaledResolution(frameBuffer.Width, frameBuffer.Height, scale);
                initialPass = new FrameBuffer(res.X, res.Y, false, new TFP
                {
                    internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgb16f,
                    maxMipmap = 11,
                    wrapMode = TextureWrapMode.MirroredRepeat,
                    magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                    minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.NearestMipmapLinear,
                });
            }
            shader.SetInt(Shader.GetShaderPropertyId("SamplesPerPixel"), SamplesPerPixel);
            shader.SetInt(Shader.GetShaderPropertyId("Steps"), Steps);
            Blit(frameBuffer, initialPass, shader);
            PerfTimer.Start("SSR AA");
            var temp = AntiAliasing.ApplyFXAA(initialPass);
            Blit(temp, initialPass);
            PerfTimer.Stop();
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("_SSRColor"), initialPass.TextureAttachments[0]);
            PerfTimer.Stop();
        }

        protected override void OnDispose()
        {
            initialPass?.Dispose();
        }
    }
}
