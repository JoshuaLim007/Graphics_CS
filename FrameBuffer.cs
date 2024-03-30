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
        public TextureMinFilter minFilter;
        public TextureMagFilter magFilter;
        public TextureWrapMode wrapMode;
        public Vector4 borderColor;
        public int maxMipmap;
        public bool isShadowMap;
        public static TFP Default => new TFP()
        {
            internalFormat = PixelInternalFormat.Rgb16f,
            minFilter = TextureMinFilter.Nearest,
            magFilter = TextureMagFilter.Nearest,
            wrapMode = TextureWrapMode.Repeat,
            borderColor = Vector4.Zero,
            maxMipmap = 0,
        };

        public TFP()
        {
            internalFormat = PixelInternalFormat.Rgb16f;
            minFilter = TextureMinFilter.Nearest;
            magFilter = TextureMagFilter.Nearest;
            wrapMode = TextureWrapMode.Repeat;
            borderColor = Vector4.Zero;
            maxMipmap = 0;
        }
    }
    public class FrameBuffer : SafeDispose, IGlobalScope
    {
        internal Texture[] TextureAttachments { get; } = null;
        DrawBuffersEnum[] ColorAttachmentsEnums = null;
        internal int FrameBufferObject { get; } = 0;
        internal int RenderBufferObject { get; } = 0;
        public int Width { get; } = 0;
        public int Height { get; } = 0;
        public override string Name => "FrameBuffer:" + FrameBufferObject + " " + NameAddon;
        public string NameAddon { get; private set; } = "";
        public void SetName(string name)
        {
            NameAddon = name;
        }
        static bool IsDepthComponent(PixelInternalFormat internalPixelFormat)
        {
            return
                internalPixelFormat == PixelInternalFormat.DepthComponent
                || internalPixelFormat == PixelInternalFormat.Depth24Stencil8
                || internalPixelFormat == PixelInternalFormat.Depth32fStencil8
                || internalPixelFormat == PixelInternalFormat.DepthComponent16
                || internalPixelFormat == PixelInternalFormat.DepthComponent16Sgix
                || internalPixelFormat == PixelInternalFormat.DepthComponent24
                || internalPixelFormat == PixelInternalFormat.DepthComponent24Sgix
                || internalPixelFormat == PixelInternalFormat.DepthComponent32
                || internalPixelFormat == PixelInternalFormat.DepthComponent32f
                || internalPixelFormat == PixelInternalFormat.DepthComponent32Sgix
                || internalPixelFormat == PixelInternalFormat.DepthStencil;
        }
        public FrameBuffer(int width, int height, bool enableDepthRenderBuffer = false, params TFP[] textureFormat) : this(width, height, 0, enableDepthRenderBuffer, textureFormat)
        {
            
        }
        public FrameBuffer(int width, int height, int MSAA, bool enableDepthRenderBuffer = false, params TFP[] textureFormat)
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
                TextureAttachments[i].textureTarget = MSAA > 0 ? TextureTarget.Texture2DMultisample : TextureTarget.Texture2D;
                TextureAttachments[i].Width = width;
                TextureAttachments[i].Height = height;
                TextureAttachments[i].internalPixelFormat = textureFormat[i].internalFormat;
                TextureAttachments[i].textureMagFilter = textureFormat[i].magFilter;
                TextureAttachments[i].textureMinFilter = textureFormat[i].minFilter;
                TextureAttachments[i].textureWrapMode = textureFormat[i].wrapMode;
                TextureAttachments[i].mipmapLevels = textureFormat[i].maxMipmap;
                TextureAttachments[i].generateMipMaps = textureFormat[i].maxMipmap != 0;
                TextureAttachments[i].borderColor = textureFormat[i].borderColor;
                TextureAttachments[i].ResolveTexture(textureFormat[i].isShadowMap, MSAA);
                if (IsDepthComponent(TextureAttachments[i].internalPixelFormat))
                {
                    GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureAttachments[i].textureTarget, TextureAttachments[i].GlTextureID, 0);
                }
                else
                {
                    FramebufferAttachment framebufferAttachment = FramebufferAttachment.ColorAttachment0 + i;
                    GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, framebufferAttachment, TextureAttachments[i].textureTarget, TextureAttachments[i].GlTextureID, 0);
                }
            }

            if (enableDepthRenderBuffer)
            {
                RenderbufferStorage renderbufferStorage = RenderbufferStorage.DepthComponent;
                RenderBufferObject = GL.GenRenderbuffer();
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, RenderBufferObject);

                if(MSAA > 0)
                    GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, MSAA, renderbufferStorage, width, height);
                else
                    GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, renderbufferStorage, width, height);

                GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, RenderBufferObject);
            }


            GL.DrawBuffer(DrawBufferMode.None);

            if(colorAttachmentCount != 0)
            {
                List<DrawBuffersEnum> attachments = new List<DrawBuffersEnum>(colorAttachmentCount);
                for (int i = 0; i < colorAttachmentCount; i++)
                {
                    if (IsDepthComponent(TextureAttachments[i].internalPixelFormat))
                    {
                        continue;
                    }
                    attachments.Add(DrawBuffersEnum.ColorAttachment0 + i);
                }
                if(attachments.Count > 0)
                {
                    ColorAttachmentsEnums = attachments.ToArray();
                }
            }

            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                Debug.Log("Framebuffer is not complete!", Debug.Flag.Error);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }
        public static void BindFramebuffer(FrameBuffer frameBuffer)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBuffer.FrameBufferObject);
            GL.DrawBuffers(frameBuffer.ColorAttachmentsEnums.Length, frameBuffer.ColorAttachmentsEnums);
            GL.ReadBuffer(ReadBufferMode.None);
            GL.Viewport(0, 0, frameBuffer.Width, frameBuffer.Height);
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

        public static FrameBuffer Copy(FrameBuffer src, float resolutionScale, bool enableDepthRenderBuffer = false)
        {
            FrameBuffer frameBuffer1;
            var tfp = TFP.Default;
            tfp.internalFormat = src.TextureAttachments[0].internalPixelFormat;
            tfp.magFilter = src.TextureAttachments[0].textureMagFilter;
            tfp.minFilter = src.TextureAttachments[0].textureMinFilter;
            tfp.maxMipmap = src.TextureAttachments[0].mipmapLevels;
            tfp.wrapMode = src.TextureAttachments[0].textureWrapMode;
            tfp.isShadowMap = false;
            tfp.borderColor = src.TextureAttachments[0].borderColor;
            int width = (int)MathHelper.Ceiling(src.TextureAttachments[0].Width * resolutionScale);
            int height = (int)MathHelper.Ceiling(src.TextureAttachments[0].Height * resolutionScale);
            frameBuffer1 = new FrameBuffer(width, height, enableDepthRenderBuffer, tfp);
            return frameBuffer1;
        }
        public static bool AlikeResolution(FrameBuffer f1, FrameBuffer f2, float f1_resolutionInvScale = 1.0f)
        {
            if (f1 == null && f2 != null)
            {
                return false;
            }
            if (f2 == null && f1 != null)
            {
                return false;
            }
            if (f2 == null && f1 == null)
            {
                return true;
            }
            bool resolutionCheck = f1.Width * f1_resolutionInvScale == f2.Width && f1.Height * f1_resolutionInvScale == f2.Height;
            if (!resolutionCheck)
            {
                return false;
            }
            return true;
        }
        public static bool Alike(FrameBuffer f1, FrameBuffer f2, float f1_resolutionScale = 1.0f)
        {
            if(f1 == null && f2 != null)
            {
                return false;
            }
            if(f2 == null && f1 != null)
            {
                return false;
            }
            if(f2 == null && f1 == null)
            {
                return true;
            }
            bool resolutionCheck = f1.Width * f1_resolutionScale == f2.Width && f1.Height * f1_resolutionScale == f2.Height;
            if (!resolutionCheck)
            {
                return false;
            }
            bool textureAmountLenghtCheck = f1.TextureAttachments.Length == f2.TextureAttachments.Length;
            if (textureAmountLenghtCheck)
            {
                for (int i = 0; i < f1.TextureAttachments.Length; i++)
                {
                    bool textureCheck1 = Texture.Alike(f1.TextureAttachments[i], f2.TextureAttachments[i], f1_resolutionScale);
                    if(!textureCheck1)
                    {
                        return false;
                    }
                }
            }
            else
            {
                return false;
            }

            return true;
        }
    }
}
