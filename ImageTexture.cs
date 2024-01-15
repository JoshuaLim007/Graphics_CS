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
        public static ImageTexture LoadTextureFromPath(string path)
        {
            var image = ImageResult.FromStream(File.OpenRead(path), ColorComponents.RedGreenBlueAlpha);
            return new ImageTexture(image);
        }
        public ImageResult image { get; }
        public ImageTexture(ImageResult image)
        {
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
        protected override IntPtr GetPixelData()
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
