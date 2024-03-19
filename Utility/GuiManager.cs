//using ImGuiNET;
//using JLUtility;
//using OpenTK.Mathematics;
//using OpenTK.Windowing.Desktop;
//using OpenTK.Windowing.GraphicsLibraryFramework;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace JLGraphics.Utility
//{
//    public class GuiManager
//    {
//        public Action OnSceneViewGui { get; set; }
//        public Action OnInspectorGui { get; set; }
//        public Action OnHierarchyGui { get; set; }
//        ImGuiController guiController;
//        GameWindow Window;
//        public GuiManager(GameWindow window)
//        {
//            Window = window;
//        }
//        public void Update()
//        {
//            guiController.Update(Window, Time.DeltaTime);
//            ImGui.DockSpaceOverViewport();
//            ImGui.ShowDebugLogWindow();
//            ImGui.ShowMetricsWindow();


//            ImGui.Begin("Scene Window");
//            var pos = ImGui.GetCursorPos();
//            var size = ImGui.GetWindowSize();
//            RenderBufferSize = new Vector2i((int)size.X, (int)size.Y);
//            if (GuiRenderSceneSize.X != RenderBufferSize.X || GuiRenderSceneSize.Y != RenderBufferSize.Y)
//            {
//                GuiRenderSceneSize = RenderBufferSize;
//                InitFramebuffers();
//            }
//            var cursorPos = ImGui.GetCursorScreenPos();
//            ImGui.GetWindowDrawList().AddImage(
//                (IntPtr)(MainFrameBuffer.TextureAttachments[0].GlTextureID),
//                new System.Numerics.Vector2(cursorPos.X, cursorPos.Y),
//                new System.Numerics.Vector2(cursorPos.X + MainFrameBuffer.Width, cursorPos.Y + MainFrameBuffer.Height),
//                new System.Numerics.Vector2(0, 1),
//                new System.Numerics.Vector2(1, 0));
//            OnSceneViewGui?.Invoke();

//            ImGui.End();
//            guiController.Render();
//        }

//    }
//}
