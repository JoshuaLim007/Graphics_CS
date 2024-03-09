using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    internal struct Plane
    {
        internal Vector3 Normal;
        internal float PlaneConstant;
    }
    public struct CameraFrustum
    {
        internal Plane topFace;
        internal Plane bottomFace;
        internal Plane rightFace;
        internal Plane leftFace;
        internal Plane farFace;
        internal Plane nearFace;
        public static CameraFrustum Create(Matrix4 viewProjMat)
        {
            CameraFrustum cameraFrustum = new();
            float length;

            //left face
            cameraFrustum.leftFace.Normal.X =       viewProjMat.M14 + viewProjMat.M11;
            cameraFrustum.leftFace.Normal.Y =       viewProjMat.M24 + viewProjMat.M21;
            cameraFrustum.leftFace.Normal.Z =       viewProjMat.M34 + viewProjMat.M31;
            cameraFrustum.leftFace.PlaneConstant =  viewProjMat.M44 + viewProjMat.M41;
            
            length = cameraFrustum.leftFace.Normal.Length;
            cameraFrustum.leftFace.Normal.X /= length;
            cameraFrustum.leftFace.Normal.Y /= length;
            cameraFrustum.leftFace.Normal.Z /= length;
            cameraFrustum.leftFace.PlaneConstant /= length;

            //right face
            cameraFrustum.rightFace.Normal.X =      viewProjMat.M14 - viewProjMat.M11;
            cameraFrustum.rightFace.Normal.Y =      viewProjMat.M24 - viewProjMat.M21;
            cameraFrustum.rightFace.Normal.Z =      viewProjMat.M34 - viewProjMat.M31;
            cameraFrustum.rightFace.PlaneConstant = viewProjMat.M44 - viewProjMat.M41;

            length = cameraFrustum.rightFace.Normal.Length;
            cameraFrustum.rightFace.Normal.X /= length;
            cameraFrustum.rightFace.Normal.Y /= length;
            cameraFrustum.rightFace.Normal.Z /= length;
            cameraFrustum.rightFace.PlaneConstant /= length;

            //top face
            cameraFrustum.topFace.Normal.X =        viewProjMat.M14 - viewProjMat.M12;
            cameraFrustum.topFace.Normal.Y =        viewProjMat.M24 - viewProjMat.M22;
            cameraFrustum.topFace.Normal.Z =        viewProjMat.M34 - viewProjMat.M32;
            cameraFrustum.topFace.PlaneConstant =   viewProjMat.M44 - viewProjMat.M42;

            length = cameraFrustum.topFace.Normal.Length;
            cameraFrustum.topFace.Normal.X /= length;
            cameraFrustum.topFace.Normal.Y /= length;
            cameraFrustum.topFace.Normal.Z /= length;
            cameraFrustum.topFace.PlaneConstant /= length;
            
            //bottom face
            cameraFrustum.bottomFace.Normal.X =         viewProjMat.M14 + viewProjMat.M12;
            cameraFrustum.bottomFace.Normal.Y =         viewProjMat.M24 + viewProjMat.M22;
            cameraFrustum.bottomFace.Normal.Z =         viewProjMat.M34 + viewProjMat.M32;
            cameraFrustum.bottomFace.PlaneConstant =    viewProjMat.M44 + viewProjMat.M42;

            length = cameraFrustum.bottomFace.Normal.Length;
            cameraFrustum.bottomFace.Normal.X /= length;
            cameraFrustum.bottomFace.Normal.Y /= length;
            cameraFrustum.bottomFace.Normal.Z /= length;
            cameraFrustum.bottomFace.PlaneConstant /= length;
            
            //near face
            cameraFrustum.nearFace.Normal.X =       viewProjMat.M13;
            cameraFrustum.nearFace.Normal.Y =       viewProjMat.M23;
            cameraFrustum.nearFace.Normal.Z =       viewProjMat.M33;
            cameraFrustum.nearFace.PlaneConstant =  viewProjMat.M43;

            length = cameraFrustum.nearFace.Normal.Length;
            cameraFrustum.nearFace.Normal.X /= length;
            cameraFrustum.nearFace.Normal.Y /= length;
            cameraFrustum.nearFace.Normal.Z /= length;
            cameraFrustum.nearFace.PlaneConstant /= length;

            //far face
            cameraFrustum.farFace.Normal.X =        viewProjMat.M14 - viewProjMat.M13;
            cameraFrustum.farFace.Normal.Y =        viewProjMat.M24 - viewProjMat.M23;
            cameraFrustum.farFace.Normal.Z =        viewProjMat.M34 - viewProjMat.M33;
            cameraFrustum.farFace.PlaneConstant =   viewProjMat.M44 - viewProjMat.M43;

            length = cameraFrustum.farFace.Normal.Length;
            cameraFrustum.farFace.Normal.X /= length;
            cameraFrustum.farFace.Normal.Y /= length;
            cameraFrustum.farFace.Normal.Z /= length;
            cameraFrustum.farFace.PlaneConstant /= length;

            return cameraFrustum;
        }
    }
    public sealed class Camera : Component
    {
        public bool EnabledWireFrame { get; set; } = false;
        public bool FrustumCull { get; set; } = true;

        public float Fov { get; set; } = 90;
        public int Width => Graphics.Instance.GetRenderWindowSize().X;
        public int Height => Graphics.Instance.GetRenderWindowSize().Y;
        public float Near { get; set; } = 0.03f;
        public float Far { get; set; } = 1000.0f;
        public float Size { get; set; } = 100;
        public static Camera Main { get; set; } = null;
        public Matrix4 ViewMatrix =>
            (Transform.Parent != null ? Transform.Parent.WorldToLocalMatrix.Inverted() : Matrix4.Identity) *
            Matrix4.CreateTranslation(-Transform.Position) * 
            Matrix4.CreateFromQuaternion(Transform.Rotation);

        public Matrix4 ProjectionMatrix => calculateProjectionMatrix();

        public CameraType CameraMode { get; set; } = CameraType.Perspecitve;

        public enum CameraType
        {
            Perspecitve,
            Orthographic
        }

        protected override void OnCreate(params object[] args)
        {
            Transform.IsCameraTransform = true;
            InternalGlobalScope<Camera>.Values.Add(this);
        }
        protected override void InternalOnImmediateDestroy()
        {
            base.InternalOnImmediateDestroy();
            InternalGlobalScope<Camera>.Values.Remove(this);
        }
        private Matrix4 calculateProjectionMatrix()
        {
            return CameraMode == CameraType.Perspecitve ? Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(Fov), (float)Width / (float)Height, Near, Far) : Matrix4.CreateOrthographicOffCenter(-Size, Size, -Size * Height / Width, Size * Height / Width, Near, Far);
        }
    }
}
