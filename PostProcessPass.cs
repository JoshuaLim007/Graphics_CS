using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;

namespace JLGraphics
{
    public class PostProcessPass : RenderPass
    {
        public float FogDensity = .002f;
        public Vector3 FogColor = new Vector3(1, 1, 1);
        public bool Tonemapping = true;
        public bool GammaCorrection = true;

        Shader shader = null;
        ShaderProgram PostProcessShader = null;
        FrameBuffer postProcessTexture = null;
        public PostProcessPass() : base(RenderQueue.AfterTransparents, 100)
        {
            PostProcessShader = new ShaderProgram("PostProcess", "./Shaders/PostProcess.frag", "./Shaders/Passthrough.vert");
            if (Graphics.GetFileTracker(out var ft))
            {
                ft.AddFileObject(PostProcessShader.FragFile);
            }
            PostProcessShader.CompileProgram();

            shader = new Shader("Postprocessing material", PostProcessShader);
            shader.SetFloat("FogDensity", .002f);
            shader.SetVector3("FogColor", new Vector3(1, 1, 1));
            postProcessTexture = new FrameBuffer(Graphics.Window.Size.X, Graphics.Window.Size.Y, false, new TFP(PixelInternalFormat.Rgb16f, PixelFormat.Rgb));
        }
        public override void Execute(in CommandBuffer cmd, in FrameBuffer frameBuffer)
        {
            shader.SetFloat("FogDensity", FogDensity);
            shader.SetVector3("FogColor", FogColor);
            shader.SetBool("Tonemapping", Tonemapping);
            shader.SetBool("GammaCorrection", GammaCorrection);
            if (Graphics.Window.Size != new Vector2i(postProcessTexture.Width, postProcessTexture.Height))
            {
                postProcessTexture.Dispose();
                postProcessTexture = new FrameBuffer(Graphics.Window.Size.X, Graphics.Window.Size.Y, false, new TFP(PixelInternalFormat.Rgb16f, PixelFormat.Rgb));
            }
            cmd.Blit(frameBuffer, postProcessTexture, true, shader);
            cmd.Blit(postProcessTexture, frameBuffer, false);
        }
        public override void Dispose()
        {
            postProcessTexture.Dispose();
        }
    }
}
