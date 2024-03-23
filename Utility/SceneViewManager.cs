﻿using ImGuiNET;
using JLGraphics.Input;
using JLGraphics.RenderPasses;
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
        ObjectHighlight objectHighlight = new ObjectHighlight();
        public class ObjectHighlight : RenderPass
        {
            public ObjectHighlight() : base(RenderQueue.AfterPostProcessing, 0)
            {
            }

            public override string Name => "Selected Object Highlighting";
            public List<Entity> ToHighlight { get; set; }
            public override void Execute(in FrameBuffer frameBuffer)
            {
                if(ToHighlight == null)
                {
                    return;
                }
                for (int i = 0; i < ToHighlight.Count; i++)
                {
                    Graphics.Instance.RenderBounginBox(CurrentCamera, ToHighlight[i].GetComponent<Renderer>());
                }
            }

            protected override void OnDispose()
            {
            }
        }
        public SceneViewManager(GuiManager guiManager, Graphics graphics)
        {
            this.graphics = graphics;

            guiManager.OnSceneViewGui += () =>
            {
                OnRender();
                if (ImGui.IsWindowFocused())
                {
                    if (graphics.Window.IsMouseButtonPressed(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Button1))
                    {
                        SceneObjectSelection();
                    }
                }
            };
            graphics.EnqueueRenderPass(objectHighlight);
        }
        void OnRender()
        {


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

            var res = graphics.GetRenderSize();
            pos.Y = res.Y - pos.Y;

            var uv = pos / res;
            if(uv.X < 0 || uv.Y < 0 || uv.X > 1 || uv.X > 1)
            {
                return;
            }
            float depth = graphics.GetDepthAt((int)pos.X, (int)pos.Y);
            Vector4 t = new Vector4(uv.X * 2 - 1, uv.Y * 2 - 1, depth * 2 - 1, 1);
            var vp = Camera.Main.ViewMatrix * Camera.Main.ProjectionMatrix;

            t = t * vp.Inverted();
            t /= t.W;

            var target_pos = t.Xyz;

            var entity = FindEntityClosestTo(target_pos);
            if(entity == null)
            {
                ObjectsSelected.Clear();
                return;
            }
            ObjectsSelected.Clear();
            ObjectsSelected.Add(entity);
            objectHighlight.ToHighlight = ObjectsSelected;
        }

    }
}
