using OpenTK.Graphics.OpenGL4;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public class ImageTexture : Texture
    {
        string path;
        public static ImageTexture LoadTextureFromPath(string path, bool bilinearFilter = true, ColorComponents colorComponents = ColorComponents.RedGreenBlueAlpha)
        {
            var image = ImageResult.FromStream(File.OpenRead(path), colorComponents);
            var m = new ImageTexture(image);
            m.path = path;
            if (bilinearFilter)
            {
                m.textureMinFilter = TextureMinFilter.LinearMipmapNearest;
                m.textureMagFilter = TextureMagFilter.Linear;
            }
            else
            {
                m.textureMinFilter = TextureMinFilter.NearestMipmapNearest;
                m.textureMagFilter = TextureMagFilter.Nearest;
            }
            m.textureWrapMode = TextureWrapMode.Repeat;
            return m;
        }
        public override string Name => path;
        public ImageResult image { get; }
        public ImageTexture(ImageResult image)
        {
            textureTarget = TextureTarget.Texture2D;
            this.image = image;
        }
        public override int Width
        {
            get => image.Width;
            set
            {
                if (image != null)
                {
                    image.Width = value;
                }
                base.Width = value;
            }
        }
        public override int Height
        {
            get => image.Height;
            set
            {
                if (image != null)
                {
                    image.Width = value;
                }
                base.Height = value;
            }
        }
        protected override IntPtr LoadPixelData()
        {
            if (image == null)
            {
                return IntPtr.Zero;
            }
            unsafe
            {
                fixed (byte* p = image.Data)
                {
                    return (IntPtr)p;
                }
            }
        }
    }
}
