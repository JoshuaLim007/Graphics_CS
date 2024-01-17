using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpenTK.Graphics.OpenGL.GL;

namespace JLGraphics
{
    public class Renderer : Component
    {
        public Shader Material { get; set; } = null;
        public Mesh Mesh { get; set; } = null;
        public Renderer()
        {

        }
        public Renderer(Shader material, Mesh mesh)
        {
            Mesh = mesh;
            Material = material;
        }
    }
}
