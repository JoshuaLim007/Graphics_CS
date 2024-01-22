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
        internal static int DepthBufferTextureId { get; private set; }
        static int instanceCounts = 0;

        public RenderTexture(int width, int height, bool enableDepth, PixelInternalFormat pixelInternalFormat, PixelFormat pixelFormat)
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

            if (enableDepth)
            {
                DepthBufferTextureId = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, DepthBufferTextureId);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32f, width, height, 0, PixelFormat.Red, PixelType.Float, IntPtr.Zero);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (float)All.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (float)All.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (float)All.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (float)All.Repeat);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, DepthBufferTextureId, 0); 
            }

            RenderbufferStorage renderbufferStorage = RenderbufferStorage.Depth24Stencil8;
            RenderBufferObject = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, RenderBufferObject);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, renderbufferStorage, Width, Height);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, RenderBufferObject);

            DrawBuffersEnum[] attachments = { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1 };
            GL.DrawBuffers(attachments.Length, attachments);

            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("ERROR::FRAMEBUFFER:: Framebuffer is not complete!");
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            instanceCounts++;
        }
        public override void Dispose()
        {
            GL.DeleteFramebuffer(FrameBufferObject);
            GL.DeleteTexture(GlTextureID);
            GL.DeleteTexture(DepthBufferTextureId);
            GL.DeleteRenderbuffer(RenderBufferObject);
            instanceCounts--;
        }
    }
}
