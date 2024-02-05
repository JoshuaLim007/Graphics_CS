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
        protected Light()
        {
            InternalGlobalScope<Light>.Values.Add(this);
            Temperature = temperature;
        }
        protected override void InternalOnImmediateDestroy()
        {
            base.InternalOnImmediateDestroy();
            InternalGlobalScope<Light>.Values.Remove(this);
        }
        public float Intensity { get; set; } = 1.0f;
        public Vector3 Tint { get; set; } = Vector3.One;

        private float temperature = 5000;
        private Vector3 blackBodyColor;
        public Vector3 Color => blackBodyColor * Tint * Intensity;
        public float Temperature {
            get { return temperature; }
            set { 
                temperature = value;
                blackBodyColor = GetBlackBodyColor(temperature);
            }
        }
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
    }
    public class PointLight : Light
    {
        public float AttenConstant { get; set; } = 0.1f;
        public float AttenLinear { get; set; } = 0.2f;
        public float AttenExp { get; set; } = 0.8f;
    }
}
