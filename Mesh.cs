﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using OpenTK.Graphics.OpenGL4;
using JLUtility;
using OpenTK.Mathematics;

namespace JLGraphics
{
    /// <summary>
    /// Basic mesh, no indices, just vertex positions
    /// </summary>
    public struct MeshPrimative
    {
        public int VAO;
        public int VBO;
        public int EBO;
        public int VertexCount;
        public int IndiciesCount;
        public bool HasEBO;
    }
    public class Mesh : SafeDispose, IFileObject
    {
        public static MeshPrimative CreateCubeMesh()
        {
            float[] vertices = {
                // front
                -0.5f, -0.5f,  0.5f,
                 0.5f, -0.5f,  0.5f,
                 0.5f,  0.5f,  0.5f,
                -0.5f,  0.5f,  0.5f,
                // back
                -0.5f, -0.5f, -0.5f,
                 0.5f, -0.5f, -0.5f,
                 0.5f,  0.5f, -0.5f,
                -0.5f,  0.5f, -0.5f
            };
            int[] indices = {
		        // front
		        0, 1, 2,
                2, 3, 0,
		        // right
		        1, 5, 6,
                6, 2, 1,
		        // back
		        7, 6, 5,
                5, 4, 7,
		        // left
		        4, 0, 3,
                3, 7, 4,
		        // bottom
		        4, 5, 1,
                1, 0, 4,
		        // top
		        3, 2, 6,
                6, 7, 3
            };

            // Vertex Array Object
            int vertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(vertexArrayObject);

            int vertexBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            int elementBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, elementBufferObject);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(int), indices, BufferUsageHint.StaticDraw);

            // Set up vertex attributes
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            return new MeshPrimative() { VAO = vertexArrayObject, VBO = vertexBufferObject, VertexCount = vertices.Length, EBO = elementBufferObject, HasEBO = true, IndiciesCount = indices.Length};
        }
        public static MeshPrimative CreateQuadMesh()
        {
            int[] quad_VertexArrayID = new int[1];
            GL.GenVertexArrays(1, quad_VertexArrayID);
            GL.BindVertexArray(quad_VertexArrayID[0]);

            float[] g_quad_vertex_buffer_data = {
                    -1.0f, -1.0f,
                    1.0f, -1.0f,
                    -1.0f,  1.0f,
                    -1.0f,  1.0f,
                    1.0f, -1.0f,
                    1.0f,  1.0f,
                };

            int[] quad_vertexbuffer = new int[1];
            GL.GenBuffers(1, quad_vertexbuffer);
            GL.BindBuffer(BufferTarget.ArrayBuffer, quad_vertexbuffer[0]);
            GL.BufferData(BufferTarget.ArrayBuffer, sizeof(float) * g_quad_vertex_buffer_data.Length, g_quad_vertex_buffer_data, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
            return new MeshPrimative() { VAO = quad_VertexArrayID[0], VBO = quad_vertexbuffer[0], VertexCount = g_quad_vertex_buffer_data.Length, HasEBO = false };
        }
        public static void FreeMeshObject(MeshPrimative meshObject)
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.DeleteBuffer(meshObject.VBO);
            if (meshObject.HasEBO)
            {
                GL.DeleteBuffer(meshObject.EBO);
            }
            GL.BindVertexArray(0);
            GL.DeleteVertexArray(meshObject.VAO);
        }
        public int ElementCount { get; private set; }
        public int ElementArrayBuffer { get; private set; }
        public int VertexArrayObject { get; private set; }
        public int VertexCount { get; private set; }
        public AABB BoundingBox { get; private set; }

        public List<Action> FileChangeCallback => new List<Action>();
        public string FilePath { get; }

        public override string Name => "Mesh: " + ElementArrayBuffer;

        public void ApplyMesh(GlMeshData Data)
        {
            Vector3 minPoint = Vector3.PositiveInfinity;
            Vector3 maxPoint = Vector3.NegativeInfinity;

            //calculate minmax for AABB
            for (int i = 0; i < Data.vertexData.Length; i++)
            {
                int positionIndex = i + Data.positionOffset;
                float x = Data.vertexData[positionIndex];
                float y = Data.vertexData[positionIndex + 1];
                float z = Data.vertexData[positionIndex + 2];

                if(x < minPoint.X)
                {
                    minPoint.X = x;
                }
                if (y < minPoint.Y)
                {
                    minPoint.Y = y;
                }
                if (z < minPoint.Z)
                {
                    minPoint.Z = z;
                }


                if (x > maxPoint.X)
                {
                    maxPoint.X = x;
                }
                if (y > maxPoint.Y)
                {
                    maxPoint.Y = y;
                }
                if (z > maxPoint.Z)
                {
                    maxPoint.Z = z;
                }

                int vertexSize = Data.elementsPerVertex;
                i += (vertexSize - 1);
            }
            BoundingBox = new AABB() { Max = maxPoint, Min = minPoint };

            float[] vertices = Data.vertexData;
            int[] indices = Data.indices;
            ElementCount = indices.Length;

            int vertexBuffer = GL.GenBuffer();
            ElementArrayBuffer = GL.GenBuffer();
            VertexArrayObject = GL.GenVertexArray();

            GL.BindVertexArray(VertexArrayObject);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, Data.positionSize, VertexAttribPointerType.Float, false, Data.elementsPerVertex * sizeof(float), Data.positionOffset * sizeof(float));
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, Data.colorSize, VertexAttribPointerType.Float, false, Data.elementsPerVertex * sizeof(float), Data.colorOffset * sizeof(float));
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, Data.texCoordSize, VertexAttribPointerType.Float, false, Data.elementsPerVertex * sizeof(float), Data.texCoordOffset * sizeof(float));
            GL.EnableVertexAttribArray(3);
            GL.VertexAttribPointer(3, Data.normalSize, VertexAttribPointerType.Float, false, Data.elementsPerVertex * sizeof(float), Data.normalOffset * sizeof(float));            
            GL.EnableVertexAttribArray(4);
            GL.VertexAttribPointer(4, Data.tangentSize, VertexAttribPointerType.Float, false, Data.elementsPerVertex * sizeof(float), Data.tangentOffset * sizeof(float));
            

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ElementArrayBuffer);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(int), indices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.DeleteBuffer(vertexBuffer);

            VertexCount = (int)(Data.vertexData.Count() / (float)Data.elementsPerVertex);
        }

        protected override void OnDispose()
        {
            DestructorCommands.Instance.QueueAction(() => GL.DeleteBuffer(ElementArrayBuffer));
            DestructorCommands.Instance.QueueAction(() => GL.DeleteVertexArray(VertexArrayObject));
        }
        public Mesh(string path)
        {
            var Data = AssetLoader.LoadMeshFromFile(path);
            FilePath = path;
            ApplyMesh(Data);
        }
        public Mesh(GlMeshData data, string path)
        {
            FilePath = path;
            ApplyMesh(data);
        }
    }
}
