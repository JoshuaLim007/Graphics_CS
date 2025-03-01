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
    internal class AntiAliasing
    {
        static bool setup = false;
        static Shader shader = null;
        static FrameBuffer FXAABuffer = null;
        static Dictionary<Vector2i, FrameBuffer> frameBuffers = new Dictionary<Vector2i, FrameBuffer>();
        static void init(FrameBuffer frameBuffer)
        {
            if (!setup)
            {
                setup = true;
                var shaderProgram = new ShaderProgram("SSGI",
                    AssetLoader.GetPathToAsset("./Shaders/fxaa.frag"),
                    AssetLoader.GetPathToAsset("./Shaders/Passthrough.vert"));
                shaderProgram.CompileProgram();
                shader = new Shader("fxaa", shaderProgram);
            }

            var res = new Vector2i(frameBuffer.Width, frameBuffer.Height);
            if (frameBuffers.ContainsKey(res))
            {
                FXAABuffer = frameBuffers[res];
                return;
            }
            else
            {
                TFP tFP = TFP.Default;
                tFP.wrapMode = TextureWrapMode.MirroredRepeat;
                tFP.magFilter = TextureMagFilter.Linear;
                tFP.minFilter = TextureMinFilter.Linear;
                FXAABuffer = new FrameBuffer(frameBuffer.Width, frameBuffer.Height, false, tFP);
                frameBuffers.Add(res, FXAABuffer);
            }
        }
        public static FrameBuffer ApplyFXAA(in FrameBuffer frameBuffer)
        {
            init(frameBuffer);
            shader.SetInt(Shader.GetShaderPropertyId("mode"), 0);
            Graphics.Instance.Blit(frameBuffer, FXAABuffer, false, shader);
            return FXAABuffer;
        }
        public static FrameBuffer ApplyEdgeBlur(in FrameBuffer frameBuffer)
        {
            init(frameBuffer);
            shader.SetInt(Shader.GetShaderPropertyId("mode"), 1);
            Graphics.Instance.Blit(frameBuffer, FXAABuffer, false, shader);
            return FXAABuffer;
        }
    }
}
