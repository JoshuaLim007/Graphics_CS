using JLUtility;
using Microsoft.VisualBasic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace JLGraphics.RenderPasses
{
    public class SSAO : RenderPass
    {
        FrameBuffer SSAORt;
        FrameBuffer blurRT;
        FrameBuffer accumRT;
        Shader shader, accum;
        Shader blur, comp;
        public float Radius = 5.0f;
        public float Intensity = 1.0f;
        public float DepthRange = 5.0f;
        public int Samples = 16;
        const int maxAccum = 32;
        public bool TemporalAccumulation = false;

        public SSAO(int queueOffset) : base(RenderQueue.AfterTransparents, queueOffset)
        {
            var program = new ShaderProgram("SSAO program",
                AssetLoader.GetPathToAsset("./Shaders/SSAO.frag"),
                AssetLoader.GetPathToAsset("./Shaders/Passthrough.vert"));
            program.CompileProgram();
            shader = new Shader("SSAO", program);

            program = new ShaderProgram("SSAO blur",
                AssetLoader.GetPathToAsset("./Shaders/BoxBlur.frag"), AssetLoader.GetPathToAsset("./Shaders/Passthrough.vert"));
            program.CompileProgram();
            blur = new Shader("SSAO Blur", program);

            program = new ShaderProgram("SSAO comp", AssetLoader.GetPathToAsset("./Shaders/SSAOComp.frag"), AssetLoader.GetPathToAsset("./Shaders/Passthrough.vert"));
            program.CompileProgram();
            comp = new Shader("SSAO Comp", program);

            program = new ShaderProgram("SSAO accum", AssetLoader.GetPathToAsset("./Shaders/SSAOAccum.frag"), AssetLoader.GetPathToAsset("./Shaders/Passthrough.vert"));
            program.CompileProgram();
            accum = new Shader("SSAO Accum", program);

            GenerateNoiseTexture(noiseX, noiseY, noiseZ);
        }
        //64 slices of 4x4 noise texture
        const int noiseX = 8;
        const int noiseY = 8;
        const int noiseZ = 64;
        public override string Name => "SSAO";
        int previousWidth = 0;
        int previousHeight = 0;
        int accumulatedFrames = 0;

        FrameBuffer CreateBuffer(int width, int height)
        {
            return new FrameBuffer(width, height, false, new TFP()
            {
                internalFormat = PixelInternalFormat.R32f,
                magFilter = TextureMagFilter.Linear,
                minFilter = TextureMinFilter.Linear
            });
        }

        Texture noiseTexture = null;
        void GenerateNoiseTexture(int xSize, int ySize, int slices)
        {
            Random random = new Random();

            Vector3[] Noise = new Vector3[slices * xSize * ySize];

            for (int i = 0; i < slices * xSize * ySize; ++i)
            {
                Noise[i] = new Vector3(
                   random.NextSingle(),
                   random.NextSingle(),
                   random.NextSingle()
                );
            }

            if (noiseTexture == null)
            {
                noiseTexture = new Texture()
                {
                    generateMipMaps = false,
                    internalPixelFormat = PixelInternalFormat.Rgb,
                    Width = slices * xSize,
                    Height = ySize,
                    textureMinFilter = TextureMinFilter.Nearest,
                    textureMagFilter = TextureMagFilter.Nearest,
                    textureWrapMode = TextureWrapMode.Repeat
                };
                noiseTexture.ResolveTexture();
            }

            noiseTexture.SetPixels(Noise, PixelType.Float, PixelFormat.Rgb);
        }
        public override void Execute(in FrameBuffer frameBuffer)
        {
            if (Intensity == 0)
            {
                return;
            }
            if (previousHeight != frameBuffer.Height || previousWidth != frameBuffer.Width)
            {
                previousWidth = frameBuffer.Width;
                previousHeight = frameBuffer.Height;
                if (SSAORt != null)
                {
                    SSAORt.Dispose();
                    blurRT.Dispose();
                }
                var res = GetResolution(frameBuffer, 0.5f);
                var res1 = GetResolution(frameBuffer, 1.0f);
                SSAORt = CreateBuffer(res.X, res.Y);
                blurRT = CreateBuffer(res.X, res.Y);
                accumRT = CreateBuffer(res1.X, res1.Y);
            }

            shader.SetTexture(Shader.GetShaderPropertyId("noiseTexture"), noiseTexture);
            shader.SetVector3(Shader.GetShaderPropertyId("noiseSize"), new Vector3()
            {
                X = noiseX,
                Y = noiseY,
                Z = noiseZ
            });
            Samples = Math.Min(Samples, noiseZ);
            shader.SetInt(Shader.GetShaderPropertyId("samples"), Samples);
            shader.SetFloat(Shader.GetShaderPropertyId("Radius"), Radius);
            shader.SetFloat(Shader.GetShaderPropertyId("DepthRange"), DepthRange);
            comp.SetFloat(Shader.GetShaderPropertyId("Intensity"), Intensity);

            //calculate SSAO
            Blit(frameBuffer, SSAORt, shader);



            //accumulate results
            if (TemporalAccumulation)
            {
                accumulatedFrames = (int)MathF.Min(++accumulatedFrames, maxAccum);
                accum.SetInt(Shader.GetShaderPropertyId("AccumCount"), accumulatedFrames);
                accum.SetTexture(Shader.GetShaderPropertyId("AccumAO"), accumRT.TextureAttachments[0]);
                Blit(SSAORt, accumRT, accum);
            }

            //blur results
            blur.SetInt(Shader.GetShaderPropertyId("DoDepthCheck"), 1);
            blur.SetFloat(Shader.GetShaderPropertyId("MaxDepthDiff"), 1.0f);
            if (TemporalAccumulation)
            {
                Blit(accumRT, blurRT, blur);
            }
            else
            {
                Blit(SSAORt, blurRT, blur);
            }

            //compose it to original screen color
            comp.SetTexture(Shader.GetShaderPropertyId("AOTex"), blurRT.TextureAttachments[0]);
            Blit(frameBuffer, frameBuffer, comp);

            //generate new noise
            if (TemporalAccumulation)
            {
                GenerateNoiseTexture(noiseX, noiseY, noiseZ);
            }
        }

        protected override void OnDispose()
        {
            accumRT.Dispose();
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
