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
        public bool UniformExistsInGLSL(string uniformName)
        {
            for (int i = 0; i < uniformTypes.Count; i++)
            {
                if(uniformTypes[i].Key == uniformName)
                {
                    return true;
                }
            }
            return false;
        }
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
}
