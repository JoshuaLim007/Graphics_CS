using Assimp;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JLGraphics
{
    public class Bloom : RenderPass
    {
        FrameBuffer[] temporaryRt;
        FrameBuffer[] blurTexture;
        FrameBuffer prepassFitlerRt;

        Shader bloomShader;
        Shader bloomCompositeShader;
        Shader bloomPrepassShader;

        int blurIterations = 7;
        float threshold = 10.0f;
        float intensity = 0.5f;
        float clamp = (1 << 16);

        public float Threshold {
            get => threshold;
            set
            {
                threshold = MathHelper.Clamp(value, 0.0f, float.MaxValue);
            }
        }
        public int Iterations {
            get => blurIterations;
            set
            {
                blurIterations = MathHelper.Clamp(value, 2, 11);
            }
        }
        public float Intensity 
        {
            get => intensity;
            set
            {
                intensity = MathHelper.Clamp(value, 0.0f, float.MaxValue);
            }
        }
        public float ClampValue
        {
            get => clamp;
            set
            {
                clamp = MathHelper.Clamp(value, 0.0f, float.MaxValue);
            }
        }

        public Bloom(int queueOffset) : base(RenderQueue.AfterTransparents, queueOffset)
        {
            var program = new ShaderProgram("Blur Program", "./Shaders/Bloom.glsl", "./Shaders/Passthrough.vert");
            program.CompileProgram();
            bloomShader = new Shader("Kawase Bloom", program);

            program = new ShaderProgram("Blur Program", "./Shaders/BloomComposite.glsl", "./Shaders/Passthrough.vert");
            program.CompileProgram();
            bloomCompositeShader = new Shader("Kawase Bloom Composite", program);

            program = new ShaderProgram("Blur Program", "./Shaders/BloomPrepass.glsl", "./Shaders/Passthrough.vert");
            program.CompileProgram();
            bloomPrepassShader = new Shader("Kawase Bloom Prepass", program);

            bloomShader.SetInt(Shader.GetShaderPropertyId("Horizontal"), 1);
            blurTexture = new FrameBuffer[blurIterations];
            temporaryRt = new FrameBuffer[blurIterations];
        }

        public override string Name => "Bloom Pass";
        protected override void OnDispose()
        {
            for (int i = 0; i < blurTexture.Length; i++)
            {
                blurTexture[i]?.Dispose();
                temporaryRt[i]?.Dispose();
            }
        }

        int previousWidth = 0;
        int previousHeight = 0;
        public override void Execute(in FrameBuffer frameBuffer)
        {
            if (intensity == 0)
            {
                return;
            }
            if(previousWidth != frameBuffer.Width || previousHeight != frameBuffer.Height)
            {
                previousWidth = frameBuffer.Width;
                previousHeight = frameBuffer.Height;
                var res = GetResolution(frameBuffer, 0.5f);
                var res2 = GetResolution(frameBuffer, 1.0f);
                if(prepassFitlerRt != null)
                {
                    prepassFitlerRt.Dispose();
                }
                prepassFitlerRt = new FrameBuffer(res2.X, res2.Y, false, new TFP()
                {
                    internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgb16f,
                    magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                    minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear,
                    maxMipmap = 0,
                    wrapMode = OpenTK.Graphics.OpenGL4.TextureWrapMode.MirroredRepeat,
                });
                for (int i = 0; i < blurIterations; i++)
                {
                    int width = MathHelper.Clamp(res.X >> i, 1, int.MaxValue);
                    int height = MathHelper.Clamp(res.Y >> i, 1, int.MaxValue);
                    if(blurTexture[i] != null)
                    {
                        blurTexture[i].Dispose();
                    }
                    blurTexture[i] = new FrameBuffer(width, height, false, new TFP()
                    {
                        internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgb16f,
                        magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                        minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear,
                        maxMipmap = 0,
                        wrapMode = OpenTK.Graphics.OpenGL4.TextureWrapMode.MirroredRepeat,
                    });
                    if (temporaryRt[i] != null)
                    {
                        temporaryRt[i].Dispose();
                    }
                    temporaryRt[i] = new FrameBuffer(width, height, false, new TFP()
                    {
                        internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgb16f,
                        magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                        minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear,
                        maxMipmap = 0,
                        wrapMode = OpenTK.Graphics.OpenGL4.TextureWrapMode.MirroredRepeat,
                    });
                }
            }

            bloomPrepassShader.SetFloat(Shader.GetShaderPropertyId("_BloomThreshold"), Threshold);
            bloomPrepassShader.SetFloat(Shader.GetShaderPropertyId("ClampValue"), ClampValue);
            Blit(frameBuffer, prepassFitlerRt, bloomPrepassShader);

            bloomShader.SetInt(Shader.GetShaderPropertyId("Horizontal"), 0);
            Blit(prepassFitlerRt, temporaryRt[0], bloomShader);
            bloomShader.SetInt(Shader.GetShaderPropertyId("Horizontal"), 1);
            Blit(temporaryRt[0], blurTexture[0], bloomShader);
            for (int i = 1; i < blurIterations; i++)
            {
                bloomShader.SetInt(Shader.GetShaderPropertyId("Horizontal"), 0);
                Blit(blurTexture[i - 1], temporaryRt[i], bloomShader);
                bloomShader.SetInt(Shader.GetShaderPropertyId("Horizontal"), 1);
                Blit(temporaryRt[i], blurTexture[i], bloomShader);
            }

            bloomCompositeShader.SetFloat(Shader.GetShaderPropertyId("intensity"), Intensity);
            bloomCompositeShader.SetInt(Shader.GetShaderPropertyId("doNormalize"), 0);
            bloomCompositeShader.SetInt(Shader.GetShaderPropertyId("iterations"), blurIterations);

            for (int i = blurIterations - 2; i >= 0; i--)
            {
                bloomCompositeShader.SetTexture(Shader.GetShaderPropertyId("HighResTex"), blurTexture[i].TextureAttachments[0]);
                Blit(blurTexture[i + 1], blurTexture[i], bloomCompositeShader);
            }

            bloomCompositeShader.SetTexture(Shader.GetShaderPropertyId("HighResTex"), frameBuffer.TextureAttachments[0]);
            bloomCompositeShader.SetInt(Shader.GetShaderPropertyId("doNormalize"), 1);
            Blit(blurTexture[0], frameBuffer, bloomCompositeShader);
        }
    }
}
