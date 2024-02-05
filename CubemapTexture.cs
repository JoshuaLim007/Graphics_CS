﻿using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using StbiSharp;
using TextureWrapMode = OpenTK.Graphics.OpenGL4.TextureWrapMode;

namespace JLGraphics
{
    internal class CubemapTexture : IDisposable
    {
        int ImageTextureID;
        MeshPrimative cubeMesh;
        public void Dispose()
        {
            if(ImageTextureID != 0)
                GL.DeleteTexture(ImageTextureID);
            
            cubeMapTextureInstances--;
            if(cubeMapTextureInstances == 0)
            {
                CubeMapProjectionShader = null;
                CubemapShaderProgram.Dispose();
            }
            Mesh.FreeMeshObject(cubeMesh);
            disposed = true;

            imageResult.Dispose();
        }
        public string Path { get; set; }
        bool disposed = false;
        static Shader CubeMapProjectionShader = null;
        static ShaderProgram CubemapShaderProgram = null;
        static int cubeMapTextureInstances = 0;
        public CubemapTexture()
        {
            if(CubeMapProjectionShader == null)
            {
                CubemapShaderProgram = new ShaderProgram("Cubemap Program", "./Shaders/Rect2CubeFrag.glsl", "./Shaders/Rect2CubeVert.glsl");
                CubemapShaderProgram.CompileProgram();
                CubeMapProjectionShader = new Shader("Rect2Cube Shader", CubemapShaderProgram, true);
            }
            cubeMapTextureInstances++;
            cubeMesh = Mesh.CreateCubeMesh();
        }
        StbiImageF imageResult = null;
        FileStream imageFile = null;
        MemoryStream imageFileData = null;
        ReadOnlySpan<float> LoadPixelData()
        {
            imageFile = File.OpenRead(Path);
            imageFileData = new MemoryStream();
            imageFile.CopyTo(imageFileData);
            if (!Stbi.IsHdrFromMemory(imageFileData))
            {
                Console.WriteLine("WARNING::Pixel data for cubemap is not HDR!");
            }
            var image = Stbi.LoadFFromMemory(imageFileData, 3);
            imageResult = image;
            return image.Data;
        }
        void ResolveTexture()
        {
            if (disposed)
            {
                Console.WriteLine("ERROR::Cubemap texture is disposed!");
                throw new Exception("ERROR::Cubemap texture is disposed!");
            }
            if (ImageTextureID == 0)
                ImageTextureID = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, ImageTextureID);
            var imageData = LoadPixelData();
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb32f, imageResult.Width, imageResult.Height, 0, PixelFormat.Rgb, PixelType.Float, imageData.ToArray());
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (float)OpenTK.Graphics.OpenGL4.TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (float)OpenTK.Graphics.OpenGL4.TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (float)OpenTK.Graphics.OpenGL4.TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (float)OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
            GL.GenerateTextureMipmap(ImageTextureID);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            imageFileData.Close();
            imageFile.Close();
        }
        public void RenderCubemap(bool isSkyBox, int size = 512)
        {
            ResolveTexture();

            if (disposed)
            {
                Console.WriteLine("ERROR::Cubemap texture is disposed!");
                throw new Exception("ERROR::Cubemap texture is disposed!");
            }
            int width = size;
            int height = size;
            TextureTarget[] targets =
            {
               TextureTarget.TextureCubeMapPositiveX, TextureTarget.TextureCubeMapNegativeX,
               TextureTarget.TextureCubeMapPositiveY, TextureTarget.TextureCubeMapNegativeY,
               TextureTarget.TextureCubeMapPositiveZ, TextureTarget.TextureCubeMapNegativeZ
            };

            // Create framebuffer
            int fbo;
            GL.GenFramebuffers(1, out fbo);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

            int tColorCubeMap, tDepthCubeMap;
            // Depth cube map
            GL.GenTextures(1, out tDepthCubeMap);
            GL.BindTexture(TextureTarget.TextureCubeMap, tDepthCubeMap);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);

            for (int face = 0; face < 6; face++)
            {
                GL.TexImage2D(targets[face], 0, PixelInternalFormat.DepthComponent,
                    width, height, 0, PixelFormat.DepthComponent, PixelType.Float, System.IntPtr.Zero);

                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, targets[face], tDepthCubeMap, 0);
            }

            // Color cube map
            GL.GenTextures(1, out tColorCubeMap);
            GL.BindTexture(TextureTarget.TextureCubeMap, tColorCubeMap);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureBaseLevel, 0);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMaxLevel, 11);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);

            for (int face = 0; face < 6; face++)
            {
                GL.TexImage2D(targets[face], 0, PixelInternalFormat.Rgb16f,
                    width, height, 0, PixelFormat.Rgb, PixelType.Float, System.IntPtr.Zero);
                GL.GenerateTextureMipmap(tColorCubeMap);

                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, targets[face], tColorCubeMap, 0);
            }

            // framebuffer object
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            {
                // Handle framebuffer error
                // For example, print an error message or throw an exception
                Console.WriteLine("ERROR::Cannot create cubemap!");
                return;
            }

            Matrix4 captureProjection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(90), 1.0f, 0.1f, 10.0f);
            Matrix4[] captureViews =
            {
                Matrix4.LookAt(Vector3.Zero, new Vector3(1.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f)),
                Matrix4.LookAt(Vector3.Zero, new Vector3(-1.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f)),
                Matrix4.LookAt(Vector3.Zero, new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.0f, 0.0f, 1.0f)),
                Matrix4.LookAt(Vector3.Zero, new Vector3(0.0f, -1.0f, 0.0f), new Vector3(0.0f, 0.0f, -1.0f)),
                Matrix4.LookAt(Vector3.Zero, new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.0f, -1.0f, 0.0f)),
                Matrix4.LookAt(Vector3.Zero, new Vector3(0.0f, 0.0f, -1.0f), new Vector3(0.0f, -1.0f, 0.0f))
            };

            
            CubeMapProjectionShader.SetTexture("equirectangularMap", ImageTextureID, TextureTarget.Texture2D);
            CubeMapProjectionShader.DepthTest = false;
            GL.Viewport(0, 0, width, height);

            Color4[] clearColors =
            {
                new Color4(1,0,0,0),
                new Color4(1,1,0,0),
                new Color4(0,1,0,0),
                new Color4(0,1,1,0),
                new Color4(0,0,1,0),
                new Color4(1,0,1,0),
            };

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);
            for (int i = 0; i < 6; ++i)
            {
                CubeMapProjectionShader.SetMat4("ViewMatrix", captureViews[i]);
                CubeMapProjectionShader.SetMat4("ProjectionMatrix", captureProjection);
                CubeMapProjectionShader.UseProgram();
                CubeMapProjectionShader.UpdateUniforms();
                
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, targets[i], tColorCubeMap, 0);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, targets[i], tDepthCubeMap, 0);
                GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
                GL.ReadBuffer(ReadBufferMode.None);

                GL.BindVertexArray(cubeMesh.VAO);
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, cubeMesh.EBO);

                GL.ClearColor(clearColors[i]);
                GL.ClearDepth(1.0f);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                GL.DrawElements(PrimitiveType.Triangles, cubeMesh.IndiciesCount, DrawElementsType.UnsignedInt, 0);
            }


            if (isSkyBox)
            {
                Shader.SetGlobalTexture("SkyBox", tColorCubeMap, TextureTarget.TextureCubeMap);
            }
            GL.Enable(EnableCap.CullFace);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DeleteFramebuffer(fbo);

            GL.Viewport(0, 0, Graphics.Window.Size.X, Graphics.Window.Size.Y); 
        }
    }
}
