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
            uniformLocations.Clear();
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

        Dictionary<string, int> uniformLocations = new Dictionary<string, int>();
        public int GetUniformLocation(string id)
        {
            if (uniformLocations.ContainsKey(id))
            {
                return uniformLocations[id];
            }
            else
            {
                int loc = GL.GetUniformLocation(Id, id);
                uniformLocations.Add(id, loc);
                return loc;
            }
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
        public enum UniformType
        {
            texture,
            vec3,
            vec4,
            vec2,
            Float,
            Int,
            mat4
        }
        public struct GlobalUniformValue
        {
            public string uniformName;
            public object value;
            public UniformType uniformType;
            public GlobalUniformValue(string uniformName, UniformType uniformType, object value)
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
        public static void SetGlobalUniformValue(UniformType globalUniformType, object value, string uniformName)
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
            SetInt("textureMask", textureMask);
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
            SetInt("textureMask", textureMask);
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
        struct TextureBindState
        {
            public int TexturePtr;
            public bool WasNull;
        }
        static TextureBindState[] PreviousTextureState = new TextureBindState[TotalTextures];
        internal void UpdateUniforms()
        {
            //get uniform locations (cached by the shader program)
            for (int i = 0; i < m_uniformValues.Count; i++)
            {
                var t = m_uniformValues[i];
                t.uniformLocation = GetUniformLocation(m_uniformValues[i].id);
                m_uniformValues[i] = t;
            }

            //apply all material specific uniforms

            //apply texture units
            for (int i = 0; i < TotalTextures; i++)
            {
                if (IntToBool(textureMask, i))
                {
                    if (PreviousTextureState[i].WasNull || PreviousTextureState[i].TexturePtr != textures[i].GlTextureID)
                    {
                        GL.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + i));
                        GL.BindTexture(TextureTarget.Texture2D, textures[i].GlTextureID);
                    }


                    PreviousTextureState[i].WasNull = false;
                    PreviousTextureState[i].TexturePtr = textures[i].GlTextureID;
                }
                else if(IntToBool(nullTextureMask, i))
                {
                    if (!PreviousTextureState[i].WasNull || PreviousTextureState[i].TexturePtr != textures[i].GlTextureID)
                    {
                        GL.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + i));
                        GL.BindTexture(TextureTarget.Texture2D, 0);
                        GL.Disable(EnableCap.Texture2D);
                    }

                    PreviousTextureState[i].WasNull = true;
                    PreviousTextureState[i].TexturePtr = 0;
                }
            }

            SetInt("textureMask", textureMask);
            for (int i = 0; i < m_uniformValues.Count; i++)
            {
                var type = m_uniformValues[i].UniformType;
                switch (type)
                {
                    case UniformType.vec3:
                        GL.Uniform3(m_uniformValues[i].uniformLocation, (Vector3)m_uniformValues[i].value);
                        break;
                    case UniformType.vec4:
                        GL.Uniform4(m_uniformValues[i].uniformLocation, (Vector4)m_uniformValues[i].value);
                        break;
                    case UniformType.vec2:
                        GL.Uniform2(m_uniformValues[i].uniformLocation, (Vector2)m_uniformValues[i].value);
                        break;
                    case UniformType.Float:
                        GL.Uniform1(m_uniformValues[i].uniformLocation, (float)m_uniformValues[i].value);
                        break;
                    case UniformType.Int:
                        GL.Uniform1(m_uniformValues[i].uniformLocation, (int)m_uniformValues[i].value);
                        break;
                    case UniformType.mat4:
                        var val = (Matrix4)m_uniformValues[i].value;
                        GL.UniformMatrix4(m_uniformValues[i].uniformLocation, false, ref val);
                        break;
                    default:
                        break;
                };
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
            public UniformType UniformType;
            public object value;
            public UniformValue(string id, int location, UniformType UniformType, object value)
            {
                this.id = id;
                this.uniformLocation = location;
                this.UniformType = UniformType;
                this.value = value;
            }
        }


        private Dictionary<string, int> m_cachedUniformValueIndex = new Dictionary<string, int>();
        private List<UniformValue> m_uniformValues = new List<UniformValue>();

        public static void SetGlobalTexture(string id, Texture texture)
        {
            SetGlobalUniformValue(UniformType.texture, texture, id);

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
            SetGlobalUniformValue(UniformType.mat4, matrix4, id);

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
            SetGlobalUniformValue(UniformType.vec4, value, id);

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
            SetGlobalUniformValue(UniformType.vec3, value, id);

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
            SetGlobalUniformValue(UniformType.vec2, value, id);

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
            SetGlobalUniformValue(UniformType.Float, value, id);

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
            SetGlobalUniformValue(UniformType.Int, value, id);

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
                    case UniformType.texture:
                        SetTexture(cur.uniformName, (Texture)cur.value);
                        break;
                    case UniformType.vec3:
                        SetVector3(cur.uniformName, (Vector3)cur.value);
                        break;
                    case UniformType.vec4:
                        SetVector4(cur.uniformName, (Vector4)cur.value);
                        break;
                    case UniformType.vec2:
                        SetVector3(cur.uniformName, (Vector3)cur.value);
                        break;
                    case UniformType.Float:
                        SetFloat(cur.uniformName, (float)cur.value);
                        break;
                    case UniformType.Int:
                        SetInt(cur.uniformName, (int)cur.value);
                        break;
                    case UniformType.mat4:
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
                int index = m_cachedUniformValueIndex[uniformValue.id];
                var temp = m_uniformValues[index];
                temp.value = uniformValue.value;
                m_uniformValues[index] = temp;
            }
            else
            {
                m_cachedUniformValueIndex.Add(uniformValue.id, m_uniformValues.Count);
                m_uniformValues.Add(uniformValue);
            }
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
            mAddUniform(new UniformValue(id, -1, UniformType.mat4, value));
        }
        public void SetFloat(string id, float value)
        {
            mAddUniform(new UniformValue(id, -1, UniformType.Float, value));
        }
        public void SetInt(string id, int value)
        {
            mAddUniform(new UniformValue(id, -1, UniformType.Int, value));
        }
        public void SetBool(string id, bool value)
        {
            SetInt(id, value ? 1 : 0);
        }
        public void SetVector4(string id, Vector4 value)
        {
            mAddUniform(new UniformValue(id, -1, UniformType.vec4, value));

        }
        public void SetVector3(string id, Vector3 value)
        {
            mAddUniform(new UniformValue(id, -1, UniformType.vec3, value));

        }
        public void SetVector2(string id, Vector2 value)
        {
            mAddUniform(new UniformValue(id, -1, UniformType.vec2, value));
        }

        public int GetUniformLocation(string id)
        {
            return Program.GetUniformLocation(id);
        }
    }
}
