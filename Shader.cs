using Assimp;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.IO;

namespace JLGraphics
{
    public sealed class ShaderFile
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public ShaderType ShaderType { get; }
        public static implicit operator int(ShaderFile d) => d.compiledShader;
        
        int compiledShader;
        public ShaderFile(string name, string path, ShaderType shaderType)
        {
            Path = path;
            ShaderType = shaderType;
            if (!File.Exists(path))
            {
                Console.WriteLine("Shader not found: " + path);
                return;
            }
            //read the shader datas
            compiledShader = GL.CreateShader(ShaderType);
        }
        public void CompileShader() { 
        
            if (!File.Exists(Path))
            {
                Console.WriteLine("Shader not found: " + Path);
                return;
            }
            GL.ShaderSource(compiledShader, File.ReadAllText(Path));

            //compile the shaders
            GL.CompileShader(compiledShader);

            string d = GL.GetShaderInfoLog(compiledShader);
            if (d != "")
            {
                Console.WriteLine(d);
                return;
            }
            for (int i = 0; i < OnCompileShader.Count; i++)
            {
                OnCompileShader[i].Invoke();
            }
        }
        public List<Action> OnCompileShader { get; private set; } = new List<Action>();
        ~ShaderFile()
        {
            GL.DeleteShader(compiledShader);
        }
    }
    public sealed class Shader
    {
        public string Name { get; }
        public int ProgramId { get; }
        
        private ShaderFile FragShaderFile { get; }
        private ShaderFile VertShaderFile { get; }

        private int textureMask = 0;
        private Texture[] textures = new Texture[32];

        public Texture GetTexture(int textureIndex)
        {
            return textures[textureIndex];
        }
        public void SetTexture(int textureIndex, string uniformName, Texture texture)
        {
            var previousProgram = GL.GetInteger(GetPName.CurrentProgram);
            GL.UseProgram(ProgramId);

            textures[textureIndex] = texture;
            
            if (texture != null)
            {
                textureMask |= 1 << textureIndex;
            }
            else
            {
                textureMask &= ~(1 << textureIndex);
            }

            var texLoc = GL.GetUniformLocation(ProgramId, uniformName);
            GL.Uniform1(texLoc, textureIndex);

            GL.UseProgram(previousProgram);
        }
        public void SetTexture(int textureIndex, string uniformName, int texturePtr)
        {
            var previousProgram = GL.GetInteger(GetPName.CurrentProgram);
            GL.UseProgram(ProgramId);

            textures[textureIndex] = (Texture)texturePtr;

            if (texturePtr != 0)
            {
                textureMask |= 1 << textureIndex;
            }
            else
            {
                textureMask &= ~(1 << textureIndex);
            }

            var texLoc = GL.GetUniformLocation(ProgramId, uniformName);
            GL.Uniform1(texLoc, textureIndex);

            GL.UseProgram(previousProgram);
        }

        static readonly List<Shader> m_shaderInstances = new List<Shader>();
        
        int CreateProgramID()
        {
            //int vshader = GL.CreateShader(ShaderType.VertexShader);
            //int fshader = GL.CreateShader(ShaderType.FragmentShader);
            //if (!File.Exists(fragmentShader))
            //{
            //    Console.WriteLine("Fragment Shader not found: " + fragmentShader);
            //    return 0;
            //}
            //if (!File.Exists(vertexShader))
            //{
            //    Console.WriteLine("Vertex Shader not found: " + vertexShader);
            //    return 0;
            //}

            ////read the shader datas
            //GL.ShaderSource(vshader, File.ReadAllText(vertexShader));
            //GL.ShaderSource(fshader, File.ReadAllText(fragmentShader));

            //compile the shaders
            //GL.CompileShader(vshader);
            //GL.CompileShader(fshader);

            //string d = GL.GetShaderInfoLog(vshader);
            //if (d != "")
            //    Console.WriteLine(d);
            //d = GL.GetShaderInfoLog(fshader);
            //if (d != "")
            //    Console.WriteLine(d);

            int program = GL.CreateProgram();

            //attach shaders
            GL.AttachShader(program, VertShaderFile);
            GL.AttachShader(program, FragShaderFile);

            //link to program
            GL.LinkProgram(program);

            //detach shaders
            GL.DetachShader(program, VertShaderFile);
            GL.DetachShader(program, FragShaderFile);

            var d = GL.GetProgramInfoLog(program);
            if (d != "")
                Console.WriteLine(d);

            return program;
        }
        public Shader(Shader shader)
        {
            FragShaderFile = shader.FragShaderFile;
            VertShaderFile = shader.VertShaderFile;

            Name = shader.Name + "_clone";
            ProgramId = CreateProgramID();
            m_shaderInstances.Add(this);
        }

        public Shader(string name, ShaderFile fragmentShader, ShaderFile vertexShader)
        {
            FragShaderFile = fragmentShader;
            VertShaderFile = vertexShader;
            Name = name;
            ProgramId = CreateProgramID();
            m_shaderInstances.Add(this);
        }
        ~Shader()
        {
            m_shaderInstances.Remove(this);
            GL.DeleteShader(ProgramId);
        }
        public static Shader FindShader(string name)
        {
            for (int i = 0; i < m_shaderInstances.Count; i++)
            {
                if(m_shaderInstances[i].Name != name)
                {
                    continue;
                }
                return m_shaderInstances[i];
            }
            return null;
        }

        /// <summary>
        /// Expensive, try to batch this with other meshes with same materials.
        /// Applies material unique uniforms.
        /// </summary>
        public void UseProgram()
        {
            GL.UseProgram(ProgramId);

            //apply all material specific uniforms
            for (int i = 0; i < 32; i++)
            {
                if(((textureMask >> i) & 1) == 1)
                {
                    GL.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + i));
                    GL.BindTexture(TextureTarget.Texture2D, textures[i].GlTextureID);
                }
            }

            SetInt("textureMask", textureMask);

            for (int i = 0; i < m_uniformValues.Count; i++)
            {
                var type = m_uniformValues[i].value.GetType();
                if (type == typeof(float))
                {
                    var val = (float)m_uniformValues[i].value;
                    GL.Uniform1(m_uniformValues[i].uniformLocation, val);
                }
                else if (type == typeof(int))
                {
                    var val = (int)m_uniformValues[i].value;
                    GL.Uniform1(m_uniformValues[i].uniformLocation, val);
                }
                else if (type == typeof(Vector2))
                {
                    var val = (Vector2)m_uniformValues[i].value;
                    GL.Uniform2(m_uniformValues[i].uniformLocation, val);
                }
                else if (type == typeof(Vector3))
                {
                    var val = (Vector3)m_uniformValues[i].value;
                    GL.Uniform3(m_uniformValues[i].uniformLocation, val);
                }
                else if (type == typeof(Vector4))
                {
                    var val = (Vector4)m_uniformValues[i].value;
                    GL.Uniform4(m_uniformValues[i].uniformLocation, val);
                }
                if (type == typeof(Matrix4))
                {
                    var val = (Matrix4)m_uniformValues[i].value;
                    GL.UniformMatrix4(m_uniformValues[i].uniformLocation, false, ref val);
                }
            }
        }
        public static void Unbind()
        {
            GL.UseProgram(0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
        public struct UniformValue
        {
            public string id;
            public int uniformLocation;
            public object value;
            public UniformValue(string id, int location, object value)
            {
                this.id = id;
                this.uniformLocation = location;
                this.value = value;
            }
        }


        private Dictionary<string, int> m_cachedUniformValueIndex = new Dictionary<string, int>();
        private readonly List<UniformValue> m_uniformValues = new List<UniformValue>();

        public static void SetGlobalMat4(string id, Matrix4 matrix4)
        {
            for (int i = 0; i < m_shaderInstances.Count; i++)
            {
                GL.UseProgram(m_shaderInstances[i].ProgramId);
                GL.UniformMatrix4(m_shaderInstances[i].GetUniformLocation(id), false, ref matrix4);
            }
        }
        public static void SetGlobalVector4(string id, Vector4 value)
        {
            for (int i = 0; i < m_shaderInstances.Count; i++)
            {
                GL.UseProgram(m_shaderInstances[i].ProgramId);
                GL.Uniform4(m_shaderInstances[i].GetUniformLocation(id), value);
            }
        }
        public static void SetGlobalVector4(string id, Vector4[] value)
        {
            float[] elements = new float[value.Length * 4];
            for (int i = 0; i < m_shaderInstances.Count; i++)
            {
                GL.UseProgram(m_shaderInstances[i].ProgramId);
                GL.Uniform4(m_shaderInstances[i].GetUniformLocation(id), elements.Length, elements);
            }
        }
        public static void SetGlobalVector3(string id, Vector3 value)
        {
            for (int i = 0; i < m_shaderInstances.Count; i++)
            {
                GL.UseProgram(m_shaderInstances[i].ProgramId);
                GL.Uniform3(m_shaderInstances[i].GetUniformLocation(id), value);
            }
        }

        public static void SetGlobalVector2(string id, Vector2 value)
        {
            for (int i = 0; i < m_shaderInstances.Count; i++)
            {
                GL.UseProgram(m_shaderInstances[i].ProgramId);
                GL.Uniform2(m_shaderInstances[i].GetUniformLocation(id), value);
            }
        }
        public static void SetGlobalFloat(string id, float value)
        {
            for (int i = 0; i < m_shaderInstances.Count; i++)
            {
                GL.UseProgram(m_shaderInstances[i].ProgramId);
                GL.Uniform1(m_shaderInstances[i].GetUniformLocation(id), value);
            }
        }
        public static void SetGlobalInt(string id, int value)
        {
            for (int i = 0; i < m_shaderInstances.Count; i++)
            {
                GL.UseProgram(m_shaderInstances[i].ProgramId);
                GL.Uniform1(m_shaderInstances[i].GetUniformLocation(id), value);
            }
        }
        public static void SetGlobalBool(string id, bool value)
        {
            SetGlobalInt(id, value ? 1 : 0);
        }


        private void mAddUniform(in UniformValue uniformValue)
        {
            if (m_cachedUniformValueIndex.ContainsKey(uniformValue.id))
            {
                m_uniformValues[m_cachedUniformValueIndex[uniformValue.id]] = uniformValue;
            }
            else
            {
                m_cachedUniformValueIndex.Add(uniformValue.id, m_uniformValues.Count);
                m_uniformValues.Add(uniformValue);
            }
        }

        public void SetUniformValue(UniformValue uniformValue)
        {
            mAddUniform(uniformValue);
        }
        public T GetUniformValue<T>(string id)
        {
            if (m_cachedUniformValueIndex.ContainsKey(id))
            {
                return (T)m_uniformValues[m_cachedUniformValueIndex[id]].value;
            }
            return default(T);
        }

        public void SetMat4(string id, Matrix4 value)
        {
            mAddUniform(new UniformValue(id, GetUniformLocation(id), value));
        }
        public void SetFloat(string id, float value)
        {
            mAddUniform(new UniformValue(id, GetUniformLocation(id), value));
        }
        public void SetInt(string id, int value)
        {
            mAddUniform(new UniformValue(id, GetUniformLocation(id), value));
        }
        public void SetBool(string id, bool value)
        {
            SetInt(id, value ? 1 : 0);
        }
        public void SetVector4(string id, Vector4 value)
        {
            mAddUniform(new UniformValue(id, GetUniformLocation(id), value));

        }
        public void SetVector3(string id, Vector3 value)
        {
            mAddUniform(new UniformValue(id, GetUniformLocation(id), value));

        }
        public void SetVector2(string id, Vector2 value)
        {
            mAddUniform(new UniformValue(id, GetUniformLocation(id), value));
        }


        private Dictionary<string, int> m_cachedUniformLocations = new Dictionary<string, int>();

        public int GetUniformLocation(string id)
        {
            if (m_cachedUniformLocations.ContainsKey(id))
            {
                return m_cachedUniformLocations[id];
            }
            else
            {
                int loc = GL.GetUniformLocation(ProgramId,id);
                m_cachedUniformLocations.Add(id, loc);
                return loc;
            }
        }

    }
}
