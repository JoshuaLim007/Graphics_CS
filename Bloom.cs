using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public class Bloom : RenderPass
    {
        FrameBuffer rt;
        Shader blurShader;
        public Bloom() : base(RenderQueue.AfterTransparents, -1)
        {
            var program = new ShaderProgram("Blur Program", "./Shaders/Blur.glsl", "./Shaders/Passthrough.vert");
            program.CompileProgram();
            blurShader = new Shader("Blur Shader", program);
        }
        public override void Execute(in CommandBuffer cmd, in FrameBuffer frameBuffer)
        {
            if(rt == null)
            {
                float scale = 1.0f;
                var res = GetResolution(frameBuffer, scale);
                rt = new FrameBuffer(res.X, res.Y, false, new TFP()
                {
                    internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgb16f,
                    pixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat.Rgb,
                    magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                    minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear,
                    MaxMipmap = 0,
                    wrapMode = OpenTK.Graphics.OpenGL4.TextureWrapMode.Repeat,
                });
            }
            cmd.Blit(frameBuffer, rt, false);
            cmd.Blit(rt, frameBuffer, false, blurShader);
        }
    }
}
