using JLUtility;
using OpenTK.Compute.OpenCL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics.RenderPasses
{
    public class SSGI : RenderPass
    {
        Shader shader;
        Shader accum;
        Shader denoise;
        public SSGI(int queueOffset) : base(RenderQueue.AfterTransparents, queueOffset)
        {
            var shaderProgram = new ShaderProgram("SSGI",
                AssetLoader.GetPathToAsset("./Shaders/SSGI.frag"),
                AssetLoader.GetPathToAsset("./Shaders/Passthrough.vert"));
            shaderProgram.CompileProgram();
            shader = new Shader("SSGI", shaderProgram);

            shaderProgram = new ShaderProgram("SSAO accum", AssetLoader.GetPathToAsset("./Shaders/SSAOAccum.frag"), AssetLoader.GetPathToAsset("./Shaders/Passthrough.vert"));
            shaderProgram.CompileProgram();
            accum = new Shader("SSGI Accum", shaderProgram);
            accum.SetBool(Shader.GetShaderPropertyId("ClearOnInvalidate"), true);
            accum.SetVector4(Shader.GetShaderPropertyId("ClearColor"), Vector4.Zero);

            shaderProgram = new ShaderProgram("SSAO accum", AssetLoader.GetPathToAsset("./Shaders/Denoise.frag"), AssetLoader.GetPathToAsset("./Shaders/Passthrough.vert"));
            shaderProgram.CompileProgram();
            denoise = new Shader("Denoise", shaderProgram);
        }

        public override string Name => "SSGI";

        FrameBuffer initialPass, accumulationPass, denoisePass;
        int accumulatedFrames = 0;
        int maxAccum = 64;
        Vector3 lastCamPos;
        Quaternion lastCamRot;

        public int SamplesPerPixel { get; set; } = 1;
        public bool FarRangeSSGI { get; set; } = false;

        public override void FrameSetup(Camera camera)
        {
            if(camera.Transform.LocalPosition != lastCamPos)
            {
                lastCamPos = camera.Transform.LocalPosition;
            }
            if (camera.Transform.LocalRotation != lastCamRot)
            {
                lastCamRot = camera.Transform.LocalRotation;
            }
        }
        public override void Execute(in FrameBuffer frameBuffer)
        {
            float scale = 0.5f;
            if(!FrameBuffer.AlikeResolution(initialPass, frameBuffer, scale))
            {
                initialPass?.Dispose();
                accumulationPass?.Dispose();
                denoisePass?.Dispose();

                var res = GetScaledResolution(frameBuffer.Width, frameBuffer.Height, scale);
                initialPass = new FrameBuffer(res.X, res.Y, false, new TFP
                {
                    internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgb16f,
                    maxMipmap = 0,
                    magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                    minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear,
                });
                accumulationPass = new FrameBuffer(res.X, res.Y, false, new TFP
                {
                    internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgb16f,
                    maxMipmap = 0,
                    magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                    minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear,
                });
                denoisePass = new FrameBuffer(res.X, res.Y, false, new TFP
                {
                    internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgb16f,
                    maxMipmap = 0,
                    magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                    minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear,
                });
            }
            shader.SetInt(Shader.GetShaderPropertyId("SamplesPerPixel"), SamplesPerPixel);
            shader.SetBool(Shader.GetShaderPropertyId("FarRangeSSGI"), FarRangeSSGI);
            Blit(frameBuffer, initialPass, shader);

            accumulatedFrames = (int)MathF.Min(++accumulatedFrames, maxAccum);
            accum.SetInt(Shader.GetShaderPropertyId("AccumCount"), accumulatedFrames);
            accum.SetTexture(Shader.GetShaderPropertyId("PrevMainTex"), accumulationPass.TextureAttachments[0]);
            Blit(initialPass, accumulationPass, accum);
            Blit(accumulationPass, denoisePass, denoise);

            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("_SSGIColor"), denoisePass.TextureAttachments[0]);
            FrameBuffer.BindFramebuffer(frameBuffer);
        }

        protected override void OnDispose()
        {
            initialPass?.Dispose();
            accumulationPass?.Dispose();
            shader.Program.Dispose();
            accum.Program.Dispose();
            denoise.Program.Dispose();
            denoisePass?.Dispose();
        }
    }
}
