using JLUtility;
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
        public SSGI(int queueOffset) : base(RenderQueue.AfterTransparents, queueOffset)
        {
            var shaderProgram = new ShaderProgram("SSGI",
                AssetLoader.GetPathToAsset("./Shaders/SSGI.frag"),
                AssetLoader.GetPathToAsset("./Shaders/Passthrough.vert"));
            shaderProgram.CompileProgram();
            shader = new Shader("SSGI", shaderProgram);
        }

        public override string Name => "SSGI";

        FrameBuffer indirectLighting;
        public override void Execute(in FrameBuffer frameBuffer)
        {
            if(!FrameBuffer.AlikeResolution(indirectLighting, frameBuffer, 1.0f))
            {
                indirectLighting?.Dispose();
                indirectLighting = null;

                var res = GetScaledResolution(frameBuffer.Width, frameBuffer.Height, 1.0f);
                indirectLighting = new FrameBuffer(res.X, res.Y, false, new TFP
                {
                    internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgb16f,
                    maxMipmap = 0,
                    magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Nearest,
                    minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.Nearest,
                });
            }

            Blit(frameBuffer, indirectLighting, shader);
            Blit(indirectLighting, frameBuffer);
        }

        protected override void OnDispose()
        {

        }
    }
}
