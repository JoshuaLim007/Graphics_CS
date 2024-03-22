using JLGraphics.Utility.GuiAttributes;
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
                UpdateChildChangeFlag(this, true);
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

        static internal void UpdateChildChangeFlag(Transform child, bool value)
        {
            child.hasChanged = value;
            for (int i = 0; i < child.m_children.Count; i++)
            {
                UpdateChildChangeFlag(child.m_children[i], value);
            }
        }
        public override void OnGuiChange()
        {
            LocalPosition = pos;
            LocalRotation = rot;
            LocalScale = scale;
        }

        public Transform[] Childs => m_children.ToArray();

        private List<Transform> m_children { get; } = new List<Transform>();

        public bool IsCameraTransform { get; internal set; } = false;

        public Vector3 Forward => CalculateLocalAxis(Vector4.UnitZ);
        public Vector3 Right => CalculateLocalAxis(Vector4.UnitX);
        public Vector3 Up => CalculateLocalAxis(Vector4.UnitY);

        private Vector3 CalculateLocalAxis(Vector4 direction)
        {
            var temp1 = Vector3.Zero;
            var tempRot = LocalRotation;
            if (!IsCameraTransform)
            {
                tempRot = LocalRotation.Inverted();
            }
            var temp = Matrix4.CreateFromQuaternion(tempRot) * Matrix4.CreateScale(1,1,-1) * direction;
            temp1.X = temp.X;
            temp1.Y = temp.Y;
            temp1.Z = temp.Z;
            return temp1.Normalized();  
        }

        [Gui("World Position", true)]
        Vector3 worldPos = Vector3.Zero;
        [Gui("World Rotation", true)]
        Quaternion worldRot = Quaternion.Identity;
        [Gui("World Scale", true)]
        Vector3 worldScale = Vector3.Zero;


        [Gui("Position")]
        Vector3 pos = Vector3.Zero;
        public Vector3 LocalPosition {
            get
            {
                return pos;
            }
            set
            {
                UpdateChildChangeFlag(this, true);
                pos = value;
                worldPos = ModelMatrix.ExtractTranslation();
            }
        }

        [Gui("Rotation")]
        Quaternion rot = Quaternion.Identity;
        public Quaternion LocalRotation {
            get
            {
                return rot;
            }
            set
            {
                UpdateChildChangeFlag(this, true);
                rot = value;
                worldRot = ModelMatrix.ExtractRotation();
            }
        }

        [Gui("Scale")]
        Vector3 scale = Vector3.One;
        public Vector3 LocalScale { 
            get
            {
                return scale;
            }
            set
            {
                UpdateChildChangeFlag(this, true);
                scale = value;
                worldScale = ModelMatrix.ExtractScale();
            }
        }

        public Matrix4 ModelMatrix => GetWorldToLocalMatrix();
        
        private bool isStatic => Entity.StaticFlag == StaticFlags.StaticDraw;
        private Matrix4 bakedMatrix;

        internal bool hasChanged = true;
        public void LookTorwards(Vector3 direction, Vector3 axis)
        {
            var pos = Transform.LocalPosition;
            var forward = pos + direction * 1000;

            var mat = Matrix4.LookAt(pos, forward, axis);
            var rot = mat.ExtractRotation();

            Transform.LocalRotation = rot;
        }
        
        private Matrix4 GetWorldToLocalMatrix()
        {
            if (!IsCameraTransform)
            {
                if (isStatic)
                {
                    hasChanged = false;
                }
                if (hasChanged)
                {
                    if(Parent != null)
                        bakedMatrix = Matrix4.CreateScale(LocalScale) * Matrix4.CreateFromQuaternion(LocalRotation) * Matrix4.CreateTranslation(LocalPosition) * Parent.ModelMatrix;
                    else
                        bakedMatrix = Matrix4.CreateScale(LocalScale) * Matrix4.CreateFromQuaternion(LocalRotation) * Matrix4.CreateTranslation(LocalPosition);

                    hasChanged = false;
                    return bakedMatrix;
                }
                return bakedMatrix;
            }
            else
            {
                if (Parent != null)
                    return Matrix4.CreateFromQuaternion(LocalRotation.Inverted()) * Matrix4.CreateTranslation(LocalPosition) * Parent.ModelMatrix;
                else
                    return Matrix4.CreateFromQuaternion(LocalRotation.Inverted()) * Matrix4.CreateTranslation(LocalPosition);
            }
        }

        public bool IsEnabled()
        {
            return Enabled;
        }

        protected override void OnCreate(params object[] args)
        {
            Parent = (Transform)args[0];
            LocalPosition = (Vector3)args[1];
            LocalRotation = (Quaternion)args[2];
            LocalScale = (Vector3)args[3];
        }

        public void Start()
        {
            PreviousModelMatrix = ModelMatrix;
        }

        public Matrix4 InvModelMatrix => Matrix4.Invert(ModelMatrix);

        internal Matrix4 PreviousModelMatrix { get; set; }
    }
}
