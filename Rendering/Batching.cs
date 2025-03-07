using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using JLGraphics.RenderPasses;
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

        static VertexSSBO[] vertexSSBOs = new VertexSSBO[MAXBATCH_SIZE];
        static FragmentSSBO[] fragSSBOs = new FragmentSSBO[MAXBATCH_SIZE];
        static MeshVerticesData[] verticesData = new MeshVerticesData[MAXBATCH_SIZE];
        static MotionVectorVertexSSBO[] motionVectorVertexSSBOs = new MotionVectorVertexSSBO[MAXBATCH_SIZE];

        static int vertexSSBO, fragSSBO, mvSSBO;
        public static void Init()
        {
            GL.GenBuffers(1, out vertexSSBO);
            GL.GenBuffers(1, out fragSSBO);
            GL.GenBuffers(1, out mvSSBO);
        }
        public static void Free()
        {
            GL.DeleteBuffer(vertexSSBO);
            GL.DeleteBuffer(fragSSBO);
            GL.DeleteBuffer(mvSSBO);
        }
        public static bool BatchRender(Renderer[] renderers, int startIndex, Shader overrideShader = null, bool isMotionVectorRender = false)
        {

            //use first renderer's material as base shader
            bool userOverride;
            userOverride = overrideShader != null;
            var shader = userOverride ? overrideShader : renderers[startIndex].Material;
            int minEnd = Math.Min(renderers.Length - startIndex, MAXBATCH_SIZE);
            int count = 0;

            for (int i = 0; i < minEnd; i++)
            {
                count++;
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

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, vertexSSBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, Unsafe.SizeOf<VertexSSBO>() * count, vertexSSBOs, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 4, vertexSSBO);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, fragSSBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, Unsafe.SizeOf<FragmentSSBO>() * count, fragSSBOs, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 5, fragSSBO);

            if (isMotionVectorRender)
            {
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, mvSSBO);
                int size = Unsafe.SizeOf<MotionVectorVertexSSBO>();
                GL.BufferData(BufferTarget.ShaderStorageBuffer, size * count, motionVectorVertexSSBOs, BufferUsageHint.DynamicDraw);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 6, mvSSBO);
            }

            GL.Uniform1(GL.GetUniformLocation(shader.Program, "IsBatched"), 1);
            
            PerfTimer.Start("Mesh combine");
            Mesh.CombineMesh(verticesData, out int VAO, out int EBO, out IntPtr[] EBO_Offsets, out int[] Indices_Counts, out int vertexCount);
            PerfTimer.Stop();

            GL.BindVertexArray(VAO);
            GL.MultiDrawElements(PrimitiveType.Triangles, Indices_Counts, DrawElementsType.UnsignedInt, EBO_Offsets, Indices_Counts.Length);
            GL.BindVertexArray(0);
            Graphics.Instance.GraphicsDebug.TotalVertices += vertexCount;

            GL.DeleteVertexArray(VAO);
            GL.DeleteBuffer(EBO);

            GL.Uniform1(GL.GetUniformLocation(shader.Program, "IsBatched"), 0);
            return true;
        }
    }
}
