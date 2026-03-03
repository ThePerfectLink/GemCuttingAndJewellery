using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace GemCuttingAndJewellery.Systems
{
    public class ModularMeshData
    {
        public class Vertex
        {
            public Vec3f Position;
            public List<Face> AdjacentFaces = new();
        }

        public class Face
        {
            public Vertex[] Vertices; // always 4 (degenerate quad for tris)
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
        public static ModularMeshData FromMeshData(MeshData mesh, TextureAtlasPosition texPos)
        {
            var mod = new ModularMeshData();

            // Build vertices
            for (int i = 0; i < mesh.VerticesCount; i++)
                mod.Vertices.Add(new Vertex { Position = GeometryHelper.GetVertex(mesh, i) });

            // Build faces from index groups of 4
            for (int i = 0; i < mesh.IndicesCount; i += 6) // 6 indices per quad (2 tris)
            {
                int i0 = mesh.Indices[i];
                int i1 = mesh.Indices[i + 1];
                int i2 = mesh.Indices[i + 2];
                int i3 = mesh.Indices[i + 4]; // second tri gives us 4th vert

                var face = new Face
                {
                    Vertices = new Vertex[]
                    {
                    mod.Vertices[i0],
                    mod.Vertices[i1],
                    mod.Vertices[i2],
                    mod.Vertices[i3]
                    },
                    TexPos = texPos
                };
                face.RecalculateNormal();

                // Register adjacency
                foreach (var v in face.Vertices)
                    v.AdjacentFaces.Add(face);

                mod.Faces.Add(face);
            }

            return mod;
        }

        // Compile back to MeshData for rendering
        public MeshData ToMeshData()
        {
            MeshData mesh = new MeshData(Vertices.Count, Faces.Count * 6);

            // Recalculate all normals before compiling
            foreach (var face in Faces)
                face.RecalculateNormal();

            foreach (var face in Faces)
            {
                int baseIndex = mesh.VerticesCount;
                int encodedNormal = VertexFlags.PackNormal(
                    face.Normal.X, face.Normal.Y, face.Normal.Z);

                foreach (var v in face.Vertices)
                {
                    var (u, w) = GeometryHelper.TriplanarUV(v.Position, face.Normal, face.TexPos);
                    mesh.AddVertex(v.Position.X, v.Position.Y, v.Position.Z, u, w, encodedNormal);
                }

                // Quad as two triangles
                mesh.AddIndex(baseIndex);
                mesh.AddIndex(baseIndex + 1);
                mesh.AddIndex(baseIndex + 2);
                mesh.AddIndex(baseIndex);
                mesh.AddIndex(baseIndex + 2);
                mesh.AddIndex(baseIndex + 3);
            }

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
            // Duplicate v2 as degenerate quad
            var face = new Face
            {
                Vertices = new Vertex[] { v0, v1, v2, v2 },
                TexPos = texPos
            };
            face.RecalculateNormal();

            foreach (var v in face.Vertices)
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
