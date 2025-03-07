using Assimp;
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
using System.Drawing;
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
        public void OverrideSelection(List<Entity> entities)
        {
            ObjectsSelected.Clear();
            ObjectsSelected.AddRange(entities);
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
        bool windowFocused = false;
        public SceneViewManager(GuiManager guiManager, Graphics graphics)
        {
            this.graphics = graphics;
            graphics.Window.MouseDown += (args) =>
            {
                if (args.IsPressed && args.Button == MouseButton.Left)
                {
                    if (windowFocused)
                    {
                        LeftMousePressed = true;
                    }
                }
            };
            guiManager.OnSceneViewGui += () =>
            {
                OnRender();
                if (ImGui.IsWindowFocused())
                {
                    windowFocused = true;
                    if (LeftMousePressed)
                    {
                        SceneObjectSelection();
                        LeftMousePressed = false;
                    }

                    if (graphics.Window.IsKeyDown(Keys.F))
                    {
                        if (ObjectsSelected.Count != 0)
                        {
                            float maxSize = 0;
                            Vector3 avgPos = Vector3.Zero;
                            var camera = Camera.Main;
                            for (int i = 0; i < ObjectsSelected.Count; i++)
                            {
                                var renderer = ObjectsSelected[i].GetComponentInChild<Renderer>();
                                if (renderer)
                                {
                                    var bounds = renderer.GetWorldBounds();
                                    var targetPos = bounds.Center;
                                    var size = bounds.Extents.Length * 0.75f;
                                    size = MathF.Max(size, 5.0f);
                                    avgPos += targetPos;
                                    maxSize = MathF.Max(size, maxSize);
                                }
                                else
                                {
                                    var targetPos = ObjectsSelected[i].Transform.LocalPosition;
                                    var size = 5.0f;
                                    avgPos += targetPos;
                                    maxSize = MathF.Max(size, maxSize);
                                }
                            }
                            if (camera != null)
                            {
                                avgPos /= ObjectsSelected.Count;
                                camera.Transform.WorldPosition = avgPos - camera.Transform.Forward * maxSize;
                            }
                        }
                    }

                }
                else
                {
                    windowFocused = false;
                }
                return typeof(SceneViewManager);
            };
            graphics.EnqueueRenderPass(objectHighlight);
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
                        if (temp != null)
                        {
                            Entity.Destroy(ref temp);
                        }
                    }
                    ObjectsSelected.Clear();
                }
            }
        }

        public Entity FindEntityClosestTo(Vector3 position, int place)
        {
            var renderers = InternalGlobalScope<Renderer>.Values;
            renderers.Sort((e, d) =>
            {
                if(e.Enabled && !d.Enabled)
                {
                    return 1;
                }
                if(d.Enabled && !e.Enabled)
                {
                    return -1;
                }
                if(!e.Enabled && !d.Enabled)
                {
                    return 0;
                }
                float eDist = 0;
                float dDist = 0;

                var bounds = e.Mesh.BoundingBox;
                var worldBounds = AABB.ApplyTransformation(bounds, e.Transform.ModelMatrix);
                if (AABB.Contains(worldBounds, position, 0.1f))
                {
                    var closestCorner = AABB.ClosestCorner(worldBounds, position);
                    eDist = Math.Min((closestCorner - position).LengthSquared, (e.Transform.WorldPosition - position).LengthSquared);
                }
                else
                {
                    eDist = float.PositiveInfinity;
                }

                bounds = d.Mesh.BoundingBox;
                worldBounds = AABB.ApplyTransformation(bounds, d.Transform.ModelMatrix);
                if (AABB.Contains(worldBounds, position, 0.1f))
                {
                    var closestCorner = AABB.ClosestCorner(worldBounds, position);
                    dDist = Math.Min((closestCorner - position).LengthSquared, (d.Transform.WorldPosition - position).LengthSquared);
                }
                else
                {
                    dDist = float.PositiveInfinity;
                }

                if(eDist < dDist)
                {
                    return 1;
                }
                else if(eDist == dDist)
                {
                    return 0;
                }
                else
                {
                    return -1;
                }

            });

            return renderers[Math.Max(renderers.Count - (place + 1),0)].Entity;
        }
        Vector3 previousHitPosition = Vector3.PositiveInfinity;
        int place = 0;
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
            pos *= graphics.RenderScale;
            var res = (Vector2)graphics.GetRenderSize();
            res *= graphics.RenderScale;
            pos.Y = res.Y - pos.Y;

            var uv = pos / res;
            if(uv.X < 0 || uv.Y < 0 || uv.X > 1 || uv.X > 1)
            {
                return;
            }

            Debug.Log(uv);
            float depth = graphics.GetDepthAt((int)pos.X, (int)pos.Y);
            Debug.Log(depth);
            if(depth == 1)
            {
                ObjectsSelected.Clear();
                return;
            }

            Vector4 t = new Vector4(uv.X * 2 - 1, uv.Y * 2 - 1, depth, 1);
            var vp = Camera.Main.ViewMatrix * Camera.Main.ProjectionMatrix;

            t = t * vp.Inverted();
            t /= t.W;

            var target_pos = t.Xyz;
            if(Vector3.DistanceSquared(previousHitPosition, target_pos) > 1)
            {
                if(ObjectsSelected.Count > 0)
                {
                    ObjectsSelected.Clear();
                    objectHighlight.ToHighlight = ObjectsSelected;
                    return;
                }
                place = 0;
            }
            else
            {
                ObjectsSelected.Clear();
                place = ++place % 3;
            }
            previousHitPosition = target_pos;

            var entity = FindEntityClosestTo(target_pos, place);
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
