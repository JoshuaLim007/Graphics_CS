using OpenTK.Graphics.OpenGL4;
using JLUtility;

namespace JLGraphics
{
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

        struct ShaderFileReferences
        {
            public ShaderFile shaderFile;
            public int referenceCount;
            public ShaderFileReferences(ShaderFile shaderFile)
            {
                this.shaderFile = shaderFile;
                referenceCount = 1;
            }
        }

        readonly static Dictionary<string, ShaderFileReferences> ExistingFragFiles = new Dictionary<string, ShaderFileReferences>();
        readonly static Dictionary<string, ShaderFileReferences> ExistingVertFiles = new Dictionary<string, ShaderFileReferences>();
        readonly static Dictionary<string, ShaderFileReferences> ExistingGeoFiles = new Dictionary<string, ShaderFileReferences>();

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

            if (ExistingFragFiles.ContainsKey(fragPath))
            {
                var temp = ExistingFragFiles[fragPath];
                temp.referenceCount++;
                Frag = temp.shaderFile;
                ExistingFragFiles[fragPath] = temp;
            }
            else
            {
                Frag = new ShaderFile(fragPath, ShaderType.FragmentShader);
                ExistingFragFiles.Add(fragPath, new ShaderFileReferences(Frag));
            }
            if (ExistingVertFiles.ContainsKey(vertPath))
            {
                var temp = ExistingVertFiles[vertPath];
                temp.referenceCount++;
                Vert = temp.shaderFile;
                ExistingVertFiles[vertPath] = temp;
            }
            else
            {
                Vert = new ShaderFile(vertPath, ShaderType.VertexShader);
                ExistingVertFiles.Add(vertPath, new ShaderFileReferences(Vert));
            }
            if (geometryPath.Trim() != "")
            {

                if (ExistingGeoFiles.ContainsKey(geometryPath))
                {
                    var temp = ExistingGeoFiles[geometryPath];
                    temp.referenceCount++;
                    Geo = temp.shaderFile;
                    ExistingGeoFiles[geometryPath] = temp;
                }
                else
                {
                    Geo = new ShaderFile(geometryPath, ShaderType.GeometryShader);
                    ExistingGeoFiles.Add(geometryPath, new ShaderFileReferences(Geo));
                }
            }

            Id = GL.CreateProgram();
            AllShaderPrograms.Add(this);
        }
        public static ShaderProgram CopyProgram(ShaderProgram shaderProgram)
        {
            if(shaderProgram == null)
            {
                throw new ArgumentNullException();
            }
            return new ShaderProgram(shaderProgram);
        }
        private ShaderProgram(ShaderProgram shaderProgram) : this(shaderProgram.Name + "_Clone", shaderProgram.FragFile.FilePath, shaderProgram.VertFile.FilePath, shaderProgram.Geo.FilePath)
        {

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
            if (!Frag.CompileShader())
            {
                Debug.Log("Frag shader compile aborted", Debug.Flag.Warning);
            }
            if (!Vert.CompileShader())
            {
                Debug.Log("Vert shader compile aborted", Debug.Flag.Warning);
            }
            var geoRet = Geo?.CompileShader();
            if (geoRet.HasValue && geoRet.Value == false)
            {
                Debug.Log("Geo shader compile aborted", Debug.Flag.Warning);
            }

            Vert.FileChangeCallback.Add(OnVertFileChangeShaderRecompile);
            Frag.FileChangeCallback.Add(OnFragFileChangeShaderRecompile);
            Geo?.FileChangeCallback.Add(OnGeoFileChangeShaderRecompile);
            UpdateProgram();
            //UpdateProgram called via callback
        }
        List<KeyValuePair<int, ActiveUniformType>> uniformTypes = new List<KeyValuePair<int, ActiveUniformType>>();
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
                int propId = Shader.GetShaderPropertyId(name);
                uniformTypes.Add(new KeyValuePair<int, ActiveUniformType>(propId, type));
                GetUniformLocation(propId);
            }

            var d = GL.GetProgramInfoLog(Id);
            if (d != "")
                Debug.Log(d);
            uniformLocations.Clear();
        }
        public List<KeyValuePair<int, ActiveUniformType>> GetUniformTypes()
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

            var temp = ExistingFragFiles[Frag.FilePath];
            temp.referenceCount--;
            ExistingFragFiles[Frag.FilePath] = temp;
            if (temp.referenceCount == 0)
            {
                ExistingFragFiles.Remove(Frag.FilePath);
                Frag.Dispose();
            }

            temp = ExistingVertFiles[Vert.FilePath];
            temp.referenceCount--;
            ExistingVertFiles[Vert.FilePath] = temp;
            if (temp.referenceCount == 0)
            {
                ExistingVertFiles.Remove(Vert.FilePath);
                Vert.Dispose();
            }

            if (Geo != null)
            {
                temp = ExistingGeoFiles[Geo.FilePath];
                temp.referenceCount--;
                ExistingGeoFiles[Geo.FilePath] = temp;

                if(temp.referenceCount == 0)
                {
                    ExistingGeoFiles.Remove(Geo.FilePath);
                    Geo.Dispose();
                }
            }

            GL.DeleteProgram(Id);
            AllShaderPrograms.Remove(this);
            Vert.FileChangeCallback.Remove(OnVertFileChangeShaderRecompile);
            Frag.FileChangeCallback.Remove(OnFragFileChangeShaderRecompile);
            Geo?.FileChangeCallback.Remove(OnGeoFileChangeShaderRecompile);
        }

        public static int ProgramCounts => AllShaderPrograms.Count;
        Dictionary<int, int> uniformLocations = new Dictionary<int, int>();
        public bool UniformExistsInGLSL(int propertyId)
        {
            for (int i = 0; i < uniformTypes.Count; i++)
            {
                if(uniformTypes[i].Key == propertyId)
                {
                    return true;
                }
            }
            return false;
        }
        public bool isCompiled { get; private set; } = false;
        public int GetUniformLocation(int id)
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
                string name = Shader.ShaderPropertyIdToName(id);
                int loc = GL.GetUniformLocation(Id, name);
                uniformLocations.Add(id, loc);
                return loc;
            }
        }
    }
}
