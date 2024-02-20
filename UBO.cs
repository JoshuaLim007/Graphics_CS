using ObjLoader.Loader.Data;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JLGraphics
{
    public class UBO<T> : SafeDispose
    {
        int ubo;
        string name;
        public override string Name => "UBO_" + name;

        public UBO(T[] data, int elements, int bindingPointIndex)
        {
            if(data == null)
            {
                throw new ArgumentNullException();
            }
            name = data.GetType().Name + "_point_" + bindingPointIndex;
            ubo = 0;
            GL.GenBuffers(1, out ubo);
            GL.BindBuffer(BufferTarget.UniformBuffer, ubo);
            GCHandle pinnedArray = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr pointer = pinnedArray.AddrOfPinnedObject();
            GL.BufferData(BufferTarget.UniformBuffer, Unsafe.SizeOf<T>() * elements, pointer, BufferUsageHint.DynamicDraw);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            pinnedArray.Free();

            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, bindingPointIndex, ubo);
        }
        public UBO(T data, int dataSizeBytes)
        {
            if (data == null)
            {
                throw new ArgumentNullException();
            }
            name = data.GetType().Name;
            ubo = 0;
            GL.GenBuffers(1, out ubo);
            GL.BindBuffer(BufferTarget.UniformBuffer, ubo);
            GCHandle pinnedArray = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr pointer = pinnedArray.AddrOfPinnedObject();
            GL.BufferData(BufferTarget.UniformBuffer, dataSizeBytes, pointer, BufferUsageHint.DynamicDraw);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            pinnedArray.Free();
        }
        public void UpdateData(T[] data, int dataSizeBytes)
        {
            if (data == null)
            {
                throw new ArgumentNullException();
            }

            GL.BindBuffer(BufferTarget.UniformBuffer, ubo);
            GCHandle pinnedArray = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr pointer = pinnedArray.AddrOfPinnedObject();
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, dataSizeBytes, pointer);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            pinnedArray.Free();
        }
        protected override void OnDispose()
        {
            GL.DeleteBuffer(ubo);
        }
    }
}
