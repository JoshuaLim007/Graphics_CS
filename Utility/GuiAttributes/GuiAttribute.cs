using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics.Utility.GuiAttributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public class GuiAttribute : Attribute
    {
        public string Label { get; set; }
        public GuiAttribute()
        {
            Label = "";
        }
        public GuiAttribute(string GuiLabel)
        {
            Label = GuiLabel;
        }
    }
}
