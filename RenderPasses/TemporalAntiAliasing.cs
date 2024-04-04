using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics.RenderPasses
{
    //reference https://alextardif.com/TAA.html
    public class TemporalAntiAliasing : RenderPass
    {
        public TemporalAntiAliasing() : base(RenderQueue.AfterPostProcessing, 0)
        {
        }

        public override string Name => "TAA Jitter";

        float Halton(int i, int b)
        {
            float f = 1.0f;
            float r = 0.0f;

            while (i > 0)
            {
                f /= (float)(b);
                r = r + f * (float)(i % b);
                i = (int)(MathF.Floor((float)(i) / (float)(b)));
            }

            return r;
        }

        Dictionary<Camera, int> CameraFrameCount = new Dictionary<Camera, int>();
        int propertyId = -1;
        int propertyId0 = -1;
        float jitterX = 0, jitterY = 0;
        public override void FrameSetup(Camera camera)
        {
            if(!CameraFrameCount.ContainsKey(camera))
            {
                CameraFrameCount.Add(camera, 0);
            }
            int index = CameraFrameCount[camera];
            if (propertyId == -1)
            {
                propertyId = Shader.GetShaderPropertyId("_TaaJitter");
                propertyId0 = Shader.GetShaderPropertyId("_PrevTaaJitter");
            }

            float haltonX = 2.0f * Halton(index + 1, 2) - 1.0f;
            float haltonY = 2.0f * Halton(index + 1, 3) - 1.0f;

            Shader.SetGlobalVector2(propertyId0, new OpenTK.Mathematics.Vector2(jitterX, jitterY));
            jitterX = (haltonX / camera.Width);
            jitterY = (haltonY / camera.Height);
            Shader.SetGlobalVector2(propertyId, new OpenTK.Mathematics.Vector2(jitterX, jitterY));
            
            //get default projection matrix
            camera.UseDefaultProjectionMatrix();
            
            //modify it
            var mat = camera.ProjectionMatrix;
            mat[2, 0] += jitterX;
            mat[2, 1] += jitterY;
            
            //use override projection matrix
            camera.OverrideProjectionMatrix(mat);
            
            CameraFrameCount[camera] = (index + 1) % 16;
        }

        public override void Execute(in FrameBuffer frameBuffer)
        {
            //nothing
        }

        protected override void OnDispose()
        {
            //nothing
        }
    }
}
