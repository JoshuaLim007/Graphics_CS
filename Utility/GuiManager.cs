using ImGuiNET;
using JLUtility;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics.Utility
{
    public class GuiManager
    {
        public Action OnSceneViewGui { get; set; }
        public Action OnInspectorGui { get; set; }
        public Action OnHierarchyGui { get; set; }
        ImGuiController guiController;
        GameWindow Window;
        public GuiManager(GameWindow window)
        {
            Window = window;

            guiController = new ImGuiController(Window.Size.X, Window.Size.Y);
            Window.RenderFrame += Update;

            Window.TextInput += (e) => {
                guiController.PressChar((char)e.Unicode);
            };
            Window.MouseWheel += (e) =>
            {
                guiController.MouseScroll(e.Offset);
            };
            PreviousWindowSize = window.Size;
            Graphics.Instance.BlitFinalResultsToScreen = false;
        }
        Vector2i PreviousWindowSize;
        Vector2i GuiRenderSceneSize;
        public void Update(FrameEventArgs fileSystemEventArgs)
        {
            Graphics.Instance.BlitFinalResultsToScreen = false;
            GL.Viewport(0, 0, Window.Size.X, Window.Size.Y);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            guiController.Update(Window, Time.DeltaTime);
            ImGui.DockSpaceOverViewport();
            ImGui.ShowDebugLogWindow();
            ImGui.ShowMetricsWindow();

            if(PreviousWindowSize != Window.Size)
            {
                guiController.WindowResized(Window.Size.X, Window.Size.Y);
                PreviousWindowSize = Window.Size;
            }

            RenderSceneView();
            guiController.Render();
            Window.SwapBuffers();
        }

        bool resized = false;
        void RenderSceneView()
        {
            ImGui.Begin("Scene Window");
            var pos = ImGui.GetCursorPos();
            var size = ImGui.GetWindowSize();

            if (size.X != GuiRenderSceneSize.X || size.Y != GuiRenderSceneSize.Y)
            {
                resized = false;
                GuiRenderSceneSize.X = (int)size.X;
                GuiRenderSceneSize.Y = (int)size.Y;
            }
            else
            {
                if(!resized)
                    Graphics.Instance.ResizeRenderSize((int)GuiRenderSceneSize.X, (int)GuiRenderSceneSize.Y);
                resized = true;
            }

            var cursorPos = ImGui.GetCursorScreenPos();
            var MainFrameBuffer = Graphics.Instance.FinalRenderTarget;
            ImGui.GetWindowDrawList().AddImage(
                (IntPtr)(MainFrameBuffer.TextureAttachments[0].GlTextureID),
                new System.Numerics.Vector2(cursorPos.X, cursorPos.Y),
                new System.Numerics.Vector2(cursorPos.X + MainFrameBuffer.Width / Graphics.Instance.RenderScale, cursorPos.Y + MainFrameBuffer.Height / Graphics.Instance.RenderScale),
                new System.Numerics.Vector2(0, 1),
                new System.Numerics.Vector2(1, 0));
            OnSceneViewGui?.Invoke();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            ImGui.End();
        }
    }
}
