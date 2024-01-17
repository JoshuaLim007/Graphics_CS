using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public sealed class Camera : Component
    {
        public bool EnabledWireFrame { get; set; } = false;

        public float Fov { get; set; } = 90;
        public float Width { get; set; } = 1280;
        public float Height { get; set; } = 720;
        public float Near { get; set; } = 0.03f;
        public float Far { get; set; } = 1000.0f;
        public float Size { get; set; } = 100;

        public Matrix4 ViewMatrix =>
            (Transform.Parent != null ? Transform.Parent.WorldToLocalMatrix.Inverted() : Matrix4.Identity) *
            Matrix4.CreateTranslation(-Transform.Position) * 
            Matrix4.CreateFromQuaternion(Transform.Rotation);

        public Matrix4 ProjectionMatrix => calculateProjectionMatrix();

        public CameraType cameraType = CameraType.Perspecitve;

        public enum CameraType
        {
            Perspecitve,
            Orthographic
        }

        protected override void SetArgs(params object[] args)
        {
            Transform.IsCameraTransform = true;
            InternalGlobalScope<Camera>.Values.Add(this);
        }
        internal override void InternalImmediateDestroy()
        {
            base.InternalImmediateDestroy();
            InternalGlobalScope<Camera>.Values.Remove(this);
        }
        private Matrix4 calculateProjectionMatrix()
        {
            return cameraType == CameraType.Perspecitve ? Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(Fov), Width / Height, Near, Far) : Matrix4.CreateOrthographicOffCenter(-Size, Size, -Size * Height / Width, Size * Height / Width, Near, Far);
        }
    }
}
