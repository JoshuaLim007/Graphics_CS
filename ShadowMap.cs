﻿using JLUtility;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace JLGraphics
{
    public abstract class ShadowMap : SafeDispose
    {
        public int Resolution { get; protected set; }

        public override string Name => "ShadowMap";

        public ShadowMap(int resolution = 2048)
        {
            Resolution = resolution;
        }

        protected override void OnDispose()
        {
        }

        public abstract void RenderShadowMap(Camera camera);
    }
    public class DirectionalShadowMap : ShadowMap
    {
        Shader shader;
        public enum FilterMode
        {
            HARD = 0,
            PCF = 1,
            PCSS = 2,
        }
        public FrameBuffer DepthOnlyFramebuffer { get; private set; }
        float nearPlane, farPlane, shadowRange;
        DirectionalLight DirectionalLight;
        Vector2 texelSize;
        
        float SamplingBox = 3.5f;
        int SampleCount = 12;
        int previousSampleCount = 0;
        public void SetSampleCount(int value)
        {
            SampleCount = MathHelper.Clamp(value, 3, 16);
        }
        public void SetBlurRadius(float value)
        {
            SamplingBox = MathHelper.Clamp(value, 1.0f, 8.0f);
        }
        public FilterMode filterMode { get; set; } = FilterMode.PCSS;
        public override string Name => "Directional Shadow Map: " + DirectionalLight.Name;
        protected override void OnDispose()
        {
            DepthOnlyFramebuffer.Dispose();
        }
        public static void SetShadowMapToWhite()
        {
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("DirectionalShadowDepthMap"), null);
            Shader.SetGlobalBool(Shader.GetShaderPropertyId("HasDirectionalShadow"), false);
        }
        void CalculateSamplingKernals()
        {
            if(previousSampleCount == SampleCount)
            {
                return;
            }
            previousSampleCount = SampleCount;
            int sc = SampleCount;
            List<Vector2> borderKernals = new List<Vector2>();
            List<Vector2> nonBorderKernals = new List<Vector2>();
            Vector2[] kernals = new Vector2[sc * sc];

            for (int i = 0; i < sc; i++)
            {
                for (int j = 0; j < sc; j++)
                {
                    //borders
                    if(i == 0 || i == sc - 1 || j == 0 || j == sc - 1)
                    {
                        borderKernals.Add(
                            new Vector2() { 
                                X = i, 
                                Y = j 
                            });
                    }
                    //non borders
                    else
                    {
                        nonBorderKernals.Add(new Vector2()
                        {
                            X = i,
                            Y = j
                        });
                    }
                }
            }

            //collect kernals
            //collect borders first
            int kernalIndex = 0;
            for (int i = 0; i < borderKernals.Count; i++)
            {
                kernals[kernalIndex] = borderKernals[i];
                kernalIndex++;
            }
            for(int i = 0; i < nonBorderKernals.Count; i++)
            {
                kernals[kernalIndex] = nonBorderKernals[i];
                kernalIndex++;
            }

            //normalize kernals
            for (int i = 0; i < sc * sc; i++)
            {
                //add noise
                kernals[i].X += (Random.Shared.NextSingle() * 2 - 1) * 0.5f;
                kernals[i].Y += (Random.Shared.NextSingle() * 2 - 1) * 0.5f;

                //map 0 -> sc to 0 -> 1
                kernals[i].X /= sc;
                kernals[i].Y /= sc;

                //map 0 -> 1 to -1 -> 1
                kernals[i].X = kernals[i].X * 2 - 1;
                kernals[i].Y = kernals[i].Y * 2 - 1;

                //map -1 -> 1 to -SamplingBox/2 -> SamplingBox/2
                kernals[i].X *= SamplingBox * 0.5f;
                kernals[i].Y *= SamplingBox * 0.5f;

                //center
                kernals[i].X += SamplingBox * 0.5f / sc;
                kernals[i].Y += SamplingBox * 0.5f / sc;
            }
            
            //var borderCount = kernals.Length - Math.Pow(MathF.Sqrt((float)kernals.Length) - 2, 2);
            for(int i = 0; i < SampleCount * SampleCount; i++)
            {
                var prop = Shader.GetShaderPropertyId("DirectionalShadowSampleKernals[" + i + "]");
                Shader.SetGlobalVector2(prop, kernals[i]);
            }
        }
        public DirectionalShadowMap(DirectionalLight directionalLight, float ShadowRange = 100.0f, float nearPlane = 1.0f, float farPlane = 1000.0f, int resolution = 2048) : base(resolution)
        {
            this.shadowRange = ShadowRange;
            ShaderProgram shaderProgram = new ShaderProgram("Directional Shadow Shader",
                AssetLoader.GetPathToAsset("./Shaders/fragmentEmpty.glsl"),
                AssetLoader.GetPathToAsset("./Shaders/vertexSimple.glsl"));
            shaderProgram.CompileProgram();
            shader = new Shader("Directional Shadow Material", shaderProgram, true);
            shader.DepthTest = true;
            shader.DepthTestFunction = DepthFunction.Lequal;
            DepthOnlyFramebuffer = new FrameBuffer(resolution, resolution, false, new TFP()
            {
                internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.DepthComponent,
                magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                maxMipmap = 0,
                minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear,
                wrapMode = OpenTK.Graphics.OpenGL4.TextureWrapMode.ClampToBorder,
                borderColor = Vector4.One,
                isShadowMap = true,
            });
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("DirectionalShadowDepthMap"), DepthOnlyFramebuffer.TextureAttachments[0]);
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("DirectionalShadowDepthMap_Smooth"), DepthOnlyFramebuffer.TextureAttachments[0]);
            texelSize = new Vector2(1.0f / DepthOnlyFramebuffer.Width, 1.0f / DepthOnlyFramebuffer.Height);
            Shader.SetGlobalVector2(Shader.GetShaderPropertyId("DirectionalShadowDepthMapTexelSize"), texelSize);
            this.DirectionalLight = directionalLight;
            this.nearPlane = nearPlane;
            this.farPlane = farPlane;

            CalculateSamplingKernals();
            Shader.SetGlobalInt(Shader.GetShaderPropertyId("DirectionalShadowSamples"), filterMode == FilterMode.PCSS ? SampleCount : SampleCount * SampleCount);
        }
        public void ResizeResolution(int resolution)
        {
            Resolution = resolution;
            if (DepthOnlyFramebuffer == null)
            {
                return;
            }
            DepthOnlyFramebuffer.Dispose();
            DepthOnlyFramebuffer = new FrameBuffer(resolution, resolution, false, new TFP()
            {
                internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.DepthComponent,
                magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                maxMipmap = 0,
                minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear,
                wrapMode = OpenTK.Graphics.OpenGL4.TextureWrapMode.ClampToBorder,
                borderColor = Vector4.One,
                isShadowMap = true,
            });
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("DirectionalShadowDepthMap"), DepthOnlyFramebuffer.TextureAttachments[0]);
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("DirectionalShadowDepthMap_Smooth"), DepthOnlyFramebuffer.TextureAttachments[0]);
            texelSize = new Vector2(1.0f / DepthOnlyFramebuffer.Width, 1.0f / DepthOnlyFramebuffer.Height);
            Shader.SetGlobalVector2(Shader.GetShaderPropertyId("DirectionalShadowDepthMapTexelSize"), texelSize);
        }
        public override void RenderShadowMap(Camera camera)
        {
#if DEBUG
            if (Graphics.Instance.Window.IsKeyPressed(OpenTK.Windowing.GraphicsLibraryFramework.Keys.K))
            {
                filterMode++;
                filterMode = (FilterMode)((int)filterMode % 3);
            }
#endif
            CalculateSamplingKernals();

            var cameraPosition = camera.Transform.Position;
            bool isUp = DirectionalLight.Transform.Forward == Vector3.UnitY || DirectionalLight.Transform.Forward == -Vector3.UnitY;

            //var directionalLightViewMatrix = Matrix4.CreateFromQuaternion(DirectionalLight.Transform.Rotation);
            var directionalLightViewMatrix = Matrix4.LookAt(Vector3.Zero, -DirectionalLight.Transform.Forward, isUp ? Vector3.UnitZ : Vector3.UnitY);
            //var directionalLightViewMatrix = Matrix4.CreateFromQuaternion(Quaternion.FromEulerAngles(90,0,0));
            var proj = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(camera.Fov), camera.Width / camera.Height, 0.1f, shadowRange);
            var invView = (Matrix4.CreateTranslation(-camera.Transform.Position) *  Matrix4.CreateFromQuaternion(camera.Transform.Rotation)).Inverted();

            //get frustum corners in world space
            var corners = CameraFrustum.GetCorners(proj.Inverted() * invView);

            var lightSpaceCorners = new Vector3[corners.Length];
            //get frustum corners in light space
            for (int i = 0; i < corners.Length; i++)
            {
                lightSpaceCorners[i] = (new Vector4(corners[i], 1.0f) * directionalLightViewMatrix).Xyz;
            }

            //get bounding box in light space
            var aabb = AABB.GetBoundingBox(lightSpaceCorners);

            //get light position in world space
            var minBot = new Vector3(aabb.Min.X, aabb.Min.Y, aabb.Min.Z);
            var minTop = new Vector3(aabb.Max.X, aabb.Max.Y, aabb.Min.Z);
            var lightPositionInLightSpace = (minTop + minBot) / 2.0f;
            var lightPositionInWorldSpace = (new Vector4(lightPositionInLightSpace, 1.0f) * directionalLightViewMatrix.Inverted()).Xyz;


            //get new light space matrix
            var directionalLightViewMatrixPosition = Matrix4.CreateTranslation(-lightPositionInWorldSpace) * directionalLightViewMatrix;

            //get frustum cornes in light space with translation
            var lightSpaceCornersWithTranslation = new Vector3[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                lightSpaceCornersWithTranslation[i] = (new Vector4(corners[i], 1.0f) * directionalLightViewMatrixPosition).Xyz;
            }

            //get final aabb
            var finalAABB = AABB.GetBoundingBox(lightSpaceCornersWithTranslation);

            float xEx = finalAABB.Extents.X - shadowRange * 2.0f;
            float yEx = finalAABB.Extents.Y - shadowRange * 2.0f;
            if(xEx < 0)
            {
                finalAABB.Max.X += -xEx;
                finalAABB.Min.X -= -xEx;
            }                          
            if(yEx < 0)                
            {                          
                finalAABB.Max.Y += -yEx;
                finalAABB.Min.Y -= -yEx;
            }
            finalAABB.Max.Z = MathHelper.Max(finalAABB.Max.Z, 1000.0f);

            //var newCorners = AABB.GetCorners(finalAABB);
            //Vector3[] newCorners3 = new Vector3[newCorners.Length];
            //for (int i = 0; i < newCorners.Length; i++)
            //{
            //    newCorners3[i] = (newCorners[i] * directionalLightViewMatrixPosition.Inverted()).Xyz;
            //}
            //Vector3 offsetCenter = AABB.GetBoundingBox()

            var camOffset = (new Vector4(finalAABB.Center, 0.0f) * directionalLightViewMatrixPosition).Xyz;
            var finalWorldCenter = cameraPosition + camOffset;

            Shader.SetGlobalInt(Shader.GetShaderPropertyId("DirectionalShadowSamples"), filterMode == FilterMode.PCSS ? SampleCount : SampleCount * SampleCount);

            GL.Viewport(0, 0, Resolution, Resolution);
            GL.CullFace(CullFaceMode.Front);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, DepthOnlyFramebuffer.FrameBufferObject);

            var lightProjectionMatrix = Matrix4.CreateOrthographic(finalAABB.Extents.X, finalAABB.Extents.Y, finalAABB.Min.Z + 0.1f, finalAABB.Max.Z + 0.1f);

            var ShadowMatrix =
                Matrix4.CreateTranslation(-finalWorldCenter)
                * directionalLightViewMatrix
                * lightProjectionMatrix;

            GL.Clear(ClearBufferMask.DepthBufferBit);
            shader.SetMat4(Shader.GetShaderPropertyId("ProjectionViewMatrix"), ShadowMatrix);
            Graphics.Instance.RenderScene(null, shader);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            Shader.SetGlobalMat4(Shader.GetShaderPropertyId("DirectionalLightMatrix"), ShadowMatrix);
            GL.CullFace(CullFaceMode.Back);

            Shader.SetGlobalInt(Shader.GetShaderPropertyId("DirectionalShadowFilterMode"), (int)filterMode);
            Shader.SetGlobalBool(Shader.GetShaderPropertyId("HasDirectionalShadow"), true);
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("DirectionalShadowDepthMap"), DepthOnlyFramebuffer.TextureAttachments[0]);
            Shader.SetGlobalFloat(Shader.GetShaderPropertyId("DirectionalShadowRange"), shadowRange);
        }
    }

    public class PointLightShadowMap : ShadowMap
    {
        PointLight point;
        int fbo;
        public Texture DepthCubemap { get; private set; }
        static Shader shadowShader = null;
        static int shaderShadowReferenceCount = 0;

        public PointLightShadowMap(PointLight pointLight, int resolution = 2048) : base(resolution)
        {
            point = pointLight;
            shaderShadowReferenceCount++;
        }
        protected override void OnDispose()
        {
            point = null;
            init = false;
            GL.DeleteFramebuffer(fbo);
            DepthCubemap.Dispose();
            DepthCubemap = null;
            shaderShadowReferenceCount--;
            if(shaderShadowReferenceCount <= 0)
            {
                shadowShader.Program.Dispose();
                shadowShader = null;
            }
        }
        public float FarPlane { get; set; } = 25.0f;
        bool init = false;
        public override void RenderShadowMap(Camera camera)
        {
            if (shadowShader == null)
            {
                var program = new ShaderProgram("point light shadow program",
                    AssetLoader.GetPathToAsset("./Shaders/PointLightShadowsFrag.glsl"),
                    AssetLoader.GetPathToAsset("./Shaders/vertexSimple.glsl"),
                    AssetLoader.GetPathToAsset("./Shaders/PointLightShadowsGeo.glsl"));
                program.CompileProgram();
                shadowShader = new Shader("point light shadow", program);
                shadowShader.DepthTestFunction = DepthFunction.Lequal;
                shadowShader.DepthTest = true;
            }

            if (!init)
            {
                int DepthTexture = GL.GenTexture();
                GL.BindTexture(TextureTarget.TextureCubeMap, DepthTexture);
                for (int i = 0; i < 6; i++)
                {
                    GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, PixelInternalFormat.DepthComponent, Resolution, Resolution, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (float)TextureMagFilter.Nearest);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (float)TextureMinFilter.Nearest);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
                }
                this.DepthCubemap = Texture.CreateTextureObjectFromID(DepthTexture, TextureTarget.TextureCubeMap, PixelFormat.DepthComponent, PixelInternalFormat.DepthComponent, Resolution, Resolution);

                fbo = GL.GenFramebuffer();
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
                GL.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, DepthTexture, 0);
                GL.DrawBuffer(DrawBufferMode.None);
                GL.ReadBuffer(ReadBufferMode.None);

                if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                    Debug.Log("Framebuffer is not complete!", Debug.Flag.Error);

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                init = true;
            }

            float aspect = 1.0f;
            float near = 1.0f;
            float far = FarPlane;
            var proj =  Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(90.0f), aspect, near, far);
            var lightPos = point.Transform.Position;
            Matrix4[] shadowTransforms = new Matrix4[6];
            shadowTransforms[0] = Matrix4.LookAt(lightPos, lightPos + new Vector3(1.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f)) * proj;
            shadowTransforms[1] = Matrix4.LookAt(lightPos, lightPos + new Vector3(-1.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f)) * proj;
            shadowTransforms[2] = Matrix4.LookAt(lightPos, lightPos + new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.0f, 0.0f, 1.0f)) * proj;
            shadowTransforms[3] = Matrix4.LookAt(lightPos, lightPos + new Vector3(0.0f, -1.0f, 0.0f), new Vector3(0.0f, 0.0f, -1.0f)) * proj;
            shadowTransforms[4] = Matrix4.LookAt(lightPos, lightPos + new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.0f, -1.0f, 0.0f)) * proj;
            shadowTransforms[5] = Matrix4.LookAt(lightPos, lightPos + new Vector3(0.0f, 0.0f, -1.0f), new Vector3(0.0f, -1.0f, 0.0f)) * proj;

            for (int i = 0; i < 6; i++)
            {
                shadowShader.SetMat4(Shader.GetShaderPropertyId("shadowMatrices[" + i + "]"), shadowTransforms[i]);
            }

            shadowShader.SetMat4(Shader.GetShaderPropertyId("ProjectionViewMatrix"), Matrix4.Identity);
            shadowShader.SetVector3(Shader.GetShaderPropertyId("lightPos"), lightPos);
            shadowShader.SetFloat(Shader.GetShaderPropertyId("far_plane"), far);

            GL.Viewport(0, 0, Resolution, Resolution);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            Graphics.Instance.RenderScene(null, shadowShader);
        }

    }
}
