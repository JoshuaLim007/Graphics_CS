using JLGraphics.Components;
using JLGraphics.Utility.GuiAttributes;
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

        public static Vector3[] GetCorners(Matrix4 mat)
        {
            Vector3[] corners = { 
                //far corners
                new Vector3(-1, 1, 1),
                new Vector3(1, 1, 1),
                new Vector3(-1, -1, 1),
                new Vector3(1, -1, 1),

                //near corners
                new Vector3(-1, 1, 0),
                new Vector3(1, 1, 0),
                new Vector3(-1, -1, 0),
                new Vector3(1, -1, 0),

            };

            for (int i = 0; i < corners.Length; i++)
            {
                Vector4 temp = new Vector4(corners[i].X, corners[i].Y, corners[i].Z, 1);
                temp = temp * mat;
                temp /= temp.W;
                corners[i] = temp.Xyz;
            }

            return corners;
        }

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
        [Gui("Frustum Cull")]
        public bool FrustumCull { get; set; } = true;

        float fov = 90;
        [Gui("Perspective FOV")]
        public float Fov {
            get => fov;
            set
            {
                fov = MathHelper.Clamp(value, 1.0f, 179.0f);
            }
        }

        public int Width => Graphics.Instance.GetRenderSize().X;
        public int Height => Graphics.Instance.GetRenderSize().Y;

        float near = 0.1f;
        [Gui("Near")]
        public float Near {
            get => near;
            set
            {
                near = MathHelper.Clamp(value, 0.01f, float.PositiveInfinity);
                if(far <= near)
                {
                    far = near + 0.01f;
                }
            }
        }

        float far = 1000.0f;
        [Gui("Far")]
        public float Far
        {
            get => far;
            set
            {
                far = MathHelper.Clamp(value, 0.01f, float.PositiveInfinity);
                if (far <= near)
                {
                    far = near + 0.01f;
                }
            }
        }

        float size = 100;
        [Gui("Orthographics Half Size")]
        public float Size
        {
            get => size;
            set
            {
                size = MathHelper.Clamp(value, 0.01f, float.PositiveInfinity);
            }
        }
        public static Camera Main { get; set; } = null;
        public Matrix4 ViewMatrix =>
            (Transform.Parent != null ? Transform.Parent.ModelMatrix.Inverted() : Matrix4.Identity) *
            Matrix4.CreateTranslation(-Transform.LocalPosition) * 
            Matrix4.CreateFromQuaternion(Transform.LocalRotation);

        public Matrix4 ProjectionMatrix => calculateProjectionMatrix();

        [Gui("Camera Mode")]
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
            InternalGlobalScope<Camera>.Values.Remove(this);
        }
        bool useOverride = false;
        Matrix4 overrideProjection;
        public void OverrideProjectionMatrix(Matrix4 projectionMatrix)
        {
            overrideProjection = projectionMatrix;
            useOverride = true;
        }
        public void UseDefaultProjectionMatrix()
        {
            useOverride = false;
        }
        private Matrix4 calculateProjectionMatrix()
        {
            if (useOverride)
            {
                return overrideProjection;
            }
            return CameraMode == CameraType.Perspecitve 
                ? Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(Fov), (float)Width / (float)Height, Near, Far) 
                : Matrix4.CreateOrthographicOffCenter(-Size, Size, -Size * Height / Width, Size * Height / Width, Near, Far);
        }
    }
}
