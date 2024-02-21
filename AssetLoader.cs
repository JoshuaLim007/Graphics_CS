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
    using F23.StringSimilarity;
    using global::ObjLoader.Loader.Loaders;
    using JLUtility;
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

    public static class AssetLoader { 
        public static Assimp.Scene LoadScene(string path)
        {
            Assimp.AssimpContext context = new Assimp.AssimpContext();
            var scene = context.ImportFile(path,
                Assimp.PostProcessSteps.CalculateTangentSpace |
                Assimp.PostProcessSteps.Triangulate |
                Assimp.PostProcessSteps.OptimizeMeshes |
                Assimp.PostProcessSteps.JoinIdenticalVertices);
            return scene;
        }
        public static GlMeshData Load(string path)
        {
            var scene = LoadScene(path);
            return GenerateGLMeshData(scene.Meshes[0]);
        }

        private static Entity HandleNode(string path, Scene scene, Node node, List<Entity> entities, List<Shader> materialPool)
        {
            Entity entity = Entity.Create(node.Name);
            entities.Add(entity);

            if(node.MeshCount == 1)
            {
                var currentMesh = scene.Meshes[node.MeshIndices[0]];
                entity.AddComponent<Renderer>(out var renderer);

                var meshData = GenerateGLMeshData(currentMesh);

                renderer.Mesh = new Mesh(meshData, path);
                if (scene.HasMaterials)
                {
                    renderer.Material = materialPool[currentMesh.MaterialIndex];
                }
                node.Transform.Decompose(out Vector3D scaling, out Assimp.Quaternion rot, out Vector3D position);
                entity.Transform.Position = new Vector3(position.X, position.Y, position.Z);
                entity.Transform.Scale = new Vector3(scaling.X, scaling.Y, scaling.Z);
                entity.Transform.Rotation = new OpenTK.Mathematics.Quaternion(rot.X, rot.Y, rot.Z, rot.W);
            }
            else
            {
                for (int i = 0; i < node.MeshCount; i++)
                {
                    var currentMesh = scene.Meshes[node.MeshIndices[i]];
                    var newEntity = Entity.Create(currentMesh.Name);
                    newEntity.AddComponent<Renderer>(out var renderer);
                    entities.Add(newEntity);
                    entity.Parent = newEntity;
                    var meshData = GenerateGLMeshData(currentMesh);

                    renderer.Mesh = new Mesh(meshData, path);
                    if (scene.HasMaterials)
                    {
                        renderer.Material = materialPool[currentMesh.MaterialIndex];
                    }
                    newEntity.Parent = entity;
                    node.Transform.Decompose(out Vector3D scaling, out Assimp.Quaternion rot, out Vector3D position);
                    newEntity.Transform.Position = new Vector3(position.X, position.Y, position.Z);
                    newEntity.Transform.Scale = new Vector3(scaling.X, scaling.Y, scaling.Z);
                    newEntity.Transform.Rotation = new OpenTK.Mathematics.Quaternion(rot.X, rot.Y, rot.Z, rot.W);
                }
            }

            for (int i = 0; i < node.ChildCount; i++)
            {
                var childEntity = HandleNode(path, scene, node.Children[i], entities, materialPool);
                childEntity.Parent = entity;
            }

            return entity;
        }
        public static List<Entity> CreateEntitiesFromAsset(string path, string entityName = "")
        {
            var scene = LoadScene(path);
            if (!scene.HasMeshes)
            {
                Debug.Log("Cannot load asset at path " + path + ", no meshes found!", Debug.Flag.Error);
                return null;
            }

            List<Entity> entites = new List<Entity>();
            List<Shader> materials = new List<Shader>();

            //create unique shaders
            if (scene.HasMaterials)
            {
                foreach (var mat in scene.Materials)
                {
                    var shader = new Shader(Graphics.Instance.DefaultMaterial);
                    shader.Name = mat.Name;
                    materials.Add(shader);
                }
            }

            Entity parent = HandleNode(path, scene, scene.RootNode, entites, materials);
            if(entityName != "")
            {
                parent.Name = entityName;
            }
            return entites;
        }
        
        public struct TextureMatchingSettings
        {
            public string path;
            public string fileType;
            public string[] diffuseTextureSuffix;
            public string[] normalTextureSuffix;
            public string[] MAOSTextureSuffix;
        }
        public static void TryApplyingTextures(TextureMatchingSettings settings, List<Entity> entities)
        {
            Dictionary<string, Shader> materialMap = new Dictionary<string, Shader>();
            for (int i = 0; i < entities.Count; i++)
            {
                var render = entities[i].GetComponent<Renderer>();
                if(render == null)
                {
                    continue;
                }

                var mat = entities[i].GetComponent<Renderer>().Material;
                if (materialMap.ContainsKey(mat.Name))
                {
                    continue;
                }

                Debug.Log("Grabbed material from: " + entities[i].Name + ", mat: " + mat.Name);
                materialMap.Add(mat.Name, mat);
            }

            var textureFiles = Directory.GetFiles(settings.path);
            Dictionary<string, ImageTexture> textureMapping = new Dictionary<string, ImageTexture>();
            for (int i = 0; i < textureFiles.Length; i++)
            {
                var name = Path.GetFileName(textureFiles[i]);
                if(Path.GetExtension(name) != settings.fileType)
                {
                    continue;
                }
                var texture = ImageTexture.LoadTextureFromPath(textureFiles[i], true, StbImageSharp.ColorComponents.RedGreenBlue);
                textureMapping.Add(name, texture);
            }
            HashSet<ImageTexture> usedTextures = new HashSet<ImageTexture>();
            var l = new Jaccard();
            foreach (var materials in materialMap)
            {
                var materialName = materials.Key;
                ImageTexture diffuse = null;
                ImageTexture normal = null;
                ImageTexture maos = null;

                bool tryDiffuse = settings.diffuseTextureSuffix != null;
                bool tryNormal = settings.normalTextureSuffix != null;
                bool tryMaos = settings.MAOSTextureSuffix != null;
                double score0 = 0;
                double score1 = 0;
                double score2 = 0;

                foreach (var textures in textureMapping)
                {
                    string texName = textures.Key;
                    if (tryDiffuse)
                    {
                        for (int i = 0; i < settings.diffuseTextureSuffix.Length; i++)
                        {
                            var target = materialName + settings.diffuseTextureSuffix[i] + settings.fileType;
                            var s0 = l.Similarity(target, texName);
                            var suffix = texName.Split(settings.fileType)[0];
                            var hasCorrectSuffix = suffix.EndsWith(settings.diffuseTextureSuffix[i]);
                            if (s0 > score0 && hasCorrectSuffix)
                            {
                                diffuse = textures.Value;
                                score0 = s0;
                            }
                        }
                    }

                    if (tryNormal)
                    {
                        for (int i = 0; i < settings.normalTextureSuffix.Length; i++)
                        {
                            var target = materialName + settings.normalTextureSuffix[i] + settings.fileType;
                            var s1 = l.Similarity(target, texName);
                            var suffix = texName.Split(settings.fileType)[0];
                            var hasCorrectSuffix = settings.normalTextureSuffix[i].Trim().Length != 0 ? suffix.EndsWith(settings.normalTextureSuffix[i]) : true;
                            if (s1 > score1 && hasCorrectSuffix)
                            {
                                normal = textures.Value;
                                score1 = s1;
                            }
                        }
                    }

                    if (tryMaos)
                    {
                        for (int i = 0; i < settings.MAOSTextureSuffix.Length; i++)
                        {
                            var target = materialName + settings.MAOSTextureSuffix[i] + settings.fileType;
                            var s2 = l.Similarity(target, texName);
                            var suffix = texName.Split(settings.fileType)[0];
                            var hasCorrectSuffix = settings.MAOSTextureSuffix[i].Trim().Length != 0 ? suffix.EndsWith(settings.MAOSTextureSuffix[i]) : true;
                            if (s2 > score2 && hasCorrectSuffix)
                            {
                                maos = textures.Value;
                                score2 = s2;
                            }
                        }
                    }
                }

                if (diffuse != null)
                {
                    if(diffuse.GlTextureID == 0)
                    {
                        diffuse.pixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat.Rgb;
                        diffuse.internalPixelFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Srgb;
                        diffuse.ResolveTexture();
                    }
                    Debug.Log("Found diffuse texture for material: " + materialName + ", texture name: " + Path.GetFileName(diffuse.Name), Debug.Flag.Normal);
                    materials.Value.SetTexture(Shader.GetShaderPropertyId(DefaultMaterialUniforms.MainTexture), diffuse);
                    usedTextures.Add(diffuse);
                }
                else if(tryDiffuse)
                {
                    Debug.Log("Couldn't find diffuse texture for material: " + materialName, Debug.Flag.Warning);
                }
                if (normal != null)
                {
                    if (normal.GlTextureID == 0)
                    {
                        normal.pixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat.Rgb;
                        normal.internalPixelFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgb8;
                        normal.ResolveTexture();
                    }
                    Debug.Log("Found normal texture for material: " + materialName + ", texture name: " + Path.GetFileName(normal.Name), Debug.Flag.Normal);
                    materials.Value.SetTexture(Shader.GetShaderPropertyId(DefaultMaterialUniforms.NormalTexture), normal);
                    usedTextures.Add(normal);
                }
                else if(tryNormal)
                {
                    Debug.Log("Couldn't find normal texture for material: " + materialName, Debug.Flag.Warning);
                }
                if (maos != null)
                {
                    if (maos.GlTextureID == 0)
                    {
                        maos.pixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat.Rgb;
                        maos.internalPixelFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat.Rgb8;
                        maos.ResolveTexture();
                    }
                    Debug.Log("Found maos texture for material: " + materialName + ", texture name: " + Path.GetFileName(maos.Name), Debug.Flag.Normal);
                    materials.Value.SetTexture(Shader.GetShaderPropertyId(DefaultMaterialUniforms.MAOS), maos);
                    usedTextures.Add(maos);
                }
                else if (tryMaos)
                {
                    Debug.Log("Couldn't find maos texture for material: " + materialName, Debug.Flag.Warning);
                }
            }

            foreach (var textures in textureMapping)
            {
                if (!usedTextures.Contains(textures.Value)){
                    textures.Value.Dispose();
                }
            }

            return;
        }

        public static GlMeshData GenerateGLMeshData(Assimp.Mesh mesh)
        {
            GlMeshData data = new();

            List<float> vertexData = new List<float>();
            List<int> indices = new List<int>();

            for (int j = 0; j < mesh.VertexCount; j++)
            {
                vertexData.Add(mesh.Vertices[j].X);
                vertexData.Add(mesh.Vertices[j].Y);
                vertexData.Add(mesh.Vertices[j].Z);


                if (mesh.HasNormals)
                {
                    vertexData.Add(mesh.Normals[j].X);
                    vertexData.Add(mesh.Normals[j].Y);
                    vertexData.Add(mesh.Normals[j].Z);
                }
                else
                {
                    vertexData.Add(0);
                    vertexData.Add(1);
                    vertexData.Add(0);
                }

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

            return data;
        }
    }

}
