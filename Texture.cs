using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using StbImageSharp;
using System.ComponentModel.DataAnnotations.Schema;

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
        public TextureTarget TextureTarget { get; set; } = TextureTarget.Texture2D;
        public PixelFormat pixelFormat { get; set; } = PixelFormat.Rgba;
        public PixelInternalFormat internalPixelFormat { get; set; } = PixelInternalFormat.Rgba;
        public bool generateMipMaps { get; set; } = true;
        public TextureWrapMode textureWrapMode { get; set; } = TextureWrapMode.ClampToEdge;
        public TextureMinFilter textureMinFilter { get; set; } = TextureMinFilter.Nearest;
        public TextureMagFilter textureMagFilter { get; set; } = TextureMagFilter.Nearest;
        public Vector4 BorderColor { get; set; } = Vector4.Zero;
        public int MipmapLevels { get; set; } = 11;

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
        public static explicit operator Texture(int textureId) => new Texture() {GlTextureID = textureId};
        public static explicit operator int(Texture texture) => texture.GlTextureID;
        protected virtual IntPtr LoadPixelData()
        {
            return IntPtr.Zero;
        }
        public virtual void ResolveTexture(bool isShadowMap = false)
        {
            if (GlTextureID == 0)
                GlTextureID = GL.GenTexture();

            GL.BindTexture(TextureTarget, GlTextureID);
            GL.TexParameter(TextureTarget, TextureParameterName.TextureWrapS, (int)textureWrapMode);
            GL.TexParameter(TextureTarget, TextureParameterName.TextureWrapT, (int)textureWrapMode);
            GL.TexParameter(TextureTarget, TextureParameterName.TextureMinFilter, (int)textureMinFilter);
            GL.TexParameter(TextureTarget, TextureParameterName.TextureMagFilter, (int)textureMagFilter);
            if (isShadowMap)
            {
                GL.TexParameter(TextureTarget, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRefToTexture);
                GL.TexParameter(TextureTarget, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);
            }
            if (generateMipMaps)
            {
                GL.TexParameter(TextureTarget, TextureParameterName.TextureMaxLevel, MipmapLevels);
                GL.TexParameter(TextureTarget, TextureParameterName.TextureBaseLevel, 0);
            }
            float[] borderColor = { BorderColor.X, BorderColor.Y, BorderColor.Z, BorderColor.W };
            GL.TexParameter(TextureTarget, TextureParameterName.TextureBorderColor, borderColor);

            if(pixelFormat == PixelFormat.DepthComponent || pixelFormat == PixelFormat.DepthStencil)
            {
                GL.TexImage2D(TextureTarget, 0, internalPixelFormat, Width, Height, 0, pixelFormat, PixelType.Float, LoadPixelData());
            }
            else
            {
                GL.TexImage2D(TextureTarget, 0, internalPixelFormat, Width, Height, 0, pixelFormat, PixelType.UnsignedByte, LoadPixelData());
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
