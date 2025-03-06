using System;
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
    public struct GlMeshObject
    {
        public int VAO;
        public int VBO;
        public int EBO;
        public int VertexCount;
        public int IndiciesCount;
        public AABB Bounds;
    }
    public struct MeshVerticesData
    {
        public int colorSize;
        public int positionSize;
        public int normalSize;
        public int texCoordSize;
        public int tangentSize;

        public float[] vertexData;
        public int[] indices;

        public int positionOffset;
        public int normalOffset;
        public int colorOffset;
        public int texCoordOffset;
        public int tangentOffset;
        public int elementsPerVertex;
    }
    public class Mesh : SafeDispose, IFileObject
    {
        public static GlMeshObject CreateCubeMesh()
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
            return new GlMeshObject() {
                VAO = vertexArrayObject,
                VBO = vertexBufferObject,
                VertexCount = vertices.Length,
                EBO = elementBufferObject,
                IndiciesCount = indices.Length,
                Bounds = new AABB() { Max = new Vector3(0.5f, 0.5f, 0.5f), Min = new Vector3(-0.5f, -0.5f, -0.5f) }
            };
        }
        public static GlMeshObject CreateQuadMesh()
        {
            int[] quad_VertexArrayID = new int[1];
            GL.GenVertexArrays(1, quad_VertexArrayID);
            GL.BindVertexArray(quad_VertexArrayID[0]);

            float[] g_quad_vertex_buffer_data = {
                    -1.0f, -1.0f,
                    -1.0f, 1.0f,
                    1.0f,  1.0f,
                    1.0f,  -1.0f,
                };
            int[] indices = {
                2, 1, 0, 
                3, 2, 0
            };


            int[] quad_vertexbuffer = new int[1];
            int ebo;
            GL.GenBuffers(1, quad_vertexbuffer);
            GL.GenBuffers(1, out ebo);
            
            GL.BindBuffer(BufferTarget.ArrayBuffer, quad_vertexbuffer[0]);
            GL.BufferData(BufferTarget.ArrayBuffer, sizeof(float) * g_quad_vertex_buffer_data.Length, g_quad_vertex_buffer_data, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, sizeof(int) * indices.Length, indices, BufferUsageHint.StaticDraw);

            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
            return new GlMeshObject() { VAO = quad_VertexArrayID[0], VBO = quad_vertexbuffer[0], EBO = ebo, IndiciesCount = indices.Length, Bounds = new AABB()
                {
                    Min = new Vector3(-1.0f, -1.0f, 0.0f),
                    Max = new Vector3(1.0f, 1.0f, 0.0f)
                },
                VertexCount = g_quad_vertex_buffer_data.Length };
        }
        public static void FreeMeshObject(GlMeshObject meshObject)
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.DeleteBuffer(meshObject.VBO);
            GL.DeleteBuffer(meshObject.EBO);
            GL.BindVertexArray(0);
            GL.DeleteVertexArray(meshObject.VAO);
        }
        public int ElementCount { get; private set; }
        public int ElementArrayBuffer { get; private set; }
        public int VertexArrayObject { get; private set; }
        public int VertexCount { get; private set; }
        public AABB BoundingBox { get; private set; }
        public MeshVerticesData RawMeshData { get; private set; }
        public List<Action> FileChangeCallback => new List<Action>();
        public string FilePath { get; }

        public override string Name => "Mesh: " + ElementArrayBuffer;

        public static void CombineMesh(
            MeshVerticesData[] meshVerticesDatas,
            out int VAO,
            out int EBO,
            out IntPtr[] EBO_Offsets,
            out int[] Indices_Counts,
            out int VertexCount)
        {

            int totalVertData = 0;
            int totalIndData = 0;
            EBO_Offsets = new IntPtr[meshVerticesDatas.Length];
            Indices_Counts = new int[meshVerticesDatas.Length];

            for (int i = 0; i < meshVerticesDatas.Length; i++)
            {
                EBO_Offsets[i] = (IntPtr)(totalIndData * sizeof(int));
                Indices_Counts[i] = meshVerticesDatas[i].indices.Length;
                totalIndData += meshVerticesDatas[i].indices.Length;
                totalVertData += meshVerticesDatas[i].vertexData.Length;
            }

            int vertIndex = 0;
            int indIndex = 0;
            float[] vertices = new float[totalVertData];
            int[] indices = new int[totalIndData];

            MeshVerticesData initialData = meshVerticesDatas[0];
            int vertexCounter = 0;
            for (int i = 0; i < meshVerticesDatas.Length; i++)
            {
                int vertexCount = meshVerticesDatas[i].vertexData.Length / meshVerticesDatas[i].elementsPerVertex;

                var curVertexBuffer = meshVerticesDatas[i].vertexData;
                var curIndicesBuffer = meshVerticesDatas[i].indices;

                for (int j = 0; j < curIndicesBuffer.Length; j++)
                {
                    curIndicesBuffer[j] += vertexCounter;
                }

                Array.Copy(curVertexBuffer, 0, vertices, vertIndex, curVertexBuffer.Length);
                Array.Copy(curIndicesBuffer, 0, indices, indIndex, curIndicesBuffer.Length);

                for (int j = 0; j < curIndicesBuffer.Length; j++)
                {
                    curIndicesBuffer[j] -= vertexCounter;
                }

                vertexCounter += vertexCount;
                vertIndex += curVertexBuffer.Length;
                indIndex += curIndicesBuffer.Length;
            }

            int VertexArrayObject = GL.GenVertexArray();
            int ElementArrayBuffer = GL.GenBuffer();
            int vertexBuffer = GL.GenBuffer();

            GL.BindVertexArray(VertexArrayObject);

            // Upload Vertex
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
            //Enable vertex attributes
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, initialData.positionSize, VertexAttribPointerType.Float, false, initialData.elementsPerVertex * sizeof(float), initialData.positionOffset * sizeof(float));
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, initialData.colorSize, VertexAttribPointerType.Float, false, initialData.elementsPerVertex * sizeof(float), initialData.colorOffset * sizeof(float));
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, initialData.texCoordSize, VertexAttribPointerType.Float, false, initialData.elementsPerVertex * sizeof(float), initialData.texCoordOffset * sizeof(float));
            GL.EnableVertexAttribArray(3);
            GL.VertexAttribPointer(3, initialData.normalSize, VertexAttribPointerType.Float, false, initialData.elementsPerVertex * sizeof(float), initialData.normalOffset * sizeof(float));
            GL.EnableVertexAttribArray(4);
            GL.VertexAttribPointer(4, initialData.tangentSize, VertexAttribPointerType.Float, false, initialData.elementsPerVertex * sizeof(float), initialData.tangentOffset * sizeof(float));

            // Upload index data
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ElementArrayBuffer);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(int), indices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.DeleteBuffer(vertexBuffer);

            VAO = VertexArrayObject;
            EBO = ElementArrayBuffer;
            VertexCount = totalVertData / initialData.elementsPerVertex;
        }
        public void ApplyMesh(MeshVerticesData Data)
        {
            RawMeshData = Data;
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
        public Mesh(MeshVerticesData data, string fileToTrack)
        {
            FilePath = fileToTrack;
            ApplyMesh(data);
        }
        public Mesh(MeshVerticesData data)
        {
            FilePath = "";
            ApplyMesh(data);
        }
        public Mesh(GlMeshObject meshPrimative)
        {
            VertexArrayObject = meshPrimative.VAO;
            ElementArrayBuffer = meshPrimative.EBO;
            ElementCount = meshPrimative.IndiciesCount;
            VertexCount = meshPrimative.VertexCount;
            BoundingBox = meshPrimative.Bounds;
        }
    }
}
