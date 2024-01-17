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
            Name = name;
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
        const int TotalTextures = 32;
        private Texture[] textures = new Texture[TotalTextures];
        private string[] textureUniformNames = new string[TotalTextures];

        public Texture GetTexture(int textureIndex)
        {
            return textures[textureIndex];
        }
        public void SetTexture(int textureIndex, string uniformName, Texture texture)
        {
            var previousProgram = GL.GetInteger(GetPName.CurrentProgram);
            GL.UseProgram(ProgramId);

            textures[textureIndex] = texture;
            textureUniformNames[textureIndex] = uniformName;

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
            textureUniformNames[textureIndex] = uniformName;

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

        WeakReference<Shader> myWeakRef;
        static List<WeakReference<Shader>> AllShaders = new List<WeakReference<Shader>>();
        
        int CreateProgramID()
        {
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
        void CopyUniforms(Shader other)
        {
            m_uniformValues = new List<UniformValue>(other.m_uniformValues);
            Array.Copy(other.textures, textures, TotalTextures);
            Array.Copy(other.textureUniformNames, textureUniformNames, TotalTextures);
            for (int i = 0; i < TotalTextures; i++)
            {
                if (textures[i] != null)
                {
                    SetTexture(i, textureUniformNames[i], textures[i]);
                }
            }
            textureMask = other.textureMask;
        }
        public Shader(Shader shader)
        {
            FragShaderFile = shader.FragShaderFile;
            VertShaderFile = shader.VertShaderFile;

            Name = shader.Name + "_clone";
            ProgramId = shader.ProgramId;
            CopyUniforms(shader);
            myWeakRef = new WeakReference<Shader>(this);
            AllShaders.Add(myWeakRef);
        }

        public Shader(string name, ShaderFile fragmentShader, ShaderFile vertexShader) 
        {
            this.Name = name;
            FragShaderFile = fragmentShader;
            VertShaderFile = vertexShader;
            ProgramId = CreateProgramID();
            myWeakRef = new WeakReference<Shader>(this);
            AllShaders.Add(myWeakRef);
        }
        ~Shader()
        {
            AllShaders.Remove(myWeakRef);
            myWeakRef = null;
            GL.DeleteShader(ProgramId);
        }
        public static Shader FindShaderInstance(string name)
        {
            for (int i = 0; i < AllShaders.Count; i++)
            {
                if(!AllShaders[i].TryGetTarget(out var shader))
                {
                    continue;
                }
                if (shader.Name != name)
                {
                    continue;
                }
                return shader;
            }
            return null;
        }

        /// <summary>
        /// Expensive, try to batch this with other meshes with same materials.
        /// Applies material unique uniforms.
        /// </summary>
        internal void UseProgram()
        {
            GL.UseProgram(ProgramId);
        }
        internal void UpdateUniforms()
        {
            //apply all material specific uniforms
            for (int i = 0; i < 32; i++)
            {
                if (((textureMask >> i) & 1) == 1)
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
        internal static void Unbind()
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
        private List<UniformValue> m_uniformValues = new List<UniformValue>();

        public static void SetGlobalMat4(string id, Matrix4 matrix4)
        {
            for (int i = 0; i < AllShaders.Count; i++)
            {
                if (!AllShaders[i].TryGetTarget(out var shader))
                {
                    continue;
                }
                shader.SetMat4(id, matrix4);
                //GL.UseProgram(shader.ProgramId);
                //GL.UniformMatrix4(shader.GetUniformLocation(id), false, ref matrix4);
            }
        }
        public static void SetGlobalVector4(string id, Vector4 value)
        {
            for (int i = 0; i < AllShaders.Count; i++)
            {
                if (!AllShaders[i].TryGetTarget(out var shader))
                {
                    continue;
                }
                shader.SetVector4(id, value);
                //GL.UseProgram(shader.ProgramId);
                //GL.Uniform4(shader.GetUniformLocation(id), value);
            }
        }
        public static void SetGlobalVector3(string id, Vector3 value)
        {
            for (int i = 0; i < AllShaders.Count; i++)
            {
                if (!AllShaders[i].TryGetTarget(out var shader))
                {
                    continue;
                }
                shader.SetVector3(id, value);
                //GL.UseProgram(shader.ProgramId);
                //GL.Uniform3(shader.GetUniformLocation(id), value);
            }
        }

        public static void SetGlobalVector2(string id, Vector2 value)
        {
            for (int i = 0; i < AllShaders.Count; i++)
            {
                if (!AllShaders[i].TryGetTarget(out var shader))
                {
                    continue;
                }
                shader.SetVector2(id, value);
                //GL.UseProgram(shader.ProgramId);
                //GL.Uniform2(shader.GetUniformLocation(id), value);
            }
        }
        public static void SetGlobalFloat(string id, float value)
        {
            for (int i = 0; i < AllShaders.Count; i++)
            {
                if (!AllShaders[i].TryGetTarget(out var shader))
                {
                    continue;
                }
                shader.SetFloat(id, value);
                //GL.UseProgram(shader.ProgramId);
                //GL.Uniform1(shader.GetUniformLocation(id), value);
            }
        }
        public static void SetGlobalInt(string id, int value)
        {
            for (int i = 0; i < AllShaders.Count; i++)
            {
                if (!AllShaders[i].TryGetTarget(out var shader))
                {
                    continue;
                }
                shader.SetInt(id, value);
                //GL.UseProgram(shader.ProgramId);
                //GL.Uniform1(shader.GetUniformLocation(id), value);
            }
        }
        public static void SetGlobalBool(string id, bool value)
        {
            SetGlobalInt(id, value ? 1 : 0);
        }


        private void mAddUniform(in UniformValue uniformValue)
        {
            //for (int i = 0; i < m_uniformValues.Count; i++)
            //{
            //    if (m_uniformValues[i].id == uniformValue.id)
            //    {
            //        m_uniformValues[i] = uniformValue;
            //        return;
            //    }
            //}
            //m_uniformValues.Add(uniformValue);

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
