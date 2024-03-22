using ImGuiNET;
using JLGraphics.Utility.GuiAttributes;
using JLUtility;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Quaternion = OpenTK.Mathematics.Quaternion;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace JLGraphics.Utility
{
    public class InspectorManager
    {
        SceneViewManager sceneViewManager;
        public InspectorManager(GuiManager guiManager, SceneViewManager sceneViewManager)
        {
            this.sceneViewManager = sceneViewManager;
            guiManager.OnInspectorGui += Update;
        }
        public void Update()
        {
            var objects = sceneViewManager.ObjectsSelected;

            for (int i = 0; i < objects.Count; i++)
            {
                string name = objects[i].Name;
                ImGui.InputText(" Entity Name", ref name, 256);
                ImGui.Separator();

                var components = objects[i].GetAllComponents();
                for (int j = 0; j < components.Length; j++)
                {
                    RenderComponent(components[j]);
                }
            }
            ImGui.Separator();
        }

        private void RenderComponent(Component component)
        {
            Type type = component.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            ImGui.Separator();
            ImGui.Text(type.Name);
            ImGui.Dummy(new System.Numerics.Vector2(0, 3));
            foreach (var field in fields)
            {
                var attribute = field.GetCustomAttribute<GuiAttribute>();
                if (attribute != null)
                {
                    var value = field.GetValue(component);
                    var newValue = HandleType(attribute, field.Name, field.FieldType, value);
                    if(newValue != value)
                    {
                        field.SetValue(component, newValue);
                        component.OnGuiChange();
                    }
                }
            }
            ImGui.Dummy(new System.Numerics.Vector2(0, 3));
        }
        private object HandleType(GuiAttribute attribute, string fallbackName, Type type, object value)
        {
            var name = attribute.Label.Trim().Length != 0 ? attribute.Label : fallbackName;
            ImGui.Text(name + ": ");
            ImGui.PushID(name);
            if (attribute.ReadOnly)
            {
                ImGui.BeginDisabled();
            }
            if(type == typeof(Vector3))
            {
                value = RenderVector3(value);
            }
            else if(type == typeof(Quaternion))
            {
                value = RenderVectorQuat(value);
            }
            else if(type == typeof(float))
            {

            }
            else if (type == typeof(int))
            {

            }
            else if (type == typeof(bool))
            {

            }
            else if (type == typeof(string))
            {

            }
            if (attribute.ReadOnly)
            {
                ImGui.EndDisabled();
            }
            return value;
        }
        private Vector3 RenderVector3(object vector3)
        {
            var v3 = (Vector3)vector3;
            System.Numerics.Vector3 val = new System.Numerics.Vector3(v3.X, v3.Y, v3.Z);
            ImGui.DragFloat3("", ref val, 0.5f, float.NegativeInfinity, float.PositiveInfinity, "%.3f");
            v3.X = val.X;
            v3.Y = val.Y;
            v3.Z = val.Z;
            return v3;
        }
        private Quaternion RenderVectorQuat(object quaternion)
        {
            var eular = ((Quaternion)quaternion).ToEulerAngles();
            System.Numerics.Vector3 val = new System.Numerics.Vector3(
                MathHelper.RadiansToDegrees(eular.X),
                MathHelper.RadiansToDegrees(eular.Y),
                MathHelper.RadiansToDegrees(eular.Z));

            ImGui.DragFloat3("", ref val, 0.5f, float.NegativeInfinity, float.PositiveInfinity, "%.3f");

            eular.X = MathHelper.DegreesToRadians(val.X);
            eular.Y = MathHelper.DegreesToRadians(val.Y);
            eular.Z = MathHelper.DegreesToRadians(val.Z);

            return Quaternion.FromEulerAngles(eular);
        }
    }
}
