using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public struct TFP
    {
        public PixelInternalFormat internalFormat;
        public PixelFormat pixelFormat;
        public TextureMinFilter minFilter;
        public TextureMagFilter magFilter;
        public TextureWrapMode wrapMode;
        public int MaxMipmap;
        public TFP()
        {
            internalFormat = PixelInternalFormat.Rgb8;
            pixelFormat = PixelFormat.Rgb;
            minFilter = TextureMinFilter.Nearest;
            magFilter = TextureMagFilter.Nearest;
            wrapMode = TextureWrapMode.Repeat;
            MaxMipmap = 0;
        }
        public TFP(PixelInternalFormat pixelInternalFormat, PixelFormat pixelFormat)
        {
            minFilter = TextureMinFilter.Nearest;
            magFilter = TextureMagFilter.Nearest;
            wrapMode = TextureWrapMode.Repeat;
            MaxMipmap = 0;

            internalFormat = pixelInternalFormat;
            this.pixelFormat = pixelFormat;
        }
    }
    public class FrameBuffer : IDisposable, IGlobalScope
    {
        internal Texture[] ColorAttachments { get; } = null;
        internal int FrameBufferObject { get; } = 0;
        internal int RenderBufferObject { get; } = 0;
        public int Width { get; } = 0;
        public int Height { get; } = 0;

        public FrameBuffer(int width, int height, bool enableDepthRenderBuffer = false, params TFP[] textureFormat)
        {
            Width = width;
            Height = height;
            int colorAttachmentCount = MathHelper.Clamp(textureFormat.Length, 0, 16);
            FrameBufferObject = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FrameBufferObject);

            ColorAttachments = new Texture[colorAttachmentCount];
            for (int i = 0; i < colorAttachmentCount; i++)
            {
                ColorAttachments[i] = new Texture();
                ColorAttachments[i].Width = width;
                ColorAttachments[i].Height = height;
                ColorAttachments[i].internalPixelFormat = textureFormat[i].internalFormat;
                ColorAttachments[i].pixelFormat = textureFormat[i].pixelFormat;
                ColorAttachments[i].textureMagFilter = textureFormat[i].magFilter;
                ColorAttachments[i].textureMinFilter = textureFormat[i].minFilter;
                ColorAttachments[i].textureWrapMode = textureFormat[i].wrapMode;
                ColorAttachments[i].MipmapLevels = textureFormat[i].MaxMipmap;
                ColorAttachments[i].generateMipMaps = textureFormat[i].MaxMipmap != 0;
                ColorAttachments[i].ResolveTexture();
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + i, TextureTarget.Texture2D, ColorAttachments[i].GlTextureID, 0);
            }

            if (enableDepthRenderBuffer)
            {
                RenderbufferStorage renderbufferStorage = RenderbufferStorage.DepthComponent;
                RenderBufferObject = GL.GenRenderbuffer();
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, RenderBufferObject);
                GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, renderbufferStorage, width, height);
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
                GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, RenderBufferObject);
            }


            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, FrameBufferObject);
            if (colorAttachmentCount == 0)
            {
                GL.DrawBuffer(DrawBufferMode.None);
            }
            else
            {
                DrawBuffersEnum[] attachments = new DrawBuffersEnum[colorAttachmentCount];// { DrawBuffersEnum.ColorAttachment0 };
                for (int i = 0; i < colorAttachmentCount; i++)
                {
                    attachments[i] = DrawBuffersEnum.ColorAttachment0 + i;
                }
                GL.DrawBuffers(attachments.Length, attachments);
            }

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, FrameBufferObject);
            GL.ReadBuffer(ReadBufferMode.None);

            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("ERROR::FRAMEBUFFER:: Framebuffer is not complete!");
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }
        public void Dispose()
        {
            GL.DeleteFramebuffer(FrameBufferObject);
            GL.DeleteRenderbuffer(RenderBufferObject);
            for (int i = 0; i < ColorAttachments.Length; i++)
            {
                ColorAttachments[i].Dispose();
            }
            //GL.DeleteTexture(DepthBufferTextureId);
        }
    }
}
