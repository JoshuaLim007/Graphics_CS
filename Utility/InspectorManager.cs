using ImGuiNET;
using JLGraphics.Utility.GuiAttributes;
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
                    var name = attribute.Label.Trim().Length != 0 ? attribute.Label : field.Name;
                    var value = field.GetValue(component);
                    var newValue = HandleType(name, field.FieldType, value);
                    field.SetValue(component, newValue);

                    if (newValue != value && type == typeof(Transform)) {
                        var t = (Transform)component;
                        t.hasChanged = true;
                    }
                }
            }
            ImGui.Dummy(new System.Numerics.Vector2(0, 3));
        }
        private object HandleType(string name, Type type, object value)
        {
            ImGui.Text(name + ": ");
            ImGui.PushID(name);
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
            return value;
        }
        private Vector3 RenderVector3(object vector3)
        {
            var v3 = (Vector3)vector3;
            System.Numerics.Vector3 val = new System.Numerics.Vector3(v3.X, v3.Y, v3.Z);
            ImGui.InputFloat3("", ref val);
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

            ImGui.InputFloat3("", ref val);

            eular.X = MathHelper.DegreesToRadians(val.X);
            eular.Y = MathHelper.DegreesToRadians(val.Y);
            eular.Z = MathHelper.DegreesToRadians(val.Z);

            return Quaternion.FromEulerAngles(eular);
        }
    }
}
