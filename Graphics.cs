using ObjLoader.Loader.Data;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Platform.Windows;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using StbImageSharp;
using System.Net;

namespace JLGraphics
{
    public struct Time
    {
        public static float UnscaledFixedDeltaTime { get => Graphics.FixedDeltaTime; set => Graphics.FixedDeltaTime = Math.Clamp(value, 0.002f, 1.0f); }
        public static float UnscaledDeltaTime => Graphics.DeltaTime;
        public static float UnscaledSmoothDeltaTime => Graphics.SmoothDeltaTime;


        public static float FixedDeltaTime => Graphics.FixedDeltaTime * TimeScale;
        public static float DeltaTime => Graphics.DeltaTime * TimeScale;
        public static float SmoothDeltaTime => Graphics.SmoothDeltaTime * TimeScale;
        public static float ElapsedTime => Graphics.ElapsedTime * TimeScale;

        private static float m_timeScale = 1.0f;
        public static float TimeScale { get => m_timeScale; set => m_timeScale = Math.Clamp(value, 0, float.MaxValue); }
    }
    public static class Graphics
    {
        private static GameWindowSettings m_gameWindowSettings;
        private static NativeWindowSettings m_nativeWindowSettings;
        public static GameWindow Window { get; private set; }
        public static List<Camera> Cameras { get; set; }
        public static Shader DefaultMaterial { get; private set; }

        internal static float FixedDeltaTime { get; set; } = 0;
        internal static float DeltaTime { get; private set; } = 0;
        internal static float SmoothDeltaTime { get; private set; } = 0;
        internal static float ElapsedTime { get; private set; } = 0;
        public static bool DisableRendering { get; set; } = false;

        private static int m_drawCount = 0;
        private static int m_shaderMeshBindCount = 0;
        private static int m_verticesCount = 0;
        private static bool m_isInit = false;
        private static List<Entity> AllInstancedObjects => Entity.AllEntities;

        public static Vector2i OutputResolution => m_nativeWindowSettings.Size;

        internal static void Main()
        {
            Console.WriteLine("Joshua Lim's Graphics API");
        }
        private static void InitWindow(float updateFrequency, int msaaSamples)
        {
            m_nativeWindowSettings.NumberOfSamples = msaaSamples;
            
            if(Window != null)
            {
                m_nativeWindowSettings.SharedContext = Window.Context;
                Window.Dispose();
            }

            Window = new GameWindow(m_gameWindowSettings, m_nativeWindowSettings);
            Window.UpdateFrequency = updateFrequency;
            Window.VSync = 0;
            Window.Resize += Resize;

            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.Dither);
            GL.Enable(EnableCap.Blend);
            if(msaaSamples != 0)
            {
                GL.Enable(EnableCap.Multisample);
            }
            
            float statsInterval = 0.0f;
            Cameras = new List<Camera>();

            DefaultMaterial = new Shader("Default", "./Shaders/fragment.glsl", "./Shaders/vertex.glsl");
            DefaultMaterial.SetVector3("AlbedoColor", new Vector3(1, 1, 1));

            DateTime time1 = DateTime.Now;
            DateTime time2 = DateTime.Now;

            Window.Load += Start;
            Window.UpdateFrame += delegate (FrameEventArgs eventArgs)
            {
                FixedUpdate();

                float updateFreq = 1.0f / FixedDeltaTime;
                if (Window.UpdateFrequency != updateFreq)
                {
                    Window.UpdateFrequency = updateFreq;
                }
            };
            int setFrame = 0;
            Window.RenderFrame += delegate (FrameEventArgs eventArgs)
            {
                time2 = DateTime.Now;
                DeltaTime = (time2.Ticks - time1.Ticks) / 10000000f;
                SmoothDeltaTime += (DeltaTime - SmoothDeltaTime) * 0.1f;
                ElapsedTime += DeltaTime;

                if (statsInterval > 0.5f)
                {
                    string stats = "";

                    stats += " | fixed delta time: " + FixedDeltaTime;
                    stats += " | draw count: " + m_drawCount;
                    stats += " | shader mesh bind count: " + m_shaderMeshBindCount;
                    stats += " | vertices: " + m_verticesCount;
                    stats += " | total world objects: " + AllInstancedObjects.Count;
                    stats += " | fps: " + 1 / SmoothDeltaTime;
                    stats += " | delta time: " + DeltaTime;

                    Window.Title = stats;

                    statsInterval = 0;
                }
                statsInterval += DeltaTime;

                if (setFrame != 0)
                {
                    Update();
                }
                else
                {
                    setFrame++;
                }

                time1 = time2;
            };
        }

        /// <param name="windowName"></param>
        /// <param name="windowResolution"></param>
        /// <param name="renderFrequency"></param>
        /// <param name="updateFrequency"></param>
        public static void Init(string windowName, Vector2i windowResolution, float renderFrequency = 0, float updateFrequency = 60.0f)
        {
            m_isInit = true;
            StbImage.stbi_set_flip_vertically_on_load(1);

            FixedDeltaTime = 1.0f / updateFrequency;

            m_gameWindowSettings = GameWindowSettings.Default;
            m_nativeWindowSettings = NativeWindowSettings.Default;
            
            //32 bit depth + stencil
            m_nativeWindowSettings.DepthBits = 24;
            m_nativeWindowSettings.StencilBits = 8;

            //32 bit color
            m_nativeWindowSettings.AlphaBits = 8;
            m_nativeWindowSettings.RedBits = 8;
            m_nativeWindowSettings.GreenBits = 8;
            m_nativeWindowSettings.BlueBits = 8;

            m_gameWindowSettings.RenderFrequency = renderFrequency;
            m_gameWindowSettings.UpdateFrequency = updateFrequency;

            m_nativeWindowSettings.Size = windowResolution;
            m_nativeWindowSettings.Title = windowName;
            m_nativeWindowSettings.IsEventDriven = false;

            m_nativeWindowSettings.API = ContextAPI.OpenGL;
            m_nativeWindowSettings.APIVersion = Version.Parse("4.1");

            InitWindow(updateFrequency, 0);
        }
        public static void Run()
        {
            if (!m_isInit)
            {
                throw new Exception("Graphics not initialized!");
            }
            Window.Run();

        }
        
        private static void Resize(ResizeEventArgs args)
        {
            GL.Viewport(0, 0, args.Width, args.Height);
            for (int i = 0; i < Cameras.Count; i++)
            {
                Cameras[i].Width = args.Width;
                Cameras[i].Height = args.Height;
            }
        }
        
        private static void Start()
        {
            for (int i = 0; i < Entity.AllStarts.Count; i++)
            {
                Entity.AllStarts[i].Start();
            }
        }
        
        private static void FixedUpdate()
        {
            for (int i = 0; i < Entity.AllFixedUpdates.Count; i++)
            {
                var current = Entity.AllFixedUpdates[i];
                if (current.IsActiveAndEnabled())
                {
                    current.FixedUpdate();
                }
            }
        }
        static void mInvokeUpdates()
        {
            for (int i = 0; i < Entity.AllUpdates.Count; i++)
            {
                var current = Entity.AllUpdates[i];
                if (!current.IsActiveAndEnabled())
                {
                    continue;
                }
                current.Update();
            }
        }
        static void mInvokeOnRenders(int cameraIndex)
        {
            //invoke render event
            for (int i = 0; i < Entity.AllOnRenders.Count; i++)
            {
                var current = Entity.AllOnRenders[i];
                if (!current.IsActiveAndEnabled())
                {
                    continue;
                }
                current.OnRender(Cameras[cameraIndex]);
            }
        }
        static void mSetShaderCameraData(int cameraIndex)
        {
            Shader.SetGlobalMat4("_projectionMatrix", Cameras[cameraIndex].ProjectionMatrix);
            Shader.SetGlobalMat4("_viewMatrix", Cameras[cameraIndex].ViewMatrix);
            Shader.SetGlobalVector3("_cameraWorldSpacePos", Cameras[cameraIndex].Transform.Position);
            Shader.SetGlobalVector3("_cameraDirection", Cameras[cameraIndex].Transform.Forward);
        }
        private static void Update()
        {
            m_drawCount = 0;
            m_shaderMeshBindCount = 0;
            m_verticesCount = 0;
            for (int cameraIndex = 0; cameraIndex < Cameras.Count; cameraIndex++)
            {
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

                //apply global matrices
                mSetShaderCameraData(cameraIndex);

                //invoke update event
                mInvokeUpdates();

                //Render Everything
                //vvvvvvvvvvvvvvvvv

                if (DisableRendering)
                {
                    Window.SwapBuffers();
                    continue;
                }

                //invoke on render
                mInvokeOnRenders(cameraIndex);

                //apply dynamic batching
                //apply static batching

                Mesh previousMesh = null;
                Shader previousMaterial = null;

                if (Cameras[cameraIndex].EnabledWireFrame)
                {
                    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                }
                else
                {
                    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                }

                //render each renderer
                for (int i = 0; i < Entity.AllRenderers.Count; i++)
                {
                    var current = Entity.AllRenderers[i];
                    if (current == null || !current.Enabled)
                    {
                        continue;
                    }

                    //if no material, then don't render object
                    if (current.Material == null)
                    {
                        continue;
                    }

                    Mesh meshData = current.Mesh;
                    Shader material = current.Material;

                    //bind
                    //don't bind if the previous mesh and previous material are the same
                    if (meshData != previousMesh || material != previousMaterial)
                    {
                        //unbind previous mesh and shader
                        GL.BindVertexArray(0);
                        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
                        Shader.Unbind();

                        m_shaderMeshBindCount++;

                        GL.BindBuffer(BufferTarget.ElementArrayBuffer, meshData.ElementArrayBuffer);
                        GL.BindVertexArray(meshData.VertexArrayObject);
                        material.UseProgram();

                        previousMaterial = current.Material;
                        previousMesh = meshData;
                    }

                    m_verticesCount += meshData.VertexCount;

                    //apply model matrix
                    var val = current.Entity.Transform.WorldToLocalMatrix;
                    GL.UniformMatrix4(current.Material.GetUniformLocation("_modelMatrix"), false, ref val);

                    //draw
                    GL.DrawElements(PrimitiveType.Triangles, meshData.ElementCount, DrawElementsType.UnsignedInt, 0);
                    //GL.DrawElements(PrimitiveType.Lines, meshData.ElementCount, DrawElementsType.UnsignedInt, 0);

                    m_drawCount++;
                }

                //unbind
                GL.BindVertexArray(0);
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
                Shader.Unbind();

                Window.SwapBuffers();
            }
        }
    }
}
