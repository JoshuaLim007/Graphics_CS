using OpenTK.Graphics.OpenGL4;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public class Texture : IDisposable
    {
        private bool disposedValue = false;

        public string Name { get; set; }
        public string path { get; set; }
        public PixelFormat pixelFormat { get; set; }
        public PixelInternalFormat internalPixelFormat { get; set; }
        public bool generateMipMaps { get; set; }
        public TextureWrapMode textureWrapMode { get; set; }
        public TextureMinFilter textureMinFilter { get; set; }
        public TextureMagFilter textureMagFilter { get; set; }

        public ImageResult image { get; private set; }
        public int textureID { get; }

        public Texture()
        {
            textureID = GL.GenTexture();
        }

        public void Apply()
        {
            GL.BindTexture(TextureTarget.Texture2D, textureID);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)textureWrapMode);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)textureWrapMode);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)textureMinFilter);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)textureMagFilter);

            image = ImageResult.FromStream(File.OpenRead(path), ColorComponents.RedGreenBlueAlpha);
            GL.TexImage2D(TextureTarget.Texture2D, 0, internalPixelFormat, image.Width, image.Height, 0, pixelFormat, PixelType.UnsignedByte, image.Data);
            if (generateMipMaps)
            {
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            }
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                GL.DeleteTexture(textureID);
                image = null;
                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
