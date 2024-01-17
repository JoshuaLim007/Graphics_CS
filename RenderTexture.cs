using Assimp;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public class RenderTexture : Texture
    {
        internal int FrameBufferObject { get; }
        internal int RenderBufferObject { get; }

        public RenderTexture(int width, int height, bool enableDepthStencil, PixelInternalFormat pixelInternalFormat, PixelFormat pixelFormat)
        {
            Width = width;
            Height = height;
            this.internalPixelFormat = pixelInternalFormat;
            this.pixelFormat = pixelFormat;

            FrameBufferObject = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FrameBufferObject);

            //generate color texture
            ResolveTexture();

            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, GlTextureID, 0);

            if (enableDepthStencil)
            {
                RenderbufferStorage renderbufferStorage = RenderbufferStorage.Depth24Stencil8;
                RenderBufferObject = GL.GenRenderbuffer();
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, RenderBufferObject);
                GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, renderbufferStorage, Width, Height);
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
                GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, RenderBufferObject);
            }


            DrawBuffersEnum[] attachments = { DrawBuffersEnum.ColorAttachment0 };
            GL.DrawBuffers(1, attachments);

            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("ERROR::FRAMEBUFFER:: Framebuffer is not complete!");
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }
        public override void Dispose()
        {
            GL.DeleteFramebuffer(FrameBufferObject);
            GL.DeleteTexture(GlTextureID);
            GL.DeleteRenderbuffer(RenderBufferObject);
        }
    }
}
