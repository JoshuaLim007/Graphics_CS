using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace JLGraphics.Rendering
{
    public class Batching
    {
        private Batching() { }
        struct VertexSSBO
        {
            public Matrix4 ModelMatrix;
        }
        struct MotionVectorVertexSSBO
        {
            public Matrix4 PrevProjectionViewModelMatrix;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct FragmentSSBO
        {
            public float smoothness;
            public float metalness;
            public float normalStrength;
            public float AoStrength;
            public Vector4 Color;
            public Vector4 EmissiveColor;
        }
        public const int MAXBATCH_SIZE = 128;
        public static bool BatchRender(Renderer[] renderers, int startIndex, Shader overrideShader = null, bool isMotionVectorRender = false)
        {

            //use first renderer's material as base shader
            bool userOverride;
            userOverride = overrideShader != null;
            var shader = userOverride ? overrideShader : renderers[startIndex].Material;

            int minEnd = Math.Min(renderers.Length - startIndex, MAXBATCH_SIZE);
            VertexSSBO[] vertexSSBOs = new VertexSSBO[minEnd];
            FragmentSSBO[] fragSSBOs = new FragmentSSBO[minEnd];
            MeshVerticesData[] verticesData = new MeshVerticesData[minEnd];
            MotionVectorVertexSSBO[] motionVectorVertexSSBOs = null;
            if (isMotionVectorRender)
            {
                motionVectorVertexSSBOs = new MotionVectorVertexSSBO[minEnd];
            }
            for (int i = 0; i < minEnd; i++)
            {
                int offsetI = i + startIndex;

                //we need same shader program
                if (!userOverride)
                {
                    if (renderers[offsetI].Material.Program != shader.Program)
                    {
                        return false;
                    }
                }
                vertexSSBOs[i].ModelMatrix = renderers[offsetI].Transform.ModelMatrix;
                renderers[offsetI].Material.GetUniform(Shader.GetShaderPropertyId(DefaultMaterialUniforms.AlbedoColor), out Vector3 color);
                renderers[offsetI].Material.GetUniform(Shader.GetShaderPropertyId(DefaultMaterialUniforms.EmissiveColor), out Vector3 ecolor);
                renderers[offsetI].Material.GetUniform(Shader.GetShaderPropertyId(DefaultMaterialUniforms.Smoothness), out float smoothness);
                renderers[offsetI].Material.GetUniform(Shader.GetShaderPropertyId(DefaultMaterialUniforms.Metalness), out float metalness);
                renderers[offsetI].Material.GetUniform(Shader.GetShaderPropertyId(DefaultMaterialUniforms.NormalsStrength), out float normalS);
                renderers[offsetI].Material.GetUniform(Shader.GetShaderPropertyId(DefaultMaterialUniforms.AOStrength), out float aoS);
                
                if (isMotionVectorRender)
                {
                    motionVectorVertexSSBOs[i].PrevProjectionViewModelMatrix = renderers[offsetI].Transform.PreviousModelMatrix * Camera.Main.PreviousViewProjection;
                    renderers[offsetI].Transform.PreviousModelMatrix = renderers[offsetI].Transform.ModelMatrix;
                }

                fragSSBOs[i].AoStrength = aoS;
                fragSSBOs[i].metalness = metalness;
                fragSSBOs[i].Color = new Vector4(color.X, color.Y, color.Z, 0);
                fragSSBOs[i].EmissiveColor = new Vector4(ecolor.X, ecolor.Y, ecolor.Z, 0);
                fragSSBOs[i].smoothness = smoothness;
                fragSSBOs[i].normalStrength = normalS;

                verticesData[i] = renderers[offsetI].Mesh.RawMeshData;
            }

            int updateFlags = shader.AttachShaderForRendering();
            if ((updateFlags & 0b10) == 0b10)
            {
                Graphics.Instance.GraphicsDebug.MaterialUpdateCount++;
            }
            if ((updateFlags & 0b01) == 0b01)
            {
                Graphics.Instance.GraphicsDebug.UseProgramCount++;
            }
            Graphics.Instance.GraphicsDebug.DrawCount++;
            Graphics.Instance.GraphicsDebug.MeshBindCount++;

            GL.GenBuffers(1, out int ssbo);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, Unsafe.SizeOf<VertexSSBO>() * minEnd, vertexSSBOs, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 4, ssbo);

            GL.GenBuffers(1, out int ssbo1);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo1);
            int size = Unsafe.SizeOf<FragmentSSBO>();
            GL.BufferData(BufferTarget.ShaderStorageBuffer, size * fragSSBOs.Length, fragSSBOs, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 5, ssbo1);

            int ssbo2 = 0;
            if (isMotionVectorRender)
            {
                GL.GenBuffers(1, out ssbo2);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo2);
                size = Unsafe.SizeOf<MotionVectorVertexSSBO>();
                GL.BufferData(BufferTarget.ShaderStorageBuffer, size * motionVectorVertexSSBOs.Length, motionVectorVertexSSBOs, BufferUsageHint.DynamicDraw);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 6, ssbo2);
            }

            GL.Uniform1(GL.GetUniformLocation(shader.Program, "IsBatched"), 1);
            Mesh.CombineMesh(verticesData, out int VAO, out int EBO, out IntPtr[] EBO_Offsets, out int[] Indices_Counts, out int vertexCount);
            GL.BindVertexArray(VAO);
            GL.MultiDrawElements(PrimitiveType.Triangles, Indices_Counts, DrawElementsType.UnsignedInt, EBO_Offsets, Indices_Counts.Length);
            GL.BindVertexArray(0);
            Graphics.Instance.GraphicsDebug.TotalVertices += vertexCount;

            GL.DeleteBuffer(ssbo);
            GL.DeleteBuffer(ssbo1);
            if (isMotionVectorRender)
            {
                GL.DeleteBuffer(ssbo2);
            }

            GL.DeleteVertexArray(VAO);
            GL.DeleteBuffer(EBO);

            GL.Uniform1(GL.GetUniformLocation(shader.Program, "IsBatched"), 0);
            return true;
        }
    }
}
