﻿using ImGuiNET;
using JLGraphics.Components;
using JLGraphics.Utility.GuiAttributes;
using JLUtility;
using Microsoft.VisualBasic;
using OpenTK.Audio.OpenAL;
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
            guiManager.AddWindow("Inspector Window", Update, typeof(SceneViewManager));
        }
        public void Update()
        {
            if (sceneViewManager.ObjectsSelected.Count > 0)
            {
                var objects = sceneViewManager.ObjectsSelected.GetRange(0, 1);

                for (int i = 0; i < objects.Count; i++)
                {
                    RenderEntity(entity: objects[i]);
                    ImGui.Separator();

                    var components = objects[i].GetAllComponents();
                    for (int j = 0; j < components.Length; j++)
                    {
                        RenderComponent(components[j]);
                    }
                }
            }
            ImGui.Separator();
        }
        private void RenderEntity(Entity entity)
        {
            string name = entity.Name;
            ImGui.InputText("Entity Name", ref name, 256);
            entity.Name = name;

            bool enable = entity.Enabled;
            ImGui.Checkbox("Enable", ref enable);
            entity.Enabled = enable;
        }
        private void RenderComponent(Component component)
        {
            Type type = component.GetType();
            ImGui.Separator();
            ImGui.Text(type.Name);
            ImGui.Dummy(new System.Numerics.Vector2(0, 3));

            //handle fields
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var field in fields)
            {
                var attribute = field.GetCustomAttribute<GuiAttribute>();
                if (attribute != null)
                {
                    var value = field.GetValue(component);
                    var newValue = HandleType(field, attribute, field.Name, field.FieldType, value);
                    if(!newValue.Equals(value))
                    {
                        field.SetValue(component, newValue);
                        component.OnGuiChange();
                    }
                }
            }

            //handle properties
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var property in properties)
            {
                if (!property.CanRead)
                {
                    continue;
                }
                var attribute = property.GetCustomAttribute<GuiAttribute>();
                if (attribute != null)
                {
                    var value = property.GetValue(component);
                    var newValue = HandleType(property, attribute, property.Name, property.PropertyType, value);
                    if (!newValue.Equals(value) && property.CanWrite)
                    {
                        property.SetValue(component, newValue);
                        component.OnGuiChange();
                    }
                }
            }
            ImGui.Dummy(new System.Numerics.Vector2(0, 3));
        }
        
        bool RenderAsSlider = false;
        float SliderMin;
        float SliderMax;
        private object HandleType(MemberInfo memberInfo, GuiAttribute attribute, string fallbackName, Type type, object value)
        {
            var name = attribute.Label.Trim().Length != 0 ? attribute.Label : fallbackName;
            ImGui.Text(name + ": ");
            ImGui.PushID(name);
            var sliderInfo = memberInfo.GetCustomAttribute<GuiSlider>();
            if(sliderInfo != null)
            {
                SliderMin = sliderInfo.min;
                SliderMax = sliderInfo.max;
                RenderAsSlider = true;
            }
            if (attribute.ReadOnly)
            {
                ImGui.BeginDisabled();
            }

            if (type.IsEnum)
            {
                value = RenderEnum(type, value);
            }
            else
            {
                if (type == typeof(Vector3))
                {
                    if (attribute.Color)
                    {
                        value = RenderColor(value);
                    }
                    else
                    {
                        value = RenderVector3(value);
                    }
                }
                else if (type == typeof(Quaternion))
                {
                    value = RenderVectorQuat(value);
                }
                else if (type == typeof(float))
                {
                    value = RenderFloat(value);
                }
                else if (type == typeof(int))
                {
                    value = RenderInt(value);
                }
                else if (type == typeof(bool))
                {
                    value = RenderBool(value);
                }
                else if (type == typeof(string))
                {
                    value = RenderString(value);
                }

            }

            if (attribute.ReadOnly)
            {
                ImGui.EndDisabled();
            }
            RenderAsSlider = false;
            return value;
        }
        private object RenderEnum(Type type, object value)
        {
            var arr = type.GetEnumValues();
            if(ImGui.BeginCombo("", value.ToString()))
            {
                foreach (var item in arr)
                {
                    bool is_selected = (value == item);
                    if (ImGui.Selectable(item.ToString(), is_selected))
                        value = item;

                    if (is_selected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            return value;
        }
        private bool RenderBool(object value)
        {
            var str = (bool)value;
            ImGui.Checkbox("", ref str);
            return str;
        }
        private int RenderInt(object value)
        {
            var str = (int)value;
            if (RenderAsSlider)
                ImGui.SliderInt("", ref str, (int)SliderMin, (int)SliderMax);
            else
                ImGui.DragInt("", ref str, 1.0f);
            return (int)str;
        }
        private float RenderFloat(object value)
        {
            var str = (float)value;
            if(!RenderAsSlider)
                ImGui.DragFloat("", ref str, 0.5f, float.NegativeInfinity, float.PositiveInfinity, "%.3f");
            else
                ImGui.SliderFloat("", ref str, SliderMin, SliderMax, "%.3f");
            return str;
        }
        private string RenderString(object value)
        {
            var str = (string)value;
            ImGui.InputText("", ref str, 256);
            return str;
        }
        private Vector3 RenderColor(object vector3)
        {
            var v3 = (Vector3)vector3;
            System.Numerics.Vector3 val = new System.Numerics.Vector3(v3.X, v3.Y, v3.Z);
            ImGui.ColorPicker3("", ref val);
            v3.X = val.X;
            v3.Y = val.Y;
            v3.Z = val.Z;
            return v3;
        }
        private Vector3 RenderVector3(object vector3)
        {
            var v3 = (Vector3)vector3;
            System.Numerics.Vector3 val = new System.Numerics.Vector3(v3.X, v3.Y, v3.Z);
            
            if (!RenderAsSlider)
                ImGui.DragFloat3("", ref val, 0.5f, float.NegativeInfinity, float.PositiveInfinity, "%.3f");
            else
                ImGui.SliderFloat3("", ref val, SliderMin, SliderMax, "%.3f");


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

            eular.X = val.X;
            eular.Y = val.Y;
            eular.Z = val.Z;
            eular = RenderVector3(eular);

            eular.X = MathHelper.DegreesToRadians(eular.X);
            eular.Y = MathHelper.DegreesToRadians(eular.Y);
            eular.Z = MathHelper.DegreesToRadians(eular.Z);
            return Quaternion.FromEulerAngles(eular);
        }
    }
}
