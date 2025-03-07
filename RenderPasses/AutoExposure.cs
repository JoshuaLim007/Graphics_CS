using ObjLoader.Loader.Data.VertexData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;
using Assimp;
using OpenTK.Mathematics;
using OpenTK.Compute.OpenCL;
using System.Runtime.InteropServices;
using System.Drawing;
using JLUtility;

namespace JLGraphics.RenderPasses
{
    public class AutoExposure : RenderPass
    {
        int pbo = -1;
        float[] pixelBuffer;
        Vector2i previousResolution = Vector2i.Zero;
        public AutoExposure(int offset) : base(RenderQueue.AfterTransparents, offset)
        {

        }

        public override string Name => "Auto Exposure";
        bool downloading = false;
        bool abort = false;
        bool doneCalculating = false;
        IntPtr readSync;

        public override void Execute(in FrameBuffer frameBuffer)
        {
            var res = new Vector2i(frameBuffer.TextureAttachments[0].Width, frameBuffer.TextureAttachments[0].Height);
            if(previousResolution != res)
            {
                abort = true;
                if (pbo != -1)
                {
                    GL.DeleteBuffers(1, ref pbo);
                }
                previousResolution = res;
                GL.GenBuffers(1, out pbo);
                GL.BindBuffer(BufferTarget.PixelPackBuffer, pbo);
                GL.BufferData(BufferTarget.PixelPackBuffer, res.X * res.Y * 3 * sizeof(float), IntPtr.Zero, BufferUsageHint.StreamRead); // Allocate buffer
                pixelBuffer = new float[res.X * res.Y * 3];
                downloading = false;
            }
            else
            {
                GL.BindBuffer(BufferTarget.PixelPackBuffer, pbo);
            }

            if (downloading == false)
            {
                // Start GPU -> PBO transfer
                GL.BindTexture(TextureTarget.Texture2D, frameBuffer.TextureAttachments[0].GlTextureID);
                GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgb, PixelType.Float, IntPtr.Zero); // Data goes to PBO
                readSync = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);
                downloading = true;
                GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);
                return;
            }

            if (downloading == true)
            {
                IntPtr ptr;

                // Map PBO to CPU memory
                if (readSync == IntPtr.Zero)
                {
                    GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);
                    return;
                }
                var syncStatus = GL.ClientWaitSync(readSync, 0, 0);
                if (syncStatus != WaitSyncStatus.AlreadySignaled && syncStatus != WaitSyncStatus.ConditionSatisfied)
                {
                    GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);
                    return;
                }
                ptr = GL.MapBuffer(BufferTarget.PixelPackBuffer, BufferAccess.ReadOnly);

                if (ptr != IntPtr.Zero)
                {
                    CalculateAvgExposure(ptr, res.X, res.Y);
                    AdjustExposure(Time.DeltaTime);
                    // Copy data from mapped buffer
                    GL.UnmapBuffer(BufferTarget.PixelPackBuffer);
                    downloading = false;
                }
            }

            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);
        }

        public float TargetExposure = 1.0f;
        public float AdjustmentSpeed = 4.0f;

        float CurrentAvgExposure = 0.0f;
        float CurrentExposureScaler = 1.0f;

        Task previousTask = null;
        void CalculateAvgExposure(IntPtr ptr, int width, int height)
        {
            if (previousTask != null)
            {
                if (!doneCalculating)
                {
                    return;
                }
            }
            doneCalculating = false;
            previousTask = Task.Run(() =>
            {
                unsafe
                {
                    float* data = (float*)ptr; // Cast the pointer to a float array
                    float sum = 0.0f;
                    for (int i = 0; i < width * height; i++)
                    {
                        if (abort)
                        {
                            break;
                        }
                        float r = data[i * 3];
                        float g = data[i * 3 + 1];
                        float b = data[i * 3 + 2];
                        float max = Math.Max(Math.Max(r, b), g);
                        sum += max;
                    }
                    CurrentAvgExposure = sum / (width * height);
                    abort = false;
                    doneCalculating = true;
                }
            });
        }

        void AdjustExposure(float deltaTime)
        {
            float errCor = TargetExposure / MathF.Max(CurrentAvgExposure, 0.1f);
            var temp = OpenTK.Mathematics.MathHelper.Lerp(CurrentExposureScaler, errCor, deltaTime * AdjustmentSpeed);
            var diff = temp - CurrentExposureScaler;
            if (diff >= 0)
            {
                CurrentExposureScaler += Math.Min(diff, AdjustmentSpeed * deltaTime);
            }
            else
            {
                CurrentExposureScaler += Math.Max(diff, -AdjustmentSpeed * deltaTime);
            }

            if (PostProcessPass.Instance != null)
            {
                PostProcessPass.Instance.Exposure = CurrentExposureScaler;
            }
        }


        protected override void OnDispose()
        {
            if(pbo != -1)
                GL.DeleteBuffers(1, ref pbo);
        }
    }
}
