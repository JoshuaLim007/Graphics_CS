using Assimp;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        int blurIterations = 8;
        bool initializeRts = false;
        float threshold = 50.0f;
        float intensity = 1.0f;
        float clamp = 200.0f;

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

        public Bloom() : base(RenderQueue.AfterTransparents, -1)
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

            bloomShader.SetInt("Horizontal", 1);
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
        public override void Execute(in FrameBuffer frameBuffer)
        {
            if(intensity == 0)
            {
                return;
            }
            if(!initializeRts)
            {
                initializeRts = true;
                var res = GetResolution(frameBuffer, 0.5f);
                prepassFitlerRt = new FrameBuffer(frameBuffer.Width, frameBuffer.Height, false, new TFP()
                {
                    internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgb16f,
                    pixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat.Rgb,
                    magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                    minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear,
                    MaxMipmap = 0,
                    wrapMode = OpenTK.Graphics.OpenGL4.TextureWrapMode.MirroredRepeat,
                });
                for (int i = 0; i < blurIterations; i++)
                {
                    int width = MathHelper.Clamp(res.X >> i, 1, int.MaxValue);
                    int height = MathHelper.Clamp(res.Y >> i, 1, int.MaxValue);
                    blurTexture[i] = new FrameBuffer(width, height, false, new TFP()
                    {
                        internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgb16f,
                        pixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat.Rgb,
                        magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                        minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear,
                        MaxMipmap = 0,
                        wrapMode = OpenTK.Graphics.OpenGL4.TextureWrapMode.MirroredRepeat,
                    });

                    temporaryRt[i] = new FrameBuffer(width, height, false, new TFP()
                    {
                        internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgb16f,
                        pixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat.Rgb,
                        magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                        minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear,
                        MaxMipmap = 0,
                        wrapMode = OpenTK.Graphics.OpenGL4.TextureWrapMode.MirroredRepeat,
                    });
                }
            }

            Vector4 threshold = new Vector4();
            threshold.X = Threshold;
            threshold.Y = threshold.X * 0.0f;
            threshold.Z = 2f * threshold.Y;
            threshold.W = 0.25f / (threshold.W + 0.00001f);
            threshold.Y -= threshold.X;
            bloomPrepassShader.SetVector4("_BloomThreshold", threshold);
            bloomPrepassShader.SetFloat("ClampValue", ClampValue);
            Blit(frameBuffer, prepassFitlerRt, bloomPrepassShader);

            bloomShader.SetInt("Horizontal", 0);
            Blit(prepassFitlerRt, temporaryRt[0], bloomShader);
            bloomShader.SetInt("Horizontal", 1);
            Blit(temporaryRt[0], blurTexture[0], bloomShader);

            for (int i = 1; i < blurIterations; i++)
            {
                bloomShader.SetInt("Horizontal", 0);
                Blit(blurTexture[i - 1], temporaryRt[i], bloomShader);
                bloomShader.SetInt("Horizontal", 1);
                Blit(temporaryRt[i], blurTexture[i], bloomShader);
            }

            bloomCompositeShader.SetFloat("intensity", Intensity);
            for (int i = blurIterations - 2; i >= 0; i--)
            {
                bloomCompositeShader.SetTexture("HighResTex", blurTexture[i].ColorAttachments[0]);
                Blit(blurTexture[i + 1], blurTexture[i], bloomCompositeShader);
            }

            bloomCompositeShader.SetTexture("HighResTex", frameBuffer.ColorAttachments[0]);
            Blit(blurTexture[0], frameBuffer, bloomCompositeShader);
        }
    }
}
