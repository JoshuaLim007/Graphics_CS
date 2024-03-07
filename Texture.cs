using JLUtility;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using StbImageSharp;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace JLGraphics
{
    public struct GLTextureObject 
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
        public static GLTextureObject CreateTexture(
            int height, 
            int width, 
            PixelFormat pixelFormat, 
            PixelInternalFormat pixelInternalFormat,
            TextureWrapMode textureWrapMode,
            TextureMinFilter textureMinFilter, 
            TextureMagFilter textureMagFilter,
            bool generateMipMaps)
        {
            var textureObject = new GLTextureObject()
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
        public static void DestroyTexture(GLTextureObject textureObject)
        {
            GL.DeleteTexture(textureObject.ID);
        }
    }
    public class Texture : SafeDispose
    {
        public TextureTarget textureTarget { get; set; } = TextureTarget.Texture2D;
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
        public override string Name => "Texture: " + GlTextureID + " " + TextureName;
        public string TextureName { get; private set; }
        public void SetName(string name)
        {
            TextureName = name;
        }

        [System.Obsolete("Use CreateTextureObjectFromID", true)]
        public static explicit operator Texture(int textureId) => new Texture() {GlTextureID = textureId};

        public static explicit operator int(Texture texture) => texture.GlTextureID;
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
            texture.Width = width;
            texture.textureTarget = textureTarget;
            texture.Height = height;

            GL.BindTexture(textureTarget, 0);
            return texture;
        }

        protected virtual (IntPtr, PixelType, PixelFormat) LoadPixelData()
        {
            if (IsDepthComponent(internalPixelFormat))
            {
                return (IntPtr.Zero, PixelType.Float, PixelFormat.DepthComponent);
            }
            else
            {
                return (IntPtr.Zero, PixelType.UnsignedByte, PixelFormat.Rgba);
            }
        }
        public virtual void SetPixels<T>(T[] data, PixelType pixelType, PixelFormat pixelFormat) where T : struct
        {
            if (!textureIsResolved)
            {
                Debug.Log("Texture has not been resolved before setting pixel data!", Debug.Flag.Error);
            }
            GL.BindTexture(textureTarget, GlTextureID);
            GL.TexSubImage2D(textureTarget, 0, 0, 0, Width, Height, pixelFormat, pixelType, data);
            GL.BindTexture(textureTarget, 0);
        }
        bool textureIsResolved = false;
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
            textureIsResolved = true;
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

            var pixelData = LoadPixelData();

            GL.TexImage2D(textureTarget, 0, internalPixelFormat, Width, Height, 0, pixelData.Item3, pixelData.Item2, pixelData.Item1);

            if (generateMipMaps)
            {
                GL.GenerateTextureMipmap(GlTextureID);
            }

            GL.BindTexture(textureTarget, 0);
        }
        public static bool Alike(Texture f1, Texture f2, float f1_resolutionInvScale = 1.0f)
        {
            bool textureCheck1 =
                f1.Width * f1_resolutionInvScale == f2.Width &&
                f1.Height * f1_resolutionInvScale == f2.Height &&
                f1.mipmapLevels == f2.mipmapLevels &&
                f1.internalPixelFormat == f2.internalPixelFormat &&
                f1.borderColor == f2.borderColor &&
                f1.generateMipMaps == f2.generateMipMaps &&
                f1.textureMagFilter == f2.textureMagFilter &&
                f1.textureMinFilter == f2.textureMinFilter &&
                f1.textureTarget == f2.textureTarget &&
                f1.textureWrapMode == f2.textureWrapMode;
            return textureCheck1;
        }
        protected override void OnDispose()
        {
            if (GlTextureID != 0)
                GL.DeleteTexture(GlTextureID);
            GlTextureID = 0;
        }
    }
}
