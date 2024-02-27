﻿using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpenTK.Graphics.OpenGL.GL;

namespace JLGraphics
{
    public class Renderer : Component, IStart
    {
        static internal bool NewRendererAdded { get; set; } = false;
        public Shader Material { get; set; } = null;
        public Mesh Mesh { get; set; } = null;
        public Renderer()
        {
            InternalGlobalScope<Renderer>.Values.Add(this);
        }
        public Renderer(Shader material, Mesh mesh)
        {
            Mesh = mesh;
            Material = material;

            InternalGlobalScope<Renderer>.Values.Add(this);
        }
        protected override void OnClone()
        {
            InternalGlobalScope<Renderer>.Values.Add(this);
        }
        public void Start()
        {
            NewRendererAdded = true;
        }
    }
}
