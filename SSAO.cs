using OpenTK.Graphics.OpenGL4;
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
        FrameBuffer SSAORt;
        FrameBuffer blurRT;
        Shader shader;
        Shader blur, comp;
        public float Radius = 12.0f;
        public float Intensity = 1.0f;
        public float DepthRange = 5.0f;
        public int Samples = 32;
        Texture noiseTexture;
        Vector3[] kernalSample;

        public SSAO(int queueOffset) : base(RenderQueue.AfterTransparents, queueOffset)
        {
            var program = new ShaderProgram("SSAO program", "./Shaders/SSAO.frag", "./Shaders/Passthrough.vert");
            program.CompileProgram();
            shader = new Shader("SSAO", program);

            program = new ShaderProgram("SSAO blur", "./Shaders/BoxBlur.frag", "./Shaders/Passthrough.vert");
            program.CompileProgram();
            blur = new Shader("SSAO Blur", program);

            program = new ShaderProgram("SSAO comp", "./Shaders/SSAOComp.frag", "./Shaders/Passthrough.vert");
            program.CompileProgram();
            comp = new Shader("SSAO Comp", program);

            noiseTexture = GenerateNoiseTexture(4 * 4);
            shader.SetTexture(Shader.GetShaderPropertyId("texRandom"), noiseTexture);
        }

        public override string Name => "SSAO";
        int previousWidth = 0;
        int previousHeight = 0;

        FrameBuffer CreateBuffer(int width, int height)
        {
            return new FrameBuffer(width, height, false, new TFP()
            {
                internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgb32f,
                magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear
            });
        }
        Vector3[] GenerateSampleKernal(int size)
        {
            Vector3[] SampleKernal = new Vector3[size];
            for (int i = 0; i < size; i++)
            {
                SampleKernal[i] = new Vector3()
                {
                    X = Random.Shared.NextSingle() * 2 - 1,
                    Y = Random.Shared.NextSingle() * 2 - 1,
                    Z = Random.Shared.NextSingle(),
                };
                SampleKernal[i].Normalize();

                float scale = i / size;
                scale = MathHelper.Lerp(0.1f, 1.0f, scale * scale);
                SampleKernal[i] *= scale;
            }

            return SampleKernal;
        }
        Texture GenerateNoiseTexture(int size)
        {
            Random random = new Random();

            size = MathHelper.NextPowerOfTwo(size);
            int sqrtSize = (int)MathHelper.Sqrt(size);
            Vector3[] Noise = new Vector3[size];

            for (int i = 0; i < size; ++i)
            {
                Noise[i] = new Vector3(
                   random.NextSingle() * 2 - 1,
                   random.NextSingle() * 2 - 1,
                   0.0f
                );
            }

            Texture texture = new Texture()
            {
                generateMipMaps = false,
                internalPixelFormat = PixelInternalFormat.Rgb,
                Width = sqrtSize,
                Height = sqrtSize,
                textureMinFilter = TextureMinFilter.Nearest,
                textureMagFilter = TextureMagFilter.Nearest,
                textureWrapMode = TextureWrapMode.Repeat
            };
            texture.ResolveTexture();
            texture.SetPixels(Noise, PixelType.Float, PixelFormat.Rgb);
            return texture;
        }
        int previousKernalSize = 0;
        public override void Execute(in FrameBuffer frameBuffer)
        {
            if (Intensity == 0)
            {
                return;
            }
            if(previousHeight != frameBuffer.Height || previousWidth != frameBuffer.Width)
            {
                previousWidth = frameBuffer.Width;
                previousHeight = frameBuffer.Height;
                if(SSAORt != null)
                {
                    SSAORt.Dispose();
                    blurRT.Dispose();
                }
                var res = GetResolution(frameBuffer, 0.5f);
                SSAORt = CreateBuffer(res.X, res.Y);
                blurRT = CreateBuffer(res.X, res.Y);
            }
            Samples = MathHelper.Clamp(Samples, 1, 64);
            shader.SetVector2(Shader.GetShaderPropertyId("noiseScale"), new Vector2()
            {
                X = SSAORt.Width / noiseTexture.Width,
                Y = SSAORt.Height / noiseTexture.Height,
            });
            shader.SetInt(Shader.GetShaderPropertyId("samples"), Samples);
            shader.SetFloat(Shader.GetShaderPropertyId("Radius"), Radius);
            shader.SetFloat(Shader.GetShaderPropertyId("DepthRange"), DepthRange);
            comp.SetFloat(Shader.GetShaderPropertyId("Intensity"), Intensity);

            if (Samples != previousKernalSize)
            {
                kernalSample = GenerateSampleKernal(Samples);
                for (int i = 0; i < kernalSample.Length; i++)
                {
                    shader.SetVector3(Shader.GetShaderPropertyId("sampleKernal[" + i + "]"), kernalSample[i]);
                }
                previousKernalSize = Samples;
            }

            Blit(frameBuffer, SSAORt, shader);
            Blit(SSAORt, blurRT, blur);
            comp.SetTexture(Shader.GetShaderPropertyId("AOTex"), blurRT.TextureAttachments[0]);
            Blit(frameBuffer, frameBuffer, comp);
        }

        protected override void OnDispose()
        {
            noiseTexture.Dispose();
            SSAORt.Dispose();
            blurRT.Dispose();
            shader.Program.Dispose();
            comp.Program.Dispose();
            blur.Program.Dispose();
            shader = null;
        }
    }
}
