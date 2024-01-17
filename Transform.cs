using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public class Transform : Component
    {
        private Transform m_parent = null;
        public Transform Parent {
            get
            {
                return m_parent;
            }
            set
            {
                if (value != null)
                {
                    m_parent = value;
                    m_parent.m_children.Add(this);
                }
                else if(m_parent != null)
                {
                    m_parent.m_children.Remove(this);
                    m_parent = value;
                }
            }
        }
        public Transform[] Childs => m_children.ToArray();

        private List<Transform> m_children { get; } = new List<Transform>();

        public bool IsCameraTransform { get; internal set; } = false;

        public Vector3 Forward => CalculateLocalAxis(-Vector4.UnitZ);
        public Vector3 Right => CalculateLocalAxis(Vector4.UnitX);
        public Vector3 Up => CalculateLocalAxis(Vector4.UnitY);

        private Vector3 CalculateLocalAxis(Vector4 direction)
        {
            var temp1 = Vector3.Zero;
            var tempRot = Rotation;
            if (!IsCameraTransform)
            {
                tempRot = Rotation.Inverted();
            }
            var temp = Matrix4.CreateFromQuaternion(tempRot) * direction;
            temp1.X = temp.X;
            temp1.Y = temp.Y;
            temp1.Z = temp.Z;
            return temp1.Normalized();  
        }

        public Vector3 Position { get; set; } = Vector3.Zero;
        public Quaternion Rotation { get; set; } = Quaternion.Identity;
        public Vector3 Scale { get; set; } = Vector3.One;
        public Matrix4 WorldToLocalMatrix => GetWorldToLocalMatrix();
        
        private bool isStatic => Entity.StaticFlag == StaticFlags.StaticDraw;
        private Matrix4 bakedMatrix;
        private bool isMatrixBaked = false;
        private Matrix4 GetWorldToLocalMatrix()
        {
            if (!IsCameraTransform)
            {
                if (isStatic)
                {
                    if (!isMatrixBaked)
                    {
                        bakedMatrix = Matrix4.CreateScale(Scale) * Matrix4.CreateFromQuaternion(Rotation) * Matrix4.CreateTranslation(Position) * (Parent != null ? Parent.WorldToLocalMatrix : Matrix4.Identity);
                        isMatrixBaked = true;
                    }
                    return bakedMatrix;
                }
                else
                {
                    return Matrix4.CreateScale(Scale) * Matrix4.CreateFromQuaternion(Rotation) * Matrix4.CreateTranslation(Position) * (Parent != null ? Parent.WorldToLocalMatrix : Matrix4.Identity);
                }
            }
            else
            {
                return Matrix4.CreateFromQuaternion(Rotation.Inverted()) * Matrix4.CreateTranslation(Position) * (Parent != null ? Parent.WorldToLocalMatrix : Matrix4.Identity);
            }
        }

        public bool IsEnabled()
        {
            return Enabled;
        }

        protected override void SetArgs(params object[] args)
        {
            Parent = (Transform)args[0];
            Position = (Vector3)args[1];
            Rotation = (Quaternion)args[2];
            Scale = (Vector3)args[3];
        }

        public Matrix4 LocalToWorldMatrix => Matrix4.Invert(WorldToLocalMatrix);
    }
}
