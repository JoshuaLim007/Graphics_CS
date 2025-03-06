using Assimp;
using JLUtility;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics.RenderPasses
{
    public class MotionVectorPass : RenderPass
    {
        public static MotionVectorPass instance { get; private set; }
        Matrix4 previousProjectionMatrix;
        Matrix4 previousViewMatrix;
        bool init = true;
        public Shader motionVectorShader { get; private set; }
        FrameBuffer motionVectorTex;

        public MotionVectorPass(RenderQueue queue = 0, int queueOffset = 0) : base(queue, queueOffset)
        {
            instance = this;
            var program = new ShaderProgram("Motion Vector Program",
                AssetLoader.GetPathToAsset("./Shaders/MotionVector.frag"),
                AssetLoader.GetPathToAsset("./Shaders/MotionVector.vert"));
            program.CompileProgram();
            motionVectorShader = new Shader("Motion Vector Shader", program);
            motionVectorShader.DepthMask = true;
            motionVectorShader.DepthTest = true;
            motionVectorShader.DepthTestFunction = DepthFunction.Lequal;
            motionVectorShader.ColorMask[0] = true;
            motionVectorShader.ColorMask[1] = true;
            motionVectorShader.ColorMask[2] = false;
            motionVectorShader.ColorMask[3] = false;
        }

        public override string Name => "Motion Vector pass";

        int location;
        void OnRenderCallback(Renderer e)
        {
            var mat = e.Transform.PreviousModelMatrix * Camera.Main.PreviousViewProjection;
            GL.UniformMatrix4(location, false, ref mat);
            e.Transform.PreviousModelMatrix = e.Transform.ModelMatrix;
        }
        public override void FrameSetup(Camera camera)
        {
            //motion vectors can double as depth prepass
            if(Graphics.Instance.DepthPrepass != DepthPrePassMode.MotionVectors)
            {
                Graphics.Instance.DepthPrepass = DepthPrePassMode.MotionVectors;
            }
        }
        public override void Execute(in FrameBuffer frameBuffer)
        {
            if (!FrameBuffer.AlikeResolution(motionVectorTex, frameBuffer))
            {
                if (motionVectorTex != null)
                {
                    motionVectorTex.Dispose();
                }
                var tfp = TFP.Default;
                tfp.internalFormat = PixelInternalFormat.Rg16f;
                tfp.minFilter = TextureMinFilter.Linear;
                tfp.magFilter = TextureMagFilter.Linear;
                tfp.wrapMode = OpenTK.Graphics.OpenGL4.TextureWrapMode.ClampToEdge;

                if (motionVectorTex != null)
                {
                    motionVectorTex.Dispose();
                }

                motionVectorTex = new FrameBuffer(frameBuffer.Width, frameBuffer.Height, false, tfp);
            }

            //copy original screen color
            GL.ClearColor(0, 0, 0, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            //init matrices
            if (init)
            {
                init = false;
                previousProjectionMatrix = Camera.Main.ProjectionMatrix;
                previousViewMatrix = Camera.Main.ViewMatrix;
            }

            //render scene
            location = motionVectorShader.Program.GetUniformLocation(Shader.GetShaderPropertyId("prevProjectionViewModelMatrix"));
            Camera.Main.PreviousViewProjection = previousViewMatrix * previousProjectionMatrix;

            var prevViewMat = Matrix4.CreateFromQuaternion(previousViewMatrix.ExtractRotation());
            var cameraViewProj = prevViewMat * previousProjectionMatrix;


            motionVectorShader.DepthTest = true;
            Graphics.Instance.RenderScene(Camera.Main, motionVectorShader, OnRenderCallback);
            motionVectorShader.SetMat4(Shader.GetShaderPropertyId("prevProjectionViewModelMatrix"), cameraViewProj);
            motionVectorShader.DepthTest = false;
            Graphics.Instance.RenderSkyBox(Camera.Main, motionVectorShader);

            //copy motion vector data to motion vector texture
            BlitThenRestore(frameBuffer, motionVectorTex);
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("_MotionTexture"), motionVectorTex.TextureAttachments[0]);

            //record matrices
            previousProjectionMatrix = Camera.Main.ProjectionMatrix;
            previousViewMatrix = Camera.Main.ViewMatrix;
        }

        protected override void OnDispose()
        {
            motionVectorShader.Program.Dispose();
            motionVectorTex?.Dispose();
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId("_MotionTexture"), null);

        }
    }
}
