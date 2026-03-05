using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using static GemCuttingAndJewellery.Systems.ModularMeshData;

namespace GemCuttingAndJewellery.Systems
{
    public class ModularMeshData
    {
        public int[]? OriginalTextureIds;

        public class Vertex
        {
            public Vec3f Position;
            public List<Face> AdjacentFaces = new();
        }

        public class VertexRenderData
        {
            public float U;
            public float V;
            public int Flags;
            public byte TextureIndex;
        }

        public class Face
        {
            public Vertex[] Vertices;        // shared geometric vertices for adjacency
            public VertexRenderData[] RenderData; // per-slot render data, parallel to Vertices
            public Vec3f Normal;
            public TextureAtlasPosition TexPos;

            public void RecalculateNormal()
            {
                Normal = GeometryHelper.CalculateQuadNormal(
                    Vertices[0].Position,
                    Vertices[1].Position,
                    Vertices[2].Position,
                    Vertices[3].Position
                );
            }
        }

        public List<Vertex> Vertices = new();
        public List<Face> Faces = new();

        // Build from existing MeshData after tesselation
        public ModularMeshData FromMeshData(MeshData mesh, TextureAtlasPosition texPos)
        {
            var mod = new ModularMeshData();
            mod.OriginalTextureIds = mesh.TextureIds;

            var indexToVertex = new Dictionary<int, Vertex>();
            var positionToVertex = new Dictionary<string, Vertex>();

            for (int i = 0; i < mesh.VerticesCount; i++)
            {
                Vec3f pos = GeometryHelper.GetVertex(mesh, i);
                string key = $"{pos.X}_{pos.Y}_{pos.Z}";

                if (!positionToVertex.ContainsKey(key))
                {
                    var vertex = new Vertex { Position = pos };
                    positionToVertex[key] = vertex;
                    mod.Vertices.Add(vertex);
                }

                indexToVertex[i] = positionToVertex[key];
            }

            for (int i = 0; i < mesh.IndicesCount; i += 6)
            {
                int i0 = mesh.Indices[i];
                int i1 = mesh.Indices[i + 1];
                int i2 = mesh.Indices[i + 2];
                int i3 = mesh.Indices[i + 5];

                var face = new Face
                {
                    Vertices =
                    [
                        indexToVertex[i0],
                        indexToVertex[i1],
                        indexToVertex[i2],
                        indexToVertex[i3]
                    ],
                    RenderData = 
                    [
                        new VertexRenderData
                        {
                            U = mesh.Uv[i0 * 2],
                            V = mesh.Uv[i0 * 2 + 1],
                            Flags = mesh.Flags != null ? mesh.Flags[i0] : 0,
                            TextureIndex = mesh.TextureIndices != null ? mesh.TextureIndices[i/6] : (byte)0
                        },
                        new VertexRenderData
                        {
                            U = mesh.Uv[i1 * 2],
                            V = mesh.Uv[i1 * 2 + 1],
                            Flags = mesh.Flags != null ? mesh.Flags[i1] : 0,
                            TextureIndex = mesh.TextureIndices != null ? mesh.TextureIndices[i/6] : (byte)0
                        },
                        new VertexRenderData
                        {
                            U = mesh.Uv[i2 * 2],
                            V = mesh.Uv[i2 * 2 + 1],
                            Flags = mesh.Flags != null ? mesh.Flags[i2] : 0,
                            TextureIndex = mesh.TextureIndices != null ? mesh.TextureIndices[i/6] : (byte)0
                        },
                        new VertexRenderData
                        {
                            U = mesh.Uv[i3 * 2],
                            V = mesh.Uv[i3 * 2 + 1],
                            Flags = mesh.Flags != null ? mesh.Flags[i3] : 0,
                            TextureIndex = mesh.TextureIndices != null ? mesh.TextureIndices[i/6] : (byte)0
                        }
                    ],
                    TexPos = texPos
                };
                face.RecalculateNormal();

                foreach (var v in face.Vertices.Distinct())
                    v.AdjacentFaces.Add(face);

                mod.Faces.Add(face);
            }

            return mod;
        }

        // Compile back to MeshData for rendering
        public MeshData ToMeshData()
        {
            MeshData mesh = new MeshData(Faces.Count * 4, Faces.Count * 6);
            mesh.TextureIndices = new byte[Faces.Count]; // one per face, not per vertex

            int faceIdx = 0;
            foreach (var face in Faces)
            {
                int baseIndex = mesh.VerticesCount;

                for (int i = 0; i < face.Vertices.Length; i++)
                {
                    Vertex v = face.Vertices[i];
                    if(face.RenderData != null)
                    {
                        VertexRenderData rd = face.RenderData[i];
                        int idx = mesh.VerticesCount;

                        mesh.AddVertex(v.Position.X, v.Position.Y, v.Position.Z, rd.U, rd.V, ColorUtil.WhiteArgb);
                        mesh.Flags[idx] = rd.Flags;
                    }

                }
                
                if (face.RenderData != null)
                {
                    mesh.TextureIndices[faceIdx] = face.RenderData[0].TextureIndex;
                }

                faceIdx++;

                mesh.AddIndex(baseIndex);
                mesh.AddIndex(baseIndex + 1);
                mesh.AddIndex(baseIndex + 2);
                mesh.AddIndex(baseIndex);
                mesh.AddIndex(baseIndex + 2);
                mesh.AddIndex(baseIndex + 3);
            }

            mesh.TextureIds = OriginalTextureIds;
            return mesh;
        }

        // Now geometric operations become clean and self-contained
        public void GrindCorner(Vertex corner, float amount)
        {
            // All adjacent faces know about this vertex directly
            foreach (var face in corner.AdjacentFaces)
            {
                Vec3f edgeDir = GetEdgeDirectionAwayFromCorner(face, corner);
                corner.Position += edgeDir * amount;
                face.RecalculateNormal();
            }
        }

        public void AddClipFace(Vertex v0, Vertex v1, Vertex v2, TextureAtlasPosition texPos)
        {
            // Calculate UVs for the new face's normal
            Vec3f normal = GeometryHelper.CalculateQuadNormal(
                v0.Position, v1.Position, v2.Position, v2.Position);

            // Only set UVs if these are genuinely new vertices without existing UVs
            // Clip face vertices are references to existing vertices so their UVs are already set

            var face = new Face
            {
                Vertices = new Vertex[] { v0, v1, v2, v2 },
                TexPos = texPos
            };
            face.RecalculateNormal();

            foreach (var v in face.Vertices.Distinct())
                v.AdjacentFaces.Add(face);

            Faces.Add(face);
        }

        private Vec3f GetEdgeDirectionAwayFromCorner(Face face, Vertex corner)
        {
            // Find the vertex on this face furthest from the corner
            Vec3f dir = Vec3f.Zero;
            foreach (var v in face.Vertices)
            {
                if (v != corner)
                {
                    dir += v.Position - corner.Position;
                }
            }
            dir.Normalize();
            return dir;
        }
    }
}
