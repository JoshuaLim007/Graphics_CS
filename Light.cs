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
        public Light()
        {
            GlobalInstance<Light>.Values.Add(this);
        }

        public float Intensity { get; set; }
        public Vector3 Tint { get; set; }

        private float temp;
        protected Vector3 color { get; private set; }
        public float Temperature {
            get { return temp; }
            set { 
                temp = value;
                color = GetBlackBodyColor(temp);
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

    }
}
