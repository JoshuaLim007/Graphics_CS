using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

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
    public class Texture : IDisposable
    {
        public TextureTarget TextureTarget { get; set; } = TextureTarget.Texture2D;
        public PixelFormat pixelFormat { get; set; } = PixelFormat.Rgba;
        public PixelInternalFormat internalPixelFormat { get; set; } = PixelInternalFormat.Rgba;
        public bool generateMipMaps { get; set; } = true;
        public TextureWrapMode textureWrapMode { get; set; } = TextureWrapMode.ClampToEdge;
        public TextureMinFilter textureMinFilter { get; set; } = TextureMinFilter.Nearest;
        public TextureMagFilter textureMagFilter { get; set; } = TextureMagFilter.Nearest;
        public int MipmapLevels { get; set; } = 11;

        public virtual int Width { get; set; } = 0;
        public virtual int Height { get; set; } = 0;
        public int GlTextureID { get; protected set; } = 0;

        public static explicit operator Texture(int ptr) => new Texture() {GlTextureID = ptr};
        public static explicit operator int(Texture texture) => texture.GlTextureID;
        protected virtual IntPtr LoadPixelData()
        {
            return IntPtr.Zero;
        }
        public virtual void ResolveTexture()
        {
            if (GlTextureID == 0)
                GlTextureID = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, GlTextureID);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)textureWrapMode);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)textureWrapMode);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)textureMinFilter);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)textureMagFilter);

            if (generateMipMaps)
            {
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, MipmapLevels);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
            }

            GL.TexImage2D(TextureTarget.Texture2D, 0, internalPixelFormat, Width, Height, 0, pixelFormat, PixelType.UnsignedByte, LoadPixelData());

            if (generateMipMaps)
            {
                GL.GenerateTextureMipmap(GlTextureID);
            }

            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
        public virtual void Dispose()
        {
            if(GlTextureID != 0)
                GL.DeleteTexture(GlTextureID);
            GlTextureID = 0;
        }
    }
}
