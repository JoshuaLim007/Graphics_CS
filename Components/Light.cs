using JLGraphics.Components;
using JLGraphics.Utility.GuiAttributes;
using JLUtility;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public abstract class Light : Component
    {
        public bool HasShadows { get; protected set; } = false;
        protected Light()
        {
            InternalGlobalScope<Light>.Values.Add(this);
            Temperature = temperature;
        }
        protected override void InternalOnImmediateDestroy()
        {
            InternalGlobalScope<Light>.Values.Remove(this);
        }
        [Gui("Intensity")]
        public float Intensity { get; set; } = 1.0f;
        [Gui("Tint", false, true)]
        public Vector3 Tint { get; set; } = Vector3.One;

        private float temperature = 5000;
        private Vector3 blackBodyColor;
        public Vector3 Color => blackBodyColor * Tint * Intensity;
        [Gui("Temperature")]
        public float Temperature {
            get { return temperature; }
            set { 
                temperature = value;
                blackBodyColor = GetBlackBodyColor(temperature);
            }
        }
        public abstract void RenderShadowMap(Camera camera);
        public static Vector3 GetBlackBodyColor(float temp)
        {
            Vector3 color = new Vector3(255.0f, 255.0f, 255.0f);
            color.X = 56100000.0f * MathF.Pow(temp, (-3.0f / 2.0f)) + 148.0f;
            color.Y = 100.04f * MathF.Log(temp) - 623.6f;
            if (temp > 6500.0) color.Y = 35200000.0f * MathF.Pow(temp, (-3.0f / 2.0f)) + 184.0f;
            color.Z = 194.18f * MathF.Log(temp) - 1448.6f;

            color.X = Math.Clamp(color.X, 0.0f, 255.0f) / 255.0f;
            color.Y = Math.Clamp(color.Y, 0.0f, 255.0f) / 255.0f;
            color.Z = Math.Clamp(color.Z, 0.0f, 255.0f) / 255.0f;

            if (temp < 1000.0) color *= temp / 1000.0f;

            return color;
        }
    }
    public class DirectionalLight : Light
    {
        DirectionalShadowMap ShadowMapper;
        DirectionalShadowMap.FilterMode p0;
        float p1;
        int p2;

        [Gui]
        DirectionalShadowMap.FilterMode shadowFilterMode {
            get
            {
                if(ShadowMapper != null)
                {
                    p0 = ShadowMapper.filterMode;
                }
                return p0;
            }
            set
            {
                p0 = value;
                ShadowMapper.filterMode = p0;
            }
        }
        [Gui]
        float shadowRange {
            get
            {
                if (ShadowMapper != null)
                {
                    p1 = ShadowMapper.shadowRange;
                }
                return p1;
            }
            set
            {
                p1 = value;
                if (enableShadows)
                {
                    RemoveShadows();
                    AddShadows(p1, p2);
                    ShadowMapper.filterMode = p0;
                }
            }
        }
        [Gui]
        int shadowResolution {
            get
            {
                if (ShadowMapper != null)
                {
                    p2 = ShadowMapper.Resolution;
                }
                return p2;
            }
            set
            {
                p2 = MathHelper.Clamp(value, 256, 8192);
                if (enableShadows)
                {
                    RemoveShadows();
                    AddShadows(p1, p2);
                    ShadowMapper.filterMode = p0;
                }
            }
        }
        [Gui]
        bool enableShadows { 
            get {
                return ShadowMapper != null;
            } 
            set 
            {
                if (value)
                {
                    RemoveShadows();
                    AddShadows(p1, p2);
                    ShadowMapper.filterMode = p0;
                }
                else
                {
                    RemoveShadows();
                }
            } 
        }

        public DirectionalLight()
        {
            HasShadows = false;
        }
        public void AddShadows(float ShadowRange, int ShadowResolution)
        {
            p1 = ShadowRange;
            p2 = ShadowResolution;
            ShadowMapper = new DirectionalShadowMap(this, ShadowRange, ShadowResolution);
            HasShadows = true;
        }
        public void RemoveShadows()
        {
            if (ShadowMapper != null)
            {
                ShadowMapper.Dispose();
            }
            ShadowMapper = null;
            HasShadows = false;
        }
        public override void RenderShadowMap(Camera camera)
        {
            PerfTimer.Start("RenderShadowMap");
            ShadowMapper.RenderShadowMap(camera);
            PerfTimer.Stop();
        }
        public DirectionalShadowMap GetShadowMapper()
        {
            return ShadowMapper;
        }
    }
    
    //std140 must be multiple of 16 (vec4)
    //only 4 byte or 16 byte elements
    public struct PointLightSSBO
    {
        public Vector4 Position;
        public Vector4 Color;
        public float Constant;
        public float Linear;
        public float Exp;
        public float Range;
        public int HasShadows;
        public float ShadowFarPlane;
        public float ShadowIndex;
        readonly public float pad1;

    }
    public class PointLight : Light
    {
        PointLightShadowMap ShadowMapper;
        [Gui("Constant")]
        public float AttenConstant { get; set; } = 0.1f;
        [Gui("Linear")]
        [GuiSlider(0.0f, 1.0f)]
        public float AttenLinear { get; set; } = 0.05f;
        [Gui("Range")]
        public float Range { get; set; } = 10.0f;

        static int shadowCount = 0;
        public const int MAX_SHADOWS = 8;
        public void AddShadows(int resolution = 1024)
        {
            if(shadowCount > MAX_SHADOWS)
            {
                return;
            }
            HasShadows = true;
            ShadowMapper = new PointLightShadowMap(this, resolution);
            shadowCount++;
        }
        public override void RenderShadowMap(Camera camera)
        {
            ShadowMapper.FarPlane = Range;
            ShadowMapper.RenderShadowMap(camera);
        }
        public void RemoveShadows()
        {
            shadowCount--;
            HasShadows = false;
            ShadowMapper.Dispose();
            ShadowMapper = null;
        }
        public PointLightShadowMap GetShadowMapper()
        {
            return ShadowMapper;
        }
    }
}
