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
        Shader denoise;
        public SSR(int queueOffset) : base(RenderQueue.AfterTransparents, queueOffset)
        {
            var shaderProgram = new ShaderProgram("SSGI",
                AssetLoader.GetPathToAsset("./Shaders/SSR.frag"),
                AssetLoader.GetPathToAsset("./Shaders/Passthrough.vert"));
            shaderProgram.CompileProgram();
            shader = new Shader("SSGI", shaderProgram);

            //var program = ShaderProgram.FindShaderProgram("SSGI denoise");
            //if(program == null)
            //{
            //    shaderProgram = new ShaderProgram("SSGI denoise", AssetLoader.GetPathToAsset("./Shaders/Denoise.frag"), AssetLoader.GetPathToAsset("./Shaders/Passthrough.vert"));
            //    shaderProgram.CompileProgram();
            //}
            //denoise = new Shader("Denoise", program);
        }

        public override string Name => "SSR Render Pass";
        FrameBuffer initialPass;//, denoisePass;
        public int SamplesPerPixel { get; set; } = 1;
        public int Steps { get; set; } = 64;
        public override void Execute(in FrameBuffer frameBuffer)
        {
            float scale = 0.5f;
            if (!FrameBuffer.AlikeResolution(initialPass, frameBuffer, scale))
            {
                initialPass?.Dispose();
                //denoisePass?.Dispose();

                var res = GetScaledResolution(frameBuffer.Width, frameBuffer.Height, scale);
                initialPass = new FrameBuffer(res.X, res.Y, false, new TFP
                {
                    internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgb16f,
                    maxMipmap = 11,
                    wrapMode = TextureWrapMode.MirroredRepeat,
                    magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                    minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.NearestMipmapLinear,
                });
                //denoisePass = new FrameBuffer(res.X, res.Y, false, new TFP
                //{
                //    internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgb16f,
                //    maxMipmap = 0,
                //    wrapMode = TextureWrapMode.MirroredRepeat,
                //    magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                //    minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.Nearest,
                //});
            }
            shader.SetInt(Shader.GetShaderPropertyId("SamplesPerPixel"), SamplesPerPixel);
            shader.SetInt(Shader.GetShaderPropertyId("Steps"), Steps);
            Blit(frameBuffer, initialPass, shader);
            //denoise.SetFloat(Shader.GetShaderPropertyId("color_intensity"), 1);
            //Blit(initialPass, denoisePass, denoise);
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("_SSRColor"), initialPass.TextureAttachments[0]);
        }

        protected override void OnDispose()
        {
            initialPass?.Dispose();
            //shader.Program.Dispose();
            //denoise.Program.Dispose();
            //denoisePass?.Dispose();
        }
    }
}
