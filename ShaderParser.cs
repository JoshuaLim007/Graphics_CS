using OpenTK.Graphics.OpenGL4;
using JLUtility;
using System.Text.RegularExpressions;

namespace JLGraphics
{
    public sealed class ShaderParser
    {
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
            var includeStatement = Regex.Match(data, "#include *\"(.*)\"", RegexOptions.Multiline);
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
        
        public static string ParseShaderPreDefines(string initialShaderCode, string debugFilePath = "")
        {
            var originalDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory("./Assets/Shaders");
            var includeStatement = Regex.Match(initialShaderCode, "#include *\"(.*)\"", RegexOptions.Multiline);
            if (includeStatement.Length == 0)
            {
                Directory.SetCurrentDirectory(originalDir);
                return initialShaderCode;
            }

            while (includeStatement != null)
            {
                if (includeStatement.Length == 0)
                {
                    break;
                }
                var includeFile = mHandleInclude(includeStatement.Groups[1].Value.Trim(), debugFilePath);
                initialShaderCode = initialShaderCode.Replace(includeStatement.Value, includeFile);
                includeStatement = includeStatement.NextMatch();
            }

            Directory.SetCurrentDirectory(originalDir);
            return initialShaderCode;
        }

    }
}
