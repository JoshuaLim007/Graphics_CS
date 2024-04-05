using JLGraphics.Input;
using JLGraphics.RenderPasses;
using JLUtility;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using StbImageSharp;
using StbiSharp;
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
    public struct GraphicsDebug
    {
        public int MaterialUpdateCount;
        public int TotalWorldEntities;
        public int MeshBindCount;
        public int UseProgramCount;
        public int TotalVertices;
        public int DrawCount;
        public int FrustumCulledEntitiesCount;
        public bool DrawAABB;
        public bool PauseFrustumCulling;
    }
    public sealed class Graphics : IDisposable
    {
        static Lazy<Graphics> m_lazyGraphics = new Lazy<Graphics>(() => new Graphics());
        public static Graphics Instance { get { return m_lazyGraphics.Value; } }
        private Graphics()
        {
        }

        public bool BlitFinalResultsToScreen { get; set; } = true;
        public const int MAXPOINTLIGHTS = 128;
        public GameWindow Window { get; private set; } = null;
        private Vector2i RenderBufferSize { get; set; }
        public Vector2i GetRenderSize()
        {
            return RenderBufferSize;
        }
        public bool RenderGUI { get; private set; } = false;
        public Shader DefaultMaterial { get; private set; } = null;
        internal float FixedDeltaTime { get; set; } = 0;
        internal float DeltaTime { get; private set; } = 0;
        internal float SmoothDeltaTime { get; private set; } = 0;
        internal float ElapsedTime { get; private set; } = 0;

        private bool m_isInit = false;
        private List<Entity> AllInstancedObjects => InternalGlobalScope<Entity>.Values;
        private List<Camera> AllCameras => InternalGlobalScope<Camera>.Values;
        public Vector2i OutputResolution => new Vector2i((int)(GetRenderSize().X * RenderScale), (int)(GetRenderSize().Y * RenderScale));
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
            Window.Resize += (e) => { 
                Resize(e); 
                Window.Size = new Vector2i(WindowResizeResults.X, WindowResizeResults.Y);
            };
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.Dither);
            GL.Enable(EnableCap.Blend);
            if (msaaSamples != 0)
            {
                GL.Enable(EnableCap.Multisample);
            }

            MouseInput.UpdateMousePosition(Window.MouseState.Position);
            MouseInput.UpdateMousePosition(Window.MouseState.Position);
            Window.UpdateFrame += UpdateFrame;
        }
        ShaderProgram DefaultShaderProgram;
        ShaderProgram PassthroughShaderProgram;
        ShaderProgram DepthPrepassShaderProgram;
        float previousRenderScale = 1.0f;
        public GraphicsDebug GraphicsDebug;
        public float RenderScale { get; set; } = 1.0f;
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
            };
            var normalBufferSettings = new TFP()
            {
                wrapMode = TextureWrapMode.MirroredRepeat,
                maxMipmap = 0,
                minFilter = TextureMinFilter.Linear,
                magFilter = TextureMagFilter.Linear,
                internalFormat = PixelInternalFormat.Rgb16f,
            };
            var specularMetal = new TFP()
            {
                wrapMode = TextureWrapMode.MirroredRepeat,
                maxMipmap = 0,
                minFilter = TextureMinFilter.Linear,
                magFilter = TextureMagFilter.Linear,
                internalFormat = PixelInternalFormat.Rgba8,
            };
            var depthSettings = new TFP() { 
                wrapMode = TextureWrapMode.ClampToEdge,
                maxMipmap = 0,
                minFilter = TextureMinFilter.Linear,
                magFilter = TextureMagFilter.Linear,
                internalFormat = PixelInternalFormat.DepthComponent24,
            };
            var depthSettingsColor = new TFP()
            {
                wrapMode = TextureWrapMode.ClampToEdge,
                maxMipmap = 0,
                minFilter = TextureMinFilter.Linear,
                magFilter = TextureMagFilter.Linear,
                internalFormat = PixelInternalFormat.R32f,
            };
            var windowSize = GetRenderSize();
            MainFrameBuffer = new FrameBuffer((int)MathF.Ceiling(windowSize.X * scale), (int)MathF.Ceiling(windowSize.Y * scale), false, colorSettings, normalBufferSettings, specularMetal, depthSettings);
            MainFrameBuffer.SetName("Main frame buffer");
            DepthTextureBuffer = new FrameBuffer((int)MathF.Ceiling(windowSize.X * scale), (int)MathF.Ceiling(windowSize.Y * scale), false, depthSettingsColor);
            DepthTextureBuffer.SetName("Depth texture buffer");
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("_CameraDepthTexture"), DepthTextureBuffer.TextureAttachments[0]);
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("_CameraNormalTexture"), MainFrameBuffer.TextureAttachments[1]);
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("_CameraSpecularTexture"), MainFrameBuffer.TextureAttachments[2]);
        }
        public void Init(string windowName, Vector2i windowResolution, float renderFrequency, float fixedUpdateFrequency)
        {
            m_isInit = true;
            previousRenderScale = RenderScale;
            StbImage.stbi_set_flip_vertically_on_load(1);
            Stbi.SetFlipVerticallyOnLoad(true);

            FixedDeltaTime = 1.0f / fixedUpdateFrequency;

            var m_gameWindowSettings = GameWindowSettings.Default;
            var m_nativeWindowSettings = NativeWindowSettings.Default;
            //m_gameWindowSettings.UpdateFrequency = renderFrequency;

            m_gameWindowSettings.UpdateFrequency = renderFrequency;
            m_nativeWindowSettings.Size = windowResolution;
            m_nativeWindowSettings.Title = windowName;
            m_nativeWindowSettings.IsEventDriven = false;

            m_nativeWindowSettings.API = ContextAPI.OpenGL;
            m_nativeWindowSettings.APIVersion = Version.Parse("4.1");

            InitWindow(0, m_gameWindowSettings, m_nativeWindowSettings);

            var defaultShader = new ShaderProgram("DefaultShader", 
                AssetLoader.GetPathToAsset("./Shaders/fragment.glsl"),
                AssetLoader.GetPathToAsset("./Shaders/vertex.glsl"));
            var passThroughShader = new ShaderProgram("PassThroughShader", 
                AssetLoader.GetPathToAsset("./Shaders/CopyToScreen.frag"), 
                AssetLoader.GetPathToAsset("./Shaders/Passthrough.vert"));
            var depthOnlyShader = new ShaderProgram("DepthOnlyShader", 
                AssetLoader.GetPathToAsset("./Shaders/DepthOnly.frag"), 
                AssetLoader.GetPathToAsset("./Shaders/vertexSimple.glsl"));
            var skyBoxShaderProrgam = new ShaderProgram("Skybox Shader", 
                AssetLoader.GetPathToAsset("./Shaders/SkyBoxFrag.glsl"), 
                AssetLoader.GetPathToAsset("./Shaders/skyboxVert.glsl"));
            var skyboxDepthPrepassProgram = new ShaderProgram("Skybox Shader", 
                AssetLoader.GetPathToAsset("./Shaders/fragmentEmpty.glsl"), 
                AssetLoader.GetPathToAsset("./Shaders/skyboxVert.glsl"));
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
            DefaultMaterial.DepthMask = false;
            DefaultMaterial.DepthTest = true;
            DefaultMaterial.DepthTestFunction = DepthFunction.Equal;

            DepthPrepassShader = new Shader("Default depth only", DepthPrepassShaderProgram);
            DepthPrepassShader.DepthTestFunction = DepthFunction.Less;
            DepthPrepassShader.DepthMask = true;
            DepthPrepassShader.DepthTest = true;
            DepthPrepassShader.ColorMask[0] = true;
            DepthPrepassShader.ColorMask[1] = false;
            DepthPrepassShader.ColorMask[2] = false;
            DepthPrepassShader.ColorMask[3] = false;
            DefaultMaterial.SetVector3(Shader.GetShaderPropertyId(DefaultMaterialUniforms.AlbedoColor), new Vector3(1, 1, 1));
            DefaultMaterial.SetFloat(Shader.GetShaderPropertyId(DefaultMaterialUniforms.Smoothness), 0.5f);
            DefaultMaterial.SetFloat(Shader.GetShaderPropertyId(DefaultMaterialUniforms.Metalness), 0.0f);
            DefaultMaterial.SetFloat(Shader.GetShaderPropertyId(DefaultMaterialUniforms.NormalsStrength), 1.0f);
            DefaultMaterial.SetVector2(Shader.GetShaderPropertyId(DefaultMaterialUniforms.UvScale), Vector2.One);
            FullScreenQuad = Mesh.CreateQuadMesh();
            BasicCube = Mesh.CreateCubeMesh();
            PassthroughShader = new Shader("Default Passthrough", PassthroughShaderProgram);
            InitFramebuffers();

            SkyboxController.Init(SkyboxShader);
        }
        public void Dispose()
        {
            if (m_isInit)
            {
                Debug.Log("Graphics is not initialized!", Debug.Flag.Error);
            }

            temporaryUpdateFrameCommands.Clear();
            SkyboxDepthPrepassShader.Program.Dispose();
            SkyboxShader.Program.Dispose();
            AABBDebugShader?.Program.Dispose();
            Window.Close();
            Window.Dispose();
            MainFrameBuffer = null;
            DepthTextureBuffer = null;
            PassthroughShader = null;
            MainFrameBuffer = null;
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
            MouseInput.UpdateMousePosition(Window.MouseState.Position);
            PerfTimer.Start("UpdateFrame");
            
            DeltaTime = (float)Window.UpdateTime;
            smoothDeltaCount = MathF.Min(++smoothDeltaCount, 60);
            SmoothDeltaTime = SmoothDeltaTime * (1.0f - 1.0f / smoothDeltaCount) + DeltaTime * (1.0f / smoothDeltaCount);
            ElapsedTime += DeltaTime;

            //do any one time update frame actions
            if (temporaryUpdateFrameCommands.Count > 0)
            {
                for (int i = 0; i < temporaryUpdateFrameCommands.Count; i++)
                {
                    temporaryUpdateFrameCommands[i].Invoke();
                }
                temporaryUpdateFrameCommands.Clear();
            }

            InvokeNewStarts();

            PerfTimer.Start("UpdateFrame::FixedUpdate");
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
            PerfTimer.Stop();

            PerfTimer.Start("UpdateFrame::Update");
            InvokeUpdates();
            PerfTimer.Stop();

            float updateFreq = 1.0f / FixedDeltaTime;


#if DEBUG
            fileTracker.ResolveFileTrackQueue();
#endif
            PerfTimer.Start("UpdateFrame::Stat display");
            string stats = "";
            stats += " | fixed delta time: " + FixedDeltaTime;
            stats += " | draw count: " + GraphicsDebug.DrawCount;
            stats += " | cull count: " + GraphicsDebug.FrustumCulledEntitiesCount;
            stats += " | shader mesh bind count: " + GraphicsDebug.UseProgramCount + ", " + GraphicsDebug.MeshBindCount;
            stats += " | material update count: " + GraphicsDebug.MaterialUpdateCount;
            stats += " | vertices: " + GraphicsDebug.TotalVertices;
            stats += " | total world objects: " + AllInstancedObjects.Count;
            stats += " | fps: " + 1.0f / SmoothDeltaTime;
            stats += " | delta time: " + SmoothDeltaTime;
            Window.Title = stats;

            GraphicsDebug.DrawCount = 0;
            GraphicsDebug.UseProgramCount = 0;
            GraphicsDebug.MaterialUpdateCount = 0;
            GraphicsDebug.MeshBindCount = 0;
            GraphicsDebug.TotalVertices = 0;
            GraphicsDebug.FrustumCulledEntitiesCount = 0;
            PerfTimer.Stop();

            PerfTimer.Start("UpdateFrame::RenderScaleChange");
            RenderScaleChange(RenderScale);
            PerfTimer.Stop();
            
            frameIncrement++;
            Shader.SetGlobalInt(Shader.GetShaderPropertyId("_Frame"), frameIncrement);

            PerfTimer.Start("UpdateFrame::DoRenderUpdate");
            DoRenderUpdate();
            PerfTimer.Stop();

            Renderer.RendererAddedOrDestroyed = false;
            PerfTimer.Start("UpdateFrame::SwapBuffers");
            if(BlitFinalResultsToScreen)
                Window.SwapBuffers();
            PerfTimer.Stop();
            DestructorCommands.Instance.ExecuteCommands();
            PerfTimer.Stop();
            PerfTimer.ResetTimers(true);
        }
        private bool WindowResized = false;
        private Vector2i WindowResizeResults;
        private void Resize(ResizeEventArgs args)
        {
            if (args.Width == WindowResizeResults.X && args.Height == WindowResizeResults.X)
            {
                return;
            }
            WindowResizeResults = new Vector2i(args.Width, args.Height);
            if (!WindowResized)
            {
                temporaryUpdateFrameCommands.Add(DoResize);
                WindowResized = true;
            }

            void DoResize()
            {
                WindowResizeResults.X = (int)MathF.Max(16, WindowResizeResults.X);
                WindowResizeResults.Y = (int)MathF.Max(16, WindowResizeResults.Y);
                Debug.Log("Window resized: " + WindowResizeResults);
                RenderBufferSize = new Vector2i(WindowResizeResults.X, WindowResizeResults.Y);
                WindowResized = false;

                InitFramebuffers();
                GL.Viewport(0, 0, WindowResizeResults.X, WindowResizeResults.Y);
            }
        }
        public void ResizeRenderSize(int width, int height)
        {
            Resize(new ResizeEventArgs(width, height));
        }

        private bool RenderScaleChanged = false;
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
                RenderScaleChanged = false;
            }

            if (!RenderScaleChanged)
            {
                temporaryUpdateFrameCommands.Add(DoRenderScaleChange);
                RenderScaleChanged = true;
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
            Shader.SetGlobalVector3(Shader.GetShaderPropertyId("CameraWorldSpacePos"), camera.Transform.LocalPosition);
            Shader.SetGlobalVector3(Shader.GetShaderPropertyId("CameraDirection"), camera.Transform.Forward);
            Shader.SetGlobalVector4(Shader.GetShaderPropertyId("CameraParams"), new Vector4(camera.Fov, camera.Width / camera.Height, camera.Near, camera.Far));
            Shader.SetGlobalVector2(Shader.GetShaderPropertyId("RenderSize"), new Vector2(camera.Width * RenderScale, camera.Height * RenderScale));
            Shader.SetGlobalFloat(Shader.GetShaderPropertyId("RenderScale"), RenderScale);
        }

        GlMeshObject FullScreenQuad;
        GlMeshObject BasicCube;
        public Shader PassthroughShader { get; private set; } = null;
        public Shader DepthPrepassShader { get; private set; } = null;
        public Shader SkyboxShader { get; private set; } = null;
        Shader SkyboxDepthPrepassShader;
        
        FrameBuffer MainFrameBuffer = null;
        FrameBuffer DepthTextureBuffer = null;

        public FrameBuffer FinalRenderTarget => MainFrameBuffer;

        internal void Blit(FrameBuffer src, FrameBuffer dst, bool restoreSrc, Shader shader = null, int srcTextureIndex = 0, int dstTextureIndex = 0)
        {
            StartBlitUnsafe(shader);
            BlitUnsafe(src, dst, srcTextureIndex, dstTextureIndex);
            EndBlitUnsafe(shader);
            if (restoreSrc)
            {
                FrameBuffer.BindFramebuffer(src);
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
        internal void BlitUnsafe(FrameBuffer src, FrameBuffer dst, int srcTextureIndex, int dstColorAttachIndex)
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

            var renderWindowSize = GetRenderSize();
            bool isDefault = dst == null;
            int width;
            int height;

            if (isDefault)
            {
                width = renderWindowSize.X;
                height = renderWindowSize.Y;
            }
            else
            {
                width = dst.Width;
                height = dst.Height;
            }

            unsafeBlitShader.SetVector2(Shader.GetShaderPropertyId("MainTex_TexelSize"), new Vector2(1.0f / width, 1.0f / height));
            unsafeBlitShader.SetTexture(Shader.GetShaderPropertyId("MainTex"), src.TextureAttachments[srcTextureIndex]);
            int results = unsafeBlitShader.AttachShaderForRendering();
            if((results & 0b10) == 0b10)
            {
                GraphicsDebug.MaterialUpdateCount++;
            }
            if ((results & 0b1) == 0b1)
            {
                GraphicsDebug.UseProgramCount++;
            }

            if (isDefault)
            {
                //bind default framebuffer
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                GL.Viewport(0, 0, width, height);
            }
            else
            {
                //bind custom framebuffer
                //force to draw only to a specified texture attachment
                FrameBuffer.BindFramebuffer(dst);
                GL.DrawBuffers(1, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0 + dstColorAttachIndex });
            }

            GL.BindVertexArray(FullScreenQuad.VAO);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, FullScreenQuad.EBO);

            GraphicsDebug.DrawCount++;
            GraphicsDebug.TotalVertices += FullScreenQuad.VertexCount;

            GL.DrawElements(BeginMode.Triangles, FullScreenQuad.IndiciesCount, DrawElementsType.UnsignedInt, 0);
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
            if (renderPass == null)
            {
                return;
            }
            renderPasses.Add(renderPass);
        }
        public void DequeueRenderPass(RenderPass renderPass)
        {
            if(renderPass == null)
            {
                return;
            }
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
                PerfTimer.Start(renderPasses[renderPassIndex].Name);
                renderPasses[renderPassIndex].Execute(MainFrameBuffer);
                PerfTimer.Stop();
            }
            return renderPassIndex;
        }

        public void RenderSkyBox(Camera camera, Shader overrideShader = null)
        {
            if(camera.CameraMode == Camera.CameraType.Orthographic)
            {
                return;
            }
            PerfTimer.Start("RenderSkyBox");
            //render skybox
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, BasicCube.EBO);
            GL.BindVertexArray(BasicCube.VAO);
            var mat = overrideShader == null ? SkyboxShader : overrideShader;
            mat.SetMat4(Shader.GetShaderPropertyId("ModelMatrix"), Matrix4.CreateTranslation(camera.Transform.LocalPosition));
            mat.AttachShaderForRendering();
            GL.CullFace(CullFaceMode.Front);
            GL.DrawElements(PrimitiveType.Triangles, BasicCube.IndiciesCount, DrawElementsType.UnsignedInt, 0);
            GL.CullFace(CullFaceMode.Back);

            GraphicsDebug.DrawCount++;
            GraphicsDebug.TotalVertices += BasicCube.VertexCount;
            GraphicsDebug.UseProgramCount++;
            GraphicsDebug.MeshBindCount++;
            PerfTimer.Stop();
        }

        private void DoRenderUpdate()
        {

            renderPasses.Sort();

            for (int cameraIndex = 0; cameraIndex < AllCameras.Count; cameraIndex++)
            {
                if (!AllCameras[cameraIndex].IsActiveAndEnabled)
                {
                    continue;
                }
                RenderPass.CurrentRenderingCamera = AllCameras[cameraIndex];
                SetupLights(AllCameras[cameraIndex]);

                for (int i = 0; i < renderPasses.Count; i++)
                {
                    renderPasses[i].FrameSetup(AllCameras[cameraIndex]);
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
                Blit(MainFrameBuffer, DepthTextureBuffer, true, null, 3, 0);
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

                if (GraphicsDebug.DrawAABB)
                {
                    RenderBoundingBoxes(AllCameras[cameraIndex], MainFrameBuffer);
                }

                if (BlitFinalResultsToScreen)
                {
                    //blit render buffer to screen
                    Blit(MainFrameBuffer, null, false, null);
                }

                //frame cleanup
                for (int i = 0; i < renderPasses.Count; i++)
                {
                    renderPasses[i].FrameCleanup();
                }
            }
        }

        private PointLightSSBO[] pointLightSSBOs = new PointLightSSBO[MAXPOINTLIGHTS];
        private UBO<PointLightSSBO> PointLightBufferData;
        void SetupLights(Camera camera)
        {
            PerfTimer.Start("SetupLights");
            if(PointLightBufferData == null)
            {
                PointLightBufferData = new UBO<PointLightSSBO>(pointLightSSBOs, pointLightSSBOs.Length, 3);
            }

            List<(PointLight, int)> pointLights = new();
            var lights = InternalGlobalScope<Light>.Values;
            int pointLightShadowCount = 0;
            bool didRenderDirectionalShadow = false;
            for (int i = 0; i < lights.Count; i++)
            {
                switch (lights[i])
                {
                    case DirectionalLight t0:
                        if (!lights[i].IsActiveAndEnabled)
                        {
                            Shader.SetGlobalVector3(Shader.GetShaderPropertyId("DirectionalLight.Color"), Vector3.Zero);
                            continue;
                        }
                        if (t0.HasShadows)
                        {
                            t0.RenderShadowMap(camera);
                            didRenderDirectionalShadow = true;
                        }
                        Shader.SetGlobalVector3(Shader.GetShaderPropertyId("DirectionalLight.Color"), t0.Color);
                        Shader.SetGlobalVector3(Shader.GetShaderPropertyId("DirectionalLight.Direction"), t0.Transform.Forward);
                        break;
                    case PointLight t0:
                        if (!lights[i].IsActiveAndEnabled)
                        {
                            continue;
                        }
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

            if (!didRenderDirectionalShadow)
            {
                DirectionalShadowMap.SetShadowMapToWhite();
            }
            //point light frustum culling?

            //render closer point lights first
            pointLights.Sort((a, b) =>
            {
                var distance0 = (a.Item1.Transform.LocalPosition - camera.Transform.LocalPosition).LengthSquared;
                var distance1 = (b.Item1.Transform.LocalPosition - camera.Transform.LocalPosition).LengthSquared;
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
                    pointLightSSBOs[i].Position = new Vector4(pointLight.Transform.LocalPosition, 0);
                    pointLightSSBOs[i].Color = new Vector4(pointLight.Color, 0);
                    pointLightSSBOs[i].Constant = pointLight.AttenConstant;
                    pointLightSSBOs[i].Linear = pointLight.AttenLinear;
                    pointLightSSBOs[i].Exp = 1 - pointLight.AttenLinear;
                    pointLightSSBOs[i].Range = pointLight.Range;
                    pointLightSSBOs[i].HasShadows = pointLight.HasShadows ? 1 : 0;
                }
                if (pointLight.HasShadows)
                {
                    pointLightSSBOs[i].ShadowFarPlane = pointLight.GetShadowMapper().FarPlane;
                    pointLightSSBOs[i].ShadowIndex = pointLights[i].Item2;
                }
            }

            PointLightBufferData.UpdateData(pointLightSSBOs, pointLightSSBOs.Length);
            Shader.SetGlobalInt(Shader.GetShaderPropertyId("PointLightCount"), (int)MathF.Min(pointLights.Count, MAXPOINTLIGHTS));
            PerfTimer.Stop();
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
        Renderer[] SortRenderersByProgramByMaterials(List<Renderer> renderers, bool AlsoSortMaterials)
        {
            Dictionary<ShaderProgram, int> programIndex = new Dictionary<ShaderProgram, int>();
            Dictionary<int, HashSet<Shader>> uniqueMaterialsAtIndex = new Dictionary<int, HashSet<Shader>>();
            Dictionary<Shader, int> materialIndex = new Dictionary<Shader, int>();

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

            return final;
        }
        
        public float GetDepthAt(int x, int y)
        {
            float[] results = new float[1];
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, MainFrameBuffer.FrameBufferObject);
            GL.ReadPixels(x, y, 1, 1, PixelFormat.DepthComponent, PixelType.Float, results);
            return results[0];
        }

        Shader AABBDebugShader = null;
        public void RenderBoundingBoxes(Camera camera, FrameBuffer frameBuffer, FrameBuffer restore = null)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBuffer.FrameBufferObject);
            var renderers = InternalGlobalScope<Renderer>.Values;
            for (int i = 0; i < renderers.Count; i++)
            {
                if (!renderers[i].Enabled)
                {
                    continue;
                }
                RenderBounginBox(camera, renderers[i]);
            }
            if (restore != null)
            {
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, restore.FrameBufferObject);
            }
        }

        public void RenderBounginBox(Camera camera, Renderer renderer)
        {
            if (AABBDebugShader == null)
            {
                var program = new ShaderProgram("Gizmo", AssetLoader.GetPathToAsset("./Shaders/aabbDebug.frag"), AssetLoader.GetPathToAsset("./Shaders/aabbDebug.vert"));
                program.CompileProgram();
                AABBDebugShader = new Shader("Gizmo", program);
                AABBDebugShader.DepthTest = false;
            }
            SetShaderCameraData(camera);
            AABBDebugShader.SetVector4(Shader.GetShaderPropertyId("color"), new Vector4(1, 0, 1, 1));
            AABBDebugShader.AttachShaderForRendering();
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            GL.Disable(EnableCap.CullFace);
            GL.LineWidth(1);

            var aabb = renderer.GetWorldBounds();
            var corners = AABB.GetCorners(aabb);
            float[] vertices = new float[corners.Length * 3];
            int vertIdx = 0;
            for (int j = 0; j < vertices.Length; j += 3)
            {
                vertices[j] = corners[vertIdx].X;
                vertices[j + 1] = corners[vertIdx].Y;
                vertices[j + 2] = corners[vertIdx].Z;
                vertIdx++;
            }

            var indices = AABB.GetIndices();
            int vbo = GL.GenBuffer();
            int eab = GL.GenBuffer();
            int vao = GL.GenVertexArray();
            GL.BindVertexArray(vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eab);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(int), indices, BufferUsageHint.StaticDraw);

            GL.DrawElements(BeginMode.TriangleStrip, indices.Length, DrawElementsType.UnsignedInt, 0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.DeleteBuffer(vbo);
            GL.DeleteBuffer(eab);
            GL.DeleteVertexArray(vao);

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.Enable(EnableCap.CullFace);
        }

        CameraFrustum cameraFrustum = new();
        public void RenderScene(Camera camera, Shader overrideShader = null, Action<Renderer> OnRender = null, in CameraFrustum? viewFrustum = null)
        {
            PerfTimer.Start("RenderScene");
            Mesh? previousMesh = null;
            Shader? previousMaterial = null;
            bool doOnRenderFunc = OnRender != null;
            int modelMatrixPropertyId = Shader.GetShaderPropertyId("ModelMatrix");

            bool useOverride = false;
            int overrideModelLoc = 0;
            if (overrideShader != null)
            {
                overrideModelLoc = overrideShader.GetUniformLocation(modelMatrixPropertyId);
                useOverride = true;
            }

            //render each renderer
            //bucket sort all renderse by rendering everything by shader, then within those shader groups, render it by materials
            //least amount of state changes
            Renderer[] renderers;
            if (Renderer.RendererAddedOrDestroyed)
            {
                sortedRenderers = SortRenderersByProgramByMaterials(InternalGlobalScope<Renderer>.Values, true);
            }
            renderers = sortedRenderers;
            if(renderers == null)
            {
                return;
            }
            bool isCameraNull = camera == null;
            bool doFrustumCulling = false;
            if (!isCameraNull)
            {
                SetShaderCameraData(camera);
                InvokeOnRenders(camera);
                if (camera.FrustumCull)
                {
                    doFrustumCulling = true;
                    this.cameraFrustum = !GraphicsDebug.PauseFrustumCulling ? CameraFrustum.Create(camera.ViewMatrix * camera.ProjectionMatrix) : this.cameraFrustum;
                }
            }
            if(viewFrustum != null)
            {
                doFrustumCulling = true;
                this.cameraFrustum = !GraphicsDebug.PauseFrustumCulling ? viewFrustum.Value : this.cameraFrustum;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                var current = renderers[i];
                if (doFrustumCulling)
                {
                    var correctedAABB = current.GetWorldBounds();
                    var skip = AABB.IsOutsideOfFrustum(cameraFrustum, correctedAABB);
                    if (skip)
                    {
                        GraphicsDebug.FrustumCulledEntitiesCount++;
                        continue;
                    }
                }

                if (current == null || !current.IsActiveAndEnabled)
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
                    GraphicsDebug.MeshBindCount++;
                    GL.BindVertexArray(meshData.VertexArrayObject);
                    //GL.BindBuffer(BufferTarget.ElementArrayBuffer, meshData.ElementArrayBuffer);
                    previousMesh = meshData;
                }

                if (material != previousMaterial)
                {
                    int updateFlags = material.AttachShaderForRendering();
                    if((updateFlags & 0b10) == 0b10)
                    {
                        GraphicsDebug.MaterialUpdateCount++;
                    }
                    if ((updateFlags & 0b01) == 0b01)
                    {
                        GraphicsDebug.UseProgramCount++;
                    }
                    previousMaterial = material;
                }
                var transform = current.Entity.Transform;
                var worlToLocalMatrix = transform.ModelMatrix;
                if (useOverride)
                {
                    //apply model matrix
                    GL.UniformMatrix4(overrideModelLoc, false, ref worlToLocalMatrix);
                }
                else
                {
                    GL.UniformMatrix4(current.Material.GetUniformLocation(modelMatrixPropertyId), false, ref worlToLocalMatrix);
                }

                GraphicsDebug.TotalVertices += meshData.VertexCount;
                if (doOnRenderFunc)
                {
                    OnRender.Invoke(current);
                }
                //render object
                GL.DrawElements(PrimitiveType.Triangles, meshData.ElementCount, DrawElementsType.UnsignedInt, 0);

                GraphicsDebug.DrawCount++;
            }

            //unbind
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            Shader.Unbind();
            PerfTimer.Stop();
        }

    }
}
