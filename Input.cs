using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public struct MouseInput
    {
        public static Vector2 Position => mPosition;
        public static Vector2 PreviousPosition => mPreviousPosition;
        public static Vector2 Delta => mDelta;


        static private Vector2 mPosition;
        static private Vector2 mPreviousPosition;
        static private Vector2 mDelta;
        static internal void UpdateMousePosition(Vector2 position)
        {
            mPreviousPosition = mPosition;
            mPosition = position;
            mDelta = mPosition - mPreviousPosition;
        }
    }
}
