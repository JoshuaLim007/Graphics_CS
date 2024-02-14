using JLUtility;
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
        public Vector4 borderColor;
        public int maxMipmap;
        public bool isShadowMap;

        public TFP()
        {
            internalFormat = PixelInternalFormat.Rgb8;
            pixelFormat = PixelFormat.Rgb;
            minFilter = TextureMinFilter.Nearest;
            magFilter = TextureMagFilter.Nearest;
            wrapMode = TextureWrapMode.Repeat;
            borderColor = Vector4.Zero;
            maxMipmap = 0;
        }
        public TFP(PixelInternalFormat pixelInternalFormat, PixelFormat pixelFormat)
        {
            borderColor = Vector4.Zero;
            minFilter = TextureMinFilter.Nearest;
            magFilter = TextureMagFilter.Nearest;
            wrapMode = TextureWrapMode.Repeat;
            maxMipmap = 0;

            internalFormat = pixelInternalFormat;
            this.pixelFormat = pixelFormat;
        }
    }
    public class FrameBuffer : SafeDispose, IGlobalScope
    {
        internal Texture[] TextureAttachments { get; } = null;
        internal int FrameBufferObject { get; } = 0;
        internal int RenderBufferObject { get; } = 0;
        public int Width { get; } = 0;
        public int Height { get; } = 0;
        public override string Name => "FrameBuffer:" + FrameBufferObject;

        public FrameBuffer(int width, int height, bool enableDepthRenderBuffer = false, params TFP[] textureFormat)
        {
            Width = width;
            Height = height;
            int colorAttachmentCount = MathHelper.Clamp(textureFormat.Length, 0, 16);
            FrameBufferObject = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FrameBufferObject);

            TextureAttachments = new Texture[colorAttachmentCount];
            for (int i = 0; i < colorAttachmentCount; i++)
            {
                TextureAttachments[i] = new Texture();
                TextureAttachments[i].Width = width;
                TextureAttachments[i].Height = height;
                TextureAttachments[i].internalPixelFormat = textureFormat[i].internalFormat;
                TextureAttachments[i].pixelFormat = textureFormat[i].pixelFormat;
                TextureAttachments[i].textureMagFilter = textureFormat[i].magFilter;
                TextureAttachments[i].textureMinFilter = textureFormat[i].minFilter;
                TextureAttachments[i].textureWrapMode = textureFormat[i].wrapMode;
                TextureAttachments[i].mipmapLevels = textureFormat[i].maxMipmap;
                TextureAttachments[i].generateMipMaps = textureFormat[i].maxMipmap != 0;
                TextureAttachments[i].borderColor = textureFormat[i].borderColor;
                TextureAttachments[i].ResolveTexture(textureFormat[i].isShadowMap);
                if (textureFormat[i].pixelFormat == PixelFormat.DepthComponent || textureFormat[i].pixelFormat == PixelFormat.DepthStencil)
                {
                    GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, TextureAttachments[i].GlTextureID, 0);
                }
                else
                {
                    GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + i, TextureTarget.Texture2D, TextureAttachments[i].GlTextureID, 0);
                }
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
            GL.DrawBuffer(DrawBufferMode.None);

            if(colorAttachmentCount != 0)
            {
                List<DrawBuffersEnum> attachments = new List<DrawBuffersEnum>(colorAttachmentCount);// { DrawBuffersEnum.ColorAttachment0 };
                for (int i = 0; i < colorAttachmentCount; i++)
                {
                    if (TextureAttachments[i].pixelFormat == PixelFormat.DepthComponent || TextureAttachments[i].pixelFormat == PixelFormat.DepthStencil)
                    {
                        continue;
                    }
                    attachments.Add(DrawBuffersEnum.ColorAttachment0 + i);
                }
                if(attachments.Count > 0)
                {
                    GL.DrawBuffers(attachments.Count, attachments.ToArray());
                }
            }

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, FrameBufferObject);
            GL.ReadBuffer(ReadBufferMode.None);

            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                Debug.Log("Framebuffer is not complete!", Debug.Flag.Error);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }
        protected override void OnDispose()
        {
            GL.DeleteFramebuffer(FrameBufferObject);
            GL.DeleteRenderbuffer(RenderBufferObject);
            for (int i = 0; i < TextureAttachments.Length; i++)
            {
                TextureAttachments[i].Dispose();
            }
        }
        //public void Dispose()
        //{
        //    GL.DeleteFramebuffer(FrameBufferObject);
        //    GL.DeleteRenderbuffer(RenderBufferObject);
        //    for (int i = 0; i < ColorAttachments.Length; i++)
        //    {
        //        ColorAttachments[i].Dispose();
        //    }
        //    //GL.DeleteTexture(DepthBufferTextureId);
        //}
    }
}
