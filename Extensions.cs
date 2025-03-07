using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public static class Extensions
    {
        public static Vector3 ConstrainToAxis(this Vector3 a, Vector3 b)
        {
            if (b == Vector3.Zero)
                return Vector3.Zero; // Prevent division by zero

            return Vector3.Dot(a, b) / Vector3.Dot(b, b) * b;
        }
        public static Vector3 ProjectOntoPlane(this Vector3 v, Vector3 n)
        {
            // Ensure the normal is normalized
            n = Vector3.Normalize(n);

            // Calculate the projection of v onto the plane
            return v - Vector3.Dot(v, n) * n;
        }
        public static Vector3 Slerp(this Vector3 v0, Vector3 v1, float t)
        {
            // Normalize vectors
            v0 = Vector3.Normalize(v0);
            v1 = Vector3.Normalize(v1);

            // Calculate the dot product
            float dot = Vector3.Dot(v0, v1);

            // Clamp the dot product to avoid numerical errors
            dot = Math.Clamp(dot, -1.0f, 1.0f);

            // Calculate the angle between the vectors
            float theta = MathF.Acos(dot);

            // If the angle is very small, return a linear interpolation
            if (MathF.Abs(theta) < 0.0001f)
            {
                return Vector3.Lerp(v0, v1, t);
            }

            // Calculate the sin(theta)
            float sinTheta = MathF.Sin(theta);

            // Calculate the interpolated vector
            float scale0 = MathF.Sin((1 - t) * theta) / sinTheta;
            float scale1 = MathF.Sin(t * theta) / sinTheta;

            return scale0 * v0 + scale1 * v1;
        }
        public static bool ReverseDepthBuffer { get; internal set; } = false;
        //https://www.gamedev.net/forums/topic/699724-reversed-depth-matrices-ortho-and-perspective/5394444/
        public static Matrix4 CreatePerspectiveProjectionMatrix01Depth(float fovy_rads, float s, float near, float far)
        {
            if (ReverseDepthBuffer)
            {
                var t = near;
                near = far;
                far = t;
            }

            float g = 1.0f / MathF.Tan(fovy_rads * 0.5f);

            return new Matrix4(
                        g / s,  0.0f,   0.0f,       0.0f,
                        0.0f,   g,      0.0f,       0.0f,
                        0.0f,   0.0f,   far / (near - far),          -1.0f,
                        0.0f,   0.0f,   -(near * far) / (far - near),  0.0f);
        }
        //https://github.com/PacktPublishing/Vulkan-Cookbook/blob/master/Library/Source%20Files/10%20Helper%20Recipes/05%20Preparing%20an%20orthographic%20projection%20matrix.cpp
        public static Matrix4 CreateOrthographicOffCenter01Depth(float left, float right, float bottom, float top, float depthNear, float depthFar)
        {
            if (ReverseDepthBuffer)
            {
                var t = depthNear;
                depthNear = depthFar;
                depthFar = t;
            }

            var result = Matrix4.Identity;
            float num = 1f / (right - left);
            float num2 = 1f / (top - bottom);
            float num3 = 1f / (depthNear - depthFar);
            result.Row0.X = 2f * num;
            result.Row1.Y = 2f * num2;
            result.Row2.Z = num3;
            result.Row3.X = (0f - (right + left)) * num;
            result.Row3.Y = (0f - (top + bottom)) * num2;
            result.Row3.Z = depthNear * num3;
            return result;
        }
    }
}
