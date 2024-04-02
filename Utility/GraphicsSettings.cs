using ImGuiNET;
using JLGraphics.RenderPasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics.Utility
{
    public class GraphicsSettings
    {

        public GraphicsSettings(GuiManager guiManager)
        {
            if(guiManager == null)
            {
                return;
            }
            guiManager.AddWindow("Graphics Settings", update, typeof(GraphicsSettings));
        }

        MotionVectorPass motionVector;
        PostProcessPass postProcess;
        MotionblurPass motionblurPass;
        SSAO ssao;
        Bloom bloom;
        public void Bloom(bool enable)
        {
            if (enable != (bloom != null))
            {
                if (enable)
                {
                    bloom = new Bloom(11);
                    Graphics.Instance.EnqueueRenderPass(bloom);
                }
                else
                {
                    Graphics.Instance.DequeueRenderPass(bloom);
                    bloom?.Dispose();
                    bloom = null;
                }
            }
        }
        public void SSAO(bool enable)
        {
            if (enable != (ssao != null))
            {
                if (enable)
                {
                    ssao = new SSAO(true, 10);
                    ssao.Radius = 5.0f;
                    ssao.DepthRange = 10.0f;
                    ssao.Samples = 16;
                    ssao.Intensity = 1.0f;
                    Graphics.Instance.EnqueueRenderPass(ssao);
                }
                else
                {
                    Graphics.Instance.DequeueRenderPass(ssao);
                    ssao?.Dispose();
                    ssao = null;
                }
            }
        }
        public void MotionBlur(bool enable)
        {
            if (enable != (motionblurPass != null))
            {
                if (enable)
                {
                    motionVector = new MotionVectorPass();
                    motionblurPass = new MotionblurPass(12);
                    Graphics.Instance.EnqueueRenderPass(motionVector);
                    Graphics.Instance.EnqueueRenderPass(motionblurPass);
                }
                else
                {
                    Graphics.Instance.DequeueRenderPass(motionVector);
                    Graphics.Instance.DequeueRenderPass(motionblurPass);

                    motionVector?.Dispose();
                    motionblurPass?.Dispose();
                    motionblurPass = null;
                    motionVector = null;
                }
            }
        }
        public void PostProcess(bool enable)
        {
            if (enable != (postProcess != null))
            {
                if (enable)
                {
                    postProcess = new PostProcessPass(13);
                    Graphics.Instance.EnqueueRenderPass(postProcess);
                }
                else
                {
                    Graphics.Instance.DequeueRenderPass(postProcess);
                    postProcess?.Dispose();
                    postProcess = null;
                }
            }
        }
        void update()
        {
            bool bloomV = bloom != null;
            bool ssaoV = ssao != null;
            bool motionblurV = motionblurPass != null;
            bool postProcessV = postProcess != null;

            ImGui.Checkbox("Bloom", ref bloomV);
            ImGui.Checkbox("SSAO", ref ssaoV);
            ImGui.Checkbox("Motion Blur", ref motionblurV);
            ImGui.Checkbox("Final Post Process", ref postProcessV);

            Bloom(bloomV);
            SSAO(ssaoV);
            MotionBlur(motionblurV);
            PostProcess(postProcessV);

            //Graphics.Instance.EnqueueRenderPass(new MotionVectorPass());
            //Graphics.Instance.EnqueueRenderPass(new PostProcessPass(13));
            //var bloom = new Bloom(11);
            //bloom.Iterations = 8;
            //bloom.Intensity = 0.75f;
            //Graphics.Instance.EnqueueRenderPass(bloom);
            //Graphics.Instance.EnqueueRenderPass(new MotionblurPass(12));
            //var ssao = new SSAO(true, 10);
            //ssao.Radius = 5.0f;
            //ssao.DepthRange = 10.0f;
            //ssao.Samples = 16;
            //ssao.Intensity = 1.0f;
            //Graphics.Instance.EnqueueRenderPass(ssao);


        }


    }
}
