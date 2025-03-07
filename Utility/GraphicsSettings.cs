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
        public GraphicsSettings(bool TAA) {
            if (Instance != null)
            {
                Debug.Log("Warning there can be only one instance of GraphicsSettings", Debug.Flag.Warning);
                return;
            }
            Instance = this;
            if (TAA)
            {
                Graphics.Instance.EnqueueRenderPass(new TemporalAntiAliasing());
            }
            Graphics.Instance.EnqueueRenderPass(new AutoExposure(13));
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
            Graphics.Instance.EnqueueRenderPass(new AutoExposure(13));
        }

        PostProcessPass postProcess;
        MotionblurPass motionblurPass;
        SSAO ssao;
        Bloom bloom;
        SSGI ssgi;
        SSR ssr;
        MotionVectorPass motionVector;

        public MotionVectorPass MV(bool enable)
        {
            if (enable != (motionVector != null))
            {
                if (enable)
                {
                    motionVector = new MotionVectorPass();
                    Graphics.Instance.EnqueueRenderPass(motionVector);
                }
                else
                {
                    Graphics.Instance.DequeueRenderPass(motionVector);
                    motionVector?.Dispose();
                    motionVector = null;
                }
            }
            return motionVector;
        }
        public SSGI SSGI(bool enable)
        {
            if (enable != (ssgi != null))
            {
                if (enable)
                {
                    ssgi = new SSGI(9);
                    ssgi.FarRangeSSGI = false;
                    Graphics.Instance.EnqueueRenderPass(ssgi);
                }
                else
                {
                    Graphics.Instance.DequeueRenderPass(ssgi);
                    ssgi?.Dispose();
                    ssgi = null;
                }
            }
            return ssgi;
        }
        public SSR SSR(bool enable)
        {
            if (enable != (ssr != null))
            {
                if (enable)
                {
                    ssr = new SSR(10);
                    Graphics.Instance.EnqueueRenderPass(ssr);
                }
                else
                {
                    Graphics.Instance.DequeueRenderPass(ssr);
                    ssr?.Dispose();
                    ssr = null;
                }
            }
            return ssr;
        }
        public Bloom Bloom(bool enable)
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
            return bloom;
        }
        public SSAO SSAO(bool enable)
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
            return ssao;
        }
        public MotionblurPass MotionBlur(bool enable)
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
            return motionblurPass;
        }
        public PostProcessPass PostProcess(bool enable)
        {
            if (enable != (postProcess != null))
            {
                if (enable)
                {
                    postProcess = new PostProcessPass(14);
                    Graphics.Instance.EnqueueRenderPass(postProcess);
                }
                else
                {
                    Graphics.Instance.DequeueRenderPass(postProcess);
                    postProcess?.Dispose();
                    postProcess = null;
                }
            }
            return postProcess;
        }
        
        void update()
        {
            bool bloomV = bloom != null;
            bool ssaoV = ssao != null;
            bool ssgiV = ssgi != null;
            bool ssrV = ssr != null;
            bool mv;
            bool motionblurV = motionblurPass != null;
            bool postProcessV = postProcess != null;

            var depthPrepass = Graphics.Instance.DepthPrepass;
            if (motionblurV || ssgiV)
            {
                depthPrepass = DepthPrePassMode.MotionVectors;
                Graphics.Instance.DepthPrepass = depthPrepass;
            }

            int currentIndex = (int)depthPrepass;
            string[] enumNames = Enum.GetNames(typeof(DepthPrePassMode));
            if (ImGui.Combo("Select Option", ref currentIndex, enumNames, enumNames.Length))
            {
                depthPrepass = (DepthPrePassMode)currentIndex;
                Graphics.Instance.DepthPrepass = depthPrepass;
            }
            if (depthPrepass == DepthPrePassMode.MotionVectors)
            {
                mv = true;
            }
            else
            {
                mv = false;
            }

            ImGui.Checkbox("Bloom", ref bloomV);
            ImGui.Checkbox("SSAO", ref ssaoV);
            ImGui.Checkbox("SSGI", ref ssgiV);
            ImGui.Checkbox("SSR", ref ssrV);
            ImGui.Checkbox("Motion Blur", ref motionblurV);
            ImGui.Checkbox("Final Post Process", ref postProcessV);

            float t = Graphics.Instance.RenderScale;
            ImGui.SliderFloat("Render Scale", ref t, 0.5f, 2.0f);
            Graphics.Instance.RenderScale = t;

            if (postProcess != null)
            {
                bool fxaa = postProcess.FXAA;
                ImGui.Checkbox("FXAA", ref fxaa);
                postProcess.FXAA = fxaa;

                t = postProcess.Exposure;
                ImGui.SliderFloat("Exposure", ref t, 0.01f, 4.0f);
                postProcess.Exposure = t;

                t = postProcess.BrightnessClamp;
                ImGui.InputFloat("Brightness Clamp", ref t);
                postProcess.BrightnessClamp = t;

                t = postProcess.Mosaic;
                ImGui.InputFloat("Mosaic", ref t);
                postProcess.Mosaic = t;

                t = postProcess.TargetExposure;
                ImGui.InputFloat("Target Exposure", ref t);
                postProcess.TargetExposure = t;
            }

            Bloom(bloomV);
            SSAO(ssaoV);
            MotionBlur(motionblurV);
            PostProcess(postProcessV);
            SSGI(ssgiV);
            MV(mv);
            SSR(ssrV);
        }
    }
}
