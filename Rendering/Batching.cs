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
        struct VertexSSBO
        {
            public Matrix4 ModelMatrix;
        }
        struct FragmentSSBO
        {
            public Vector3 Color;
            public Vector3 EmissiveColor;
            public float smoothness;
            public float metalness;
            public float normalStrength;
            public float AoStrength;
        }
        public const int MAXBATCH_SIZE = 32;
        public static void BatchRender(List<Renderer> renderers)
        {
            var shader = renderers[0].Material;
            int minEnd = Math.Min(renderers.Count, MAXBATCH_SIZE);
            VertexSSBO[] vertexSSBOs = new VertexSSBO[minEnd];
            FragmentSSBO[] fragSSBOs = new FragmentSSBO[minEnd];
            MeshVerticesData[] verticesData = new MeshVerticesData[minEnd];
            for (int i = 0; i < minEnd; i++)
            {
                vertexSSBOs[i].ModelMatrix = renderers[i].Transform.ModelMatrix;

                renderers[i].Material.GetUniform(Shader.GetShaderPropertyId(DefaultMaterialUniforms.AlbedoColor), out Vector3 color);
                renderers[i].Material.GetUniform(Shader.GetShaderPropertyId(DefaultMaterialUniforms.EmissiveColor), out Vector3 ecolor);
                renderers[i].Material.GetUniform(Shader.GetShaderPropertyId(DefaultMaterialUniforms.Smoothness), out float smoothness);
                renderers[i].Material.GetUniform(Shader.GetShaderPropertyId(DefaultMaterialUniforms.Metalness), out float metalness);
                renderers[i].Material.GetUniform(Shader.GetShaderPropertyId(DefaultMaterialUniforms.NormalsStrength), out float normalS);
                renderers[i].Material.GetUniform(Shader.GetShaderPropertyId(DefaultMaterialUniforms.AOStrength), out float aoS);

                fragSSBOs[i].AoStrength = aoS;
                fragSSBOs[i].metalness = metalness;
                fragSSBOs[i].Color = color;
                fragSSBOs[i].EmissiveColor = ecolor;
                fragSSBOs[i].smoothness = smoothness;
                fragSSBOs[i].normalStrength = normalS;

                verticesData[i] = renderers[i].Mesh.RawMeshData;
            }

            shader.AttachShaderForRendering();

            Mesh.CombineMesh(verticesData, out int VAO, out int EBO, out IntPtr[] EBO_Offsets, out int[] Indices_Counts, out int vertexCount);
            GL.BindVertexArray(VAO);
            GL.MultiDrawElements(PrimitiveType.Triangles, Indices_Counts, DrawElementsType.UnsignedInt, EBO_Offsets, Indices_Counts.Length);
            GL.BindVertexArray(0);

            GL.GenBuffers(1, out int ssbo);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, Unsafe.SizeOf<VertexSSBO>() * minEnd, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 4, ssbo);

            GL.GenBuffers(1, out int ssbo1);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo1);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, Unsafe.SizeOf<FragmentSSBO>() * minEnd, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 5, ssbo1);

        }
    }
}
