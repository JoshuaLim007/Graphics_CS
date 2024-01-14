using Assimp.Unmanaged;
using ObjLoader.Loader.Data.VertexData;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace JLGraphics
{
    public sealed class Shader
    {
        public string Name { get; }
        public int ProgramId { get; }
        public string FragmentShaderPath { get; }
        public string VertexShaderPath { get; }

        private int textureMask = 0;
        private Texture[] textures = new Texture[32];

        public Texture GetTexture(int textureIndex)
        {
            return textures[textureIndex];
        }
        public void SetTexture(int textureIndex, string uniformName, Texture texture)
        {
            var previousProgram = GL.GetInteger(GetPName.CurrentProgram);
            GL.UseProgram(ProgramId);

            textures[textureIndex] = texture;
            
            if (texture != null)
            {
                textureMask |= 1 << textureIndex;
            }
            else
            {
                textureMask &= ~(1 << textureIndex);
            }

            var texLoc = GL.GetUniformLocation(ProgramId, uniformName);
            GL.Uniform1(texLoc, textureIndex);

            GL.UseProgram(previousProgram);
        }
        public void SetTexture(int textureIndex, string uniformName, int texturePtr)
        {
            var previousProgram = GL.GetInteger(GetPName.CurrentProgram);
            GL.UseProgram(ProgramId);

            textures[textureIndex] = (Texture)texturePtr;

            if (texturePtr != 0)
            {
                textureMask |= 1 << textureIndex;
            }
            else
            {
                textureMask &= ~(1 << textureIndex);
            }

            var texLoc = GL.GetUniformLocation(ProgramId, uniformName);
            GL.Uniform1(texLoc, textureIndex);

            GL.UseProgram(previousProgram);
        }

        static readonly List<Shader> m_shaderInstances = new List<Shader>();
        
        

        public Shader(Shader shader)
        {
            FragmentShaderPath = shader.FragmentShaderPath;
            VertexShaderPath = shader.VertexShaderPath;
            Name = shader.Name + "_clone";
            int vshader = GL.CreateShader(ShaderType.VertexShader);
            int fshader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(vshader, File.ReadAllText(VertexShaderPath));
            GL.ShaderSource(fshader, File.ReadAllText(FragmentShaderPath));
            GL.CompileShader(vshader);
            GL.CompileShader(fshader);
            string d = GL.GetShaderInfoLog(vshader);
            if(d != "")
                Console.WriteLine(d);
            d = GL.GetShaderInfoLog(fshader);
            if (d != "")
                Console.WriteLine(d);
            int program = GL.CreateProgram();
            GL.AttachShader(program, vshader);
            GL.AttachShader(program, fshader);
            GL.LinkProgram(program);
            GL.DetachShader(program, vshader);
            GL.DetachShader(program, fshader);
            GL.DeleteShader(vshader);
            GL.DeleteShader(fshader);
            d = GL.GetProgramInfoLog(program);
            if(d != "")
                Console.WriteLine(d);
            ProgramId = program;
            m_shaderInstances.Add(this);
        }

        public Shader(string name, string fragmentShader, string vertexShader)
        {
            FragmentShaderPath = fragmentShader;
            VertexShaderPath = vertexShader;
            Name = name;
            int vshader = GL.CreateShader(ShaderType.VertexShader);
            int fshader = GL.CreateShader(ShaderType.FragmentShader);

            if (!File.Exists(fragmentShader))
            {
                Console.WriteLine("Fragment Shader not found: " + fragmentShader);
                return;
            }
            if (!File.Exists(vertexShader))
            {
                Console.WriteLine("Vertex Shader not found: " + vertexShader);
                return;
            }

            GL.ShaderSource(vshader, File.ReadAllText(vertexShader));
            GL.ShaderSource(fshader, File.ReadAllText(fragmentShader));
            GL.CompileShader(vshader);
            GL.CompileShader(fshader);
            string d = GL.GetShaderInfoLog(vshader);
            if (d != "")
                Console.WriteLine(d);
            d = GL.GetShaderInfoLog(fshader);
            if (d != "")
                Console.WriteLine(d);
            int program = GL.CreateProgram();
            GL.AttachShader(program, vshader);
            GL.AttachShader(program, fshader);
            GL.LinkProgram(program);
            GL.DetachShader(program, vshader);
            GL.DetachShader(program, fshader);
            GL.DeleteShader(vshader);
            GL.DeleteShader(fshader);
            d = GL.GetProgramInfoLog(program);
            if (d != "")
                Console.WriteLine(d);
            ProgramId = program;
            m_shaderInstances.Add(this);
        }
        ~Shader()
        {
            m_shaderInstances.Remove(this);
            GL.DeleteShader(ProgramId);
        }
        public static Shader FindShader(string name)
        {
            for (int i = 0; i < m_shaderInstances.Count; i++)
            {
                if(m_shaderInstances[i].Name != name)
                {
                    continue;
                }
                return m_shaderInstances[i];
            }
            return null;
        }

        /// <summary>
        /// Expensive, try to batch this with other meshes with same materials.
        /// Applies material unique uniforms.
        /// </summary>
        public void UseProgram()
        {
            GL.UseProgram(ProgramId);

            //apply all material specific uniforms
            for (int i = 0; i < 32; i++)
            {
                if(((textureMask >> i) & 1) == 1)
                {
                    GL.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + i));
                    GL.BindTexture(TextureTarget.Texture2D, textures[i].textureID);
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
        public static void Unbind()
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
        private readonly List<UniformValue> m_uniformValues = new List<UniformValue>();
        private static int m_prevProgram;

        public static void SetGlobalMat4(string id, Matrix4 matrix4)
        {
            for (int i = 0; i < m_shaderInstances.Count; i++)
            {
                GL.UseProgram(m_shaderInstances[i].ProgramId);
                GL.UniformMatrix4(m_shaderInstances[i].GetUniformLocation(id), false, ref matrix4);
            }
        }
        public static void SetGlobalVector4(string id, Vector4 value)
        {
            for (int i = 0; i < m_shaderInstances.Count; i++)
            {
                GL.UseProgram(m_shaderInstances[i].ProgramId);
                GL.Uniform4(m_shaderInstances[i].GetUniformLocation(id), value);
            }
        }
        public static void SetGlobalVector3(string id, Vector3 value)
        {
            for (int i = 0; i < m_shaderInstances.Count; i++)
            {
                GL.UseProgram(m_shaderInstances[i].ProgramId);
                GL.Uniform3(m_shaderInstances[i].GetUniformLocation(id), value);
            }
        }
        public static void SetGlobalVector2(string id, Vector2 value)
        {
            for (int i = 0; i < m_shaderInstances.Count; i++)
            {
                GL.UseProgram(m_shaderInstances[i].ProgramId);
                GL.Uniform2(m_shaderInstances[i].GetUniformLocation(id), value);
            }
        }
        public static void SetGlobalFloat(string id, float value)
        {
            for (int i = 0; i < m_shaderInstances.Count; i++)
            {
                GL.UseProgram(m_shaderInstances[i].ProgramId);
                GL.Uniform1(m_shaderInstances[i].GetUniformLocation(id), value);
            }
        }
        public static void SetGlobalInt(string id, int value)
        {
            for (int i = 0; i < m_shaderInstances.Count; i++)
            {
                GL.UseProgram(m_shaderInstances[i].ProgramId);
                GL.Uniform1(m_shaderInstances[i].GetUniformLocation(id), value);
            }
        }
        public static void SetGlobalBool(string id, bool value)
        {
            SetGlobalInt(id, value ? 1 : 0);
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
                int loc = GL.GetUniformLocation(ProgramId,id);
                m_cachedUniformLocations.Add(id, loc);
                return loc;
            }
        }

    }
}
