using JLUtility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public static class SkyboxController
    {
        static Shader shader;
        internal static void Init(Shader SkyBoxShader)
        {
            shader = SkyBoxShader;
            SetMode(Mode.Procedural);
            SetExposure(1.0f);
            SetSkyboxTexture(null);
        }
        public enum Mode
        {
            Texture,
            Procedural,
        }
        public static void SetExposure(float value)
        {
            Shader.SetGlobalFloat(Shader.GetShaderPropertyId(GlobalUniformNames.SkyBoxIntensity), value);
        }
        public static void SetSkyboxTexture(Texture texture)
        {
            Shader.SetGlobalTexture(Shader.GetShaderPropertyId(GlobalUniformNames.SkyBox), texture);
        }
        public static void SetMode(Mode mode)
        {
            if(mode == Mode.Texture)
            {
                shader.SetBool(Shader.GetShaderPropertyId("UseProcedural"), false);
            }
            else
            {
                shader.SetBool(Shader.GetShaderPropertyId("UseProcedural"), true);
            }
        }
    }
}
