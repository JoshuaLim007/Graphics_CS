using JLUtility;
using Microsoft.VisualBasic;
using ObjLoader.Loader.Data;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public FrameBuffer DepthOnlyFramebuffer { get; private set; }
        float nearPlane, farPlane, size;
        DirectionalLight DirectionalLight;
        Vector2 texelSize;
        public override string Name => "Directional Shadow Map: " + DirectionalLight.Name;
        protected override void OnDispose()
        {
            DepthOnlyFramebuffer.Dispose();
        }
        public static void SetShadowMapToWhite()
        {
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("DirectionalShadowDepthMap"), null);
        }
        public DirectionalShadowMap(DirectionalLight directionalLight, float size = 100.0f, float nearPlane = 1.0f, float farPlane = 1000.0f, int resolution = 2048) : base(resolution)
        {
            this.size = size;
            ShaderProgram shaderProgram = new ShaderProgram("Directional Shadow Shader", "./Shaders/fragmentEmpty.glsl", "./Shaders/vertexSimple.glsl");
            shaderProgram.CompileProgram();
            shader = new Shader("Directional Shadow Material", shaderProgram, true);
            shader.DepthTest = true;
            shader.DepthTestFunction = DepthFunction.Lequal;
            DepthOnlyFramebuffer = new FrameBuffer(resolution, resolution, false, new TFP()
            {
                internalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.DepthComponent32,
                pixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat.DepthComponent,
                magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                maxMipmap = 0,
                minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear,
                wrapMode = OpenTK.Graphics.OpenGL4.TextureWrapMode.ClampToBorder,
                borderColor = Vector4.One,
                isShadowMap = true,
            });
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("DirectionalShadowDepthMap"), DepthOnlyFramebuffer.TextureAttachments[0]);
            texelSize = new Vector2(1.0f / DepthOnlyFramebuffer.Width, 1.0f / DepthOnlyFramebuffer.Height);
            Shader.SetGlobalVector2(Shader.GetShaderPropertyId("DirectionalShadowDepthMapTexelSize"), texelSize);
            this.DirectionalLight = directionalLight;
            this.nearPlane = nearPlane;
            this.farPlane = farPlane;
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
                pixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat.DepthComponent,
                magFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear,
                maxMipmap = 0,
                minFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear,
                wrapMode = OpenTK.Graphics.OpenGL4.TextureWrapMode.Repeat,
            });
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("DirectionalShadowDepthMap"), DepthOnlyFramebuffer.TextureAttachments[0]);
            texelSize = new Vector2(1.0f / DepthOnlyFramebuffer.Width, 1.0f / DepthOnlyFramebuffer.Height);
            Shader.SetGlobalVector2(Shader.GetShaderPropertyId("DirectionalShadowDepthMapTexelSize"), texelSize);
        }
        public override void RenderShadowMap(Camera camera)
        {
            GL.Viewport(0, 0, Resolution, Resolution);
            GL.CullFace(CullFaceMode.Front);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, DepthOnlyFramebuffer.FrameBufferObject);

            var offset = DirectionalLight.Transform.Forward * farPlane * 0.5f;
            var cameraPosition = camera.Transform.Position;
            bool isUp = DirectionalLight.Transform.Forward == Vector3.UnitY || DirectionalLight.Transform.Forward == -Vector3.UnitY;
            var lightProjectionMatrix = Matrix4.CreateOrthographic(size, size, nearPlane, farPlane);
            var view = Matrix4.LookAt(offset, Vector3.Zero, isUp ? Vector3.UnitX : Vector3.UnitY);

            float perSize = size * 0.015625f;
            cameraPosition.X = MathF.Floor(cameraPosition.X / perSize) * perSize;
            cameraPosition.Y = MathF.Floor(cameraPosition.Y / perSize) * perSize;
            cameraPosition.Z = MathF.Floor(cameraPosition.Z / perSize) * perSize;
            var offsetMatrix = Matrix4.CreateTranslation(-cameraPosition);
            var ShadowMatrix = offsetMatrix * view * lightProjectionMatrix;

            GL.Clear(ClearBufferMask.DepthBufferBit);
            shader.SetMat4(Shader.GetShaderPropertyId("ProjectionViewMatrix"), ShadowMatrix);
            Graphics.Instance.RenderScene(null, shader);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            Shader.SetGlobalMat4(Shader.GetShaderPropertyId("DirectionalLightMatrix"), ShadowMatrix);
            GL.CullFace(CullFaceMode.Back);

            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("DirectionalShadowDepthMap"), DepthOnlyFramebuffer.TextureAttachments[0]);
            Shader.SetGlobalFloat(Shader.GetShaderPropertyId("DirectionalShadowRange"), size);
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
                var program = new ShaderProgram("point light shadow program", "./Shaders/PointLightShadowsFrag.glsl", "./Shaders/vertexSimple.glsl", "./Shaders/PointLightShadowsGeo.glsl");
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
