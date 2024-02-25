﻿using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public class MotionVectorPass : RenderPass
    {
        Matrix4 previousProjectionMatrix;
        Matrix4 previousViewMatrix;
        bool init = true;
        Shader motionVectorShader;
        FrameBuffer motionVectorTex;

        public MotionVectorPass(RenderQueue queue = 0, int queueOffset = 0) : base(queue, queueOffset)
        {
            var program = new ShaderProgram("Motion Vector Program", "./Shaders/MotionVector.frag", "./Shaders/MotionVector.vert");
            program.CompileProgram();
            motionVectorShader = new Shader("Motion Vector Shader", program);
        }

        public override string Name => "Motion Vector pass";

        int location;
        Matrix4 viewProjMat;
        void OnRenderCallback(Renderer e)
        {
            var mat = e.Transform.PreviousWorldToLocalMatrix * viewProjMat;
            GL.UniformMatrix4(location, false, ref mat);
            e.Transform.PreviousWorldToLocalMatrix = e.Transform.WorldToLocalMatrix;
        }

        public override void Execute(in FrameBuffer frameBuffer)
        {
            if(!FrameBuffer.AlikeResolution(motionVectorTex, frameBuffer))
            {
                var tfp = TFP.Default;
                tfp.internalFormat = PixelInternalFormat.Rg16f;
                tfp.pixelFormat = PixelFormat.Rg;

                if (motionVectorTex != null)
                {
                    motionVectorTex.Dispose();
                }

                motionVectorTex = new FrameBuffer(frameBuffer.Width, frameBuffer.Height, false, tfp);
            }
            
            //copy original screen color
            GL.ClearColor(0,0,0,0);
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
            viewProjMat = previousViewMatrix * previousProjectionMatrix;
            Graphics.Instance.RenderScene(Camera.Main, Graphics.RenderSort.None, motionVectorShader, OnRenderCallback);

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
            motionVectorTex.Dispose();
        }
    }
}
