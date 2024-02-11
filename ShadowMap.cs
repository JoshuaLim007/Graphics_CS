using JLUtility;
using ObjLoader.Loader.Data;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public class ShadowMap : SafeDispose
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
        public DirectionalShadowMap(DirectionalLight directionalLight, float size = 250.0f, float nearPlane = 1.0f, float farPlane = 1000.0f, int resolution = 2048) : base(resolution)
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
            Shader.SetGlobalTexture("DirectionalShadowDepthMap", DepthOnlyFramebuffer.TextureAttachments[0]);
            texelSize = new Vector2(1.0f / DepthOnlyFramebuffer.Width, 1.0f / DepthOnlyFramebuffer.Height);
            Shader.SetGlobalVector2("DirectionalShadowDepthMapTexelSize", texelSize);
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
            Shader.SetGlobalTexture("DirectionalShadowDepthMap", DepthOnlyFramebuffer.TextureAttachments[0]);
            texelSize = new Vector2(1.0f / DepthOnlyFramebuffer.Width, 1.0f / DepthOnlyFramebuffer.Height);
            Shader.SetGlobalVector2("DirectionalShadowDepthMapTexelSize", texelSize);
        }
        public void RenderDepthmap(Camera camera)
        {
            GL.Viewport(0, 0, Resolution, Resolution);
            GL.CullFace(CullFaceMode.Front);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, DepthOnlyFramebuffer.FrameBufferObject);
            //float mult = size / Resolution;

            var offset = DirectionalLight.Transform.Forward * farPlane * 0.5f;
            var cameraPosition = camera.Transform.Position;
            bool isUp = DirectionalLight.Transform.Forward == Vector3.UnitY || DirectionalLight.Transform.Forward == -Vector3.UnitY;
            var lightProjectionMatrix = Matrix4.CreateOrthographic(size, size, nearPlane, farPlane);
            var view = Matrix4.LookAt(offset, Vector3.Zero, isUp ? Vector3.UnitX : Vector3.UnitY);

            //var viewSpacePosition = view * new Vector4(cameraPosition, 1.0f);
            //viewSpacePosition.X = -MathF.Floor(viewSpacePosition.X / mult) * mult;
            //viewSpacePosition.Y = -MathF.Floor(viewSpacePosition.Y / mult) * mult;
            //viewSpacePosition.Z = -MathF.Floor(viewSpacePosition.Z / mult) * mult;
            //var offsetPostViewMatrix = Matrix4.CreateTranslation(new Vector3(viewSpacePosition.X, viewSpacePosition.Y, viewSpacePosition.Z));
            //var ShadowMatrix = view * offsetPostViewMatrix * lightProjectionMatrix;

            float perSize = size * 0.05f;
            cameraPosition.X = MathF.Floor(cameraPosition.X / perSize) * perSize;
            cameraPosition.Y = MathF.Floor(cameraPosition.Y / perSize) * perSize;
            cameraPosition.Z = MathF.Floor(cameraPosition.Z / perSize) * perSize;
            var offsetMatrix = Matrix4.CreateTranslation(-cameraPosition);
            var ShadowMatrix = offsetMatrix * view * lightProjectionMatrix;

            GL.Clear(ClearBufferMask.DepthBufferBit);
            shader.SetMat4("ProjectionViewMatrix", ShadowMatrix);
            Graphics.Instance.RenderScene(null, Graphics.RenderSort.None, shader);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            Shader.SetGlobalMat4("DirectionalLightMatrix", ShadowMatrix);
            GL.CullFace(CullFaceMode.Back);
        }
    }
}
