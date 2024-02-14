using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using StbImageSharp;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace JLGraphics
{
    public struct TextureObject 
    {
        public PixelFormat pixelFormat { get; private set; }
        public PixelInternalFormat internalPixelFormat { get; private set; }
        public TextureWrapMode textureWrapMode { get; private set; }
        public TextureMinFilter textureMinFilter { get; private set; }
        public TextureMagFilter textureMagFilter { get; private set; }
        public bool generateMipMaps { get; private set; }
        public int width { get; private set; }
        public int height { get; private set; }

        public int ID { get; private set; }
        public static TextureObject CreateTexture(
            int height, 
            int width, 
            PixelFormat pixelFormat, 
            PixelInternalFormat pixelInternalFormat,
            TextureWrapMode textureWrapMode,
            TextureMinFilter textureMinFilter, 
            TextureMagFilter textureMagFilter,
            bool generateMipMaps)
        {
            var textureObject = new TextureObject()
            {
                pixelFormat = pixelFormat,
                internalPixelFormat = pixelInternalFormat,
                textureWrapMode = textureWrapMode,
                textureMinFilter = textureMinFilter,
                textureMagFilter = textureMagFilter,
                generateMipMaps = generateMipMaps,
                width = width,
                height = height,
            };
            textureObject.ID = GL.GenTexture();
            return textureObject;
        }
        public static void DestroyTexture(TextureObject textureObject)
        {
            GL.DeleteTexture(textureObject.ID);
        }
    }
    public class Texture : SafeDispose
    {
        public TextureTarget textureTarget { get; set; } = TextureTarget.Texture2D;
        public PixelFormat pixelFormat { get; set; } = PixelFormat.Rgba;
        public PixelInternalFormat internalPixelFormat { get; set; } = PixelInternalFormat.Rgba;
        public bool generateMipMaps { get; set; } = true;
        public TextureWrapMode textureWrapMode { get; set; } = TextureWrapMode.ClampToEdge;
        public TextureMinFilter textureMinFilter { get; set; } = TextureMinFilter.Nearest;
        public TextureMagFilter textureMagFilter { get; set; } = TextureMagFilter.Nearest;
        public Vector4 borderColor { get; set; } = Vector4.Zero;
        public int mipmapLevels { get; set; } = 11;

        public virtual int Width { get; set; } = 0;
        public virtual int Height { get; set; } = 0;

        int textureId = 0;
        public int GlTextureID {
            get
            {
                return textureId;
            }
            protected set
            {
                textureId = value;
            }
        }
        public override string Name => "Texture: " + GlTextureID;
        [System.Obsolete("Use CreateTextureObjectFromID", true)]
        public static explicit operator Texture(int textureId) => new Texture() {GlTextureID = textureId};

        public static explicit operator int(Texture texture) => texture.GlTextureID;

        public static Texture CreateTextureObjectFromID(int glId, TextureTarget textureTarget, PixelFormat pixelFormat, PixelInternalFormat pixelInternalFormat, int width, int height)
        {
            var texture = new Texture() { GlTextureID = glId };
            
            GL.BindTexture(textureTarget, glId);
            GL.GetTexParameter(textureTarget, GetTextureParameter.TextureWrapS, out int textureWrapMode);
            GL.GetTexParameter(textureTarget, GetTextureParameter.TextureMinFilter, out int textureMinFilter);
            GL.GetTexParameter(textureTarget, GetTextureParameter.TextureMagFilter, out int textureMagFilter);
            float[] colors = new float[4];
            GL.GetTexParameter(textureTarget, GetTextureParameter.TextureBorderColor, colors);
            GL.GetTexParameter(textureTarget, GetTextureParameter.GenerateMipmap, out int generateMipMaps);
            GL.GetTexParameter(textureTarget, GetTextureParameter.TextureMaxLevel, out int MipmapLevels);

            texture.textureWrapMode = (TextureWrapMode)textureWrapMode;
            texture.textureMinFilter = (TextureMinFilter)textureMinFilter;
            texture.textureMagFilter = (TextureMagFilter)textureMagFilter;
            texture.borderColor = new Vector4(colors[0], colors[1], colors[2], colors[3]);
            texture.generateMipMaps = generateMipMaps == 1 ? true : false;
            texture.mipmapLevels = MipmapLevels;
            texture.internalPixelFormat = pixelInternalFormat;
            texture.pixelFormat = pixelFormat;
            texture.Width = width;
            texture.textureTarget = textureTarget;
            texture.Height = height;

            GL.BindTexture(textureTarget, 0);
            return texture;
        }

        protected virtual IntPtr LoadPixelData()
        {
            return IntPtr.Zero;
        }
        public virtual void ResolveTexture(bool isShadowMap = false)
        {
            if (GlTextureID == 0)
            {
                GlTextureID = GL.GenTexture();
            }
            else
            {
                GL.DeleteTexture(GlTextureID);
            }

            GL.BindTexture(textureTarget, GlTextureID);
            GL.TexParameter(textureTarget, TextureParameterName.TextureWrapS, (int)textureWrapMode);
            GL.TexParameter(textureTarget, TextureParameterName.TextureWrapT, (int)textureWrapMode);
            GL.TexParameter(textureTarget, TextureParameterName.TextureMinFilter, (int)textureMinFilter);
            GL.TexParameter(textureTarget, TextureParameterName.TextureMagFilter, (int)textureMagFilter);
            if (isShadowMap)
            {
                GL.TexParameter(textureTarget, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRefToTexture);
                GL.TexParameter(textureTarget, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);
            }
            if (generateMipMaps)
            {
                GL.TexParameter(textureTarget, TextureParameterName.TextureMaxLevel, mipmapLevels);
                GL.TexParameter(textureTarget, TextureParameterName.TextureBaseLevel, 0);
            }
            float[] borderColor = { this.borderColor.X, this.borderColor.Y, this.borderColor.Z, this.borderColor.W };
            GL.TexParameter(textureTarget, TextureParameterName.TextureBorderColor, borderColor);

            if(pixelFormat == PixelFormat.DepthComponent || pixelFormat == PixelFormat.DepthStencil)
            {
                GL.TexImage2D(textureTarget, 0, internalPixelFormat, Width, Height, 0, pixelFormat, PixelType.Float, LoadPixelData());
            }
            else
            {
                GL.TexImage2D(textureTarget, 0, internalPixelFormat, Width, Height, 0, pixelFormat, PixelType.UnsignedByte, LoadPixelData());
            }

            if (generateMipMaps)
            {
                GL.GenerateTextureMipmap(GlTextureID);
            }

            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        protected override void OnDispose()
        {
            if (GlTextureID != 0)
                GL.DeleteTexture(GlTextureID);
            GlTextureID = 0;
        }
    }
}
