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
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;
using Assimp;

namespace JLGraphics
{
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
    public struct UniformValue
    {
        public string uniformName;
        public object value;
        public UniformType uniformType;
        public UniformValue(string uniformName, UniformType uniformType, object value)
        {
            this.uniformType = uniformType;
            this.uniformName = uniformName;
            this.value = value;
        }
        public static implicit operator UniformValue(UniformValueWithLocation uniformValue) => new UniformValue(uniformValue.uniformName, uniformValue.UniformType, uniformValue.value);
    }
    public struct UniformValueWithLocation
    {
        public string uniformName;
        public object value;
        public UniformType UniformType;
        public int uniformLocation;
        public UniformValueWithLocation(string id, UniformType UniformType, object value)
        {
            this.uniformName = id;
            this.UniformType = UniformType;
            this.value = value;
            uniformLocation = -1;
        }
        public static implicit operator UniformValueWithLocation(UniformValue uniformValue) => new UniformValueWithLocation(uniformValue.uniformName, uniformValue.uniformType, uniformValue.value);
    }
    public sealed class ShaderParser
    {
        struct Shader
        {
            public string name;
            public ShaderType shaderType;
            public List<string> Passes;
            public Shader()
            {
                name = "";
                shaderType = ShaderType.FragmentShader;
                Passes = new List<string>();
            }
        }
        public struct ParsedShader
        {
            public string ShaderCode;
            public ShaderType ShaderType;
        }
        static string ReplaceFirst(string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }
        static string mHandleInclude(string filePath, string fromPath)
        {
            var outString = "";
            var data = File.ReadAllText(filePath);
            var includeStatement = Regex.Match(data, "INCLUDE *\"(.*)\"", RegexOptions.Multiline);
            while(includeStatement != null)
            {
                if(includeStatement.Length == 0)
                {
                    data += "\n";
                    break;
                }
                var includeFile = includeStatement.Groups[1];
                var moreIncluded = mHandleInclude(includeFile.Value.Trim(), filePath);
                data = ReplaceFirst(data, includeStatement.Value, moreIncluded);
                includeStatement = includeStatement.NextMatch();
            }
            outString += data + "//Shader Parser: Auto generated file include: " + filePath  + " from: " + fromPath + "\n";
            return outString;
        }
        public static ParsedShader? ParseShader(string filePath, string targetShadernName, int targetShaderPass)
        {
            string data = File.ReadAllText(filePath);
            List<Shader> shaders = new List<Shader>();
            var match = Regex.Match(data, "SHADER +(.*) +(.*)", RegexOptions.Multiline);
            var splits = Regex.Split(data, "SHADER +.* +.*", RegexOptions.Multiline).ToList();
            splits.RemoveAt(0);
            if (match.Length == 0)
            {
                Debug.Log("Missing: SHADER (FRAG | VERT) (SHADER_NAME) in " + filePath, Debug.Flag.Error);
                return null;
            }
            while (match != null)
            {
                if(match.Length == 0)
                {
                    break;
                }
                var ShaderProgram = new Shader();
                var type = match.Groups[1].ToString().Trim();
                var name = match.Groups[2].ToString().Trim();
                ShaderProgram.name = name;
                if (type == "FRAG")
                {
                    ShaderProgram.shaderType = ShaderType.FragmentShader;
                }
                else if(type == "VERT")
                {
                    ShaderProgram.shaderType = ShaderType.VertexShader;
                }
                else
                {
                    Debug.Log("Unknown shader type! Use FRAG or VERT for " + name + " in " + filePath, Debug.Flag.Error);
                    break;
                }
                shaders.Add(ShaderProgram);
                match = match.NextMatch();
            }

            for (int i = 0; i < shaders.Count; i++)
            {
                var includeStatement = Regex.Match(splits[i], "INCLUDE *\"(.*)\"", RegexOptions.Multiline);
                List<string> includedFiles = new List<string>();
                while(includeStatement != null)
                {
                    if(includeStatement.Length == 0)
                    {
                        break;
                    }
                    includedFiles.Add(mHandleInclude(includeStatement.Groups[1].Value.Trim(), filePath));
                    splits[i] = splits[i].Replace(includeStatement.Value, "");
                    includeStatement = includeStatement.NextMatch();
                }
                splits[i] = splits[i].Trim();
                var passSplits = Regex.Split(splits[i], "PASS\\s*", RegexOptions.Multiline);
                if(passSplits.Length <= 1)
                {
                    Debug.Log("Missing pass: " + shaders[i].shaderType.ToString() + " " + shaders[i].name + " in " + filePath, Debug.Flag.Error);
                }
                for (int j = 1; j < passSplits.Length; j++)
                {
                    var passString = "";

                    for (int k = 0; k < includedFiles.Count; k++)
                    {
                        passString += includedFiles[k];
                    }

                    passString += passSplits[j].Trim();
                    shaders[i].Passes.Add(passString);
                }
            }

            int index = 0;
            for (int i = 0; i < shaders.Count; i++)
            {
                if (shaders[i].name == targetShadernName)
                {
                    index = i;
                    break;
                }
            }
            data = shaders[index].Passes[targetShaderPass];

            return new ParsedShader() { ShaderCode = data, ShaderType = shaders[index].shaderType };
        }
    }
    internal sealed class ShaderFile : SafeDispose, IFileObject
    {
        internal ShaderType ShaderType { get; private set; }
        public List<Action> FileChangeCallback => new List<Action>();
        public string FilePath { get; }

        public override string Name => FilePath;

        public static implicit operator int(ShaderFile d) => d.compiledShader;

        int compiledShader = 0;
        bool addedCallback = false;
        internal ShaderFile(string path, ShaderType shaderType)
        {
            FilePath = path;
            ShaderType = shaderType;
            if (!File.Exists(path))
            {
                Debug.Log("Shader not found: " + path, Debug.Flag.Error);
                return;
            }
        }

        bool useShaderParser = false;
        string shaderToUse = "";
        int passToUse = 0;
        internal ShaderFile(string path, string targetShaderName, int targetShaderPass)
        {
            useShaderParser = true;
            this.shaderToUse = targetShaderName;
            this.passToUse = targetShaderPass;
            FilePath = path;
            if (!File.Exists(path))
            {
                Debug.Log("Shader not found: " + path, Debug.Flag.Error);
                return;
            }
        }
        internal void CompileShader() {
            Debug.Log("Compiling Shader: " + FilePath);
            string data;
            if (useShaderParser)
            {
                Debug.Log("Compiling Shader name: " + shaderToUse);
                Debug.Log("Compiling Shader pass: " + passToUse);
                var shaders = ShaderParser.ParseShader(FilePath, shaderToUse, passToUse);
                if(shaders != null)
                {
                    data = shaders.Value.ShaderCode;
                    ShaderType = shaders.Value.ShaderType;
                }
                else
                {
                    Debug.Log("Cannot find shader within parsed shader " + FilePath + ". Are your shader name and pass correct?", Debug.Flag.Warning);
                    data = "";
                    ShaderType = ShaderType.FragmentShader;
                }
            }
            else
            {
                data = File.ReadAllText(FilePath);
            }

            if (!addedCallback)
            {
                FileChangeCallback.Add(() => {
                    GL.DeleteShader(compiledShader);
                    compiledShader = GL.CreateShader(ShaderType);
                    CompileShader();
                });
                addedCallback = true;
            }

            compiledShader = GL.CreateShader(ShaderType);

            GL.ShaderSource(compiledShader, data);

            //compile the shaders
            GL.CompileShader(compiledShader);

            string d = GL.GetShaderInfoLog(compiledShader);
            if (d != "")
            {
                Debug.Log(d);
                return;
            }
        }
        protected override void OnDispose()
        {
            GL.DeleteShader(compiledShader);
        }
    }
    public sealed class ShaderProgram : IDisposable
    {
        internal bool Disposed { get; private set; } = false;
        internal ShaderFile Frag { get; } = null;
        internal ShaderFile Geo { get; } = null;
        internal ShaderFile Vert { get; } = null;

        public IFileObject FragFile => Frag;
        public IFileObject VertFile => Vert;
        public IFileObject GeoFile => Geo;

        string Name { get; }
        public int Id { get; private set; } = 0;
        public static implicit operator int(ShaderProgram d) => d.Id;
        internal static List<ShaderProgram> AllShaderPrograms { get; private set; } = new List<ShaderProgram>();
        internal Action OnShaderReload { get; set; } = null;
        internal Action OnDispose { get; set; } = null;
        void OnFragFileChangeShaderRecompile()
        {
            GL.DeleteProgram(Id);
            Id = GL.CreateProgram();
            //recompile vert shader
            Vert.CompileShader();
            Geo.CompileShader();
            UpdateProgram();
            OnShaderReload?.Invoke();
        }
        void OnVertFileChangeShaderRecompile()
        {
            GL.DeleteProgram(Id);
            Id = GL.CreateProgram();
            //recompile frag shader
            Frag.CompileShader();
            Geo.CompileShader();
            UpdateProgram();
            OnShaderReload?.Invoke();
        }
        void OnGeoFileChangeShaderRecompile()
        {
            GL.DeleteProgram(Id);
            Id = GL.CreateProgram();
            //recompile frag shader
            Vert.CompileShader();
            Frag.CompileShader();
            UpdateProgram();
            OnShaderReload?.Invoke();
        }
        public ShaderProgram(string name, string fragPath, string vertPath, string geometryPath = "")
        {
            Name = name;
            Vert = new ShaderFile(vertPath, ShaderType.VertexShader);
            Frag = new ShaderFile(fragPath, ShaderType.FragmentShader);
            if(geometryPath.Trim() != "")
            {
                Geo = new ShaderFile(geometryPath, ShaderType.GeometryShader);
            }
            Id = GL.CreateProgram();
            AllShaderPrograms.Add(this);
        }
        public ShaderProgram(string name, string fragPath, string targetFragShaderName, int targetFragPass, string vertPath, string targetVertShaderName, int targetVertPass)
        {
            Name = name;
            Vert = new ShaderFile(vertPath, targetVertShaderName, targetVertPass);
            Frag = new ShaderFile(fragPath, targetFragShaderName, targetFragPass);
            Id = GL.CreateProgram();
            AllShaderPrograms.Add(this);
        }
        public ShaderProgram(string name, string fragPath, string vertPath, string targetVertShaderName, int targetVertPass)
        {
            Name = name;
            Vert = new ShaderFile(vertPath, targetVertShaderName, targetVertPass);
            Frag = new ShaderFile(fragPath, ShaderType.FragmentShader);
            Id = GL.CreateProgram();
            AllShaderPrograms.Add(this);
        }
        public ShaderProgram(string name, string fragPath, string targetFragShaderName, int targetFragPass, string vertPath)
        {
            Name = name;
            Vert = new ShaderFile(vertPath, ShaderType.VertexShader);
            Frag = new ShaderFile(fragPath, targetFragShaderName, targetFragPass);
            Id = GL.CreateProgram();
            AllShaderPrograms.Add(this);
        }
        public ShaderProgram(ShaderProgram shaderProgram)
        {
            Name = shaderProgram.Name;
            Vert = new ShaderFile(shaderProgram.VertFile.FilePath, ShaderType.VertexShader);
            Frag = new ShaderFile(shaderProgram.FragFile.FilePath, ShaderType.FragmentShader);
            if(shaderProgram.Geo != null)
                Geo = new ShaderFile(shaderProgram.GeoFile.FilePath, ShaderType.GeometryShader);
            
            Id = GL.CreateProgram();
            AllShaderPrograms.Add(this);
        }
        public static ShaderProgram FindShaderProgram(string name)
        {
            for (int i = 0; i < AllShaderPrograms.Count; i++)
            {
                if (AllShaderPrograms[i].Name == name)
                {
                    return AllShaderPrograms[i];
                }
            }
            return null;
        }
        public void CompileProgram()
        {
            if (Disposed)
            {
                Debug.Log("Program has been disposed!", Debug.Flag.Error);
            }
            isCompiled = true;
            Frag.CompileShader();
            Vert.CompileShader();
            Geo?.CompileShader();
            Vert.FileChangeCallback.Add(OnVertFileChangeShaderRecompile);
            Frag.FileChangeCallback.Add(OnFragFileChangeShaderRecompile);
            Geo?.FileChangeCallback.Add(OnGeoFileChangeShaderRecompile);
            UpdateProgram();
            //UpdateProgram called via callback
        }
        List<KeyValuePair<string, ActiveUniformType>> uniformTypes = new List<KeyValuePair<string, ActiveUniformType>>();
        void UpdateProgram()
        {
            Debug.Log("\tLinking shader to program " + Id + ", " + Name);
            //attach shaders
            GL.AttachShader(Id, Frag);
            GL.AttachShader(Id, Vert);
            if(Geo != null)
                GL.AttachShader(Id, Geo);

            //link to program
            GL.LinkProgram(Id);

            //detach shaders
            GL.DetachShader(Id, Frag);
            GL.DetachShader(Id, Vert);
            if (Geo != null)
                GL.DetachShader(Id, Geo);

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
                Debug.Log(d);
            uniformLocations.Clear();
        }
        public List<KeyValuePair<string, ActiveUniformType>> GetUniformTypes()
        {
            if (!isCompiled)
            {
                Debug.Log("Program is not compiled! " + Name, Debug.Flag.Error);
            }
            if (Disposed)
            {
                Debug.Log("Program has been disposed! " + Name, Debug.Flag.Error);
            }
            return uniformTypes;
        }
        public void Dispose()
        {
            isCompiled = false;
            OnDispose?.Invoke();
            Disposed = true;
            Frag.Dispose();
            Vert.Dispose();
            Geo?.Dispose();
            GL.DeleteProgram(Id);
            AllShaderPrograms.Remove(this);
            Vert.FileChangeCallback.Remove(OnVertFileChangeShaderRecompile);
            Frag.FileChangeCallback.Remove(OnFragFileChangeShaderRecompile);
            Geo?.FileChangeCallback.Remove(OnGeoFileChangeShaderRecompile);
        }

        public static int ProgramCounts => AllShaderPrograms.Count;
        Dictionary<string, int> uniformLocations = new Dictionary<string, int>();
        public bool isCompiled { get; private set; } = false;
        public int GetUniformLocation(string id)
        {
            if (!isCompiled)
            {
                Debug.Log("Program is not compiled! " + Name, Debug.Flag.Error);
            }
            if (Disposed)
            {
                Debug.Log("Program has been disposed! " + Name, Debug.Flag.Error);
            }

            if (uniformLocations.TryGetValue(id, out int value))
            {
                return value;
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
        public bool IsTransparent { get; set; } = false;
        public BlendingFactor BlendingFactor { get; set; } = BlendingFactor.SrcAlpha;
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
            for (int i = TotalTextures - 1; i >= 0; i--)
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
        internal void SetTextureUnsafe(string uniformName, Texture texture, TextureTarget? textureTarget = null)
        {
            int textureIndex = textureIndexFromUniform(uniformName);

            if (textureTarget != null)
                texture.textureTarget = textureTarget.Value;

            if (textureIndex == -1)
            {
                if (texture == null)
                {
                    return;
                }
                if (availableTextureSlots.Count == 0)
                {
                    Debug.Log("Cannot add more textures to shader material: " + Name, Debug.Flag.Error);
                    return;
                }
                textureIndex = availableTextureSlots.Pop();
                textureUniformNames[textureIndex] = uniformName;
            }

            textures[textureIndex] = texture;
            if (texture != null)
            {
                SetInt(uniformName, textureIndex);
                set_int_bool(textureIndex, true, ref textureMask);
            }
            else
            {
                textureUniformNames[textureIndex] = "";
                set_int_bool(textureIndex, false, ref textureMask);
                availableTextureSlots.Push(textureIndex);
            }
        }
        public void SetTexture(string uniformName, Texture texture)
        {
            if (!isWithinShader)
            {
                GL.UseProgram(Program);
                SetTextureUnsafe(uniformName, texture, texture.textureTarget);
            }
            else
            {
                SetTextureUnsafe(uniformName, texture, texture.textureTarget);
            }
        }

        WeakReference<Shader> myWeakRef;
        static List<WeakReference<Shader>> AllInstancedShaders = new List<WeakReference<Shader>>();
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
            m_uniformValues = new List<UniformValueWithLocation>(other.m_uniformValues);
            m_uniformValuesDefaultFlag = new List<bool>(other.m_uniformValuesDefaultFlag);
            //m_cachedUniformValueIndex = new List<(string, int)>(other.m_cachedUniformValueIndex);

            availableTextureSlots = new Stack<int>(new Stack<int>(other.availableTextureSlots));

            Array.Copy(other.textures, textures, TotalTextures);
            Array.Copy(other.textureUniformNames, textureUniformNames, TotalTextures);
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
        }
        void ShaderReload()
        {
            Debug.Log("\tShader Reload.. Rebinding Textures, Setting uniforms for program " + Program.Id + " for material " + Name);
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
                    SetInt(location, 0, true);
                    break;
                case ActiveUniformType.Float:
                    SetFloat(location, 0.0f, true);
                    break;
                case ActiveUniformType.FloatVec2:
                    SetVector2(location, new Vector2(0,0), true);
                    break;
                case ActiveUniformType.FloatVec3:
                    SetVector3(location, new Vector3(0, 0, 0), true);
                    break;
                case ActiveUniformType.FloatVec4:
                    SetVector4(location, new Vector4(0, 0, 0, 0), true);
                    break;
                case ActiveUniformType.Bool:
                    SetVector4(location, new Vector4(0, 0, 0, 0), true);
                    break;
                case ActiveUniformType.FloatMat4:
                    SetMat4(location, Matrix4.Zero, true);
                    break;
                default:
                    SetInt(location, 0, true);
                    break;
            }
        }
        internal ShaderProgram UseProgram()
        {
            if (Program.Disposed)
            {
                Debug.Log("Shader program has been dispoed!", Debug.Flag.Error);
                return null;
            }
            GL.UseProgram(Program);
            //rarely the case
            if(textureMask == 0)
            {
                GL.ActiveTexture(TextureUnit.Texture0);
            }
            if (!dontFetchGlobals)
                mPushAllGlobalUniformsToShaderProgram();
            return Program;
        }
        static bool IntToBool(int val, int index)
        {
            return ((val >> index) & 1) == 1;
        }

        //#################### PREVIOUS UNIFORM STATE ####################
        /// <summary>
        /// These are cached values from the last time you call update uniforms
        /// These are used to check if the previous update uniform (from any instance of the shader class) contains overlapping
        /// Shader program, uniform location, and uniform values
        /// If the last UniformUpdate call contains the same program, uniform location and uniform value, it will skip sending that uniform to the gpu
        /// </summary>
        struct TextureBindState
        {
            public int TexturePtr;
            public TextureTarget textureTarget;
        }
        struct UniformBindState
        {
            public string uniformName;
            public object value;
        }
        readonly static TextureBindState[] PreviousTextureState = new TextureBindState[TotalTextures];
        readonly static Dictionary<int, UniformBindState> PreviousUniformState = new Dictionary<int, UniformBindState>();
        //######################################################

        internal static void ClearStateCheckCache()
        {
            PreviousUniformState.Clear();
            Array.Clear(PreviousTextureState);
        }
        static void SendUniformDataToGPU(int uniformLocation, UniformType type, object value)
        {
            switch (type)
            {
                case UniformType.vec3:
                    GL.Uniform3(uniformLocation, (Vector3)value);
                    break;
                case UniformType.vec4:
                    GL.Uniform4(uniformLocation, (Vector4)value);
                    break;
                case UniformType.vec2:
                    GL.Uniform2(uniformLocation, (Vector2)value);
                    break;
                case UniformType.Float:
                    GL.Uniform1(uniformLocation, (float)value);
                    break;
                case UniformType.Int:
                    GL.Uniform1(uniformLocation, (int)value);
                    break;
                case UniformType.mat4:
                    var val = (Matrix4)value;
                    GL.UniformMatrix4(uniformLocation, false, ref val);
                    break;
                default:
                    break;
            };
        }
        bool checkIfPreviousUniformUpdatePushed(in UniformValueWithLocation current)
        {
            if (PreviousUniformState.TryGetValue(current.uniformLocation, out UniformBindState prevState))
            {
                if (PreviousProgram == Program && prevState.value.Equals(current.value))
                {
                    return true;
                }
                else
                {
                    PreviousUniformState[current.uniformLocation] = new UniformBindState()
                    {
                        value = current.value,
                        uniformName = current.uniformName,
                    };
                }
            }
            else
            {
                PreviousUniformState.Add(current.uniformLocation, new UniformBindState()
                {
                    value = current.value,
                    uniformName = current.uniformName
                });
            }
            return false;
        }
        internal bool UpdateUniforms()
        {
            if (Program.Disposed)
            {
                Debug.Log("Shader program has been dispoed!", Debug.Flag.Error);
                return false;
            }
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
            if (IsTransparent)
            {
                GL.BlendFunc(BlendingFactor, BlendingFactor + 1);
                GL.Enable(EnableCap.Blend);
            }
            else
            {
                GL.Disable(EnableCap.Blend);
            }
            GL.DepthMask(DepthMask);
            GL.ColorMask(ColorMask[0], ColorMask[1], ColorMask[2], ColorMask[3]);

            //fetch global textures
            if(!dontFetchGlobals)
                mFetchGlobalTextures();

            //set default values once
            if (!initialDefaultValueSet)
            {
                initialDefaultValueSet = true;
                var types = Program.GetUniformTypes();
                for (int i = 0; i < types.Count; i++)
                {
                    var name = types[i].Key;
                    var type = types[i].Value;
                    //dont add a default value if there is a global uniform already existing
                    if (mFindLocalUniformIndex(name, out int li) || mFindGlobalUniformIndex(name, out int gi))
                    {
                        continue;
                    }
                    SetDefaultValue(type, name);
                }
            }

            //apply texture units, set local textures
            //optimized such that the update uniform wont update the same uniforms with same values as the previously update uniform
            for (int i = 0; i < TotalTextures; i++)
            {
                if (IntToBool(textureMask, i))
                {
                    if (PreviousTextureState[i].TexturePtr != textures[i].GlTextureID)
                    {
                        //go to texture unit i
                        GL.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + i));
                        
                        //bind the texture to it
                        GL.BindTexture(textures[i].textureTarget, textures[i].GlTextureID);
                        hasUpdated = true;
                    }


                    PreviousTextureState[i].TexturePtr = textures[i].GlTextureID;
                    PreviousTextureState[i].textureTarget = textures[i].textureTarget;
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

            //apply local uniforms
            //optimized such that the update uniform wont update the same uniforms with same values as the previously update uniform
            for (int i = 0; i < m_uniformValues.Count; i++)
            {
                var current = m_uniformValues[i];

                //if the uniform value is a default value but we have a global uniform with same name skip it
                if (m_uniformValuesDefaultFlag[i] && mIsGlobalUniform(current.uniformName))
                {
                    continue;
                }

                var type = current.UniformType;
                if(current.uniformLocation == -1)
                {
                    var temp = current;
                    temp.uniformLocation = Program.GetUniformLocation(current.uniformName);
                    m_uniformValues[i] = temp;
                    current = temp;
                }

                if (checkIfPreviousUniformUpdatePushed(current))
                {
                    continue;
                }

                hasUpdated = true;
                //send local uniform to GPU
                SendUniformDataToGPU(current.uniformLocation, type, current.value);
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

        //##################### CACHED LOCAL UNIFORM VALUES #################################
        /// <summary>
        /// These store data about this instance's local uniforms (non global uniforms)
        /// Dictionary to convert uniform name to index, index that can be used to index uniform value and uniform isdefault flag
        /// uniforms that are flagged as default will be overriden by any global uniforms that contain the same uniform name (ignores type and actual location)
        /// uniforms that are flagged as non-default will override any global uniforms that contain the same uniform name (ignores type and actual location)
        /// </summary>
        //private List<(string, int)> m_cachedUniformValueIndex = new List<(string, int)>();
        private List<UniformValueWithLocation> m_uniformValues = new List<UniformValueWithLocation>();
        private List<bool> m_uniformValuesDefaultFlag = new List<bool>();
        private bool mFindLocalUniformIndex(in string value, out int index)
        {
            for (int i = 0; i < m_uniformValues.Count; i++)
            {
                if (m_uniformValues[i].uniformName == value)
                {
                    index = i;
                    return true;
                }
            }
            index = -1;
            return false;
        }
        //##################################################################################

        static ShaderProgram PreviousProgram { get; set; } = null;

        //##################### CACHED GLOBAL UNIFORM VALUES #################################
        readonly static List<UniformValue> GlobalUniformValues = new();
        readonly static Dictionary<string, int> GlobalUniformIndexCache = new();
        //##################################################################################

        private static bool mFindGlobalUniformIndex(in string uniformName, out int index)
        {
            if(GlobalUniformIndexCache.TryGetValue(uniformName, out index))
            {
                return true;
            }
            return false;
        }
        private bool mIsGlobalUniform(in string name)
        {
            return GlobalUniformIndexCache.ContainsKey(name);
        }
        private static void mSetGlobalUniformValue(in UniformType globalUniformType, in object value, in string uniformName)
        {
            var has = mFindGlobalUniformIndex(uniformName, out int index);
            if (!has)
            {
                GlobalUniformValues.Add(new UniformValue(uniformName, globalUniformType, value));
                GlobalUniformIndexCache.Add(uniformName, GlobalUniformValues.Count-1);
            }
            else
            {
                var val = GlobalUniformValues[index];
                val.value = value;
                GlobalUniformValues[index] = val;
            }
        }
        public static void SetGlobalTexture(string id, Texture texture)
        {
            mSetGlobalUniformValue(UniformType.texture, texture, id);
        }
        public static void SetGlobalMat4(string id, Matrix4 matrix4)
        {
            mSetGlobalUniformValue(UniformType.mat4, matrix4, id);
        }
        public static void SetGlobalVector4(string id, Vector4 value)
        {
            mSetGlobalUniformValue(UniformType.vec4, value, id);
        }
        public static void SetGlobalVector3(string id, Vector3 value)
        {
            mSetGlobalUniformValue(UniformType.vec3, value, id);
        }
        public static void SetGlobalVector2(string id, Vector2 value)
        {
            mSetGlobalUniformValue(UniformType.vec2, value, id);
        }
        public static void SetGlobalFloat(string id, float value)
        {
            mSetGlobalUniformValue(UniformType.Float, value, id);
        }
        public static void SetGlobalInt(string id, int value)
        {
            mSetGlobalUniformValue(UniformType.Int, value, id);
        }
        public static void SetGlobalBool(string id, bool value)
        {
            SetGlobalInt(id, value ? 1 : 0);
        }
        void mPushAllGlobalUniformsToShaderProgram()
        {
            for (int i = 0; i < GlobalUniformValues.Count; i++)
            {
                //Fetch global texture data via update uniforms, not here
                var cur = GlobalUniformValues[i];
                if(cur.uniformType == UniformType.texture)
                {
                    continue;
                }

                //if there is a non default local uniform, then dont push to gpu
                if (mFindLocalUniformIndex(cur.uniformName, out int index))
                {
                    if (m_uniformValuesDefaultFlag[index] == false)
                    {
                        continue;
                    }
                }

                SendUniformDataToGPU(Program.GetUniformLocation(cur.uniformName), cur.uniformType, cur.value);
            }
        }
        private void mFetchGlobalTextures()
        {
            for (int i = 0; i < GlobalUniformValues.Count; i++)
            {
                var cur = GlobalUniformValues[i];
                if(cur.uniformType != UniformType.texture)
                {
                    continue;
                }
                //we operate a little different here, unlike other global uniform values we 
                //actually get the global texture uniform and set it as a local uniform
                //other global uniforms have there own cache space, thats why on their side
                //we check if there are existing local uniforms before we send it to the gpu
                Texture? tex = (Texture)cur.value;
                SetTexture(cur.uniformName, tex);
            }
        }
        private void mAddUniform(string uniformName, UniformType uniformType, object uniformValue, bool isDefault = false)
        {
            if (mFindLocalUniformIndex(uniformName, out int value))
            {
                int index = value;
                var temp = m_uniformValues[index];
                temp.value = uniformValue;
                m_uniformValuesDefaultFlag[index] = isDefault;
                m_uniformValues[index] = temp;
            }
            else
            {
                //m_cachedUniformValueIndex.Add((uniformName, m_uniformValues.Count));
                m_uniformValues.Add(new UniformValueWithLocation(uniformName, uniformType, uniformValue));
                m_uniformValuesDefaultFlag.Add(isDefault);
            }
        }
        public T GetUniformValue<T>(string id)
        {
            if (mFindLocalUniformIndex(id, out int index))
            {
                return (T)m_uniformValues[index].value;
            }
            return default(T);
        }

        public void SetMat4(string id, Matrix4 value, bool isDefault = false)
        {
            mAddUniform(id, UniformType.mat4, value, isDefault);
        }
        public void SetFloat(string id, float value, bool isDefault = false)
        {
            mAddUniform(id, UniformType.Float, value, isDefault);
        }
        public void SetInt(string id, int value, bool isDefault = false)
        {
            mAddUniform(id, UniformType.Int, value, isDefault);
        }
        public void SetBool(string id, bool value, bool isDefault = false)
        {
            SetInt(id, value ? 1 : 0, isDefault);
        }
        public void SetVector4(string id, Vector4 value, bool isDefault = false)
        {
            mAddUniform(id, UniformType.vec4, value, isDefault);

        }
        public void SetVector3(string id, Vector3 value, bool isDefault = false)
        {
            mAddUniform(id, UniformType.vec3, value, isDefault);

        }
        public void SetVector2(string id, Vector2 value, bool isDefault = false)
        {
            mAddUniform(id, UniformType.vec2, value, isDefault);
        }

        public int GetUniformLocation(string id)
        {
            return Program.GetUniformLocation(id);
        }
    }
}
