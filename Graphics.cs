using JLUtility;
using OpenTK.Compute.OpenCL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using StbImageSharp;
using System;
using System.Diagnostics;
using System.Xml.Serialization;
using All = OpenTK.Graphics.OpenGL4.All;

namespace JLGraphics
{
    public struct Time
    {
        public static float FixedDeltaTime { get => Graphics.FixedDeltaTime; set => Graphics.FixedDeltaTime = Math.Clamp(value, 0.002f, 1.0f); }
        public static float UnscaledDeltaTime => Graphics.DeltaTime;
        public static float UnscaledSmoothDeltaTime => Graphics.SmoothDeltaTime;


        public static float DeltaTime => Graphics.DeltaTime * TimeScale;
        public static float SmoothDeltaTime => Graphics.SmoothDeltaTime * TimeScale;
        public static float ElapsedTime => Graphics.ElapsedTime * TimeScale;

        private static float m_timeScale = 1.0f;
        public static float TimeScale { get => m_timeScale; set => m_timeScale = Math.Clamp(value, 0, float.MaxValue); }
    }
    public static class Graphics
    {
        public const int MAXPOINTLIGHTS = 8;
        private static GameWindowSettings m_gameWindowSettings = null;
        private static NativeWindowSettings m_nativeWindowSettings = null;
        public static GameWindow Window { get; private set; } = null;
        public static Shader DefaultMaterial { get; private set; } = null;
        internal static float FixedDeltaTime { get; set; } = 0;
        internal static float DeltaTime { get; private set; } = 0;
        internal static float SmoothDeltaTime { get; private set; } = 0;
        internal static float ElapsedTime { get; private set; } = 0;
        public static bool DisableRendering { get; set; } = false;

        private static int m_drawCount = 0;
        private static int m_meshBindCount = 0;
        private static int m_shaderBindCount = 0;
        private static int m_materialUpdateCount = 0;
        private static int m_verticesCount = 0;
        private static bool m_isInit = false;
        private static List<Entity> AllInstancedObjects => InternalGlobalScope<Entity>.Values;
        private static List<Camera> AllCameras => InternalGlobalScope<Camera>.Values;
        public static Vector2i OutputResolution => m_nativeWindowSettings.Size;
        static FileTracker fileTracker;
        public static bool GetFileTracker(out FileTracker fileTracker)
        {
            fileTracker = Graphics.fileTracker;
            if(fileTracker == null)
            {
                return false;
            }
            return true;
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

            //Window.Load += Start; //start now happens after the first frame
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
#if DEBUG
                fileTracker.ResolveFileTrackQueue();
#endif
                DeltaTime = (time2.Ticks - time1.Ticks) / 10000000f;
                SmoothDeltaTime += (DeltaTime - SmoothDeltaTime) * 0.1f;
                ElapsedTime += DeltaTime;
                if (statsInterval > 0.5f)
                {
                    string stats = "";

                    stats += " | fixed delta time: " + FixedDeltaTime;
                    stats += " | draw count: " + m_drawCount;
                    stats += " | shader mesh bind count: " + m_shaderBindCount + ", " + m_meshBindCount;
                    stats += " | material update count: " + m_materialUpdateCount;
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
                    m_drawCount = 0;
                    m_shaderBindCount = 0;
                    m_materialUpdateCount = 0;
                    m_meshBindCount = 0;
                    m_verticesCount = 0;

                    InvokeNewStarts();
                    Update();
                }
                else
                {
                    setFrame++;
                }

                time1 = time2;
            };
        }
        static ShaderProgram DefaultShaderProgram;
        static ShaderProgram PassthroughShaderProgram;

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

            m_gameWindowSettings.RenderFrequency = renderFrequency;
            m_gameWindowSettings.UpdateFrequency = updateFrequency;

            m_nativeWindowSettings.Size = windowResolution;
            m_nativeWindowSettings.Title = windowName;
            m_nativeWindowSettings.IsEventDriven = false;

            m_nativeWindowSettings.API = ContextAPI.OpenGL;
            m_nativeWindowSettings.APIVersion = Version.Parse("4.1");

            InitWindow(updateFrequency, 0);

            var defaultShader = new ShaderProgram("DefaultShader", "./Shaders/fragment.glsl", "./Shaders/vertex.glsl");
            var passThroughShader = new ShaderProgram("PassThroughShader", "./Shaders/CopyToScreen.frag", "./Shaders/Passthrough.vert");

#if DEBUG
            fileTracker = new FileTracker();
            fileTracker.AddFileObject(defaultShader.FragFile);
            fileTracker.AddFileObject(defaultShader.VertFile);
            fileTracker.AddFileObject(passThroughShader.FragFile);
            fileTracker.AddFileObject(passThroughShader.VertFile);
#endif

            defaultShader.CompileProgram();
            passThroughShader.CompileProgram();

            DefaultShaderProgram = defaultShader;
            PassthroughShaderProgram = passThroughShader;

            DefaultMaterial = new Shader("Default Material", defaultShader);

            DefaultMaterial.SetVector3("AlbedoColor", new Vector3(1, 1, 1));
            FullScreenQuad = CreateFullScreenQuad();
            PassthroughShader = new Shader("Default Passthrough", passThroughShader);
            MainFrameBuffer = new FrameBuffer(m_nativeWindowSettings.Size.X, m_nativeWindowSettings.Size.Y, false, new TFP(PixelInternalFormat.Rgb16f, PixelFormat.Rgb));
            renderPassCommandBuffer = new CommandBuffer();
        }
        public static void Free()
        {
            Window.Close();
            PassthroughShader = null;
            MainFrameBuffer = null;
            renderPassCommandBuffer = null;
            DefaultMaterial = null;
            m_gameWindowSettings = null;
            m_nativeWindowSettings = null;
            Window = null;
            fileTracker = null;
            for (int i = 0; i < renderPasses.Count; i++)
            {
                renderPasses[i].Dispose();
            }
            renderPasses = null;
            DefaultShaderProgram.Dispose();
            PassthroughShaderProgram.Dispose();
            GL.DeleteVertexArray(FullScreenQuad);
            m_isInit = false;
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
            m_nativeWindowSettings.Size = new Vector2i(args.Width, args.Height);
            MainFrameBuffer.Dispose();
            MainFrameBuffer = new FrameBuffer(m_nativeWindowSettings.Size.X, m_nativeWindowSettings.Size.Y, true, new TFP(PixelInternalFormat.Rgb16f, PixelFormat.Rgb));
            GL.Viewport(0, 0, args.Width, args.Height);
            for (int i = 0; i < AllCameras.Count; i++)
            {
                AllCameras[i].Width = args.Width;
                AllCameras[i].Height = args.Height;
            }
        }

        private static void InvokeNewStarts()
        {
            for (int i = 0; i < InternalGlobalScope<IStart>.Count; i++)
            {
                var current = InternalGlobalScope<IStart>.Values[i];
                if (!current.IsActiveAndEnabled())
                {
                    continue;
                }
                current.Start();
            }
            InternalGlobalScope<IStart>.Clear();
        }

        private static void FixedUpdate()
        {
            for (int i = 0; i < InternalGlobalScope<IFixedUpdate>.Count; i++)
            {
                var current = InternalGlobalScope<IFixedUpdate>.Values[i];
                if (current.IsActiveAndEnabled())
                {
                    current.FixedUpdate();
                }
            }
        }
        
        static void InvokeUpdates()
        {
            for (int i = 0; i < InternalGlobalScope<IUpdate>.Count; i++)
            {
                var current = InternalGlobalScope<IUpdate>.Values[i];
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
            for (int i = 0; i < InternalGlobalScope<IOnRender>.Count; i++)
            {
                var current = InternalGlobalScope<IOnRender>.Values[i];
                if (!current.IsActiveAndEnabled())
                {
                    continue;
                }
                current.OnRender(camera);
            }
        }
        static void SetShaderCameraData(Camera camera)
        {
            Shader.SetGlobalMat4("ProjectionMatrix", camera.ProjectionMatrix);
            Shader.SetGlobalMat4("ViewMatrix", camera.ViewMatrix);
            Shader.SetGlobalVector3("CameraWorldSpacePos", camera.Transform.Position);
            Shader.SetGlobalVector3("CameraDirection", camera.Transform.Forward);
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

        static int FullScreenQuad = 0;
        static Shader PassthroughShader = null;
        static FrameBuffer MainFrameBuffer = null;
        internal static int CreateFullScreenQuad()
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
        internal static void Blit(FrameBuffer src, FrameBuffer dst, Shader shader = null)
        {
            // second pass
            int width = dst != null ? dst.Width : Window.Size.X;
            int height = dst != null ? dst.Height : Window.Size.Y;
            int fbo = dst != null ? dst.FrameBufferObject : 0;

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.Viewport(0, 0, width, height);
            GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            Shader blitShader = shader ?? PassthroughShader;
            blitShader.SetVector2("MainTex_Size", new Vector2(width, height));
            blitShader.SetTexture("MainTex", src.ColorAttachments[0]);
            //blitShader.SetTexture("DepthTex", src.DepthBufferTextureId);
            blitShader.UseProgram();
            blitShader.UpdateUniforms();

            GL.BindVertexArray(FullScreenQuad);
            GL.Disable(EnableCap.DepthTest);
            m_drawCount++;
            m_verticesCount += 6;

            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }
        
        static List<RenderPass> renderPasses = new List<RenderPass>();
        public static void EnqueueRenderPass(RenderPass renderPass)
        {
            renderPasses.Add(renderPass);
        }
        public static void DequeueRenderPass(RenderPass renderPass)
        {
            renderPasses.Remove(renderPass);
        }
        static int ExecuteRenderPasses(int startingIndex, int renderQueueEnd)
        {
            int renderPassIndex;
            for (renderPassIndex = startingIndex; renderPassIndex < renderPasses.Count; renderPassIndex++)
            {
                if (renderPasses[renderPassIndex].Queue > (renderQueueEnd - 1))
                {
                    return renderPassIndex;
                }
                renderPasses[renderPassIndex].Execute(renderPassCommandBuffer, MainFrameBuffer);
                renderPassCommandBuffer.Invoke();
            }
            return renderPassIndex;
        }
        static CommandBuffer renderPassCommandBuffer;
        private static void Update()
        {
            renderPasses.Sort();
            for (int i = 0; i < renderPasses.Count; i++)
            {
                renderPasses[i].FrameSetup();
            }

            //bind Main render texture RT
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, MainFrameBuffer.FrameBufferObject);
            GL.Viewport(0, 0, MainFrameBuffer.Width, MainFrameBuffer.Height);

            //draw opaques (first pass) (forward rendering)
            GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
            GL.Enable(EnableCap.DepthTest);
            InvokeUpdates();

            //prepass (Prepass -> Opaque - 1)
            int renderPassIndex = ExecuteRenderPasses(0, (int)RenderQueue.AfterOpaques);

            //render Opaques
            //TODO: move to render pass class
            for (int cameraIndex = 0; cameraIndex < AllCameras.Count && !DisableRendering; cameraIndex++)
            {
                SetupLights(AllCameras[cameraIndex]);
                RenderCamera(AllCameras[cameraIndex]);
            }

            //Post opaque pass (Opaque -> Transparent - 1)
            renderPassIndex = ExecuteRenderPasses(renderPassIndex, (int)RenderQueue.AfterTransparents);

            //render Transparents

            //Post transparent render pass (Transparent -> PostProcess - 1)
            renderPassIndex = ExecuteRenderPasses(renderPassIndex, (int)RenderQueue.AfterPostProcessing);

            //Post post processing (Transparent -> End)
            ExecuteRenderPasses(renderPassIndex, int.MaxValue);

            //blit render buffer to screen
            Blit(MainFrameBuffer, null, null);

            //frame cleanup
            for (int i = 0; i < renderPasses.Count; i++)
            {
                renderPasses[i].FrameCleanup();
            }

            Window.SwapBuffers();

        }

        static void SetupLights(Camera camera)
        {
            List<PointLight> pointLights = new List<PointLight>();
            var lights = InternalGlobalScope<Light>.Values;
            for (int i = 0; i < lights.Count; i++)
            {
                switch (lights[i])
                {
                    case DirectionalLight t0:
                        Shader.SetGlobalVector3("DirectionalLight.Color", t0.Color);
                        Shader.SetGlobalVector3("DirectionalLight.Direction", t0.Transform.Forward);
                        break;
                    case PointLight t0:
                        pointLights.Add(t0);
                        break;
                }
            }

            //point light frustum culling?

            //render closer point lights first
            pointLights.Sort((a, b) =>
            {
                var distance0 = (a.Transform.Position - camera.Transform.Position).LengthSquared;
                var distance1 = (b.Transform.Position - camera.Transform.Position).LengthSquared;
                if(distance0 < distance1)
                {
                    return -1;
                }
                else
                {
                    return 1;
                }
            });
            for (int i = 0; i < MathF.Min(pointLights.Count, MAXPOINTLIGHTS); i++)
            {
                Shader.SetGlobalVector3("PointLights[" + i + "].Position", pointLights[i].Transform.Position);
                Shader.SetGlobalVector3("PointLights[" + i + "].Color", pointLights[i].Color);
                Shader.SetGlobalFloat("PointLights[" + i + "].Constant", pointLights[i].AttenConstant);
                Shader.SetGlobalFloat("PointLights[" + i + "].Linear", pointLights[i].AttenLinear);
                Shader.SetGlobalFloat("PointLights[" + i + "].Exp", pointLights[i].AttenExp);
            }
            Shader.SetGlobalInt("PointLightCount", (int)MathF.Min(pointLights.Count, MAXPOINTLIGHTS));
        }

        static void RenderCamera(Camera camera)
        {
            Mesh? previousMesh = null;
            Shader? previousMaterial = null;

            SetShaderCameraData(camera);
            InvokeOnRenders(camera);
            SetDrawMode(camera.EnabledWireFrame);

            //TODO:
            //apply dynamic batching
            //apply static batching

            //render each renderer
            for (int i = 0; i < InternalGlobalScope<Renderer>.Count; i++)
            {
                var current = InternalGlobalScope<Renderer>.Values[i];
                if (current == null || !current.Enabled || current.Material == null)
                {
                    continue;
                }
                Mesh meshData = current.Mesh;
                Shader material = current.Material;

                //bind shader and mesh
                //don't bind if the previous mesh and previous material are the same
                if (meshData != previousMesh)
                {
                    m_meshBindCount++;
                    GL.BindVertexArray(0);
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, meshData.ElementArrayBuffer);
                    GL.BindVertexArray(meshData.VertexArrayObject);
                    previousMesh = meshData;
                }
                if (material != previousMaterial)
                {
                    if(material.Program != previousMaterial?.Program)
                    {
                        m_shaderBindCount++;
                        material.UseProgram();
                    }
                    m_materialUpdateCount++;
                    material.UpdateUniforms();
                    previousMaterial = material;
                }

                m_verticesCount += meshData.VertexCount;

                //apply model matrix
                var val = current.Entity.Transform.WorldToLocalMatrix;
                GL.UniformMatrix4(current.Material.GetUniformLocation("ModelMatrix"), false, ref val);

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
