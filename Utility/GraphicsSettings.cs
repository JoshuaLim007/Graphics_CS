using ImGuiNET;
using JLGraphics.RenderPasses;
using JLUtility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics.Utility
{
    public class GraphicsSettings
    {
        public static GraphicsSettings Instance { get; private set; }
        public GraphicsSettings() {
            if (Instance != null)
            {
                Debug.Log("Warning there can be only one instance of GraphicsSettings", Debug.Flag.Warning);
                return;
            }
            Instance = this;
        }
        public GraphicsSettings(GuiManager guiManager, bool TAA)
        {
            if(Instance != null)
            {
                Debug.Log("Warning there can be only one instance of GraphicsSettings", Debug.Flag.Warning);
                return;
            }
            Instance = this;
            if (guiManager == null)
            {
                return;
            }
            guiManager.AddWindow("Graphics Settings", update, typeof(GraphicsSettings));
            if (TAA)
            {
                Graphics.Instance.EnqueueRenderPass(new TemporalAntiAliasing());
            }
            Graphics.Instance.EnqueueRenderPass(new MotionVectorPass());
        }

        PostProcessPass postProcess;
        MotionblurPass motionblurPass;
        SSAO ssao;
        Bloom bloom;
        SSGI ssgi;

        public void SSGI(bool enable)
        {
            if (enable != (ssgi != null))
            {
                if (enable)
                {
                    ssgi = new SSGI(9);
                    Graphics.Instance.EnqueueRenderPass(ssgi);
                }
                else
                {
                    Graphics.Instance.DequeueRenderPass(ssgi);
                    ssgi?.Dispose();
                    ssgi = null;
                }
            }
        }
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
                    motionblurPass = new MotionblurPass(12);
                    Graphics.Instance.EnqueueRenderPass(motionblurPass);
                }
                else
                {
                    Graphics.Instance.DequeueRenderPass(motionblurPass);

                    motionblurPass?.Dispose();
                    motionblurPass = null;
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
            float t = Graphics.Instance.RenderScale;
            ImGui.SliderFloat("Render Scale", ref t, 0.5f, 2.0f);
            Graphics.Instance.RenderScale = t;

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
