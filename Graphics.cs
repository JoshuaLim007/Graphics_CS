using ImGuiNET;
using JLUtility;
using OpenTK.Graphics.Egl;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using StbImageSharp;
using StbiSharp;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Debug = JLUtility.Debug;

namespace JLGraphics
{
    public static class GlobalUniformNames
    {
        readonly static public string SkyBox = "SkyBox";
        readonly static public string ViewProjectionMatrix = "ProjectionViewMatrix";
        readonly static public string SkyBoxIntensity = "skyBoxIntensity";
        readonly static public string AmbientSkyColor = "SkyColor";
        readonly static public string AmbientHorizonColor = "HorizonColor";
        readonly static public string AmbientGroundColor = "GroundColor";
        readonly static public string DepthTexture = "_CameraDepthTexture";
    }
    public struct Time
    {
        public static float FixedDeltaTime { get => Graphics.Instance.FixedDeltaTime; set => Graphics.Instance.FixedDeltaTime = Math.Clamp(value, float.Epsilon, 1.0f); }
        public static float UnscaledDeltaTime => Graphics.Instance.DeltaTime;
        public static float UnscaledSmoothDeltaTime => Graphics.Instance.SmoothDeltaTime;


        public static float DeltaTime => Graphics.Instance.DeltaTime * TimeScale;
        public static float SmoothDeltaTime => Graphics.Instance.SmoothDeltaTime * TimeScale;
        public static float ElapsedTime => Graphics.Instance.ElapsedTime * TimeScale;

        private static float m_timeScale = 1.0f;
        public static float TimeScale { get => m_timeScale; set => m_timeScale = Math.Clamp(value, 0, float.MaxValue); }
    }
    public sealed class Graphics : IDisposable
    {
        static Lazy<Graphics> m_lazyGraphics = new Lazy<Graphics>(() => new Graphics());
        public static Graphics Instance { get { return m_lazyGraphics.Value; } }
        private Graphics()
        {
        }

        public const int MAXPOINTLIGHTS = 128;
        public GameWindow Window { get; private set; } = null;
        private Vector2i RenderBufferSize { get; set; }
        public Vector2i GetRenderWindowSize()
        {
            return RenderBufferSize;
        }
        public bool IsCursorInSceneWindow { get; private set; } = false;
        public bool IsSceneViewFocused { get; private set; } = false;
        public bool RenderGUI { get; private set; } = false;
        public Shader DefaultMaterial { get; private set; } = null;
        internal float FixedDeltaTime { get; set; } = 0;
        internal float DeltaTime { get; private set; } = 0;
        internal float SmoothDeltaTime { get; private set; } = 0;
        internal float ElapsedTime { get; private set; } = 0;
        public bool DisableRendering { get; set; } = false;

        private int m_drawCount = 0;
        private int m_meshBindCount = 0;
        private int m_shaderBindCount = 0;
        private int m_materialUpdateCount = 0;
        private int m_verticesCount = 0;
        private bool m_isInit = false;
        private List<Entity> AllInstancedObjects => InternalGlobalScope<Entity>.Values;
        private List<Camera> AllCameras => InternalGlobalScope<Camera>.Values;
        public Vector2i OutputResolution => new Vector2i((int)(GetRenderWindowSize().X * RenderScale), (int)(GetRenderWindowSize().Y * RenderScale));
        FileTracker fileTracker;
        public bool GetFileTracker(out FileTracker fileTracker)
        {
            fileTracker = this.fileTracker;
            if(fileTracker == null)
            {
                return false;
            }
            return true;
        }
        private void InitWindow(int msaaSamples, GameWindowSettings m_gameWindowSettings, NativeWindowSettings m_nativeWindowSettings)
        {
            m_nativeWindowSettings.NumberOfSamples = msaaSamples;

            if (Window != null)
            {
                m_nativeWindowSettings.SharedContext = Window.Context;
                Window.Dispose();
            }

            Window = new GameWindow(m_gameWindowSettings, m_nativeWindowSettings);
            RenderBufferSize = Window.Size;
            Window.VSync = 0;
            Window.Resize += Resize;
            if (RenderGUI)
            {
                guiController = new ImGuiController(Window.Size.X, Window.Size.Y);
                Window.TextInput += (e) => {
                    guiController.PressChar((char)e.Unicode);
                };
                Window.MouseWheel += (e) =>
                {
                    guiController.MouseScroll(e.Offset);
                };
            }
            else
            {
                IsCursorInSceneWindow = true;
                IsSceneViewFocused = true;
            }
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.Dither);
            GL.Enable(EnableCap.Blend);
            if (msaaSamples != 0)
            {
                GL.Enable(EnableCap.Multisample);
            }

            Window.UpdateFrame += UpdateFrame;
        }
        ShaderProgram DefaultShaderProgram;
        ShaderProgram PassthroughShaderProgram;
        ShaderProgram DepthPrepassShaderProgram;
        float previousRenderScale = 1.0f;
        public float RenderScale { get; set; } = 1.0f;
        ImGuiController guiController;
        void InitFramebuffers() {

            if (MainFrameBuffer != null)
            {
                MainFrameBuffer.Dispose();
                DepthTextureBuffer.Dispose();
            }

            float scale = RenderScale;// MathF.Sqrt(RenderScale);
            var colorSettings = new TFP()
            {
                wrapMode = TextureWrapMode.MirroredRepeat,
                maxMipmap = 0,
                minFilter = TextureMinFilter.Linear,
                magFilter = TextureMagFilter.Linear,
                internalFormat = PixelInternalFormat.Rgb32f,
                pixelFormat = PixelFormat.Rgb
            };
            var depthSettings = new TFP() { 
                wrapMode = TextureWrapMode.ClampToEdge,
                maxMipmap = 0,
                minFilter = TextureMinFilter.Linear,
                magFilter = TextureMagFilter.Linear,
                internalFormat = PixelInternalFormat.R32f,
                pixelFormat = PixelFormat.Red,
            };
            var windowSize = GetRenderWindowSize();
            MainFrameBuffer = new FrameBuffer((int)MathF.Ceiling(windowSize.X * scale), (int)MathF.Ceiling(windowSize.Y * scale), true, colorSettings);
            MainFrameBuffer.SetName("Main frame buffer");
            DepthTextureBuffer = new FrameBuffer((int)MathF.Ceiling(windowSize.X * scale), (int)MathF.Ceiling(windowSize.Y * scale), false, depthSettings);
            DepthTextureBuffer.SetName("Depth texture buffer");
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("_CameraDepthTexture"), DepthTextureBuffer.TextureAttachments[0]);
        }
        public void Init(string windowName, Vector2i windowResolution, float renderFrequency, float fixedUpdateFrequency, bool renderDebugGui)
        {
            m_isInit = true;
            previousRenderScale = RenderScale;
            StbImage.stbi_set_flip_vertically_on_load(1);
            Stbi.SetFlipVerticallyOnLoad(true);

            FixedDeltaTime = 1.0f / fixedUpdateFrequency;

            var m_gameWindowSettings = GameWindowSettings.Default;
            var m_nativeWindowSettings = NativeWindowSettings.Default;
            m_gameWindowSettings.UpdateFrequency = renderFrequency;

            m_nativeWindowSettings.Size = windowResolution;
            m_nativeWindowSettings.Title = windowName;
            m_nativeWindowSettings.IsEventDriven = false;

            m_nativeWindowSettings.API = ContextAPI.OpenGL;
            m_nativeWindowSettings.APIVersion = Version.Parse("4.1");
            RenderGUI = renderDebugGui;

            InitWindow(0, m_gameWindowSettings, m_nativeWindowSettings);

            var defaultShader = new ShaderProgram("DefaultShader", "./Shaders/fragment.glsl", "./Shaders/vertex.glsl");
            var passThroughShader = new ShaderProgram("PassThroughShader", "./Shaders/CopyToScreen.frag", "./Shaders/Passthrough.vert");
            var depthOnlyShader = new ShaderProgram("DepthOnlyShader", "./Shaders/DepthOnly.frag", "./Shaders/vertexSimple.glsl");
            var skyBoxShaderProrgam = new ShaderProgram("Skybox Shader", "./Shaders/SkyBoxFrag.glsl", "./Shaders/skyboxVert.glsl");
            var skyboxDepthPrepassProgram = new ShaderProgram("Skybox Shader", "./Shaders/fragmentEmpty.glsl", "./Shaders/skyboxVert.glsl");
#if DEBUG
            fileTracker = new FileTracker();
            fileTracker.AddFileObject(defaultShader.FragFile);
            fileTracker.AddFileObject(defaultShader.VertFile);
            fileTracker.AddFileObject(passThroughShader.FragFile);
            fileTracker.AddFileObject(passThroughShader.VertFile);
#endif
            skyBoxShaderProrgam.CompileProgram();
            defaultShader.CompileProgram();
            passThroughShader.CompileProgram();
            depthOnlyShader.CompileProgram();
            skyboxDepthPrepassProgram.CompileProgram();

            DefaultShaderProgram = defaultShader;
            PassthroughShaderProgram = passThroughShader;
            DepthPrepassShaderProgram = depthOnlyShader;
            SkyboxShader = new Shader("Skybox material", skyBoxShaderProrgam);
            SkyboxDepthPrepassShader = new Shader("Skybox depth prepass material", skyboxDepthPrepassProgram);
            SkyboxDepthPrepassShader.DepthTestFunction = DepthFunction.Less;

            DefaultMaterial = new Shader("Default Material", DefaultShaderProgram);
            DepthPrepassShader = new Shader("Default depth only", DepthPrepassShaderProgram);
            DepthPrepassShader.DepthTestFunction = DepthFunction.Less;
            DepthPrepassShader.ColorMask[0] = true;
            DepthPrepassShader.ColorMask[1] = false;
            DepthPrepassShader.ColorMask[2] = false;
            DepthPrepassShader.ColorMask[3] = false;
            DefaultMaterial.SetVector3(Shader.GetShaderPropertyId(DefaultMaterialUniforms.AlbedoColor), new Vector3(1, 1, 1));
            DefaultMaterial.SetFloat(Shader.GetShaderPropertyId(DefaultMaterialUniforms.Smoothness), 0.5f);
            DefaultMaterial.SetFloat(Shader.GetShaderPropertyId(DefaultMaterialUniforms.Metalness), 0.0f);
            DefaultMaterial.SetFloat(Shader.GetShaderPropertyId(DefaultMaterialUniforms.NormalsStrength), 1.0f);
            FullScreenQuad = Mesh.CreateQuadMesh();
            BasicCube = Mesh.CreateCubeMesh();
            PassthroughShader = new Shader("Default Passthrough", PassthroughShaderProgram);
            InitFramebuffers();
        }
        public void Dispose()
        {
            if (m_isInit)
            {
                Debug.Log("Graphics is not initialized!", Debug.Flag.Error);
            }
            if(RenderGUI)
                guiController.Dispose();
            temporaryUpdateFrameCommands.Clear();
            SkyboxDepthPrepassShader.Program.Dispose();
            SkyboxShader.Program.Dispose();
            Window.Close();
            Window.Dispose();
            MainFrameBuffer = null;
            DepthTextureBuffer = null;
            PassthroughShader = null;
            MainFrameBuffer = null;
            DepthTextureBuffer = null;
            DefaultMaterial = null;
            Window = null;
            fileTracker = null;
            for (int i = 0; i < renderPasses.Count; i++)
            {
                renderPasses[i].Dispose();
            }
            renderPasses = null;
            DefaultShaderProgram.Dispose();
            PassthroughShaderProgram.Dispose();
            DepthPrepassShaderProgram.Dispose();
            Mesh.FreeMeshObject(FullScreenQuad);
            m_isInit = false;
            m_lazyGraphics = new Lazy<Graphics>(() => new Graphics());
            DestructorCommands.Instance.ExecuteCommands();
        }
        public void Run()
        {
            if (!m_isInit)
            {
                Debug.Log("Graphics not initialized!", Debug.Flag.Error);
                throw new Exception("Graphics not initialized!");
            }
            Window.Run();

        }
        float fixedTimer = 0;
        int frameIncrement = 0;
        List<Action> temporaryUpdateFrameCommands = new List<Action>();
        float smoothDeltaCount = 0;
        private void UpdateFrame(FrameEventArgs eventArgs)
        {
            //do any one time update frame actions
            if(temporaryUpdateFrameCommands.Count > 0)
            {
                for (int i = 0; i < temporaryUpdateFrameCommands.Count; i++)
                {
                    temporaryUpdateFrameCommands[i].Invoke();
                }
                temporaryUpdateFrameCommands.Clear();
            }

            if (RenderGUI)
            {
                guiController.Update(Window, Time.DeltaTime);
                ImGui.DockSpaceOverViewport();
                ImGui.ShowDebugLogWindow();
                ImGui.ShowMetricsWindow();
            }

            InvokeNewStarts();

            fixedTimer += Time.DeltaTime;
            if (fixedTimer >= Time.FixedDeltaTime)
            {
                float t = fixedTimer / Time.FixedDeltaTime;
                int howMany = (int)MathF.Floor(t);
                for (int i = 0; i < howMany; i++)
                {
                    FixedUpdate();
                }
                fixedTimer = 0;
            }

            InvokeUpdates();

            float updateFreq = 1.0f / FixedDeltaTime;


#if DEBUG
            fileTracker.ResolveFileTrackQueue();
#endif
            DeltaTime = (float)Window.UpdateTime;// (time.Ticks - time1.Ticks) / 10000000f;
            smoothDeltaCount = MathF.Min(++smoothDeltaCount, 60);
            SmoothDeltaTime = SmoothDeltaTime * (1.0f - 1.0f / smoothDeltaCount) + DeltaTime * (1.0f / smoothDeltaCount);

            ElapsedTime += DeltaTime;

            string stats = "";
            stats += " | fixed delta time: " + FixedDeltaTime;
            stats += " | draw count: " + m_drawCount;
            stats += " | shader mesh bind count: " + m_shaderBindCount + ", " + m_meshBindCount;
            stats += " | material update count: " + m_materialUpdateCount;
            stats += " | vertices: " + m_verticesCount;
            stats += " | total world objects: " + AllInstancedObjects.Count;
            stats += " | fps: " + 1.0f / SmoothDeltaTime;
            stats += " | delta time: " + SmoothDeltaTime;
            Window.Title = stats;

            m_drawCount = 0;
            m_shaderBindCount = 0;
            m_materialUpdateCount = 0;
            m_meshBindCount = 0;
            m_verticesCount = 0;
            RenderScaleChange(RenderScale);

            if (RenderGUI)
            {
                ImGui.Begin("Scene Window");
                var pos = ImGui.GetCursorPos();
                var size = ImGui.GetWindowSize();
                RenderBufferSize = new Vector2i((int)size.X, (int)size.Y);
                if(GuiRenderSceneSize.X != RenderBufferSize.X || GuiRenderSceneSize.Y != RenderBufferSize.Y)
                {
                    GuiRenderSceneSize = RenderBufferSize;
                    InitFramebuffers();
                }
                var cursorPos = ImGui.GetCursorScreenPos();
                ImGui.GetWindowDrawList().AddImage(
                    (IntPtr)(MainFrameBuffer.TextureAttachments[0].GlTextureID),
                    new System.Numerics.Vector2(cursorPos.X, cursorPos.Y),
                    new System.Numerics.Vector2(cursorPos.X + MainFrameBuffer.Width, cursorPos.Y + MainFrameBuffer.Height),
                    new System.Numerics.Vector2(0, 1),
                    new System.Numerics.Vector2(1, 0));

                if (Window.CursorState == CursorState.Grabbed || Window.CursorState == CursorState.Hidden)
                {
                    if (!ImGui.IsWindowHovered())
                    {
                        Window.MousePosition = new Vector2(cursorPos.X + GuiRenderSceneSize.X * 0.5f, cursorPos.Y + GuiRenderSceneSize.Y * 0.5f);
                        Window.ProcessInputEvents();
                    }
                    IsCursorInSceneWindow = true;
                    IsSceneViewFocused = true;
                }
                else
                {
                    IsSceneViewFocused = false;
                }

                if (ImGui.IsWindowHovered())
                {
                    IsCursorInSceneWindow = true;
                }
                else
                {
                    IsSceneViewFocused = false;
                    IsCursorInSceneWindow = false;
                }
            }
            
            frameIncrement++;
            Shader.SetGlobalInt(Shader.GetShaderPropertyId("_Frame"), frameIncrement);

            DoRenderUpdate();

            if (RenderGUI)
            {
                ImGui.End();
                guiController.Render();
            }

            Renderer.NewRendererAdded = false;
            Window.SwapBuffers();
            DestructorCommands.Instance.ExecuteCommands();
        }
        private bool WindowResized = false;
        private Vector2i WindowResizeResults;
        private Vector2i GuiRenderSceneSize = new Vector2i(0,0);
        private void Resize(ResizeEventArgs args)
        {
            if(args.Width == Window.Size.X && args.Height == Window.Size.X)
            {
                return;
            }
            void DoResize()
            {
                Debug.Log("Window resized: " + WindowResizeResults);
                Window.Size = new Vector2i(WindowResizeResults.X, WindowResizeResults.Y);
                InitFramebuffers();
                GL.Viewport(0, 0, WindowResizeResults.X, WindowResizeResults.Y);
                
                if(RenderGUI)
                    guiController.WindowResized(WindowResizeResults.X, WindowResizeResults.Y);
                else
                    RenderBufferSize = Window.Size;

                WindowResized = false;
            }
            WindowResizeResults = new Vector2i(args.Width, args.Height);
            if (!WindowResized)
            {
                temporaryUpdateFrameCommands.Add(DoResize);
                WindowResized = true;
            }
        }

        private bool WindowScaleChanged = false;
        private void RenderScaleChange(float newScale)
        {
            RenderScale = MathHelper.Clamp(newScale, 0.1f, 2.0f);
            if(previousRenderScale == RenderScale)
            {
                return;
            }
            previousRenderScale = RenderScale;

            void DoRenderScaleChange()
            {
                Debug.Log("Render Scale Update: " + RenderScale);
                InitFramebuffers();
                WindowScaleChanged = false;
            }

            if (!WindowScaleChanged)
            {
                temporaryUpdateFrameCommands.Add(DoRenderScaleChange);
                WindowScaleChanged = true;
            }
        }
        private void InvokeNewStarts()
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

        private void FixedUpdate()
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
        
        void InvokeUpdates()
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
        void InvokeOnRenders(Camera camera)
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
        void SetShaderCameraData(Camera camera)
        {
            Shader.SetGlobalMat4(Shader.GetShaderPropertyId("ProjectionMatrix"), camera.ProjectionMatrix);
            Shader.SetGlobalMat4(Shader.GetShaderPropertyId("ViewMatrix"), camera.ViewMatrix);
            var vp = camera.ViewMatrix * camera.ProjectionMatrix;
            Shader.SetGlobalMat4(Shader.GetShaderPropertyId("InvProjectionViewMatrix"), vp.Inverted());
            Shader.SetGlobalMat4(Shader.GetShaderPropertyId("ProjectionViewMatrix"), vp);
            Shader.SetGlobalVector3(Shader.GetShaderPropertyId("CameraWorldSpacePos"), camera.Transform.Position);
            Shader.SetGlobalVector3(Shader.GetShaderPropertyId("CameraDirection"), camera.Transform.Forward);
            Shader.SetGlobalVector4(Shader.GetShaderPropertyId("CameraParams"), new Vector4(camera.Fov, camera.Width / camera.Height, camera.Near, camera.Far));
            Shader.SetGlobalVector2(Shader.GetShaderPropertyId("RenderSize"), new Vector2(camera.Width * RenderScale, camera.Height * RenderScale));
            Shader.SetGlobalFloat(Shader.GetShaderPropertyId("RenderScale"), RenderScale);
        }

        MeshPrimative FullScreenQuad;
        MeshPrimative BasicCube;
        public Shader PassthroughShader { get; private set; } = null;
        public Shader DepthPrepassShader { get; private set; } = null;
        public Shader SkyboxShader { get; private set; } = null;
        Shader SkyboxDepthPrepassShader;
        FrameBuffer MainFrameBuffer = null;
        FrameBuffer DepthTextureBuffer = null;
        internal void Blit(FrameBuffer src, FrameBuffer dst, bool restoreSrc, Shader shader = null)
        {
            StartBlitUnsafe(shader);
            BlitUnsafe(src, dst);
            EndBlitUnsafe(shader);
            if (restoreSrc)
            {
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, src.FrameBufferObject);
                GL.Viewport(0, 0, src.Width, src.Height);
            }
        }
        
        bool blitUnsafeFlag = false;
        Shader unsafeBlitShader = null;
        internal void StartBlitUnsafe(Shader shader)
        {
            if (blitUnsafeFlag)
            {
                Debug.Log("StartBlitUnsafe already started!", Debug.Flag.Error);
            }
            blitUnsafeFlag = true;
            Shader blitShader = shader ?? PassthroughShader;
            blitShader.DepthTest = false;
            this.unsafeBlitShader = blitShader;
        }
        internal void BlitUnsafe(FrameBuffer src, FrameBuffer dst)
        {
            if (!blitUnsafeFlag)
            {
                Debug.Log("BlitUnsafe out of range!", Debug.Flag.Error);
            }
            if (src == null)
            {
                Debug.Log("Cannot blit with null src!", Debug.Flag.Error);
                return;
            }

            // second pass
            var renderWindowSize = GetRenderWindowSize();
            int width = dst != null ? dst.Width : renderWindowSize.X;
            int height = dst != null ? dst.Height : renderWindowSize.Y;
            int fbo = dst != null ? dst.FrameBufferObject : 0;
            
            unsafeBlitShader.SetVector2(Shader.GetShaderPropertyId("MainTex_TexelSize"), new Vector2(1.0f / width, 1.0f / height));
            unsafeBlitShader.SetTexture(Shader.GetShaderPropertyId("MainTex"), src.TextureAttachments[0]);
            unsafeBlitShader.AttachShaderForRendering();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.Viewport(0, 0, width, height);
            GL.BindVertexArray(FullScreenQuad.VAO);

            m_drawCount++;
            m_verticesCount += FullScreenQuad.VertexCount;
            m_shaderBindCount++;
            m_meshBindCount++;

            GL.DrawArrays(PrimitiveType.Triangles, 0, FullScreenQuad.VertexCount);
        }
        internal void EndBlitUnsafe(Shader shader)
        {
            Shader blitShader = shader ?? PassthroughShader;
            if (!blitUnsafeFlag)
            {
                Debug.Log("EndBlitUnsafe already ended!", Debug.Flag.Error);
            }
            if(blitShader != unsafeBlitShader)
            {
                Debug.Log("EndBlitUnsafe blit shader mismatch!", Debug.Flag.Error);
            }
            blitUnsafeFlag = false;
        }
        List<RenderPass> renderPasses = new List<RenderPass>();
        public void EnqueueRenderPass(RenderPass renderPass)
        {
            renderPasses.Add(renderPass);
        }
        public void DequeueRenderPass(RenderPass renderPass)
        {
            renderPasses.Remove(renderPass);
        }
        public T GetRenderPass<T>() where T : RenderPass
        {
            for (int i = 0; i < renderPasses.Count; i++)
            {
                if (renderPasses[i].GetType() == typeof(T))
                {
                    return (T)renderPasses[i];
                }
            }
            return null;
        }
        int ExecuteRenderPasses(int startingIndex, int renderQueueEnd)
        {
            int renderPassIndex;
            for (renderPassIndex = startingIndex; renderPassIndex < renderPasses.Count; renderPassIndex++)
            {
                if (renderPasses[renderPassIndex].Queue > (renderQueueEnd - 1))
                {
                    return renderPassIndex;
                }
                var stopw = Stopwatch.StartNew();
                renderPasses[renderPassIndex].Execute(MainFrameBuffer);
                stopw.Stop();
                var ms = stopw.Elapsed.TotalMilliseconds;
                //Debug.Log(renderPasses[renderPassIndex].Name + ": " + ms + " ms");
            }
            return renderPassIndex;
        }

        public void RenderSkyBox(Camera camera, Shader overrideShader = null)
        {
            //render skybox
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, BasicCube.EBO);
            GL.BindVertexArray(BasicCube.VAO);
            var mat = overrideShader == null ? SkyboxShader : overrideShader;
            mat.SetMat4(Shader.GetShaderPropertyId("ModelMatrix"), Matrix4.CreateTranslation(camera.Transform.Position));
            mat.AttachShaderForRendering();
            GL.CullFace(CullFaceMode.Front);
            GL.DrawElements(PrimitiveType.Triangles, BasicCube.IndiciesCount, DrawElementsType.UnsignedInt, 0);
            GL.CullFace(CullFaceMode.Back);
            m_drawCount++;
            m_verticesCount += BasicCube.VertexCount;
            m_shaderBindCount++;
            m_meshBindCount++;
        }

        private void DoRenderUpdate()
        {
            renderPasses.Sort();
            for (int cameraIndex = 0; cameraIndex < AllCameras.Count && !DisableRendering; cameraIndex++)
            {
                SetupLights(AllCameras[cameraIndex]);

                for (int i = 0; i < renderPasses.Count; i++)
                {
                    renderPasses[i].FrameSetup();
                }
                GL.Disable(EnableCap.Blend);
                //bind Main render texture RT
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, MainFrameBuffer.FrameBufferObject);
                GL.Viewport(0, 0, MainFrameBuffer.Width, MainFrameBuffer.Height);

                //render depth prepass
                GL.ClearDepth(1);
                GL.ClearColor(Color4.Magenta);
                GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
                RenderScene(AllCameras[cameraIndex], DepthPrepassShader);
                Blit(MainFrameBuffer, DepthTextureBuffer, true, null);
                RenderSkyBox(AllCameras[cameraIndex], SkyboxDepthPrepassShader);

                //copy color texture
                GL.Clear(ClearBufferMask.ColorBufferBit);

                //prepass (Prepass -> Opaque - 1)
                int renderPassIndex = ExecuteRenderPasses(0, (int)RenderQueue.AfterOpaques);

                //render Opaques
                //TODO: move to render pass class
                RenderScene(AllCameras[cameraIndex]);
                RenderSkyBox(AllCameras[cameraIndex]);

                //Post opaque pass (Opaque -> Transparent - 1)
                renderPassIndex = ExecuteRenderPasses(renderPassIndex, (int)RenderQueue.AfterTransparents);
                //render Transparents

                //Post transparent render pass (Transparent -> PostProcess - 1)
                renderPassIndex = ExecuteRenderPasses(renderPassIndex, (int)RenderQueue.AfterPostProcessing);

                //Post post processing (Transparent -> End)
                ExecuteRenderPasses(renderPassIndex, int.MaxValue);

                //blit render buffer to screen
                if(!RenderGUI)
                    Blit(MainFrameBuffer, null, false, null);

                //frame cleanup
                for (int i = 0; i < renderPasses.Count; i++)
                {
                    renderPasses[i].FrameCleanup();
                }
            }

            if (RenderGUI)
            {
                GL.Viewport(0, 0, Window.Size.X, Window.Size.Y);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            }
        }

        /// <summary>
        /// Disable camera frustum culling
        /// </summary>
        public bool DisableFrustumCulling { get; set; } = false;

        private PointLightSSBO[] pointLightSSBOs = new PointLightSSBO[MAXPOINTLIGHTS];
        private UBO<PointLightSSBO> PointLightBufferData;
        void SetupLights(Camera camera)
        {
            if(PointLightBufferData == null)
            {
                PointLightBufferData = new UBO<PointLightSSBO>(pointLightSSBOs, pointLightSSBOs.Length, 3);
            }

            List<(PointLight, int)> pointLights = new();
            var lights = InternalGlobalScope<Light>.Values;
            int pointLightShadowCount = 0;
            for (int i = 0; i < lights.Count; i++)
            {
                switch (lights[i])
                {
                    case DirectionalLight t0:
                        t0.RenderShadowMap(camera);
                        Shader.SetGlobalVector3(Shader.GetShaderPropertyId("DirectionalLight.Color"), t0.Color);
                        Shader.SetGlobalVector3(Shader.GetShaderPropertyId("DirectionalLight.Direction"), t0.Transform.Forward);
                        break;
                    case PointLight t0:
                        pointLights.Add((t0, pointLightShadowCount));
                        if (t0.HasShadows)
                        {
                            t0.RenderShadowMap(camera);
                            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("PointLightShadowMap[" + pointLightShadowCount + "]"), t0.GetShadowMapper().DepthCubemap);
                            pointLightShadowCount++;
                        }
                        break;
                }
            }

            //point light frustum culling?

            //render closer point lights first
            pointLights.Sort((a, b) =>
            {
                var distance0 = (a.Item1.Transform.Position - camera.Transform.Position).LengthSquared;
                var distance1 = (b.Item1.Transform.Position - camera.Transform.Position).LengthSquared;
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
                PointLight pointLight = pointLights[i].Item1;
                unsafe
                {
                    pointLightSSBOs[i].Position = new Vector4(pointLight.Transform.Position, 0);
                    pointLightSSBOs[i].Color = new Vector4(pointLight.Color, 0);
                    pointLightSSBOs[i].Constant = pointLight.AttenConstant;
                    pointLightSSBOs[i].Linear = pointLight.AttenLinear;
                    pointLightSSBOs[i].Exp = pointLight.AttenExp;
                    pointLightSSBOs[i].Range = pointLight.Range;
                    pointLightSSBOs[i].HasShadows = pointLight.HasShadows ? 1 : 0;
                }
                if (pointLight.HasShadows)
                {
                    pointLightSSBOs[i].ShadowFarPlane = pointLight.GetShadowMapper().FarPlane;
                    pointLightSSBOs[i].ShadowIndex = pointLights[i].Item2;
                }
            }

            PointLightBufferData.UpdateData(pointLightSSBOs, Unsafe.SizeOf<PointLightSSBO>() * pointLightSSBOs.Length);
            Shader.SetGlobalInt(Shader.GetShaderPropertyId("PointLightCount"), (int)MathF.Min(pointLights.Count, MAXPOINTLIGHTS));
        }

        Renderer[] SortRenderersByMaterial(List<Renderer> renderers, int uniqueMaterials, Dictionary<Shader, int> caching)
        {
            Dictionary<Shader, int> materialIndex = caching;
            List<Renderer>[] materials = new List<Renderer>[uniqueMaterials];
            int materialCount;
            int index;

            for (int i = 0; i < renderers.Count; i++)
            {
                materialCount = materialIndex.Count;
                var current = renderers[i];
                if (!materialIndex.ContainsKey(current.Material))
                {
                    materialIndex.Add(current.Material, materialCount);
                    var list = new List<Renderer>();
                    materials[materialCount] = list;
                    list.Add(current);
                }
                else
                {
                    index = materialIndex[current.Material];
                    materials[index].Add(current);
                }
            }

            index = 0;
            var final = new Renderer[renderers.Count];
            for (int i = 0; i < materialIndex.Count; i++)
            {
                for (int j = 0; j < materials[i].Count; j++)
                {
                    final[index] = materials[i][j];
                    index++;
                }
            }

            materialIndex.Clear();
            return final;
        }

        Renderer[] sortedRenderers = null;
        Dictionary<ShaderProgram, int> programIndex = new Dictionary<ShaderProgram, int>();
        Dictionary<int, HashSet<Shader>> uniqueMaterialsAtIndex = new Dictionary<int, HashSet<Shader>>();
        Dictionary<Shader, int> materialIndex = new Dictionary<Shader, int>();
        Renderer[] SortRenderersByProgramByMaterials(List<Renderer> renderers, bool AlsoSortMaterials)
        {
            List<Renderer>[] programs = new List<Renderer>[ShaderProgram.ProgramCounts];
            int index;
            int programCount;
            int totalRenderers = renderers.Count;
            for (int i = 0; i < totalRenderers; i++)
            {
                programCount = programIndex.Count;
                var current = renderers[i];
                if(current.Material == null)
                {
                    continue;
                }
                if (!programIndex.ContainsKey(current.Material.Program))
                {
                    programIndex.Add(current.Material.Program, programCount);
                    var list = new List<Renderer>();
                    programs[programCount] = list;
                    list.Add(current);

                    if (!uniqueMaterialsAtIndex.ContainsKey(programCount))
                    {
                        uniqueMaterialsAtIndex.Add(programCount, new HashSet<Shader>());
                    }
                    uniqueMaterialsAtIndex[programCount].Add(current.Material);
                }
                else
                {
                    index = programIndex[current.Material.Program];

                    if (!uniqueMaterialsAtIndex.ContainsKey(index))
                    {
                        uniqueMaterialsAtIndex.Add(index, new HashSet<Shader>());
                    }
                    uniqueMaterialsAtIndex[index].Add(current.Material);

                    var hashSet = uniqueMaterialsAtIndex[index];
                    if (hashSet.Contains(current.Material))
                        hashSet.Add(current.Material);

                    programs[index].Add(current);
                }
            }

            var final = new Renderer[totalRenderers];

            index = 0;
            for (int i = 0; i < programIndex.Count; i++)
            {
                ICollection<Renderer> sorted = programs[i];
                if (AlsoSortMaterials)
                {
                    sorted = SortRenderersByMaterial(programs[i], uniqueMaterialsAtIndex[i].Count, materialIndex);
                }
                for (int j = 0; j < sorted.Count; j++)
                {
                    final[index] = sorted.ElementAt(j);
                    index++;
                }
            }

            programIndex.Clear();
            uniqueMaterialsAtIndex.Clear();
            return final;
        }
        Renderer[] FrustumCullCPU(Renderer[] renderers)
        {
            return renderers;
        }
        
        struct RendererDistancePair
        {
            public Renderer renderer;
            public float distanceSqrd;
        }
        Renderer[] SortByDistanceToCamera(List<Renderer> renderers, Camera camera)
        {
            RendererDistancePair[] rendererDistancePairs = new RendererDistancePair[renderers.Count];
            Parallel.For(0, rendererDistancePairs.Length, (i) =>
            {
                rendererDistancePairs[i] = new RendererDistancePair() {
                    distanceSqrd = (renderers[i].Transform.Position - camera.Transform.Position).LengthSquared,
                    renderer = renderers[i]
                };
            });
            Array.Sort(rendererDistancePairs, (a, b) => { 
                if(a.distanceSqrd < b.distanceSqrd)
                {
                    return -1;
                }
                else
                {
                    return 1;
                }
            });
            Renderer[] output = new Renderer[renderers.Count];
            for (int i = 0; i < rendererDistancePairs.Length; i++)
            {
                output[i] = rendererDistancePairs[i].renderer;
            }
            return output;
        }
        public void RenderScene(Camera camera, Shader overrideShader = null, Action<Renderer> OnRender = null)
        {
            Mesh? previousMesh = null;
            Shader? previousMaterial = null;
            bool doOnRenderFunc = OnRender != null;

            if(camera != null)
            {
                SetShaderCameraData(camera);
                InvokeOnRenders(camera);
            }

            int modelMatrixPropertyId = Shader.GetShaderPropertyId("ModelMatrix");

            bool useOverride = false;
            int overrideModelLoc = 0;
            if (overrideShader != null)
            {
                overrideModelLoc = overrideShader.GetUniformLocation(modelMatrixPropertyId);
                useOverride = true;
            }

            //TODO:
            //apply batching

            //render each renderer
            //bucket sort all renderse by rendering everything by shader, then within those shader groups, render it by materials
            //least amount of state changes
            Renderer[] renderers;
            if (Renderer.NewRendererAdded)
            {
                sortedRenderers = SortRenderersByProgramByMaterials(InternalGlobalScope<Renderer>.Values, true);
            }
            renderers = sortedRenderers;
            if(renderers == null)
            {
                return;
            }

            if (!DisableFrustumCulling)
            {
                renderers = FrustumCullCPU(renderers);
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                var current = renderers[i];
                if (current == null || !current.Enabled)
                {
                    continue;
                }
                Shader? material = useOverride ? overrideShader : current.Material;
                if (material == null)
                {
                    continue;
                }
                Mesh meshData = current.Mesh;
                //bind shader and mesh
                //don't bind if the previous mesh and previous material are the same
                if (meshData != previousMesh)
                {
                    m_meshBindCount++;
                    GL.BindVertexArray(meshData.VertexArrayObject);
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, meshData.ElementArrayBuffer);
                    previousMesh = meshData;
                }

                if (material != previousMaterial)
                {
                    int updateFlags = material.AttachShaderForRendering();
                    if((updateFlags & 0b10) == 0b10)
                    {
                        m_materialUpdateCount++;
                    }
                    if ((updateFlags & 0b01) == 0b01)
                    {
                        m_shaderBindCount++;
                    }
                    previousMaterial = material;
                }
                var transform = current.Entity.Transform;
                var worlToLocalMatrix = transform.WorldToLocalMatrix;
                if (useOverride)
                {
                    //apply model matrix
                    GL.UniformMatrix4(overrideModelLoc, false, ref worlToLocalMatrix);
                }
                else
                {
                    GL.UniformMatrix4(current.Material.GetUniformLocation(modelMatrixPropertyId), false, ref worlToLocalMatrix);
                }

                m_verticesCount += meshData.VertexCount;
                if (doOnRenderFunc)
                {
                    OnRender.Invoke(current);
                }
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
