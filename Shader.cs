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
using static JLGraphics.Shader;
using System.Data;

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
        public static ShaderProgram FindShaderProgram(string name)
        {
            for (int i = 0; i < shaderPrograms.Count; i++)
            {
                if (shaderPrograms[i].Name == name)
                {
                    return shaderPrograms[i];
                }
            }
            return null;
        }
        public void CompileProgram()
        {
            if (Disposed)
            {
                Console.WriteLine("ERROR::Program has been disposed!");
                throw new Exception("ERROR::Program has been disposed!");
            }
            isCompiled = true;
            Frag.CompileShader();
            Vert.CompileShader();
            UpdateProgram();
            //UpdateProgram called via callback
        }
        List<KeyValuePair<string, ActiveUniformType>> uniformTypes = new List<KeyValuePair<string, ActiveUniformType>>();
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

            GL.GetProgram(Id, GetProgramParameterName.ActiveUniforms, out int uniformCount);
            for (int i = 0; i < uniformCount; i++)
            {
                string name = GL.GetActiveUniform(Id, i, out int size, out ActiveUniformType type);
                int loc = GL.GetUniformLocation(Id, name);
                uniformTypes.Add(new KeyValuePair<string, ActiveUniformType>(name, type));
                GetUniformLocation(name);
            }

            var d = GL.GetProgramInfoLog(Id);
            if (d != "")
                Console.WriteLine(d);
            uniformLocations.Clear();
        }
        public List<KeyValuePair<string, ActiveUniformType>> GetUniformTypes()
        {
            if (!isCompiled)
            {
                Console.WriteLine("ERROR::Program is not compiled! " + Name);
                throw new Exception("ERROR::Program is not compiled! " + Name);
            }
            if (Disposed)
            {
                Console.WriteLine("ERROR::Program has been disposed! " + Name);
                throw new Exception("ERROR::Program has been disposed! " + Name);
            }
            return uniformTypes;
        }
        public void Dispose()
        {
            isCompiled = false;
            OnDispose.Invoke();
            Disposed = true;
            Frag.Dispose();
            Vert.Dispose();
            GL.DeleteProgram(Id);
            shaderPrograms.Remove(this);
            Vert.FileChangeCallback.Remove(OnVertFileChangeShaderRecompile);
            Frag.FileChangeCallback.Remove(OnFragFileChangeShaderRecompile);
        }

        public static int ProgramCounts => shaderPrograms.Count;
        Dictionary<string, int> uniformLocations = new Dictionary<string, int>();
        public bool isCompiled { get; private set; } = false;
        public int GetUniformLocation(string id)
        {
            if (!isCompiled)
            {
                Console.WriteLine("ERROR::Program is not compiled! " + Name);
                throw new Exception("ERROR::Program is not compiled! " + Name);
            }
            if (Disposed)
            {
                Console.WriteLine("ERROR::Program has been disposed! " + Name);
                throw new Exception("ERROR::Program has been disposed! " + Name);
            }
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
        public DepthFunction DepthTestFunction { get; set; } = DepthFunction.Equal;
        public bool DepthTest { get; set; } = true;
        public bool DepthMask { get; set; } = true;
        public bool[] ColorMask { get; private set; } = new bool[4] { true, true, true, true };
        private int textureMask = 0;
        const int TotalTextures = 32;
        private Texture[] textures = new Texture[TotalTextures];
        private string[] textureUniformNames = new string[TotalTextures];

        public Texture GetTexture(string uniformName)
        {
            int textureIndex = textureIndexFromUniform(uniformName);
            if(textureIndex == -1)
            {
                return null;
            }
            return textures[textureIndex];
        }
        Stack<int> availableTextureSlots = new Stack<int>();
        int textureIndexFromUniform(string uniformName)
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
        bool isWithinShader { get; set; } = false;
        /// <summary>
        /// Must be called after calling UseProgram()
        /// </summary>
        /// <param name="uniformName"></param>
        /// <param name="texture"></param>
        /// <param name="textureTarget"></param>
        internal void SetTextureUnsafe(string uniformName, Texture texture, TextureTarget? textureTarget = null)
        {
            int textureIndex = textureIndexFromUniform(uniformName);

            if (textureTarget != null)
                texture.TextureTarget = textureTarget.Value;

            if (textureIndex == -1)
            {
                if (texture == null)
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
                //GL.Uniform1(Program.GetUniformLocation(uniformName), textureIndex);
                SetInt(uniformName, textureIndex);
                set_int_bool(textureIndex, true, ref textureMask);
            }
            else
            {
                textureUniformNames[textureIndex] = "";
                set_int_bool(textureIndex, false, ref textureMask);
                availableTextureSlots.Push(textureIndex);
            }
            SetInt("textureMask", textureMask);
        }
        public void SetTexture(string uniformName, Texture texture, TextureTarget? textureTarget = null)
        {
            if (!isWithinShader)
            {
                int previousProgram;
                previousProgram = GL.GetInteger(GetPName.CurrentProgram);
                GL.UseProgram(Program);
                SetTextureUnsafe(uniformName, texture, textureTarget);
                GL.UseProgram(previousProgram);
            }
            else
            {
                SetTextureUnsafe(uniformName, texture, textureTarget);
            }
        }
        public void SetTexture(string uniformName, int texturePtr, TextureTarget textureTarget)
        {
            if(texturePtr == 0)
            {
                SetTexture(uniformName, null, textureTarget);
            }
            else
            {
                SetTexture(uniformName, (Texture)texturePtr, textureTarget);
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

        void SetAllTextureUnitToUniform()
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
            
            availableTextureSlots = new Stack<int>(new Stack<int>(other.availableTextureSlots));

            Array.Copy(other.textures, textures, TotalTextures);
            Array.Copy(other.textureUniformNames, textureUniformNames, TotalTextures);
            m_cachedUniformValueIndex = new Dictionary<string, int>(other.m_cachedUniformValueIndex);
            textureMask = other.textureMask;
            SetAllTextureUnitToUniform();
        }
        public Shader(Shader shader)
        {
            dontFetchGlobals = shader.dontFetchGlobals;
            Name = shader.Name + "_clone";
            Program = shader.Program;
            CopyUniforms(shader);   //this already contains global uniforms
            myWeakRef = new WeakReference<Shader>(this);
            AllInstancedShaders.Add(myWeakRef);
            init();

            if (!dontFetchGlobals)
                fetchAllGlobalUniforms();   //get all previous global values before the shader was created
        }
        bool dontFetchGlobals = false;
        public Shader(string name, ShaderProgram shaderProgram, bool excludeFromGlobalUniforms = false) 
        {
            dontFetchGlobals = excludeFromGlobalUniforms;
            Name = name;
            Program = shaderProgram;
            myWeakRef = new WeakReference<Shader>(this);
            AllInstancedShaders.Add(myWeakRef);
            init();
            for (int i = 0; i < TotalTextures; i++)
            {
                availableTextureSlots.Push(i);
            }

            if (!dontFetchGlobals)
                fetchAllGlobalUniforms();   //get all previous global values before the shader was created
        }
        void ShaderReload()
        {
            Console.WriteLine("\tShader Reload.. Rebinding Textures, Setting uniforms for program " + Program.Id + " for material " + Name);
            SetInt("textureMask", textureMask);
            UseProgram();
            SetAllTextureUnitToUniform();
            UpdateUniforms();
            Unbind();
            return;
        }
        void init()
        {
            Program.OnShaderReload += ShaderReload;
            SetInt("textureMask", textureMask);
        }
        ~Shader()
        {
            Program.OnShaderReload -= ShaderReload;
            AllInstancedShaders.Remove(myWeakRef);
            myWeakRef = null;
        }
        bool initialDefaultValueSet = false;
        void SetDefaultValue(ActiveUniformType activeUniformType, string location)
        {
            switch (activeUniformType)
            {
                case ActiveUniformType.Int:
                    SetInt(location, 0);
                    break;
                case ActiveUniformType.Float:
                    SetFloat(location, 0.0f);
                    break;
                case ActiveUniformType.FloatVec2:
                    SetVector2(location, new Vector2(0,0));
                    break;
                case ActiveUniformType.FloatVec3:
                    SetVector3(location, new Vector3(0, 0, 0));
                    break;
                case ActiveUniformType.FloatVec4:
                    SetVector4(location, new Vector4(0, 0, 0, 0));
                    break;
                case ActiveUniformType.Bool:
                    SetVector4(location, new Vector4(0, 0, 0, 0));
                    break;
                case ActiveUniformType.FloatMat4:
                    SetMat4(location, Matrix4.Zero);
                    break;
                default:
                    SetInt(location, 0);
                    break;
            }
        }
        /// <summary>
        /// Expensive, try to batch this with other meshes with same materials.
        /// Applies material unique uniforms.
        /// </summary>
        internal void UseProgram()
        {
            GL.UseProgram(Program);
            //rarely the case
            if(textureMask == 0)
            {
                GL.ActiveTexture(TextureUnit.Texture0);
            }
        }
        bool IntToBool(int val, int index)
        {
            return ((val >> index) & 1) == 1;
        }
        struct TextureBindState
        {
            public int TexturePtr;
            public TextureTarget textureTarget;
        }
        struct UniformBindState
        {
            public int uniformLocation;
            public object value;
        }
        static TextureBindState[] PreviousTextureState = new TextureBindState[TotalTextures];
        static Dictionary<string, UniformBindState> PreviousUniformState = new Dictionary<string, UniformBindState>();
        internal static void ClearStateCheckCache()
        {
            PreviousUniformState.Clear();
            PreviousTextureState = new TextureBindState[TotalTextures];
        }
        internal bool UpdateUniforms()
        {
            bool hasUpdated = false;
            isWithinShader = true;
            if (DepthTest)
            {
                GL.Enable(EnableCap.DepthTest);
                GL.DepthFunc(DepthTestFunction);
            }
            else
            {
                GL.Disable(EnableCap.DepthTest);
            }
            GL.DepthMask(DepthMask);
            GL.ColorMask(ColorMask[0], ColorMask[1], ColorMask[2], ColorMask[3]);

            //apply all material specific uniforms
            fetchAllGlobalUniforms();

            //set default values once
            if (!initialDefaultValueSet)
            {
                initialDefaultValueSet = true;
                var types = Program.GetUniformTypes();
                for (int i = 0; i < types.Count; i++)
                {
                    var name = types[i].Key;
                    var type = types[i].Value;
                    if (m_cachedUniformValueIndex.ContainsKey(name))
                    {
                        continue;
                    }
                    SetDefaultValue(type, name);
                }
            }

            //apply texture units
            for (int i = 0; i < TotalTextures; i++)
            {
                if (IntToBool(textureMask, i))
                {
                    if (PreviousTextureState[i].TexturePtr != textures[i].GlTextureID)
                    {
                        //go to texture unit i
                        GL.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + i));
                        
                        //bind the texture to it
                        GL.BindTexture(textures[i].TextureTarget, textures[i].GlTextureID);
                        hasUpdated = true;
                    }


                    PreviousTextureState[i].TexturePtr = textures[i].GlTextureID;
                    PreviousTextureState[i].textureTarget = textures[i].TextureTarget;
                }
                else
                {
                    if (PreviousTextureState[i].TexturePtr != 0)
                    {
                        //go to texture unit i
                        GL.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + i));

                        //unbind any textures to it
                        GL.BindTexture(PreviousTextureState[i].textureTarget, 0);
                        hasUpdated = true;
                    }

                    PreviousTextureState[i].TexturePtr = 0;
                }
            }
            
            for (int i = 0; i < m_uniformValues.Count; i++)
            {
                var type = m_uniformValues[i].UniformType;
                int uniformLocation = Program.GetUniformLocation(m_uniformValues[i].uniformName);

                if (PreviousUniformState.TryGetValue(m_uniformValues[i].uniformName, out UniformBindState prevState))
                {
                    if (prevState.value.GetType() == m_uniformValues[i].value.GetType()
                        && prevState.value.Equals(m_uniformValues[i].value)
                        && prevState.uniformLocation == uniformLocation
                        && PreviousProgram == Program)
                    {
                        continue;
                    }
                    else
                    {
                        hasUpdated = true;
                        PreviousUniformState[m_uniformValues[i].uniformName] = new UniformBindState()
                        {
                            value = m_uniformValues[i].value,
                            uniformLocation = uniformLocation,
                        };
                    }
                }
                else
                {
                    hasUpdated = true;
                    PreviousUniformState.Add(m_uniformValues[i].uniformName, new UniformBindState()
                    {
                        value = m_uniformValues[i].value,
                        uniformLocation = uniformLocation
                    });
                }

                switch (type)
                {
                    case UniformType.vec3:
                        GL.Uniform3(uniformLocation, (Vector3)m_uniformValues[i].value);
                        break;
                    case UniformType.vec4:
                        GL.Uniform4(uniformLocation, (Vector4)m_uniformValues[i].value);
                        break;
                    case UniformType.vec2:
                        GL.Uniform2(uniformLocation, (Vector2)m_uniformValues[i].value);
                        break;
                    case UniformType.Float:
                        GL.Uniform1(uniformLocation, (float)m_uniformValues[i].value);
                        break;
                    case UniformType.Int:
                        GL.Uniform1(uniformLocation, (int)m_uniformValues[i].value);
                        break;
                    case UniformType.mat4:
                        var val = (Matrix4)m_uniformValues[i].value;
                        GL.UniformMatrix4(uniformLocation, false, ref val);
                        break;
                    default:
                        break;
                };
            }

            PreviousProgram = Program;
            isWithinShader = false;
            return hasUpdated;
        }
        internal static void Unbind()
        {
            GL.UseProgram(0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
        public struct UniformValue
        {
            public string uniformName;
            public UniformType UniformType;
            public object value;
            public UniformValue(string id, UniformType UniformType, object value)
            {
                this.uniformName = id;
                this.UniformType = UniformType;
                this.value = value;
            }
        }


        private Dictionary<string, int> m_cachedUniformValueIndex = new Dictionary<string, int>();
        private List<UniformValue> m_uniformValues = new List<UniformValue>();
        static ShaderProgram PreviousProgram = null;
        public static void SetGlobalTexture(string id, Texture texture)
        {
            SetGlobalUniformValue(UniformType.texture, texture, id);
        }
        public static void SetGlobalTexture(string id, int texture, TextureTarget textureTarget)
        {
            Texture texture1 = (Texture)texture;
            texture1.TextureTarget = textureTarget;
            SetGlobalUniformValue(UniformType.texture, texture1, id);
        }
        public static void SetGlobalMat4(string id, Matrix4 matrix4)
        {
            SetGlobalUniformValue(UniformType.mat4, matrix4, id);
        }
        public static void SetGlobalVector4(string id, Vector4 value)
        {
            SetGlobalUniformValue(UniformType.vec4, value, id);
        }
        public static void SetGlobalVector3(string id, Vector3 value)
        {
            SetGlobalUniformValue(UniformType.vec3, value, id);
        }

        public static void SetGlobalVector2(string id, Vector2 value)
        {
            SetGlobalUniformValue(UniformType.vec2, value, id);
        }
        public static void SetGlobalFloat(string id, float value)
        {
            SetGlobalUniformValue(UniformType.Float, value, id);
        }
        public static void SetGlobalInt(string id, int value)
        {
            SetGlobalUniformValue(UniformType.Int, value, id);
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
            if (m_cachedUniformValueIndex.ContainsKey(uniformValue.uniformName))
            {
                int index = m_cachedUniformValueIndex[uniformValue.uniformName];
                var temp = m_uniformValues[index];
                temp.value = uniformValue.value;
                m_uniformValues[index] = temp;
            }
            else
            {
                m_cachedUniformValueIndex.Add(uniformValue.uniformName, m_uniformValues.Count);
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
            mAddUniform(new UniformValue(id, UniformType.mat4, value));
        }
        public void SetFloat(string id, float value)
        {
            mAddUniform(new UniformValue(id, UniformType.Float, value));
        }
        public void SetInt(string id, int value)
        {
            mAddUniform(new UniformValue(id, UniformType.Int, value));
        }
        public void SetBool(string id, bool value)
        {
            SetInt(id, value ? 1 : 0);
        }
        public void SetVector4(string id, Vector4 value)
        {
            mAddUniform(new UniformValue(id, UniformType.vec4, value));

        }
        public void SetVector3(string id, Vector3 value)
        {
            mAddUniform(new UniformValue(id, UniformType.vec3, value));

        }
        public void SetVector2(string id, Vector2 value)
        {
            mAddUniform(new UniformValue(id, UniformType.vec2, value));
        }

        public int GetUniformLocation(string id)
        {
            return Program.GetUniformLocation(id);
        }
    }
}
