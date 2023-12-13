using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using OpenTK.Graphics.OpenGL4;
using static JLGraphics.AssetLoader;
using Assimp;

namespace JLGraphics
{
    public class Mesh
    {
        public string Path { get; set; }
        public int ElementCount { get; private set; }
        public int ElementArrayBuffer { get; private set; }
        public int VertexArrayObject { get; private set; }
        public int VertexCount { get; private set; }

        public void ApplyMesh(GlMeshData Data)
        {
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
        public Mesh(string path)
        {
            Path = path;
            var Data = MeshLoader.Load(path);
            ApplyMesh(Data[0]);
        }
        ~Mesh()
        {
            GL.DeleteBuffer(ElementArrayBuffer);
            GL.DeleteVertexArray(VertexArrayObject);
        }
  
    }
}
