using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics.Utility.GuiAttributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class GuiAttribute : Attribute
    {
        public string Label { get; }
        public bool ReadOnly { get; }
        public bool Color { get; }
        public GuiAttribute()
        {
            Label = "";
        }
        public GuiAttribute(string GuiLabel, bool readOnly = false, bool color = false)
        {
            Color = color;
            ReadOnly = readOnly;
            Label = GuiLabel;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class GuiSlider : Attribute
    {
        public float min { get; }
        public float max { get; }
        public GuiSlider(float min, float max)
        {
            this.min = min;
            this.max = max;
        }
    }
}
