using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

namespace JLGraphics
{
    public class Texture : IDisposable
    {
        public PixelFormat pixelFormat { get; set; } = PixelFormat.Rgba;
        public PixelInternalFormat internalPixelFormat { get; set; } = PixelInternalFormat.Rgba;
        public bool generateMipMaps { get; set; } = true;
        public TextureWrapMode textureWrapMode { get; set; } = TextureWrapMode.ClampToEdge;
        public TextureMinFilter textureMinFilter { get; set; } = TextureMinFilter.Linear;
        public TextureMagFilter textureMagFilter { get; set; } = TextureMagFilter.Linear;
        public virtual int Width { get; set; }
        public virtual int Height { get; set; }

        public int GlTextureID { get; private set; }

        public Texture()
        {
            GlTextureID = GL.GenTexture();
        }
        public static explicit operator Texture(int ptr) => new Texture() {GlTextureID = ptr};
        public static explicit operator int(Texture texture) => texture.GlTextureID;
        protected virtual IntPtr GetPixelData()
        {
            return IntPtr.Zero;
        }
        public void ResolveTexture()
        {
            GL.BindTexture(TextureTarget.Texture2D, GlTextureID);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)textureWrapMode);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)textureWrapMode);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)textureMinFilter);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)textureMagFilter);

            GL.TexImage2D(TextureTarget.Texture2D, 0, internalPixelFormat, Width, Height, 0, pixelFormat, PixelType.UnsignedByte, GetPixelData());

            if (generateMipMaps)
            {
                GL.GenerateTextureMipmap(GlTextureID);
            }
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
        public virtual void Dispose()
        {
            GL.DeleteTexture(GlTextureID);
        }
    }
}
