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
        public int propertyId { get; }
        public string uniformName { get; }
        public object value { get; set; }
        public UniformType uniformType { get; }
        public UniformValue(int propId, string uniformName, UniformType uniformType, object value)
        {
            propertyId = propId;
            this.uniformType = uniformType;
            this.uniformName = uniformName;
            this.value = value;
        }
        public static implicit operator UniformValue(UniformValueWithLocation uniformValue) => new UniformValue(uniformValue.propertyId, uniformValue.uniformName, uniformValue.UniformType, uniformValue.value);
    }
    public struct UniformValueWithLocation
    {
        public int propertyId { get; }
        public string uniformName { get; }
        public object value { get; set; }
        public UniformType UniformType { get; }
        public int uniformLocation { get; set; }
        public UniformValueWithLocation(int propertyId, string name, UniformType UniformType, object value)
        {
            this.propertyId = propertyId;
            this.uniformName = name;
            this.UniformType = UniformType;
            this.value = value;
            uniformLocation = -1;
        }
        public static implicit operator UniformValueWithLocation(UniformValue uniformValue) => new UniformValueWithLocation(uniformValue.propertyId, uniformValue.uniformName, uniformValue.uniformType, uniformValue.value);
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
        private int[] texturePropertyIds = new int[TotalTextures];
        Stack<int> availableTextureSlots = new Stack<int>();
        int textureIndexFromUniform(int propertyId)
        {
            for (int i = TotalTextures - 1; i >= 0; i--)
            {
                if (texturePropertyIds[i] == propertyId)
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
        bool mIsWithinShader = false;
        internal void SetTextureUnsafe(int propertyId, Texture texture, TextureTarget? textureTarget = null)
        {
            int textureIndex = textureIndexFromUniform(propertyId);

            if (textureTarget != null)
                texture.textureTarget = textureTarget.Value;

            //add new texture if we dont have that texture slot added yet
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
                texturePropertyIds[textureIndex] = propertyId;
            }

            textures[textureIndex] = texture;
            if (texture != null)
            {
                SetInt(propertyId, textureIndex);
                set_int_bool(textureIndex, true, ref textureMask);
            }
            else
            {
                texturePropertyIds[textureIndex] = 0;
                set_int_bool(textureIndex, false, ref textureMask);
                availableTextureSlots.Push(textureIndex);
            }
        }
        static Texture DefaultTexture = null;
        private void SetDefaultTexture()
        {
            if (DefaultTexture == null)
            {
                DefaultTexture = ImageTexture.LoadTextureFromPath("./Textures/1x1_white.bmp");
                DefaultTexture.ResolveTexture();
            }
            var types = Program.GetUniformTypes();
            for (int i = 0; i < types.Count; i++)
            {
                if (types[i].Value == ActiveUniformType.Sampler2D)
                {
                    SetTexture(types[i].Key, DefaultTexture);
                }
            }
        }
        public void SetTexture(int propertyId, Texture texture)
        {
            //if (!mIsWithinShader)
            //{
            //    GL.UseProgram(Program);
            //}
            if (texture != null)
            {
                SetTextureUnsafe(propertyId, texture);
            }
            else
            {
                SetTextureUnsafe(propertyId, DefaultTexture);
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
                    SetTexture(texturePropertyIds[i], textures[i]);
                }
            }
        }
        void CopyUniforms(Shader other)
        {
            m_uniformValues = new List<UniformValueWithLocation>(other.m_uniformValues);
            m_uniformValuesDefaultFlag = new List<bool>(other.m_uniformValuesDefaultFlag);
            m_uniformValues_cache = new Dictionary<int, int>(other.m_uniformValues_cache);

            availableTextureSlots = new Stack<int>(new Stack<int>(other.availableTextureSlots));

            Array.Copy(other.textures, textures, TotalTextures);
            Array.Copy(other.texturePropertyIds, texturePropertyIds, TotalTextures);
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
            for (int i = 0; i < TotalTextures; i++)
            {
                availableTextureSlots.Push(i);
            }
            init();
        }
        void ShaderReload()
        {
            Debug.Log("\tShader Reload.. Rebinding Textures, Setting uniforms for program " + Program.Id + " for material " + Name);
            SetInt(GetShaderPropertyId("textureMask"), textureMask);
            UseProgram();
            SetAllTextureUnitToUniform();
            AttachShaderForRendering();
            Unbind();
            return;
        }
        void init()
        {
            Program.OnShaderReload += ShaderReload;
            SetInt(GetShaderPropertyId("textureMask"), textureMask);
            SetDefaultTexture();
        }
        ~Shader()
        {
            Program.OnShaderReload -= ShaderReload;
            AllInstancedShaders.Remove(myWeakRef);
            myWeakRef = null;
        }
        bool initialDefaultValueSet = false;
        void SetDefaultValue(ActiveUniformType activeUniformType, int propertyId)
        {
            switch (activeUniformType)
            {
                case ActiveUniformType.Int:
                    SetInt(propertyId, 0, true);
                    break;
                case ActiveUniformType.Float:
                    SetFloat(propertyId, 0.0f, true);
                    break;
                case ActiveUniformType.FloatVec2:
                    SetVector2(propertyId, new Vector2(0,0), true);
                    break;
                case ActiveUniformType.FloatVec3:
                    SetVector3(propertyId, new Vector3(0, 0, 0), true);
                    break;
                case ActiveUniformType.FloatVec4:
                    SetVector4(propertyId, new Vector4(0, 0, 0, 0), true);
                    break;
                case ActiveUniformType.Bool:
                    SetVector4(propertyId, new Vector4(0, 0, 0, 0), true);
                    break;
                case ActiveUniformType.FloatMat4:
                    SetMat4(propertyId, Matrix4.Zero, true);
                    break;
                default:
                    SetInt(propertyId, 0, true);
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
        readonly static TextureBindState[] PreviousTextureState = new TextureBindState[TotalTextures];
        readonly static Dictionary<int, object> PreviousUniformState = new Dictionary<int, object>();
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
            if (PreviousUniformState.TryGetValue(current.uniformLocation, out var prevState))
            {
                if (PreviousProgram == Program && prevState.Equals(current.value))
                {
                    return true;
                }
                else
                {
                    PreviousUniformState[current.uniformLocation] = current.value;
                }
            }
            else
            {
                PreviousUniformState[current.uniformLocation] = current.value;
            }
            return false;
        }
        internal int AttachShaderForRendering()
        {
            if (Program.Disposed)
            {
                Debug.Log("Shader program has been dispoed!", Debug.Flag.Error);
                return 0;
            }

            bool hasUpdateProgram = false;
            if (PreviousProgram != Program)
            {
                UseProgram();
                hasUpdateProgram = true;
            }

            bool hasUpdated = false;
            mIsWithinShader = true;
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
                if (m_uniformValuesDefaultFlag[i] && mIsGlobalUniform(current.propertyId))
                {
                    continue;
                }

                var type = current.UniformType;
                if(current.uniformLocation == -1)
                {
                    var temp = current;
                    temp.uniformLocation = Program.GetUniformLocation(current.propertyId);

                    //check if uniform exists, if it doesnt, skip
                    if(temp.uniformLocation < 0)
                    {
                        continue;
                    }
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
            mIsWithinShader = false;
            return (hasUpdated ? 1 : 0) << 1 | (hasUpdateProgram ? 1 : 0);
        }
        internal static void Unbind()
        {
            GL.UseProgram(0);
        }

        //##################### CACHED LOCAL UNIFORM VALUES #################################
        /// <summary>
        /// These store data about this instance's local uniforms (non global uniforms)
        /// Dictionary to convert uniform name to index, index that can be used to index uniform value and uniform isdefault flag
        /// uniforms that are flagged as default will be overriden by any global uniforms that contain the same uniform name (ignores type and actual location)
        /// uniforms that are flagged as non-default will override any global uniforms that contain the same uniform name (ignores type and actual location)
        /// </summary>
        private List<UniformValueWithLocation> m_uniformValues = new List<UniformValueWithLocation>();
        private Dictionary<int, int> m_uniformValues_cache = new();
        private List<bool> m_uniformValuesDefaultFlag = new List<bool>();
        private bool mFindLocalUniformIndex(in int propId, out int index)
        {
            if(m_uniformValues_cache.TryGetValue(propId, out index))
            {
                return true;
            }
            return false;
        }
        //##################################################################################

        internal static ShaderProgram PreviousProgram { get; set; } = null;

        //##################### CACHED GLOBAL UNIFORM VALUES #################################
        readonly static List<UniformValue> GlobalUniformValues = new();
        readonly static Dictionary<int, int> GlobalUniformIndexCache = new();
        static int GlobalUniformPropertyIdCounter = 1;
        static Dictionary<string, int> NameToIdCache = new();
        static Dictionary<int, string> IdToNameCache = new();
        //##################################################################################
        static public int GetShaderPropertyId(string uniformName)
        {
            if(NameToIdCache.TryGetValue(uniformName, out int value))
            {
                return value;
            }
#if DEBUG
            uint tempValue = (uint)GlobalUniformPropertyIdCounter;
            if(tempValue == uint.MaxValue)
            {
                throw new OverflowException("No more shader properties available!");
            }
#endif
            NameToIdCache.Add(uniformName, GlobalUniformPropertyIdCounter);
            IdToNameCache.Add(GlobalUniformPropertyIdCounter, uniformName);
            return GlobalUniformPropertyIdCounter++;
        }
        static public string ShaderPropertyIdToName(int id)
        {
            if (IdToNameCache.TryGetValue(id, out var value))
            {
                return value;
            }
            throw new Exception("Property Id not found!");
        }

        private static bool mFindGlobalUniformIndex(in int uniformId, out int index)
        {
            if(GlobalUniformIndexCache.TryGetValue(uniformId, out index))
            {
                return true;
            }
            return false;
        }
        private bool mIsGlobalUniform(int propertyId)
        {
            return GlobalUniformIndexCache.ContainsKey(propertyId);
        }
        private static void mSetGlobalUniformValue(in UniformType globalUniformType, in object value, in int uniformId)
        {
            var has = mFindGlobalUniformIndex(uniformId, out int index);
            if (!has)
            {
                GlobalUniformValues.Add(new UniformValue(uniformId, ShaderPropertyIdToName(uniformId), globalUniformType, value));
                GlobalUniformIndexCache.Add(uniformId, GlobalUniformValues.Count-1);
            }
            else
            {
                var val = GlobalUniformValues[index];
                val.value = value;
                GlobalUniformValues[index] = val;
            }
        }
        public static void SetGlobalTexture(int id, Texture texture)
        {
            mSetGlobalUniformValue(UniformType.texture, texture, id);
        }
        public static void SetGlobalMat4(int id, Matrix4 matrix4)
        {
            mSetGlobalUniformValue(UniformType.mat4, matrix4, id);
        }
        public static void SetGlobalVector4(int id, Vector4 value)
        {
            mSetGlobalUniformValue(UniformType.vec4, value, id);
        }
        public static void SetGlobalVector3(int id, Vector3 value)
        {
            mSetGlobalUniformValue(UniformType.vec3, value, id);
        }
        public static void SetGlobalVector2(int id, Vector2 value)
        {
            mSetGlobalUniformValue(UniformType.vec2, value, id);
        }
        public static void SetGlobalFloat(int id, float value)
        {
            mSetGlobalUniformValue(UniformType.Float, value, id);
        }
        public static void SetGlobalInt(int id, int value)
        {
            mSetGlobalUniformValue(UniformType.Int, value, id);
        }
        public static void SetGlobalBool(int id, bool value)
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
                if (mFindLocalUniformIndex(cur.propertyId, out int index))
                {
                    if (m_uniformValuesDefaultFlag[index] == false)
                    {
                        continue;
                    }
                }

                int uniformLocation = Program.GetUniformLocation(cur.propertyId);
                SendUniformDataToGPU(uniformLocation, cur.uniformType, cur.value);
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
                SetTexture(cur.propertyId, tex);
            }
        }
        private void mAddUniform(int propertyId, UniformType uniformType, object uniformValue, bool isDefault = false)
        {
            if (mFindLocalUniformIndex(propertyId, out int value))
            {
                int index = value;
                var temp = m_uniformValues[index];
                temp.value = uniformValue;
                m_uniformValuesDefaultFlag[index] = isDefault;
                m_uniformValues[index] = temp;
            }
            else
            {
                m_uniformValues_cache.Add(propertyId, m_uniformValues.Count);
                m_uniformValues.Add(new UniformValueWithLocation(propertyId, ShaderPropertyIdToName(propertyId), uniformType, uniformValue));
                m_uniformValuesDefaultFlag.Add(isDefault);
            }
        }

        public void SetMat4(int id, Matrix4 value, bool isDefault = false)
        {
            mAddUniform(id, UniformType.mat4, value, isDefault);
        }
        public void SetFloat(int id, float value, bool isDefault = false)
        {
            mAddUniform(id, UniformType.Float, value, isDefault);
        }
        public void SetInt(int id, int value, bool isDefault = false)
        {
            mAddUniform(id, UniformType.Int, value, isDefault);
        }
        public void SetBool(int id, bool value, bool isDefault = false)
        {
            SetInt(id, value ? 1 : 0, isDefault);
        }
        public void SetVector4(int id, Vector4 value, bool isDefault = false)
        {
            mAddUniform(id, UniformType.vec4, value, isDefault);

        }
        public void SetVector3(int id, Vector3 value, bool isDefault = false)
        {
            mAddUniform(id, UniformType.vec3, value, isDefault);

        }
        public void SetVector2(int id, Vector2 value, bool isDefault = false)
        {
            mAddUniform(id, UniformType.vec2, value, isDefault);
        }

        public int GetUniformLocation(string id)
        {
            int d = GetShaderPropertyId(id);
            return Program.GetUniformLocation(d);
        }
        public int GetUniformLocation(int propertyId)
        {
            if(mFindLocalUniformIndex(propertyId, out int index))
            {
                return m_uniformValues[index].uniformLocation;
            }
            return -1;
        }
    }
}
