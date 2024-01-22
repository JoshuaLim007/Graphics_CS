using OpenTK.Compute.OpenCL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.IO;
using System.Xml.Linq;
using System.ComponentModel.Design.Serialization;
using JLUtility;
using ObjLoader.Loader.Data.VertexData;

namespace JLGraphics
{
    internal sealed class ShaderFile : FileObject, IDisposable
    {
        internal ShaderType ShaderType { get; }
        public static implicit operator int(ShaderFile d) => d.compiledShader;
        
        int compiledShader;
        string path;
        internal ShaderFile(string path, ShaderType shaderType) : base(path)
        {
            this.path = path;
            ShaderType = shaderType;
            if (!File.Exists(path))
            {
                Console.WriteLine("Shader not found: " + path);
                return;
            }
            //read the shader datas
            compiledShader = GL.CreateShader(ShaderType);
            FileChangeCallback.Add(()=> {
                GL.DeleteShader(compiledShader);
                compiledShader = GL.CreateShader(shaderType);
                CompileShader();
            });
        }
        internal void CompileShader() { 
            Console.WriteLine("Compiling Shader: " + path);
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
        }
        public void Dispose()
        {
            GL.DeleteShader(compiledShader);
        }

    }
    public sealed class ShaderProgram : IDisposable
    {
        internal bool Disposed { get; private set; } = false;
        internal ShaderFile Frag { get; }
        internal ShaderFile Vert { get; }

        public FileObject FragFile => Frag;
        public FileObject VertFile => Vert;

        string Name { get; }
        public int Id { get; private set; } = 0;
        public static implicit operator int(ShaderProgram d) => d.Id;
        static List<ShaderProgram> shaderPrograms = new List<ShaderProgram>();
        internal Action OnShaderReload { get; set; } = null;
        internal Action OnDispose { get; set; } = null;
        void OnFragFileChangeShaderRecompile()
        {
            GL.DeleteProgram(Id);
            Id = GL.CreateProgram();
            //recompile vert shader
            Vert.CompileShader();
            UpdateProgram();
            OnShaderReload?.Invoke();
        }
        void OnVertFileChangeShaderRecompile()
        {
            GL.DeleteProgram(Id);
            Id = GL.CreateProgram();
            //recompile frag shader
            Frag.CompileShader();
            UpdateProgram();
            OnShaderReload?.Invoke();
        }
        public ShaderProgram(string name, string fragPath, string vertPath)
        {
            Name = name;
            Vert = new ShaderFile(vertPath, ShaderType.VertexShader);
            Frag = new ShaderFile(fragPath, ShaderType.FragmentShader);
            Vert.FileChangeCallback.Add(OnVertFileChangeShaderRecompile);
            Frag.FileChangeCallback.Add(OnFragFileChangeShaderRecompile);
            Id = GL.CreateProgram();
            shaderPrograms.Add(this);
        }
        public ShaderProgram(ShaderProgram shaderProgram)
        {
            Name = shaderProgram.Name;
            Vert = new ShaderFile(shaderProgram.VertFile.Path, ShaderType.VertexShader);
            Frag = new ShaderFile(shaderProgram.FragFile.Path, ShaderType.FragmentShader);
            Vert.FileChangeCallback.Add(OnVertFileChangeShaderRecompile);
            Frag.FileChangeCallback.Add(OnFragFileChangeShaderRecompile);
            Id = GL.CreateProgram();
            shaderPrograms.Add(this);
        }
        public ShaderProgram FindShaderProgram(string name)
        {
            for (int i = 0; i < shaderPrograms.Count; i++)
            {
                if (shaderPrograms[i].Name == name)
                {
                    return this;
                }
            }
            return null;
        }
        public void CompileProgram()
        {
            Frag.CompileShader();
            Vert.CompileShader();
            UpdateProgram();
            //UpdateProgram called via callback
        }
        void UpdateProgram()
        {
            Console.WriteLine("\tLinking shader to program " + Id + ", " + Name);
            //attach shaders
            GL.AttachShader(Id, Frag);
            GL.AttachShader(Id, Vert);

            //link to program
            GL.LinkProgram(Id);

            //detach shaders
            GL.DetachShader(Id, Frag);
            GL.DetachShader(Id, Vert);

            var d = GL.GetProgramInfoLog(Id);
            if (d != "")
                Console.WriteLine(d);
        }
        public void Dispose()
        {
            OnDispose.Invoke();
            Disposed = true;
            Frag.Dispose();
            Vert.Dispose();
            GL.DeleteProgram(Id);
            shaderPrograms.Remove(this);
        }
    }
    public sealed class Shader : IName
    {
        public ShaderProgram Program { get; private set; }
        public string Name { get; set; }

        private int textureMask = 0;
        private int nullTextureMask = 0;
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
            GL.UseProgram(Program);

            textures[textureIndex] = texture;
            textureUniformNames[textureIndex] = uniformName;

            if (texture != null)
            {
                textureMask |= 1 << textureIndex;
            }
            else
            {
                textureMask &= ~(1 << textureIndex);
                nullTextureMask |= 1 << textureIndex;
            }
            var texLoc = GL.GetUniformLocation(Program, uniformName);
            GL.Uniform1(texLoc, textureIndex);

            GL.UseProgram(previousProgram);
        }
        public void SetTexture(int textureIndex, string uniformName, int texturePtr)
        {
            var previousProgram = GL.GetInteger(GetPName.CurrentProgram);
            GL.UseProgram(Program);

            textures[textureIndex] = (Texture)texturePtr;
            textureUniformNames[textureIndex] = uniformName;

            if (texturePtr != 0)
            {
                textureMask |= 1 << textureIndex;
            }
            else
            {
                textureMask &= ~(1 << textureIndex);
                nullTextureMask |= 1 << textureIndex;
            }
            var texLoc = GL.GetUniformLocation(Program, uniformName);
            GL.Uniform1(texLoc, textureIndex);

            GL.UseProgram(previousProgram);
        }

        WeakReference<Shader> myWeakRef;
        static List<WeakReference<Shader>> AllInstancedShaders = new List<WeakReference<Shader>>();
        
        void BindAllTextures()
        {
            for (int i = 0; i < TotalTextures; i++)
            {
                if (textures[i] != null)
                {
                    SetTexture(i, textureUniformNames[i], textures[i]);
                }
            }
        }
        void CopyUniforms(Shader other)
        {
            m_uniformValues = new List<UniformValue>(other.m_uniformValues);
            Array.Copy(other.textures, textures, TotalTextures);
            Array.Copy(other.textureUniformNames, textureUniformNames, TotalTextures);
            BindAllTextures();
            textureMask = other.textureMask;
        }
        public Shader(Shader shader)
        {
            Name = shader.Name + "_clone";
            Program = shader.Program;
            CopyUniforms(shader);
            myWeakRef = new WeakReference<Shader>(this);
            AllInstancedShaders.Add(myWeakRef);
            init();
        }

        public Shader(string name, ShaderProgram shaderProgram) 
        {
            Name = name;
            Program = shaderProgram;
            myWeakRef = new WeakReference<Shader>(this);
            AllInstancedShaders.Add(myWeakRef);
            init();
        }
        void ShaderReload()
        {
            Console.WriteLine("\tShader Reload.. Rebinding Textures, Setting uniforms for program " + Program.Id + " for material " + Name);
            UseProgram();
            BindAllTextures();
            UpdateUniforms();
            Unbind();
            return;
        }
        void ShaderProgramDispose()
        {
            //if the shader program gets disposed, clone it and own the program
            Program = new ShaderProgram(Program);
            UseProgram();
            BindAllTextures();
            UpdateUniforms();
            Unbind();
            return;
        }
        void init()
        {
            Program.OnShaderReload += ShaderReload;
            Program.OnDispose += ShaderProgramDispose;
        }
        ~Shader()
        {
            Program.OnDispose -= ShaderProgramDispose;
            Program.OnShaderReload -= ShaderReload;
            AllInstancedShaders.Remove(myWeakRef);
            myWeakRef = null;
        }
        /// <summary>
        /// Expensive, try to batch this with other meshes with same materials.
        /// Applies material unique uniforms.
        /// </summary>
        internal void UseProgram()
        {
            GL.UseProgram(Program);
        }
        bool IntToBool(int val, int index)
        {
            return ((val >> index) & 1) == 1;
        }
        internal void UpdateUniforms()
        {
            //apply all material specific uniforms
            for (int i = 0; i < TotalTextures; i++)
            {
                if (IntToBool(textureMask, i))
                {
                    GL.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + i));
                    GL.BindTexture(TextureTarget.Texture2D, textures[i].GlTextureID);
                }
                else if(IntToBool(nullTextureMask, i))
                {
                    GL.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + i));
                    GL.BindTexture(TextureTarget.Texture2D, 0);
                    GL.Disable(EnableCap.Texture2D);
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
            for (int i = 0; i < AllInstancedShaders.Count; i++)
            {
                if (!AllInstancedShaders[i].TryGetTarget(out var shader))
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
            for (int i = 0; i < AllInstancedShaders.Count; i++)
            {
                if (!AllInstancedShaders[i].TryGetTarget(out var shader))
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
            for (int i = 0; i < AllInstancedShaders.Count; i++)
            {
                if (!AllInstancedShaders[i].TryGetTarget(out var shader))
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
            for (int i = 0; i < AllInstancedShaders.Count; i++)
            {
                if (!AllInstancedShaders[i].TryGetTarget(out var shader))
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
            for (int i = 0; i < AllInstancedShaders.Count; i++)
            {
                if (!AllInstancedShaders[i].TryGetTarget(out var shader))
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
            for (int i = 0; i < AllInstancedShaders.Count; i++)
            {
                if (!AllInstancedShaders[i].TryGetTarget(out var shader))
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
                int loc = GL.GetUniformLocation(Program,id);
                m_cachedUniformLocations.Add(id, loc);
                return loc;
            }
        }
    }
}
