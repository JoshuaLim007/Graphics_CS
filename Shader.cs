using OpenTK.Compute.OpenCL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.IO;
using System.Xml.Linq;
using System.ComponentModel.Design.Serialization;
using JLUtility;
using ObjLoader.Loader.Data.VertexData;
using Assimp.Unmanaged;
using System;

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

        public Texture GetTexture(string uniformName)
        {
            int textureIndex = findShaderIndex(uniformName);
            if(textureIndex == -1)
            {
                return null;
            }
            return textures[textureIndex];
        }
        Stack<int> availableTextureSlots = new Stack<int>();
        int findShaderIndex(string uniformName)
        {
            for (int i = 0; i < TotalTextures; i++)
            {
                if (textureUniformNames[i] == uniformName)
                {
                    return i;
                }
            }
            return -1;
        }
        void set_int_bool(int index, bool value, ref int number)
        {
            if (value)
            {
                number |= 1 << index;
            }
            else
            {
                number &= ~(1 << index);
            }
        }
        public void SetTexture(string uniformName, Texture texture)
        {
            var previousProgram = GL.GetInteger(GetPName.CurrentProgram);
            GL.UseProgram(Program);

            int textureIndex = findShaderIndex(uniformName);
            if (textureIndex == -1) { 
                if(texture == null)
                {
                    return;
                }
                if (availableTextureSlots.Count == 0)
                {
                    Console.WriteLine("ERROR::Cannot add more textures to shader material: " + Name);
                    return;
                }
                textureIndex = availableTextureSlots.Pop();
                textureUniformNames[textureIndex] = uniformName;
            }

            textures[textureIndex] = texture;
            if (texture != null)
            {
                var texLoc = GL.GetUniformLocation(Program, uniformName);
                GL.Uniform1(texLoc, textureIndex);
                set_int_bool(textureIndex, true, ref textureMask);
            }
            else
            {
                textureUniformNames[textureIndex] = "";
                set_int_bool(textureIndex, false, ref textureMask);
                set_int_bool(textureIndex, true, ref nullTextureMask);
                availableTextureSlots.Push(textureIndex);
            }
            GL.UseProgram(previousProgram);
        }
        public void SetTexture(string uniformName, int texturePtr)
        {
            if(texturePtr == 0)
            {
                SetTexture(uniformName, null);
            }
            else
            {
                SetTexture(uniformName, (Texture)texturePtr);
            }
        }

        WeakReference<Shader> myWeakRef;
        static List<WeakReference<Shader>> AllInstancedShaders = new List<WeakReference<Shader>>();
        
        //global uniform caches
        public struct GlobalUniformValue
        {
            public enum GlobalUniformType
            {
                texture,
                vec3,
                vec4,
                vec2,
                Float,
                Int,
                mat4
            }
            public string uniformName;
            public object value;
            public GlobalUniformType uniformType;
            public GlobalUniformValue(string uniformName, GlobalUniformType uniformType, object value)
            {
                this.uniformType = uniformType;
                this.uniformName = uniformName;
                this.value = value;
            }
        }
        static List<GlobalUniformValue> GlobalUniforms = new List<GlobalUniformValue>();
        public static int FindGlobalUniformIndex(string uniformName)
        {
            var d = GlobalUniforms.FindIndex((p) => { return p.uniformName == uniformName; });
            return d;
        }
        public static GlobalUniformValue GetGlobalUniformValue(int index)
        {
            return GlobalUniforms[index];
        }
        public static void SetGlobalUniformValue(GlobalUniformValue.GlobalUniformType globalUniformType, object value, string uniformName)
        {
            var index = FindGlobalUniformIndex(uniformName);
            if (index == -1)
                GlobalUniforms.Add(new GlobalUniformValue(uniformName, globalUniformType, value));
            else
            {
                var val = GlobalUniforms[index];
                val.value = value;
                GlobalUniforms[index] = val;
            }
        }
        public static void RemoveGlobalUniformValue(string uniformName)
        {
            int id = FindGlobalUniformIndex(uniformName);
            if(id != -1)
            {
                GlobalUniforms.RemoveAt(id);
            }
        }
        public static GlobalUniformValue[] GetCopyOfGlobalUniforms()
        {
            var t = new GlobalUniformValue[GlobalUniforms.Count];
            GlobalUniforms.CopyTo(t);
            return t;
        } 

        void BindAllTextures()
        {
            for (int i = 0; i < TotalTextures; i++)
            {
                if (textures[i] != null)
                {
                    SetTexture(textureUniformNames[i], textures[i]);
                }
            }
        }
        void CopyUniforms(Shader other)
        {
            m_uniformValues = new List<UniformValue>(other.m_uniformValues);
            availableTextureSlots = new Stack<int>(other.availableTextureSlots);
            Array.Copy(other.textures, textures, TotalTextures);
            Array.Copy(other.textureUniformNames, textureUniformNames, TotalTextures);
            textureMask = other.textureMask;
            BindAllTextures();
        }
        public Shader(Shader shader)
        {
            Name = shader.Name + "_clone";
            Program = shader.Program;
            CopyUniforms(shader);   //this already contains global uniforms
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
            fetchAllGlobalUniforms();   //get all previous global values before the shader was created
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
            ShaderReload();
            return;
        }
        void init()
        {
            for (int i = 0; i < TotalTextures; i++)
            {
                availableTextureSlots.Push(i);
            }
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

        public static void SetGlobalTexture(string id, Texture texture)
        {
            SetGlobalUniformValue(GlobalUniformValue.GlobalUniformType.texture, texture, id);

            for (int i = 0; i < AllInstancedShaders.Count; i++)
            {
                if (!AllInstancedShaders[i].TryGetTarget(out var shader))
                {
                    continue;
                }
                shader.SetTexture(id, texture);
            }
        }
        public static void SetGlobalMat4(string id, Matrix4 matrix4)
        {
            SetGlobalUniformValue(GlobalUniformValue.GlobalUniformType.mat4, matrix4, id);

            for (int i = 0; i < AllInstancedShaders.Count; i++)
            {
                if (!AllInstancedShaders[i].TryGetTarget(out var shader))
                {
                    continue;
                }
                shader.SetMat4(id, matrix4);
            }
        }
        public static void SetGlobalVector4(string id, Vector4 value)
        {
            SetGlobalUniformValue(GlobalUniformValue.GlobalUniformType.vec4, value, id);

            for (int i = 0; i < AllInstancedShaders.Count; i++)
            {
                if (!AllInstancedShaders[i].TryGetTarget(out var shader))
                {
                    continue;
                }
                shader.SetVector4(id, value);
            }
        }
        public static void SetGlobalVector3(string id, Vector3 value)
        {
            SetGlobalUniformValue(GlobalUniformValue.GlobalUniformType.vec3, value, id);

            for (int i = 0; i < AllInstancedShaders.Count; i++)
            {
                if (!AllInstancedShaders[i].TryGetTarget(out var shader))
                {
                    continue;
                }
                shader.SetVector3(id, value);
            }
        }

        public static void SetGlobalVector2(string id, Vector2 value)
        {
            SetGlobalUniformValue(GlobalUniformValue.GlobalUniformType.vec2, value, id);

            for (int i = 0; i < AllInstancedShaders.Count; i++)
            {
                if (!AllInstancedShaders[i].TryGetTarget(out var shader))
                {
                    continue;
                }
                shader.SetVector2(id, value);
            }
        }
        public static void SetGlobalFloat(string id, float value)
        {
            SetGlobalUniformValue(GlobalUniformValue.GlobalUniformType.Float, value, id);

            for (int i = 0; i < AllInstancedShaders.Count; i++)
            {
                if (!AllInstancedShaders[i].TryGetTarget(out var shader))
                {
                    continue;
                }
                shader.SetFloat(id, value);
            }
        }
        public static void SetGlobalInt(string id, int value)
        {
            SetGlobalUniformValue(GlobalUniformValue.GlobalUniformType.Int, value, id);

            for (int i = 0; i < AllInstancedShaders.Count; i++)
            {
                if (!AllInstancedShaders[i].TryGetTarget(out var shader))
                {
                    continue;
                }
                shader.SetInt(id, value);
            }
        }
        public static void SetGlobalBool(string id, bool value)
        {
            SetGlobalInt(id, value ? 1 : 0);
        }

        private void fetchAllGlobalUniforms()
        {
            for (int i = 0; i < GlobalUniforms.Count; i++)
            {
                var cur = GlobalUniforms[i];
                switch (cur.uniformType)
                {
                    case GlobalUniformValue.GlobalUniformType.texture:
                        SetTexture(cur.uniformName, (Texture)cur.value);
                        break;
                    case GlobalUniformValue.GlobalUniformType.vec3:
                        SetVector3(cur.uniformName, (Vector3)cur.value);
                        break;
                    case GlobalUniformValue.GlobalUniformType.vec4:
                        SetVector4(cur.uniformName, (Vector4)cur.value);
                        break;
                    case GlobalUniformValue.GlobalUniformType.vec2:
                        SetVector3(cur.uniformName, (Vector3)cur.value);
                        break;
                    case GlobalUniformValue.GlobalUniformType.Float:
                        SetFloat(cur.uniformName, (float)cur.value);
                        break;
                    case GlobalUniformValue.GlobalUniformType.Int:
                        SetInt(cur.uniformName, (int)cur.value);
                        break;
                    case GlobalUniformValue.GlobalUniformType.mat4:
                        SetMat4(cur.uniformName, (Matrix4)cur.value);
                        break;
                    default:
                        break;
                }
            }
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
