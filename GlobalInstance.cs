using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public interface IGlobalScope
    {

    }
    internal sealed class InternalGlobalScope<T> where T : IGlobalScope
    {
        private InternalGlobalScope() { }
        internal static List<T> Values { get; private set; } = new List<T>();
        internal static Dictionary<string, T> NamedValues { get; private set; } = new Dictionary<string, T>();
        internal static void Add(string name, T value)
        {
            NamedValues.Add(name, value);
        }
        internal static T Get(string name)
        {
            return NamedValues[name];
        }
        internal static void Add(T value)
        {
            Values.Add(value);
        }
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
