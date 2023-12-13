using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    using System.Collections.Generic;
    using System.IO;
    using Assimp;
    using global::ObjLoader.Loader.Loaders;
    using OpenTK;
    using OpenTK.Mathematics;

    public struct GlMeshData
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

    public static class AssetLoader
    {
        private struct Face {
            public const int VerticesCount = 3;
            public List<int> vertexIndices;
            public List<int> textureIndices;
            public List<int> normalIndices;
        }
        public static GlMeshData Load(string path)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> texCoords = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();
            List<Face> Faces = new List<Face>();

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Unable to open \"" + path + "\", does not exist.");
            }

            using (StreamReader streamReader = new StreamReader(path))
            {
                while (!streamReader.EndOfStream)
                {
                    List<string> words = new List<string>(streamReader.ReadLine().ToLower().Split(' '));
                    words.RemoveAll(s => s == string.Empty);

                    if (words.Count == 0)
                        continue;

                    string type = words[0];
                    words.RemoveAt(0);

                    switch (type)
                    {
                        // vertex
                        case "v":
                            vertices.Add(new Vector3(float.Parse(words[0]), float.Parse(words[1]), float.Parse(words[2])));

                            break;

                        case "vt":
                            texCoords.Add(new Vector2(float.Parse(words[0]), float.Parse(words[1])));

                            break;

                        case "vn":
                            normals.Add(new Vector3(float.Parse(words[0]), float.Parse(words[1]), float.Parse(words[2])));

                            break;

                        // face
                        case "f":
                            Face face = new Face()
                            {
                                normalIndices = new List<int>(),
                                textureIndices = new List<int>(),
                                vertexIndices = new List<int>()
                            };
                            //words.RemoveAt(words.Count - 1);
                            foreach (string w in words)
                            {
                                if (w.Length == 0)
                                    continue;

                                string[] comps = w.Split('/');

                                // subtract 1: indices start from 1, not 0
                                face.vertexIndices.Add(int.Parse(comps[0]) - 1);

                                if (comps.Length > 1 && comps[1].Length != 0)
                                {
                                    face.textureIndices.Add(int.Parse(comps[1]) - 1);
                                }
                                if (comps.Length > 2)
                                {
                                    face.normalIndices.Add(int.Parse(comps[2]) - 1);
                                }
                            }
                            Faces.Add(face);
                            break;

                        default:
                            break;
                    }
                }
            }

            //position, normal, color,  tex
            //xyz,      xyz,    rgb,    uv
            int elementsPerVertex = 11;
            int positionOffset = 0;
            int normalOffset = 3;
            int colorOffset = 6;
            int texCoordOffset = 9;

            Dictionary<int, int> tripletVertexIndex = new Dictionary<int, int>();
            List<int> outIndices = new List<int>();
            List<float> outVertices = new List<float>();

            for (int i = 0; i < Faces.Count; i++)
            {
                var face = Faces[i];
                for (int d = 0; d < Face.VerticesCount; d++)
                {
                    int vi = face.vertexIndices[d];
                    int vti = face.textureIndices[d];
                    int vni = face.normalIndices[d];

                    string tripletId = vi + "/" + vti + "/" + vni;
                    if (tripletVertexIndex.ContainsKey(tripletId.GetHashCode()))
                    {
                        outIndices.Add(tripletVertexIndex[tripletId.GetHashCode()]);
                    }
                    else
                    {
                        int vertexIndex = (int)(outVertices.Count() / (float)elementsPerVertex);

                        outVertices.Add(vertices[vi].X);
                        outVertices.Add(vertices[vi].Y);
                        outVertices.Add(vertices[vi].Z);

                        outVertices.Add(normals[vni].X);
                        outVertices.Add(normals[vni].Y);
                        outVertices.Add(normals[vni].Z);

                        outVertices.Add(1);
                        outVertices.Add(1);
                        outVertices.Add(1);

                        outVertices.Add(texCoords[vti].X);
                        outVertices.Add(texCoords[vti].Y);

                        outIndices.Add(vertexIndex);

                        tripletVertexIndex.Add(tripletId.GetHashCode(), vertexIndex);
                    }
                }
            }

            return new GlMeshData
            {
                vertexData = outVertices.ToArray(),
                indices = outIndices.ToArray(),
                positionOffset = positionOffset,
                normalOffset = normalOffset,
                colorOffset = colorOffset,
                texCoordOffset = texCoordOffset,
                elementsPerVertex = elementsPerVertex,
                positionSize = 3,
                normalSize = 3,
                colorSize = 3,
                texCoordSize = 2,
                tangentSize = 3,
        };
        }
    }

    public static class MeshLoader { 
        public static List<Assimp.Mesh> LoadMesh(string path)
        {
            Assimp.AssimpContext context = new Assimp.AssimpContext();
            var scene = context.ImportFile(path,
                Assimp.PostProcessSteps.CalculateTangentSpace |
                Assimp.PostProcessSteps.Triangulate |
                Assimp.PostProcessSteps.OptimizeMeshes |
                Assimp.PostProcessSteps.JoinIdenticalVertices);
            return scene.Meshes;
        }
        public static List<GlMeshData> Load(string path)
        {
            Assimp.AssimpContext context = new Assimp.AssimpContext();
            var scene = context.ImportFile(path, 
                Assimp.PostProcessSteps.CalculateTangentSpace | 
                Assimp.PostProcessSteps.Triangulate | 
                Assimp.PostProcessSteps.OptimizeMeshes | 
                Assimp.PostProcessSteps.JoinIdenticalVertices);
            
            return GenerateGLMeshData(scene.Meshes);
        }
        public static List<GlMeshData> GenerateGLMeshData(List<Assimp.Mesh> meshes)
        {
            List<GlMeshData> meshDatas = new List<GlMeshData>();
            for (int i = 0; i < meshes.Count; i++)
            {
                var mesh = meshes[i];
                GlMeshData data = new();

                List<float> vertexData = new List<float>();
                List<int> indices = new List<int>();

                for (int j = 0; j < mesh.VertexCount; j++)
                {
                    vertexData.Add(mesh.Vertices[j].X);
                    vertexData.Add(mesh.Vertices[j].Y);
                    vertexData.Add(mesh.Vertices[j].Z);

                    vertexData.Add(mesh.Normals[j].X);
                    vertexData.Add(mesh.Normals[j].Y);
                    vertexData.Add(mesh.Normals[j].Z);

                    if (mesh.HasVertexColors(0))
                    {
                        vertexData.Add(mesh.VertexColorChannels[0][j].R);
                        vertexData.Add(mesh.VertexColorChannels[0][j].G);
                        vertexData.Add(mesh.VertexColorChannels[0][j].B);
                    }
                    else
                    {
                        vertexData.Add(1);
                        vertexData.Add(1);
                        vertexData.Add(1);
                    }

                    if (mesh.HasTextureCoords(0))
                    {
                        vertexData.Add(mesh.TextureCoordinateChannels[0][j].X);
                        vertexData.Add(mesh.TextureCoordinateChannels[0][j].Y);
                    }
                    else
                    {
                        vertexData.Add(0);
                        vertexData.Add(0);
                    }

                    if (mesh.HasTangentBasis)
                    {
                        vertexData.Add(mesh.Tangents[j].X);
                        vertexData.Add(mesh.Tangents[j].Y);
                        vertexData.Add(mesh.Tangents[j].Z);
                    }
                    else
                    {
                        vertexData.Add(0);
                        vertexData.Add(0);
                        vertexData.Add(0);
                    }
                }
                for (int j = 0; j < mesh.FaceCount; j++)
                {
                    indices.Add(mesh.Faces[j].Indices[0]);
                    indices.Add(mesh.Faces[j].Indices[1]);
                    indices.Add(mesh.Faces[j].Indices[2]);
                }

                data.elementsPerVertex = 14;
                data.positionOffset = 0;
                data.normalOffset = 3;
                data.colorOffset = 6;
                data.texCoordOffset = 9;
                data.tangentOffset = 11;


                data.positionSize = 3;
                data.normalSize = 3;
                data.colorSize = 3;
                data.texCoordSize = 2;
                data.tangentSize = 3;

                data.indices = indices.ToArray();
                data.vertexData = vertexData.ToArray();

                meshDatas.Add(data);
            }

            return meshDatas;
        }
    }

}
