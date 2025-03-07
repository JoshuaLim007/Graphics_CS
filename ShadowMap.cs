using JLUtility;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Quaternion = OpenTK.Mathematics.Quaternion;

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
        public float shadowRange { get; private set; }
        DirectionalLight DirectionalLight;
        Vector2 texelSize;
        
        int FilterRadius = 4;
        public void SetFilterRadius(int value)
        {
            FilterRadius = MathHelper.Clamp(value, 0, 4);
        }
        public FilterMode filterMode { get; set; } = FilterMode.PCF;
        public override string Name => "Directional Shadow Map: " + DirectionalLight.Name;
        protected override void OnDispose()
        {
            SetShadowMapToWhite();
            DepthOnlyFramebuffer.Dispose();
        }
        public static void SetShadowMapToWhite()
        {
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("DirectionalShadowDepthMap"), null);
            Shader.SetGlobalBool(Shader.GetShaderPropertyId("HasDirectionalShadow"), false);
        }
        public DirectionalShadowMap(DirectionalLight directionalLight, float ShadowRange = 100.0f, int resolution = 2048) : base(resolution)
        {
            this.shadowRange = ShadowRange > 1000.0f ? 1000.0f : (ShadowRange < 16 ? 16 : ShadowRange);
            ShaderProgram shaderProgram = new ShaderProgram("Directional Shadow Shader",
                AssetLoader.GetPathToAsset("./Shaders/fragmentEmpty.glsl"),
                AssetLoader.GetPathToAsset("./Shaders/vertexSimple.glsl"));
            shaderProgram.CompileProgram();
            shader = new Shader("Directional Shadow Material", shaderProgram, true);
            shader.DepthTest = true;
            shader.DepthTestFunction = Graphics.Instance.ReverseDepth ? DepthFunction.Gequal : DepthFunction.Lequal;
            DepthOnlyFramebuffer = new FrameBuffer(resolution, resolution, false, new TFP()
            {
                internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.DepthComponent16,
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

            Shader.SetGlobalInt(Shader.GetShaderPropertyId("DirectionalFilterRadius"), FilterRadius);
        }
        public void UpdateRange(float ShadowRange)
        {
            this.shadowRange = ShadowRange > 1000.0f ? 1000.0f : (ShadowRange < 16 ? 16 : ShadowRange);
        }
        public void ResizeResolution(int resolution)
        {
            Resolution = resolution;
            if (DepthOnlyFramebuffer == null)
            {
                return;
            }
            var temp = DepthOnlyFramebuffer;
            DepthOnlyFramebuffer = new FrameBuffer(resolution, resolution, false, new TFP()
            {
                internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.DepthComponent16,
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
            temp.Dispose();
        }
        public static Vector3[] shadowCorners = new Vector3[8];

        AABB CalculateShadowFrustum(Transform lightTransform, Quaternion CameraRotation, Vector3 CameraPosition, float CameraFOV, float aspect, out Matrix4 LightViewMatrix)
        {
            var proj = Extensions.CreatePerspectiveProjectionMatrix01Depth(MathHelper.DegreesToRadians(CameraFOV), aspect, 0.1f, shadowRange);
            var invView = (Matrix4.CreateTranslation(-CameraPosition) * Matrix4.CreateFromQuaternion(CameraRotation)).Inverted();

            //get frustum corners in world space
            var corners = CameraFrustum.GetCorners(proj.Inverted() * invView);
            shadowCorners[0] = corners[0];
            shadowCorners[1] = corners[1];
            shadowCorners[2] = corners[2];
            shadowCorners[3] = corners[3];

            shadowCorners[4] = corners[4];
            shadowCorners[5] = corners[5];
            shadowCorners[6] = corners[6];
            shadowCorners[7] = corners[7];

            //convert view frustum into light space
            var yAxis = Vector3.Dot(-lightTransform.Forward, Vector3.UnitZ) < 0 ? -Vector3.UnitY : Vector3.UnitY;
            var lightSpaceViewMatrix = Matrix4.LookAt(Vector3.Zero, -lightTransform.Forward, yAxis);

            Vector3[] aabb_corners_light = new Vector3[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                Vector4 temp = new Vector4(corners[i], 1);
                aabb_corners_light[i] = (temp * lightSpaceViewMatrix).Xyz;
            }
            var light_aabb = AABB.GetBoundingBox(aabb_corners_light);

            LightViewMatrix = Matrix4.LookAt(Vector3.Zero, -lightTransform.Forward, yAxis);
            return light_aabb;
        }
        public override void RenderShadowMap(Camera camera)
        {
            var light_aabb = CalculateShadowFrustum(
                DirectionalLight.Transform,
                camera.Transform.LocalRotation,
                camera.Transform.LocalPosition, 
                camera.Fov, 
                camera.Width / (float)camera.Height, 
                out var directionalLightViewMatrix);

            Shader.SetGlobalInt(Shader.GetShaderPropertyId("DirectionalFilterRadius"), FilterRadius);

            GL.Viewport(0, 0, Resolution, Resolution);
            GL.CullFace(CullFaceMode.Front);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, DepthOnlyFramebuffer.FrameBufferObject);

            float worldUnitPerTexelX = light_aabb.Extents.X / Resolution;
            float worldUnitPerTexelY = light_aabb.Extents.Y / Resolution;
            light_aabb.Min.X = MathF.Floor(light_aabb.Min.X / worldUnitPerTexelX) * worldUnitPerTexelX;
            light_aabb.Min.Y = MathF.Floor(light_aabb.Min.Y / worldUnitPerTexelY) * worldUnitPerTexelY;

            light_aabb.Max.X = MathF.Floor(light_aabb.Max.X / worldUnitPerTexelX) * worldUnitPerTexelX;
            light_aabb.Max.Y = MathF.Floor(light_aabb.Max.Y / worldUnitPerTexelY) * worldUnitPerTexelY;

            var lightProjectionMatrix = Extensions.CreateOrthographicOffCenter01Depth(
                light_aabb.Min.X - 1.0f,
                light_aabb.Max.X + 1.0f,
                light_aabb.Min.Y - 1.0f,
                light_aabb.Max.Y + 1.0f,
                light_aabb.Min.Z - 500,
                light_aabb.Max.Z + 500);

            var ShadowMatrix =
                directionalLightViewMatrix
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
                shadowShader.DepthTestFunction = Graphics.Instance.ReverseDepth ? DepthFunction.Gequal : DepthFunction.Lequal;
                shadowShader.DepthTest = true;
            }

            if (!init)
            {
                int DepthTexture = GL.GenTexture();
                GL.BindTexture(TextureTarget.TextureCubeMap, DepthTexture);
                for (int i = 0; i < 6; i++)
                {
                    GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, PixelInternalFormat.DepthComponent, Resolution, Resolution, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (float)TextureMagFilter.Linear);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (float)TextureMinFilter.Linear);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToBorder);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRefToTexture);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureBorderColor, new float[] { 1, 1, 1, 1 });
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);
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
            var lightPos = point.Transform.LocalPosition;
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
