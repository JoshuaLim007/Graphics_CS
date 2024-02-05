using JLUtility;
using OpenTK.Compute.OpenCL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using StbImageSharp;
using StbiSharp;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Xml.Serialization;
using All = OpenTK.Graphics.OpenGL4.All;

namespace JLGraphics
{
    public struct Time
    {
        public static float FixedDeltaTime { get => Graphics.FixedDeltaTime; set => Graphics.FixedDeltaTime = Math.Clamp(value, float.Epsilon, 1.0f); }
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
        private static void InitWindow(int msaaSamples)
        {
            m_nativeWindowSettings.NumberOfSamples = msaaSamples;

            if (Window != null)
            {
                m_nativeWindowSettings.SharedContext = Window.Context;
                Window.Dispose();
            }

            Window = new GameWindow(m_gameWindowSettings, m_nativeWindowSettings);
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
            float fixedTimer = 0;
            var time = DateTime.Now;
            var time1 = DateTime.Now;
            Window.Load += delegate ()
            {

            };
            //Window.Load += Start; //start now happens after the first frame
            Window.UpdateFrame += delegate (FrameEventArgs eventArgs)
            {
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


                time = DateTime.Now;
#if DEBUG
                fileTracker.ResolveFileTrackQueue();
#endif
                DeltaTime = (time.Ticks - time1.Ticks) / 10000000f;
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

                m_drawCount = 0;
                m_shaderBindCount = 0;
                m_materialUpdateCount = 0;
                m_meshBindCount = 0;
                m_verticesCount = 0;
                DoRenderUpdate();

                time1 = time;
            };
        }
        static CubemapTexture SkyBox;
        static ShaderProgram DefaultShaderProgram;
        static ShaderProgram PassthroughShaderProgram;
        static ShaderProgram DepthPrepassShaderProgram;
        static void InitFramebuffers() {
            MainFrameBuffer = new FrameBuffer(m_nativeWindowSettings.Size.X, m_nativeWindowSettings.Size.Y, true, new TFP(PixelInternalFormat.Rgb32f, PixelFormat.Rgb));
            DepthTextureBuffer = new FrameBuffer(m_nativeWindowSettings.Size.X, m_nativeWindowSettings.Size.Y, false, new TFP(PixelInternalFormat.R32f, PixelFormat.Red));
            Shader.SetGlobalTexture("_CameraDepthTexture", DepthTextureBuffer.ColorAttachments[0]);
        }
        /// <param name="windowName"></param>
        /// <param name="windowResolution"></param>
        /// <param name="renderFrequency"></param>
        /// <param name="fixedUpdateFrequency"></param>
        public static void Init(string windowName, Vector2i windowResolution, float renderFrequency, float fixedUpdateFrequency)
        {
            m_isInit = true;
            StbImage.stbi_set_flip_vertically_on_load(1);
            Stbi.SetFlipVerticallyOnLoad(true);

            FixedDeltaTime = 1.0f / fixedUpdateFrequency;

            m_gameWindowSettings = GameWindowSettings.Default;
            m_nativeWindowSettings = NativeWindowSettings.Default;
            m_gameWindowSettings.UpdateFrequency = renderFrequency;

            m_nativeWindowSettings.Size = windowResolution;
            m_nativeWindowSettings.Title = windowName;
            m_nativeWindowSettings.IsEventDriven = false;

            m_nativeWindowSettings.API = ContextAPI.OpenGL;
            m_nativeWindowSettings.APIVersion = Version.Parse("4.1");

            InitWindow(0);

            var defaultShader = new ShaderProgram("DefaultShader", "./Shaders/fragment.glsl", "./Shaders/vertex.glsl");
            var passThroughShader = new ShaderProgram("PassThroughShader", "./Shaders/CopyToScreen.frag", "./Shaders/Passthrough.vert");
            var depthOnlyShader = new ShaderProgram("DepthOnlyShader", "./Shaders/DepthOnly.frag", "./Shaders/vertexSimple.glsl");
            var skyBoxShaderProrgam = new ShaderProgram("Skybox Shader", "./Shaders/cubemapFrag.glsl", "./Shaders/skyboxVert.glsl");
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

            SkyBox = new CubemapTexture();
            SkyBox.Path = "D:\\joshu\\Downloads\\rural_asphalt_road_4k.hdr";
            SkyBox.RenderCubemap(1024, "SkyBox");

            DefaultShaderProgram = defaultShader;
            PassthroughShaderProgram = passThroughShader;
            DepthPrepassShaderProgram = depthOnlyShader;
            SkyboxShader = new Shader("Skybox material", skyBoxShaderProrgam);
            SkyboxDepthPrepassShader = new Shader("Skybox depth prepass material", skyboxDepthPrepassProgram);

            DefaultMaterial = new Shader("Default Material", DefaultShaderProgram);
            DepthPrepassShader = new Shader("Default depth only", DepthPrepassShaderProgram);
            DepthPrepassShader.DepthTestFunction = DepthFunction.Less;
            DepthPrepassShader.ColorMask[0] = true;
            DepthPrepassShader.ColorMask[1] = false;
            DepthPrepassShader.ColorMask[2] = false;
            DepthPrepassShader.ColorMask[3] = false;
            DefaultMaterial.SetVector3("AlbedoColor", new Vector3(1, 1, 1));
            DefaultMaterial.SetFloat("Smoothness", 0.5f);
            FullScreenQuad = Mesh.CreateQuadMesh();
            BasicCube = Mesh.CreateCubeMesh();
            PassthroughShader = new Shader("Default Passthrough", PassthroughShaderProgram);
            InitFramebuffers();
            renderPassCommandBuffer = new CommandBuffer();
        }
        public static void Free()
        {
            SkyboxDepthPrepassShader.Program.Dispose();
            SkyboxShader.Program.Dispose();
            SkyBox.Dispose();
            Window.Close();
            MainFrameBuffer.Dispose();
            DepthTextureBuffer.Dispose();
            PassthroughShader = null;
            MainFrameBuffer = null;
            DepthTextureBuffer = null;
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
            DepthPrepassShaderProgram.Dispose();
            Mesh.FreeMeshObject(FullScreenQuad);
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
            if(args.Width == Window.Size.X && args.Height == Window.Size.X)
            {
                return;
            }
            Window.Size = new Vector2i(args.Width, args.Height);
            MainFrameBuffer.Dispose();
            DepthTextureBuffer.Dispose();
            InitFramebuffers();
            GL.Viewport(0, 0, args.Width, args.Height);
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
            Shader.SetGlobalVector4("CameraParams", new Vector4(camera.Width, camera.Height, camera.Near, camera.Far));
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

        static MeshPrimative FullScreenQuad;
        static MeshPrimative BasicCube;
        public static Shader PassthroughShader { get; private set; } = null;
        public static Shader DepthPrepassShader { get; private set; } = null;
        public static Shader SkyboxShader { get; private set; } = null;
        static Shader SkyboxDepthPrepassShader;
        static FrameBuffer MainFrameBuffer = null;
        static FrameBuffer DepthTextureBuffer = null;
        internal static void Blit(FrameBuffer src, FrameBuffer dst, bool restoreSrc, Shader shader = null)
        {
            if(src == null)
            {
                Console.WriteLine("ERROR::Cannot blit with null src");
                return;
            }

            // second pass
            int width = dst != null ? dst.Width : Window.Size.X;
            int height = dst != null ? dst.Height : Window.Size.Y;
            int fbo = dst != null ? dst.FrameBufferObject : 0;

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.Viewport(0, 0, width, height);

            Shader blitShader = shader ?? PassthroughShader;
            blitShader.SetVector2("MainTex_Size", new Vector2(width, height));
            blitShader.UseProgram();
            blitShader.SetTextureUnsafe("MainTex", src.ColorAttachments[0]);
            blitShader.UpdateUniforms();

            GL.BindVertexArray(FullScreenQuad.VAO);
            GL.Disable(EnableCap.DepthTest);
            m_drawCount++;
            m_verticesCount += FullScreenQuad.VertexCount;
            m_shaderBindCount++;
            m_meshBindCount++;

            GL.DrawArrays(PrimitiveType.Triangles, 0, FullScreenQuad.VertexCount);

            if (restoreSrc)
            {
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, src.FrameBufferObject);
            }
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

        static void RenderSkyBox(bool doDepthPrepass, Camera camera)
        {
            //render skybox
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, BasicCube.EBO);
            GL.BindVertexArray(BasicCube.VAO);
            var mat = SkyboxShader;
            if (doDepthPrepass)
            {
                mat = SkyboxDepthPrepassShader;
                mat.DepthTestFunction = DepthFunction.Less;
            }
            else
            {
                mat.DepthTestFunction = DepthFunction.Lequal;
            }
            mat.SetMat4("ModelMatrix", Matrix4.CreateTranslation(camera.Transform.Position));
            mat.UseProgram();
            mat.UpdateUniforms();
            GL.CullFace(CullFaceMode.Front);
            GL.DrawElements(PrimitiveType.Triangles, BasicCube.IndiciesCount, DrawElementsType.UnsignedInt, 0);
            GL.CullFace(CullFaceMode.Back);
            m_drawCount++;
            m_verticesCount += BasicCube.VertexCount;
            m_shaderBindCount++;
            m_meshBindCount++;
        }

        private static void DoRenderUpdate()
        {
            renderPasses.Sort();
            for (int cameraIndex = 0; cameraIndex < AllCameras.Count && !DisableRendering; cameraIndex++)
            {
                for (int i = 0; i < renderPasses.Count; i++)
                {
                    renderPasses[i].FrameSetup();
                }
                GL.Disable(EnableCap.Blend);
                //bind Main render texture RT
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, MainFrameBuffer.FrameBufferObject);
                GL.Viewport(0, 0, MainFrameBuffer.Width, MainFrameBuffer.Height);

                //render depth prepass
                GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
                RenderCamera(AllCameras[cameraIndex], RenderSort.None, DepthPrepassShader);
                Blit(MainFrameBuffer, DepthTextureBuffer, true, null);
                RenderSkyBox(true, AllCameras[cameraIndex]);

                //copy depth texture
                GL.Clear(ClearBufferMask.ColorBufferBit);

                //prepass (Prepass -> Opaque - 1)
                int renderPassIndex = ExecuteRenderPasses(0, (int)RenderQueue.AfterOpaques);

                //render Opaques
                //TODO: move to render pass class
                SetupLights(AllCameras[cameraIndex]);
                RenderCamera(AllCameras[cameraIndex], RenderingSortMode);
                RenderSkyBox(false, AllCameras[cameraIndex]);

                //Post opaque pass (Opaque -> Transparent - 1)
                renderPassIndex = ExecuteRenderPasses(renderPassIndex, (int)RenderQueue.AfterTransparents);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.Enable(EnableCap.Blend);
                //render Transparents

                //Post transparent render pass (Transparent -> PostProcess - 1)
                renderPassIndex = ExecuteRenderPasses(renderPassIndex, (int)RenderQueue.AfterPostProcessing);

                //Post post processing (Transparent -> End)
                ExecuteRenderPasses(renderPassIndex, int.MaxValue);

                //blit render buffer to screen
                Blit(MainFrameBuffer, null, false, null);

                //frame cleanup
                for (int i = 0; i < renderPasses.Count; i++)
                {
                    renderPasses[i].FrameCleanup();
                }

                Window.SwapBuffers();
            }
        }

        /// <summary>
        /// Disable camera frustum culling
        /// </summary>
        public static bool DisableFrustumCulling { get; set; } = false;

        public enum RenderSort
        {
            ShaderProgram,
            ShaderProgramMaterial,
            FrontToBack,
            None
        }
        /// <summary>
        /// Sets rendering sorting
        /// </summary>
        public static RenderSort RenderingSortMode { get; set; } = RenderSort.ShaderProgramMaterial;

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

        static Dictionary<Shader, int> materialIndex = new Dictionary<Shader, int>();
        static Renderer[] SortRenderersByMaterial(List<Renderer> renderers, int uniqueMaterials)
        {
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

        static Dictionary<ShaderProgram, int> programIndex = new Dictionary<ShaderProgram, int>();
        static Dictionary<int, HashSet<Shader>> uniqueMaterialsAtIndex = new Dictionary<int, HashSet<Shader>>();
        static Renderer[] SortRenderersByProgramByMaterials(List<Renderer> renderers, bool AlsoSortMaterials)
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
                if(AlsoSortMaterials)
                    sorted = SortRenderersByMaterial(programs[i], uniqueMaterialsAtIndex[i].Count);
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
        static Renderer[] FrustumCullCPU(Renderer[] renderers)
        {
            return renderers;
        }
        
        struct RendererDistancePair
        {
            public Renderer renderer;
            public float distanceSqrd;
        }
        static Renderer[] SortByDistanceToCamera(List<Renderer> renderers, Camera camera)
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
        static void RenderCamera(Camera camera, RenderSort renderingMode, Shader overrideShader = null)
        {
            Mesh? previousMesh = null;
            Shader? previousMaterial = null;

            SetShaderCameraData(camera);
            InvokeOnRenders(camera);
            SetDrawMode(camera.EnabledWireFrame);

            bool useOverride = false;
            int overrideModelLoc = 0;
            if (overrideShader != null)
            {
                overrideModelLoc = overrideShader.GetUniformLocation("ModelMatrix");
                useOverride = true;
            }

            //TODO:
            //apply static batching

            //render each renderer
            //bucket sort all renderse by rendering everything by shader, then within those shader groups, render it by materials
            Renderer[] renderers = null;
            switch (renderingMode)
            {
                case RenderSort.None:
                    renderers = InternalGlobalScope<Renderer>.Values.ToArray();
                    break;
                case RenderSort.FrontToBack:
                    renderers = SortByDistanceToCamera(InternalGlobalScope<Renderer>.Values, camera);
                    break;
                case RenderSort.ShaderProgramMaterial:
                    renderers = SortRenderersByProgramByMaterials(InternalGlobalScope<Renderer>.Values, true);
                    break;
                case RenderSort.ShaderProgram:
                    renderers = SortRenderersByProgramByMaterials(InternalGlobalScope<Renderer>.Values, false);
                    break;
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
                    if (material.Program != previousMaterial?.Program)
                    {
                        m_shaderBindCount++;
                        material.UseProgram();
                    }
                    if (material.UpdateUniforms())
                    {
                        m_materialUpdateCount++;
                    }
                    previousMaterial = material;
                }
                
                var worlToLocalMatrix = current.Entity.Transform.WorldToLocalMatrix;
                if (useOverride)
                {
                    //apply model matrix
                    GL.UniformMatrix4(overrideModelLoc, false, ref worlToLocalMatrix);
                }
                else
                {
                    GL.UniformMatrix4(current.Material.GetUniformLocation("ModelMatrix"), false, ref worlToLocalMatrix);
                }

                m_verticesCount += meshData.VertexCount;

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
