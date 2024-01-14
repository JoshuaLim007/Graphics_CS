using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using StbImageSharp;
using System;
using All = OpenTK.Graphics.OpenGL4.All;

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

            if (Window != null)
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
            if (msaaSamples != 0)
            {
                GL.Enable(EnableCap.Multisample);
            }

            float statsInterval = 0.0f;

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
            Cameras = new List<Camera>();

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

            DefaultMaterial = new Shader("Default", "./Shaders/fragment.glsl", "./Shaders/vertex.glsl");
            DefaultMaterial.SetVector3("AlbedoColor", new Vector3(1, 1, 1));
            FullScreenQuad = CreateFullScreenQuad();
            PassthroughShader = new Shader("PassthroughShader", "./Shaders/CopyToScreen.frag", "./Shaders/Passthrough.vert");
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
        
        static void InvokeUpdates()
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
        static void InvokeOnRenders(Camera camera)
        {
            //invoke render event
            for (int i = 0; i < Entity.AllOnRenders.Count; i++)
            {
                var current = Entity.AllOnRenders[i];
                if (!current.IsActiveAndEnabled())
                {
                    continue;
                }
                current.OnRender(camera);
            }
        }
        static void SetShaderCameraData(Camera camera)
        {
            Shader.SetGlobalMat4("_projectionMatrix", camera.ProjectionMatrix);
            Shader.SetGlobalMat4("_viewMatrix", camera.ViewMatrix);
            Shader.SetGlobalVector3("_cameraWorldSpacePos", camera.Transform.Position);
            Shader.SetGlobalVector3("_cameraDirection", camera.Transform.Forward);
        }
        static void SetDrawMode(bool wireframe)
        {
            if (wireframe)
            {
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            }
            else
            {
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            }
        }

        public struct FrameBuffer
        {
            public int Width { get; }
            public int Height { get; }
            public int FrameBufferObject { get; }
            public int ColorAttach0 { get; }
            public int RenderBufferObject { get; }
            public FrameBuffer(int width, int height, int fbo, int c0, int rbo)
            {
                Width = width;
                Height = height;
                FrameBufferObject = fbo;
                ColorAttach0 = c0;
                RenderBufferObject = rbo;
            }
        }
        static int FullScreenQuad = 0;
        static Shader PassthroughShader = null;
        static int CreateFullScreenQuad()
        {
            int[] quad_VertexArrayID = new int[1];
            GL.GenVertexArrays(1, quad_VertexArrayID);
            GL.BindVertexArray(quad_VertexArrayID[0]);

            float[] g_quad_vertex_buffer_data = {
                    -1.0f, -1.0f,
                    1.0f, -1.0f,
                    -1.0f,  1.0f,
                    -1.0f,  1.0f,
                    1.0f, -1.0f,
                    1.0f,  1.0f,
                };

            int[] quad_vertexbuffer = new int[1];
            GL.GenBuffers(1, quad_vertexbuffer);
            GL.BindBuffer(BufferTarget.ArrayBuffer, quad_vertexbuffer[0]);
            GL.BufferData(BufferTarget.ArrayBuffer, sizeof(float) * g_quad_vertex_buffer_data.Length, g_quad_vertex_buffer_data, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
            int vao = quad_VertexArrayID[0];
            return vao;
        }
        public static void Blit(FrameBuffer src, FrameBuffer dst, Shader shader = null)
        {
            // second pass
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, dst.FrameBufferObject);
            GL.Viewport(0, 0, dst.Width, dst.Height);
            GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            Shader blitShader = shader ?? PassthroughShader;
            blitShader.SetVector2("MainTex_Size", new Vector2(dst.Width, dst.Height));
            blitShader.SetTexture(0, "MainTex", src.ColorAttach0);
            blitShader.UseProgram();

            GL.BindVertexArray(FullScreenQuad);
            GL.Disable(EnableCap.DepthTest);

            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }
        public static FrameBuffer CreateFrameBuffer(int width, int height, PixelInternalFormat pixelInternalFormat, PixelFormat pixelFormat)
        {
            int fbo;
            int textureColorbuffer;
            int rbo;

            // generate texture
            unsafe
            {
                GL.GenFramebuffers(1, &fbo);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
                GL.GenTextures(1, &textureColorbuffer);
            }

            GL.BindTexture(TextureTarget.Texture2D, textureColorbuffer);
            GL.TexImage2D(TextureTarget.Texture2D, 0, pixelInternalFormat,
                width, height, 0, pixelFormat, PixelType.UnsignedByte, (IntPtr)null);
            int linearFilter = (int)All.Linear;
            GL.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, ref linearFilter);
            GL.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, ref linearFilter);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, textureColorbuffer, 0);

            //render buffers
            unsafe
            {
                GL.GenRenderbuffers(1, &rbo);
            }
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, rbo);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, m_nativeWindowSettings.Size.X, m_nativeWindowSettings.Size.Y);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, rbo);

            DrawBuffersEnum[] attachments = { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1 };
            GL.DrawBuffers(2, attachments);

            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("ERROR::FRAMEBUFFER:: Framebuffer is not complete!");
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            return new FrameBuffer(width, height, fbo, textureColorbuffer, rbo);
        }
        public static void FreeFrameBuffer(FrameBuffer frameBuffer)
        {
            GL.DeleteFramebuffer(frameBuffer.FrameBufferObject);
            GL.DeleteTexture(frameBuffer.ColorAttach0);
            GL.DeleteRenderbuffer(frameBuffer.RenderBufferObject);
        }
        public static FrameBuffer GetScreenFrameBuffer()
        {
            return new FrameBuffer(m_nativeWindowSettings.Size.X, m_nativeWindowSettings.Size.Y, 0, 0, 0);
        }

        private static void Update()
        {
            var srcBuffer = CreateFrameBuffer(m_nativeWindowSettings.Size.X, m_nativeWindowSettings.Size.Y, PixelInternalFormat.Rgba, PixelFormat.Rgba);

            //bind RT
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, srcBuffer.FrameBufferObject);
            GL.Viewport(0, 0, srcBuffer.Width, srcBuffer.Height);

            //draw scene (first pass)
            m_drawCount = 0;
            m_shaderMeshBindCount = 0;
            m_verticesCount = 0;
            GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
            GL.Enable(EnableCap.DepthTest);
            for (int cameraIndex = 0; cameraIndex < Cameras.Count && !DisableRendering; cameraIndex++)
            {
                RenderCamera(Cameras[cameraIndex]);
            }

            // second pass
            Blit(srcBuffer, GetScreenFrameBuffer(), null);

            FreeFrameBuffer(srcBuffer);

            Window.SwapBuffers();
        }

        static void RenderCamera(Camera camera)
        {
            Mesh? previousMesh = null;
            Shader? previousMaterial = null;

            SetShaderCameraData(camera);
            InvokeUpdates();
            InvokeOnRenders(camera);
            SetDrawMode(camera.EnabledWireFrame);

            //TODO:
            //apply dynamic batching
            //apply static batching

            //render each renderer
            for (int i = 0; i < Entity.AllRenderers.Count; i++)
            {
                var current = Entity.AllRenderers[i];
                if (current == null || !current.Enabled || current.Material == null)
                {
                    continue;
                }
                Mesh meshData = current.Mesh;
                Shader material = current.Material;

                //bind shader and mesh
                //don't bind if the previous mesh and previous material are the same
                if (meshData != previousMesh || material != previousMaterial)
                {
                    //unbind previous mesh and shader
                    GL.BindVertexArray(0);
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
                    Shader.Unbind();

                    m_shaderMeshBindCount++;

                    //bind new stuff
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

                //render object
                GL.DrawElements(PrimitiveType.Triangles, meshData.ElementCount, DrawElementsType.UnsignedInt, 0);

                m_drawCount++;
            }

            //unbind
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            Shader.Unbind();
        }

    }
}
