using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    internal class Attributes
    {
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class Range : Attribute
    {
        public float min { get; }
        public float max { get; }

        public Range(float min, float max)
        {
            this.min = min;
            this.max = max;
        }
    }
}
