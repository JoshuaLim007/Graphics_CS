using ImGuiNET;
using JLGraphics.Input;
using JLUtility;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics.Utility
{
    public class SceneViewManager
    {
        Graphics graphics;
        public List<Entity> ObjectsSelected { get; private set; } = new List<Entity>();
        public SceneViewManager(Graphics graphics)
        {
            this.graphics = graphics;

            this.graphics.OnSceneViewGui += () =>
            {
                OnRender();
                if (graphics.Window.IsMouseButtonPressed(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Button1)){
                    SceneObjectSelection();
                }
            };
        }
        void OnRender()
        {
            //handle scene view mouse input
            var pos = graphics.Window.MousePosition;
            if (graphics.Window.IsMouseButtonPressed(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Right))
            {
                MouseInput.UpdateMousePosition(pos);
            }
            if (graphics.Window.IsMouseButtonDown(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Right)) {
                MouseInput.UpdateMousePosition(pos);
            }
            else
            {
                if (graphics.Window.IsMouseButtonReleased(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Right))
                {
                    MouseInput.UpdateMousePosition(pos);
                    MouseInput.UpdateMousePosition(pos);
                }
            }
        }
        public Entity FindEntityClosestTo(Vector3 position)
        {
            var renderers = InternalGlobalScope<Renderer>.Values;
            Entity entity = null;
            float shortestDist = float.MaxValue;

            for (int i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                if(!renderer.Enabled)
                {
                    continue;
                }

                var bounds = renderer.Mesh.BoundingBox;
                var worldBounds = AABB.ApplyTransformation(bounds, renderer.Transform.ModelMatrix);

                if(AABB.Contains(worldBounds, position, 0.1f))
                {
                    var closestCorner = AABB.ClosestCorner(worldBounds, position);
                    float dist = (closestCorner - position).LengthSquared;
                    if(dist < shortestDist)
                    {
                        entity = renderer.Entity;
                        shortestDist = dist;
                    }
                }
            }
            return entity;
        }
        void SceneObjectSelection()
        {
            var windowPos = ImGui.GetCursorScreenPos();
            Vector2 pos;
            pos = graphics.Window.MousePosition - new Vector2(windowPos.X, windowPos.Y);

            var res = graphics.GetRenderWindowSize();
            pos.Y = res.Y - pos.Y;

            var uv = pos / res;

            float depth = graphics.GetDepthAt((int)pos.X, (int)pos.Y);
            Vector4 t = new Vector4(uv.X * 2 - 1, uv.Y * 2 - 1, depth * 2 - 1, 1);
            var vp = Camera.Main.ViewMatrix * Camera.Main.ProjectionMatrix;

            t = t * vp.Inverted();
            t /= t.W;

            var target_pos = t.Xyz;

            var entity = FindEntityClosestTo(target_pos);
            if(entity == null)
            {
                return;
            }
            ObjectsSelected.Clear();
            ObjectsSelected.Add(entity);
        }

    }
}
