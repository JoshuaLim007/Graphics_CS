using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JLGraphics
{
    public struct AABB
    {
        public Vector3 Min;
        public Vector3 Max;
        public Vector3 Center => (Min + Max) * 0.5f;
        public Vector3 Extents { 
            get{
                var cent = Center;
                return new Vector3(Max.X - cent.X, Max.Y - cent.Y, Max.Z - cent.Z);
            } 
        }
        public static int[] GetIndices()
        {
            return new int[]{
                0, 1, 2, 3, 7, 1, 5, 4, 7, 6, 2, 4, 0, 1
            };
        }

        public static AABB GetBoundingBox(Vector3[] positions)
        {
            var corners = positions;

            float minx = float.PositiveInfinity;
            float miny = float.PositiveInfinity;
            float minz = float.PositiveInfinity;

            float maxx = float.NegativeInfinity;
            float maxy = float.NegativeInfinity;
            float maxz = float.NegativeInfinity;


            for (int i = 0; i < corners.Length; i++)
            {
                if (corners[i].X < minx)
                {
                    minx = corners[i].X;
                }
                if (corners[i].Y < miny)
                {
                    miny = corners[i].Y;
                }
                if (corners[i].Z < minz)
                {
                    minz = corners[i].Z;
                }

                if (corners[i].X > maxx)
                {
                    maxx = corners[i].X;
                }
                if (corners[i].Y > maxy)
                {
                    maxy = corners[i].Y;
                }
                if (corners[i].Z > maxz)
                {
                    maxz = corners[i].Z;
                }
            }

            var min = new Vector3(minx, miny, minz);
            var max = new Vector3(maxx, maxy, maxz);

            return new AABB() { Max = max, Min = min };
        }
        private static bool CheckPlane(Plane plane, AABB box)
        {
            Vector3 axisVert = new();
            // x-axis
            if (plane.Normal.X < 0.0f)    // Which AABB vertex is furthest down (plane normals direction) the x axis
                axisVert.X = box.Min.X; 
            else
                axisVert.X = box.Max.X;

            if (plane.Normal.Y < 0.0f)    // Which AABB vertex is furthest down (plane normals direction) the x axis
                axisVert.Y = box.Min.Y;
            else
                axisVert.Y = box.Max.Y;

            if (plane.Normal.Z < 0.0f)    // Which AABB vertex is furthest down (plane normals direction) the x axis
                axisVert.Z = box.Min.Z;
            else
                axisVert.Z = box.Max.Z;

            // Now we get the signed distance from the AABB vertex that's furthest down the frustum planes normal,
            // and if the signed distance is negative, then the entire bounding box is behind the frustum plane, which means
            // that it should be culled
            if (Vector3.Dot(plane.Normal, axisVert) + plane.PlaneConstant < 0.0f)
                return true;

            return false;
        }
        public static bool IsOutsideOfFrustum(CameraFrustum cameraFrustum, AABB box)
        {
            return CheckPlane(cameraFrustum.leftFace, box) ||
                CheckPlane(cameraFrustum.rightFace, box) ||
                CheckPlane(cameraFrustum.topFace, box) ||
                CheckPlane(cameraFrustum.nearFace, box) ||
                CheckPlane(cameraFrustum.bottomFace, box) ||
                CheckPlane(cameraFrustum.farFace, box);
        }
        public static Vector4[] GetCorners(AABB boundingBox)
        {
            Vector4[] aabbVertices = new Vector4[8];

            //-1, -1, 1
            aabbVertices[0] = new Vector4(boundingBox.Min.X, boundingBox.Min.Y, boundingBox.Max.Z, 1);
            //1, -1, 1
            aabbVertices[1] = new Vector4(boundingBox.Max.X, boundingBox.Min.Y, boundingBox.Max.Z, 1);
            //-1,1,1
            aabbVertices[2] = new Vector4(boundingBox.Min.X, boundingBox.Max.Y, boundingBox.Max.Z, 1);
            //1, 1, 1
            aabbVertices[3] = new Vector4(boundingBox.Max, 1);
            //-1, -1, -1
            aabbVertices[4] = new Vector4(boundingBox.Min, 1);
            //1, -1, -1
            aabbVertices[5] = new Vector4(boundingBox.Max.X, boundingBox.Min.Y, boundingBox.Min.Z, 1);
            //-1, 1, -1
            aabbVertices[6] = new Vector4(boundingBox.Min.X, boundingBox.Max.Y, boundingBox.Min.Z, 1);
            //1,1,-1
            aabbVertices[7] = new Vector4(boundingBox.Max.X, boundingBox.Max.Y, boundingBox.Min.Z, 1);

            return aabbVertices;
        }
        public static AABB ApplyTransformation(AABB boundingBox, Matrix4 transformation)
        {
            Vector4[] aabbVertices = GetCorners(boundingBox);

            Vector3 minPoint = Vector3.PositiveInfinity;
            Vector3 maxPoint = Vector3.NegativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                aabbVertices[i] = aabbVertices[i] * transformation;
                float x = aabbVertices[i].X;
                float y = aabbVertices[i].Y;
                float z = aabbVertices[i].Z;

                if (x < minPoint.X)
                {
                    minPoint.X = x;
                }
                if (y < minPoint.Y)
                {
                    minPoint.Y = y;
                }
                if (z < minPoint.Z)
                {
                    minPoint.Z = z;
                }


                if (x > maxPoint.X)
                {
                    maxPoint.X = x;
                }
                if (y > maxPoint.Y)
                {
                    maxPoint.Y = y;
                }
                if (z > maxPoint.Z)
                {
                    maxPoint.Z = z;
                }
            }

            return new AABB() { Min = minPoint, Max = maxPoint };
        }
    }
}
