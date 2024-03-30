using ImGuiNET;
using ObjLoader.Loader.Data.VertexData;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics.Utility
{
    public class TextureWindow
    {
        string propId;
        float TextureScale = 1.0f;
        public TextureWindow(GuiManager guiManager, string GlobalTexturePropertyID)
        {
            propId = GlobalTexturePropertyID;
            string name = GlobalTexturePropertyID + " texture preview";
            guiManager.AddWindow(name, Update, typeof(TextureWindow));
            guiManager.OnGuiFinish += Reset;
        }
        Texture texture1;
        void Reset()
        {

            if(texture1 != null)
            {
                GL.BindTexture(TextureTarget.Texture2D, texture1.GlTextureID);
                //reset swizzle
                var swizzle = new int[]{
                    (int)All.Red,   // Shader Red   channel source = Texture Red
                    (int)All.Green, // Shader Green channel source = Texture Green
                    (int)All.Blue,  // Shader Blue  channel source = Texture Blue
                    (int)All.Alpha    // Shader Alpha channel source = One
                };
                GL.TextureParameterI(texture1.GlTextureID, TextureParameterName.TextureSwizzleRgba, swizzle);
            }
        }
        public void Update()
        {
            if(Shader.GetGlobalUniform<Texture>(Shader.GetShaderPropertyId(propId), out Texture texture)){
                texture1 = texture;
                ImGui.SliderFloat("Texture Scale", ref TextureScale, 0.5f, 1.0f);
                var cursorPos = ImGui.GetCursorScreenPos();

                var imageSize = new Vector2(texture.Width, texture.Height);
                var normImageSize = imageSize / imageSize.Length();

                //var minDim = MathF.Min(normImageSize.X, normImageSize.Y);
                //var diff = 1 - minDim;
                //normImageSize.X += diff;
                //normImageSize.Y += diff;

                var windowSize = ImGui.GetWindowSize();
                if(windowSize.X < windowSize.Y)
                {
                    float scaler = windowSize.X / MathF.Max(normImageSize.X, normImageSize.Y);
                    normImageSize.X *= scaler;
                    normImageSize.Y *= scaler;
                }
                else
                {
                    float scaler = windowSize.Y / MathF.Max(normImageSize.X, normImageSize.Y);
                    normImageSize.X *= scaler;
                    normImageSize.Y *= scaler;
                }

                normImageSize *= TextureScale;

                var min = cursorPos;
                var max = cursorPos + normImageSize;
                
                //make alpha one
                GL.BindTexture(TextureTarget.Texture2D, texture.GlTextureID);
                int[] swizzle = new int[]{
                    (int)All.Red,   // Shader Red   channel source = Texture Red
                    (int)All.Green, // Shader Green channel source = Texture Green
                    (int)All.Blue,  // Shader Blue  channel source = Texture Blue
                    (int)All.One    // Shader Alpha channel source = One
                };
                GL.TextureParameterI(texture.GlTextureID, TextureParameterName.TextureSwizzleRgba, swizzle);

                ImGui.GetWindowDrawList().AddImage(
                    (IntPtr)(texture.GlTextureID),
                    min,
                    max,
                    new System.Numerics.Vector2(0, 1),
                    new System.Numerics.Vector2(1, 0));
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            }
        }
    }
}
