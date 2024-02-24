using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public class Transform : Component, IStart
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

        Vector3 pos = Vector3.Zero;
        public Vector3 Position {
            get
            {
                return pos;
            }
            set
            {
                HasChanged = true;
                pos = value;
            }
        }

        Quaternion rot = Quaternion.Identity;
        public Quaternion Rotation {
            get
            {
                return rot;
            }
            set
            {
                HasChanged = true;
                rot = value;
            } 
        }

        Vector3 scale = Vector3.One;
        public Vector3 Scale { 
            get
            {
                return scale;
            }
            set
            {
                HasChanged = true;
                scale = value;
            }
        }

        public Matrix4 WorldToLocalMatrix => GetWorldToLocalMatrix();
        
        private bool isStatic => Entity.StaticFlag == StaticFlags.StaticDraw;
        private Matrix4 bakedMatrix;
        private bool isMatrixBaked = false;

        bool hasChanged = true;
        public bool HasChanged {
            get { 
                if(Parent != null)
                {
                    return Parent.HasChanged | hasChanged;
                }
                return hasChanged;
            }
            set {
                if(value == true)
                {
                    hasChanged = value;
                    return;
                }

                if(Parent != null)
                {
                    Parent.HasChanged = false;
                }
                hasChanged = value;
            }
        }

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
                    if (HasChanged)
                    {
                        bakedMatrix = Matrix4.CreateScale(Scale) * Matrix4.CreateFromQuaternion(Rotation) * Matrix4.CreateTranslation(Position) * (Parent != null ? Parent.WorldToLocalMatrix : Matrix4.Identity);
                        HasChanged = false;
                        return bakedMatrix;
                    }
                    return bakedMatrix;
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

        protected override void OnCreate(params object[] args)
        {
            Parent = (Transform)args[0];
            Position = (Vector3)args[1];
            Rotation = (Quaternion)args[2];
            Scale = (Vector3)args[3];
        }

        public void Start()
        {
            PreviousWorldToLocalMatrix = WorldToLocalMatrix;
        }

        public Matrix4 LocalToWorldMatrix => Matrix4.Invert(WorldToLocalMatrix);

        internal Matrix4 PreviousWorldToLocalMatrix { get; set; }
    }
}
