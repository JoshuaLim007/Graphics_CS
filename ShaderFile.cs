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
}
