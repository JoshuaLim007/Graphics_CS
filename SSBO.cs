using ObjLoader.Loader.Data;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Collections;
using System.Runtime.InteropServices;

namespace JLGraphics
{
    public class SSBO<T> : SafeDispose
    {
        int ssbo;
        string name;
        public override string Name => "SSBO_" + name;

        public SSBO(T[] data, int dataSizeBytes)
        {
            if(data == null)
            {
                throw new ArgumentNullException();
            }
            name = data.GetType().Name;
            ssbo = 0;
            GL.GenBuffers(1, out ssbo);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo);
            GCHandle pinnedArray = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr pointer = pinnedArray.AddrOfPinnedObject();
            GL.BufferData(BufferTarget.ShaderStorageBuffer, dataSizeBytes, pointer, BufferUsageHint.DynamicDraw);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            pinnedArray.Free();
        }
        public SSBO(T data, int dataSizeBytes)
        {
            if (data == null)
            {
                throw new ArgumentNullException();
            }
            name = data.GetType().Name;
            ssbo = 0;
            GL.GenBuffers(1, out ssbo);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo);
            GCHandle pinnedArray = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr pointer = pinnedArray.AddrOfPinnedObject();
            GL.BufferData(BufferTarget.ShaderStorageBuffer, dataSizeBytes, pointer, BufferUsageHint.DynamicDraw);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            pinnedArray.Free();
        }
        public void UpdateData(T[] data, int dataSizeBytes)
        {
            if (data == null)
            {
                throw new ArgumentNullException();
            }

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo);
            GCHandle pinnedArray = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr pointer = pinnedArray.AddrOfPinnedObject();
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, dataSizeBytes, pointer);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            pinnedArray.Free();
        }
        public void UpdateData(T data, int dataSizeBytes)
        {
            if (data == null)
            {
                throw new ArgumentNullException();
            }

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo);
            GCHandle pinnedArray = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr pointer = pinnedArray.AddrOfPinnedObject();
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, dataSizeBytes, pointer);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            pinnedArray.Free();
        }
        public void BindDataToShader(int shaderProgram, int bindingPointIndex, string name)
        {
            int block_index;
            block_index = GL.GetProgramResourceIndex(shaderProgram, ProgramInterface.ShaderStorageBlock, name);
            if (block_index < 0)
            {
                return;
            }

            int ssbo_binding_point_index = bindingPointIndex;
            GL.ShaderStorageBlockBinding(shaderProgram, block_index, ssbo_binding_point_index);

            int binding_point_index = bindingPointIndex;
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, binding_point_index, ssbo);
        }
        public void BindDataToAllShaders(int bindingPointIndex, string name)
        {
            for (int i = 0; i < ShaderProgram.AllShaderPrograms.Count; i++)
            {
                BindDataToShader(ShaderProgram.AllShaderPrograms[i].Id, bindingPointIndex, name);
            }
        }
        protected override void OnDispose()
        {
            GL.DeleteBuffer(ssbo);
        }
    }
}
