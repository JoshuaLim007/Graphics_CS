using System;
using System.Collections.Generic;
using System.Text;

namespace JLGraphics
{
    public struct GlobalUniform<T> where T: struct
    {
        private static int IdCounter = 0;
        public int Identifier { get; }
        public object Value { get; private set; }
        public string Name { get; }
        public GlobalUniform(string name, T value)
        {
            Identifier = IdCounter++;
            Name = name;
            Value = value;
        }
        public void SetValue(T value)
        {
            Value = value;
        }
    }
}
