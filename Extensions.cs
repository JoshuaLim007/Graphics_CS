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
        //https://www.gamedev.net/forums/topic/699724-reversed-depth-matrices-ortho-and-perspective/5394444/
        public static Matrix4 CreatePerspectiveProjectionMatrix01Depth(float fovy_rads, float s, float near, float far)
        {
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
