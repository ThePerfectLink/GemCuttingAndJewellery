using GemCuttingAndJewellery.Systems.Enums;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Common;

namespace GemCuttingAndJewellery.Systems
{
    public static class GeometryHelper
    {
        public static readonly int[][] CubeCornerGroups = new int[][]
        {
            new int[] {  0, 15, 21 }, // corner 0,0,0
            new int[] {  1, 14, 18 }, // corner 1,0,0
            new int[] {  2,  5, 17 }, // corner 1,1,0
            new int[] {  3,  4, 22 }, // corner 0,1,0
            new int[] {  6,  9, 16 }, // corner 0,0,1
            new int[] {  7,  8, 23 }, // corner 1,0,1
            new int[] { 10, 13, 19 }, // corner 1,1,1
            new int[] { 11, 12, 20 }, // corner 0,1,1
        };

        public static Vec3f CalculateQuadNormal(Vec3f v0, Vec3f v1, Vec3f v2, Vec3f v3)
        {
            Vec3f d1 = v2 - v0;
            Vec3f d2 = v3 - v1;
            Vec3f normal = d1.Cross(d2);
            normal.Normalize();
            return normal;
        }
        public static Vec3f GetVertex(MeshData mesh, int index)
        {
            return new Vec3f(
                mesh.xyz[index * 3],
                mesh.xyz[index * 3 + 1],
                mesh.xyz[index * 3 + 2]
            );
        }

        public static void SetVertex(MeshData mesh, int index, Vec3f newVertex)
        {
            mesh.xyz[index * 3] = newVertex.X;
            mesh.xyz[index * 3 + 1] = newVertex.Y;
            mesh.xyz[index * 3 + 2] = newVertex.Z;
        }

        public static int[]? GetOverlappingVertices(int index)
        {
            foreach (int[] row in CubeCornerGroups)
            {
                if(row.Contains<int>(index%24))
                {
                    int cubeIndex = (index/24)*24;
                    int[] temp = {0,0,0};
                    row.CopyTo(temp, 0);
                    for (int i = 0; i < temp.Length; i++)
                    {
                        temp[i] += cubeIndex;
                    }
                    return temp; 
                }
            }
            return null;
        }

        public static Vec3f AlterVertex(Vec3f newPoint, Vec3f index, Operator @operator)
        {
            Vec3f temp = index;
            switch (@operator)
            {
                case Operator.Add:
                    temp.Add(newPoint);
                    break;
                case Operator.Subtract:
                    temp.Sub(newPoint);
                    break;
                case Operator.Multiply:
                    temp.X *= newPoint.X;
                    temp.Y *= newPoint.Y;
                    temp.Z *= newPoint.Z;
                    break;
                case Operator.Divide:
                    temp.X /= newPoint.X;
                    temp.Y /= newPoint.Y;
                    temp.Z /= newPoint.Z;
                    break;
                case Operator.Modulo:
                    temp.X %= newPoint.X;
                    temp.Y %= newPoint.Y;
                    temp.Z %= newPoint.Z;
                    break;
            }
            return temp;
        }

        public static (float u, float v) TriplanarUV(Vec3f vertex, Vec3f normal, TextureAtlasPosition texPos)
        {
            Vec3f absNormal = new Vec3f(Math.Abs(normal.X), Math.Abs(normal.Y), Math.Abs(normal.Z));

            float u, v;
            if (absNormal.X >= absNormal.Y && absNormal.X >= absNormal.Z)
            {
                u = vertex.Z; v = vertex.Y; // Project along X
            }
            else if (absNormal.Y >= absNormal.X && absNormal.Y >= absNormal.Z)
            {
                u = vertex.X; v = vertex.Z; // Project along Y
            }
            else
            {
                u = vertex.X; v = vertex.Y; // Project along Z
            }

            return (
                texPos.x1 + (u % 1f) * (texPos.x2 - texPos.x1),
                texPos.y1 + (v % 1f) * (texPos.y2 - texPos.y1)
            );
        }
    }
}
