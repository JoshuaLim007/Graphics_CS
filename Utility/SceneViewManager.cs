using ImGuiNET;
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
        public void OverrideSelection(Entity entity)
        {
            ObjectsSelected.Clear();
            ObjectsSelected.Add(entity);
            objectHighlight.ToHighlight = ObjectsSelected;
        }
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
                    var rend = ToHighlight[i].GetComponentInChild<Renderer>();
                    if(rend == null)
                    {
                        continue;
                    }
                    Graphics.Instance.RenderBounginBox(CurrentCamera, rend);
                }
            }

            protected override void OnDispose()
            {
            }
        }
        bool LeftMousePressed = false;
        public SceneViewManager(GuiManager guiManager, Graphics graphics)
        {
            this.graphics = graphics;
            graphics.Window.MouseDown += (args) =>
            {
                if (args.IsPressed && args.Button == MouseButton.Left)
                {
                    LeftMousePressed = true;
                }
            };
            guiManager.OnSceneViewGui += () =>
            {
                OnRender();
                if (ImGui.IsWindowFocused())
                {
                    if (LeftMousePressed)
                    {
                        SceneObjectSelection();
                        LeftMousePressed = false;
                    }

                    if (graphics.Window.IsKeyDown(Keys.F))
                    {
                        if (ObjectsSelected.Count != 0)
                        {
                            var renderer = ObjectsSelected[0].GetComponentInChild<Renderer>();
                            if (renderer)
                            {
                                var bounds = renderer.GetWorldBounds();
                                var targetPos = bounds.Center;
                                var size = bounds.Extents.Length * 0.75f;
                                size = MathF.Max(size, 5.0f);
                                var camera = Camera.Main;
                                if (camera != null)
                                {
                                    camera.Transform.WorldPosition = targetPos - camera.Transform.Forward * size;
                                }
                            }
                            else
                            {
                                var targetPos = ObjectsSelected[0].Transform.LocalPosition;
                                var size = 5.0f;
                                var camera = Camera.Main;
                                if (camera != null)
                                {
                                    camera.Transform.WorldPosition = targetPos - camera.Transform.Forward * size;
                                }
                            }
                        }
                    }

                }
                return typeof(SceneViewManager);
            };
            graphics.EnqueueRenderPass(objectHighlight);
        }

        private void Window_MouseDown(MouseButtonEventArgs obj)
        {
            throw new NotImplementedException();
        }

        void OnRender()
        {
            if (ImGui.IsWindowFocused())
            {
                if (graphics.Window.KeyboardState.IsKeyDown(Keys.Delete))
                {
                    for (int i = 0; i < ObjectsSelected.Count; i++)
                    {
                        var temp = ObjectsSelected[i];
                        Entity.Destroy(ref temp);
                    }
                    ObjectsSelected.Clear();
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
            if(Camera.Main == null)
            {
                Debug.Log("No main camera found!", Debug.Flag.Warning);
                return;
            }
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
            if (ObjectsSelected.Count > 0)
            {
                if(ObjectsSelected[0] == entity)
                {
                    entity = null;
                }
            }
            if (entity == null)
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
