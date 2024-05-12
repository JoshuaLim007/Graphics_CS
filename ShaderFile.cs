using OpenTK.Graphics.OpenGL4;

using JLUtility;

namespace JLGraphics
{
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
        string GetShaderString()
        {
            var data = File.ReadAllText(FilePath);
            data = ShaderParser.ParseShaderPreDefines(data, FilePath);
            return data;
        }
        internal bool CompileShader() {
            if(compiledShader != 0)
            {
                return false;
            }

            Debug.Log("Compiling Shader: " + FilePath);
            string data = GetShaderString();


            if (!addedCallback)
            {
                FileChangeCallback.Add(() => {
                    GL.DeleteShader(compiledShader);
                    compiledShader = 0;
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
                return false;
            }

            return true;
        }
        protected override void OnDispose()
        {
            GL.DeleteShader(compiledShader);
        }
    }
}
