using System.Collections.Generic;
using Chopper.World.Generation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chopper.World.Voxel
{
    // Meshes the column table as chunks and draws them in place of the terrain paint.
    // The Terrain collider and WorldGround stay, so movement is unchanged.
    public static class VoxelWorld
    {
        public const int ChunkSize = 32;

        public static int Present(VoxelColumns columns, Transform parent, Terrain terrain)
        {
            BlockPalette.Ensure();
            Material material = Resources.Load<Material>("M_Blocks");
            if (material == null)
            {
                Shader shader = Shader.Find("Chopper/Block");
                if (shader == null)
                {
                    Debug.LogError("[Voxel] Chopper/Block shader is missing. Terrain paint left on.");
                    return 0;
                }
                material = new Material(shader) { name = "M_Blocks" };
            }

            var root = new GameObject("VoxelWorld");
            root.transform.SetParent(parent, false);
            int triangles = 0;
            int chunks = VoxelColumns.Resolution / ChunkSize;
            for (int cz = 0; cz < chunks; cz++)
            {
                for (int cx = 0; cx < chunks; cx++)
                {
                    Mesh mesh = BuildChunk(columns, cx * ChunkSize, cz * ChunkSize);
                    if (mesh.vertexCount == 0)
                    {
                        Object.Destroy(mesh);
                        continue;
                    }
                    triangles += mesh.triangles.Length / 3;
                    var go = new GameObject("Chunk_" + cx + "_" + cz);
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    // Flat tops shadow themselves into a black field. Steps get their shade from the face normal.
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            if (terrain != null && triangles > 0) terrain.drawHeightmap = false;
            return triangles;
        }

        public static int CountTriangles(VoxelColumns columns)
        {
            BlockPalette.Ensure();
            int triangles = 0;
            int chunks = VoxelColumns.Resolution / ChunkSize;
            for (int cz = 0; cz < chunks; cz++)
            {
                for (int cx = 0; cx < chunks; cx++)
                {
                    Mesh mesh = BuildChunk(columns, cx * ChunkSize, cz * ChunkSize);
                    triangles += mesh.triangles.Length / 3;
                    Object.DestroyImmediate(mesh);
                }
            }
            return triangles;
        }

        static Mesh BuildChunk(VoxelColumns columns, int x0, int z0)
        {
            var verts = new List<Vector3>(4096);
            var normals = new List<Vector3>(4096);
            var colors = new List<Color>(4096);
            var tris = new List<int>(8192);
            var used = new bool[ChunkSize, ChunkSize];

            for (int z = 0; z < ChunkSize; z++)
            {
                for (int x = 0; x < ChunkSize; x++)
                {
                    if (used[z, x]) continue;
                    int gx = x0 + x;
                    int gz = z0 + z;
                    short height = columns.TopUnits(gx, gz);
                    BlockType type = columns.TypeAt(gx, gz);
                    int width = 1;
                    while (x + width < ChunkSize && !used[z, x + width]
                           && columns.TopUnits(gx + width, gz) == height
                           && columns.TypeAt(gx + width, gz) == type)
                        width++;
                    int depth = 1;
                    while (z + depth < ChunkSize && RowMatches(columns, used, x, z + depth, width, gx, gz + depth, height, type))
                        depth++;
                    for (int dz = 0; dz < depth; dz++)
                        for (int dx = 0; dx < width; dx++)
                            used[z + dz, x + dx] = true;

                    float y = height * VoxelColumns.Step;
                    float wx = VoxelColumns.WorldX(gx);
                    float wz = VoxelColumns.WorldZ(gz);
                    Color color = BlockPalette.Of(type).Top;
                    AddQuad(verts, normals, colors, tris,
                        new Vector3(wx, y, wz),
                        new Vector3(wx + width, y, wz),
                        new Vector3(wx + width, y, wz + depth),
                        new Vector3(wx, y, wz + depth),
                        Vector3.up, color);
                }
            }

            AddSides(columns, x0, z0, verts, normals, colors, tris, alongX: true);
            AddSides(columns, x0, z0, verts, normals, colors, tris, alongX: false);

            var mesh = new Mesh { name = "VoxelChunk", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static bool RowMatches(VoxelColumns columns, bool[,] used, int x, int z, int width, int gx, int gz, short height, BlockType type)
        {
            for (int i = 0; i < width; i++)
            {
                if (used[z, x + i]) return false;
                if (columns.TopUnits(gx + i, gz) != height || columns.TypeAt(gx + i, gz) != type) return false;
            }
            return true;
        }

        static void AddSides(VoxelColumns columns, int x0, int z0, List<Vector3> verts, List<Vector3> normals, List<Color> colors, List<int> tris, bool alongX)
        {
            var used = new bool[ChunkSize, ChunkSize];
            for (int z = 0; z < ChunkSize; z++)
            {
                for (int x = 0; x < ChunkSize; x++)
                {
                    if (used[z, x]) continue;
                    int gx = x0 + x;
                    int gz = z0 + z;
                    if (!TryDrop(columns, gx, gz, alongX, out short height, out short neighbor, out BlockType type)) continue;
                    int run = 1;
                    while (run < (alongX ? ChunkSize - z : ChunkSize - x))
                    {
                        int nx = alongX ? x : x + run;
                        int nz = alongX ? z + run : z;
                        if (used[nz, nx]) break;
                        if (!TryDrop(columns, x0 + nx, z0 + nz, alongX, out short h2, out short n2, out BlockType t2)) break;
                        if (h2 != height || n2 != neighbor || t2 != type) break;
                        run++;
                    }
                    for (int i = 0; i < run; i++)
                    {
                        if (alongX) used[z + i, x] = true;
                        else used[z, x + i] = true;
                    }

                    float y0 = neighbor * VoxelColumns.Step;
                    float y1 = height * VoxelColumns.Step;
                    Color color = BlockPalette.Of(type).Side;
                    if (alongX)
                    {
                        float faceX = VoxelColumns.WorldX(gx) + 1f;
                        float wz = VoxelColumns.WorldZ(gz);
                        AddQuad(verts, normals, colors, tris,
                            new Vector3(faceX, y0, wz),
                            new Vector3(faceX, y0, wz + run),
                            new Vector3(faceX, y1, wz + run),
                            new Vector3(faceX, y1, wz),
                            Vector3.right, color);
                    }
                    else
                    {
                        float faceZ = VoxelColumns.WorldZ(gz) + 1f;
                        float wx = VoxelColumns.WorldX(gx);
                        AddQuad(verts, normals, colors, tris,
                            new Vector3(wx, y0, faceZ),
                            new Vector3(wx, y1, faceZ),
                            new Vector3(wx + run, y1, faceZ),
                            new Vector3(wx + run, y0, faceZ),
                            Vector3.forward, color);
                    }
                }
            }
        }

        static bool TryDrop(VoxelColumns columns, int x, int z, bool alongX, out short height, out short neighbor, out BlockType type)
        {
            height = columns.TopUnits(x, z);
            type = columns.TypeAt(x, z);
            int nx = alongX ? x + 1 : x;
            int nz = alongX ? z : z + 1;
            neighbor = columns.Inside(nx, nz) ? columns.TopUnits(nx, nz) : height;
            return neighbor < height;
        }

        static void AddQuad(List<Vector3> verts, List<Vector3> normals, List<Color> colors, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color color)
        {
            int i = verts.Count;
            verts.Add(a);
            verts.Add(b);
            verts.Add(c);
            verts.Add(d);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            tris.Add(i);
            tris.Add(i + 1);
            tris.Add(i + 2);
            tris.Add(i);
            tris.Add(i + 2);
            tris.Add(i + 3);
        }
    }
}
