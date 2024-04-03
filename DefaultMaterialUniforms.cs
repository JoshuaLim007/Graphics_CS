using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public class DefaultMaterialUniforms
    {
        private DefaultMaterialUniforms() { }
        public const string MainTexture = "AlbedoTex";
        public const string AlbedoColor = "AlbedoColor";
        public const string NormalTexture = "NormalTex";
        public const string MAOS = "MAOSTex";
        public const string EmissionTexture = "EmissionTex";
        public const string EmissiveColor = "EmissiveColor";
        public const string Smoothness = "Smoothness";
        public const string Metalness = "Metalness";
        public const string NormalsStrength = "NormalStrength";
        public const string AOStrength = "AoStrength";
        public const string UvScale = "UvScale";
    }
}
