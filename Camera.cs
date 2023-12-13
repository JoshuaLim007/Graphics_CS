using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public sealed class Camera
    {
        public bool EnabledWireFrame { get; set; } = false;

        public Transform Transform { get; }
        public float Fov { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float Near { get; set; }
        public float Far { get; set; }
        public float Size { get; set; }

        public Matrix4 ViewMatrix =>
            (Transform.Parent != null ? Transform.Parent.WorldToLocalMatrix.Inverted() : Matrix4.Identity) *
            Matrix4.CreateTranslation(-Transform.Position) * 
            Matrix4.CreateFromQuaternion(Transform.Rotation);

        public Matrix4 ProjectionMatrix => calculateProjectionMatrix();

        public CameraType cameraType;

        public enum CameraType
        {
            Perspecitve,
            Orthographic
        }

        public Camera(CameraType cameraType, Vector3 position, Quaternion rotation)
        {
            this.cameraType = cameraType;
            Transform = new Transform(null, position, rotation, Vector3.One);
            Transform.IsCameraTransform = true;
        }

        private Matrix4 calculateProjectionMatrix()
        {
            return cameraType == CameraType.Perspecitve ? Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(Fov), Width / Height, Near, Far) : Matrix4.CreateOrthographicOffCenter(-Size, Size, -Size * Height / Width, Size * Height / Width, Near, Far);
        }

        public readonly static Camera Default = new(CameraType.Perspecitve, Vector3.Zero, Quaternion.Identity) { Fov = 90, Width = 1280, Height = 720, Near = 0.01f, Far = 1000.0f };
        public readonly static Camera Orthographic = new(CameraType.Orthographic, Vector3.Zero, Quaternion.Identity) { Height = 720, Width = 1280, Near = 0.01f, Far = 1000.0f, Size = 10f};
    }
}
