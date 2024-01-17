using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    internal sealed class GlobalInstance<T>
    {
        private GlobalInstance() { }
        internal static List<T> Values { get; private set; } = new List<T>();
        internal static T Value
        {
            get
            {
                return Values.Count == 0 ? default(T) : Values[0];
            }
            set
            {
                if(Values.Count == 0)
                {
                    Values.Add(value);
                }
                else
                {
                    Values[0] = value;
                }
            }
        }
        internal static void Clear()
        {
            Values.Clear();
        }
        internal static void Dispose()
        {
            Values.TrimExcess();
        }
        internal static int Count => Values.Count;
    }
}
