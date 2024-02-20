using OpenTK.Graphics.OpenGL4;
using JLUtility;
using System.Text.RegularExpressions;

namespace JLGraphics
{
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
}
