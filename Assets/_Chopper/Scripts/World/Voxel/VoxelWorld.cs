using System.Collections.Generic;
using Chopper.World.Generation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chopper.World.Voxel
{
    // Meshes the column table as chunks. Every peer gets the chunk colliders (W5-08 step 5: they replace the Terrain
    // collider, so players, the server's NavMesh bake and placement all stand on the blocks); peers with a screen also
    // draw the chunks in place of the terrain paint.
    public static class VoxelWorld
    {
        public const int ChunkSize = 32;
        public const float Chamfer = 0.1f;
        public static bool Bevel = true;

        private static Transform _root;

        // The ground itself (the block chunks, or the Terrain if it is still in use): placement and probes skip it.
        public static bool IsGround(Collider collider) =>
            collider is TerrainCollider || (_root != null && collider != null && collider.transform.parent == _root);

        public static int Present(VoxelColumns columns, Transform parent, Terrain terrain, bool graphics = true)
        {
            BlockPalette.Ensure();
            Material material = null;
            if (graphics)
            {
                material = Resources.Load<Material>("M_Blocks");
                if (material == null)
                {
                    Shader shader = Shader.Find("Chopper/Block");
                    if (shader == null) Debug.LogError("[Voxel] Chopper/Block shader is missing. Terrain paint left on.");
                    else material = new Material(shader) { name = "M_Blocks" };
                }
            }

            var root = new GameObject("VoxelWorld");
            root.transform.SetParent(parent, false);
            _root = root.transform;
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
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                    if (material == null) continue;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    // Flat tops shadow themselves into a black field. Steps get their shade from the face normal.
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            if (terrain != null && triangles > 0)
            {
                if (material != null) terrain.drawHeightmap = false;
                TerrainCollider smooth = terrain.GetComponent<TerrainCollider>();
                if (smooth != null) smooth.enabled = false;
            }
            return triangles;
        }

        public static void Forget()
        {
            _root = null;
            VoxelColumns.Current = null;
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
            var uvs = new List<Vector2>(4096);
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
                    if (Bevel && IsRim(columns, gx, gz))
                    {
                        used[z, x] = true;
                        float ry = height * VoxelColumns.Step;
                        float rx = VoxelColumns.WorldX(gx);
                        float rz = VoxelColumns.WorldZ(gz);
                        float xa = rx + (Lower(columns, gx, gz, -1, 0) ? Chamfer : 0f);
                        float xb = rx + 1f - (Lower(columns, gx, gz, 1, 0) ? Chamfer : 0f);
                        float za = rz + (Lower(columns, gx, gz, 0, -1) ? Chamfer : 0f);
                        float zb = rz + 1f - (Lower(columns, gx, gz, 0, 1) ? Chamfer : 0f);
                        AddQuad(verts, normals, colors, uvs, ry, tris,
                            new Vector3(xa, ry, za), new Vector3(xb, ry, za), new Vector3(xb, ry, zb), new Vector3(xa, ry, zb),
                            Vector3.up, Tagged(BlockPalette.Of(type).Top, type));
                        continue;
                    }
                    int width = 1;
                    while (x + width < ChunkSize && !used[z, x + width]
                           && columns.TopUnits(gx + width, gz) == height
                           && columns.TypeAt(gx + width, gz) == type
                           && !(Bevel && IsRim(columns, gx + width, gz)))
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
                    Color color = Tagged(BlockPalette.Of(type).Top, type);
                    AddQuad(verts, normals, colors, uvs, y, tris,
                        new Vector3(wx, y, wz),
                        new Vector3(wx + width, y, wz),
                        new Vector3(wx + width, y, wz + depth),
                        new Vector3(wx, y, wz + depth),
                        Vector3.up, color);
                }
            }

            AddSides(columns, x0, z0, verts, normals, colors, uvs, tris, alongX: true);
            AddSides(columns, x0, z0, verts, normals, colors, uvs, tris, alongX: false);

            var mesh = new Mesh { name = "VoxelChunk", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
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
                if (Bevel && IsRim(columns, gx + i, gz)) return false;
            }
            return true;
        }

        // Chopper/Block reads the block type from the colour's alpha (type / 8 + 1 / 16).
        static bool Lower(VoxelColumns columns, int x, int z, int dx, int dz)
        {
            int nx = x + dx, nz = z + dz;
            return columns.Inside(nx, nz) && columns.TopUnits(nx, nz) < columns.TopUnits(x, z);
        }

        static bool IsRim(VoxelColumns columns, int x, int z) =>
            Lower(columns, x, z, 1, 0) || Lower(columns, x, z, -1, 0) || Lower(columns, x, z, 0, 1) || Lower(columns, x, z, 0, -1);

        static Color Tagged(Color color, BlockType type)
        {
            color.a = (int)type / 8f + 1f / 16f;
            return color;
        }

        static void AddSides(VoxelColumns columns, int x0, int z0, List<Vector3> verts, List<Vector3> normals, List<Color> colors, List<Vector2> uvs, List<int> tris, bool alongX)
        {
            var used = new bool[ChunkSize, ChunkSize];
            for (int z = 0; z < ChunkSize; z++)
            {
                for (int x = 0; x < ChunkSize; x++)
                {
                    if (used[z, x]) continue;
                    int gx = x0 + x;
                    int gz = z0 + z;
                    if (!TryDrop(columns, gx, gz, alongX, out short height, out short neighbor, out BlockType type, out bool rise)) continue;
                    int run = 1;
                    while (run < (alongX ? ChunkSize - z : ChunkSize - x))
                    {
                        int nx = alongX ? x : x + run;
                        int nz = alongX ? z + run : z;
                        if (used[nz, nx]) break;
                        if (!TryDrop(columns, x0 + nx, z0 + nz, alongX, out short h2, out short n2, out BlockType t2, out bool r2)) break;
                        if (h2 != height || n2 != neighbor || t2 != type || r2 != rise) break;
                        run++;
                    }
                    for (int i = 0; i < run; i++)
                    {
                        if (alongX) used[z + i, x] = true;
                        else used[z, x + i] = true;
                    }

                    float y0 = neighbor * VoxelColumns.Step;
                    float top = height * VoxelColumns.Step;
                    float ch = Bevel ? Chamfer : 0f;
                    float y1 = top - ch;
                    Color color = Tagged(BlockPalette.Of(type).Side, type);
                    Color rim = Tagged(BlockPalette.Of(type).Top, type);
                    float s2 = 0.70710678f;
                    // A drop faces +X/+Z (out of this column); a rise is the neighbour's wall and faces back -X/-Z.
                    // The winding flips with it so physics and lighting both see the face from the open side.
                    if (alongX)
                    {
                        float faceX = VoxelColumns.WorldX(gx) + 1f;
                        float wz = VoxelColumns.WorldZ(gz);
                        Vector3 a = new Vector3(faceX, y0, wz), b = new Vector3(faceX, y0, wz + run);
                        Vector3 c = new Vector3(faceX, y1, wz + run), d = new Vector3(faceX, y1, wz);
                        if (rise) AddQuad(verts, normals, colors, uvs, top, tris, b, a, d, c, Vector3.left, color);
                        else AddQuad(verts, normals, colors, uvs, top, tris, a, b, c, d, Vector3.right, color);
                        if (ch > 0f)
                        {
                            float back = rise ? ch : -ch;
                            Vector3 e = new Vector3(faceX, y1, wz), f = new Vector3(faceX, y1, wz + run);
                            Vector3 g = new Vector3(faceX + back, top, wz + run), h = new Vector3(faceX + back, top, wz);
                            if (rise) AddQuad(verts, normals, colors, uvs, top, tris, f, e, h, g, new Vector3(-s2, s2, 0f), rim);
                            else AddQuad(verts, normals, colors, uvs, top, tris, e, f, g, h, new Vector3(s2, s2, 0f), rim);
                        }
                    }
                    else
                    {
                        float faceZ = VoxelColumns.WorldZ(gz) + 1f;
                        float wx = VoxelColumns.WorldX(gx);
                        Vector3 a = new Vector3(wx, y0, faceZ), b = new Vector3(wx, y1, faceZ);
                        Vector3 c = new Vector3(wx + run, y1, faceZ), d = new Vector3(wx + run, y0, faceZ);
                        if (rise) AddQuad(verts, normals, colors, uvs, top, tris, d, c, b, a, Vector3.back, color);
                        else AddQuad(verts, normals, colors, uvs, top, tris, a, b, c, d, Vector3.forward, color);
                        if (ch > 0f)
                        {
                            float back = rise ? ch : -ch;
                            Vector3 e = new Vector3(wx, y1, faceZ), f = new Vector3(wx, top, faceZ + back);
                            Vector3 g = new Vector3(wx + run, top, faceZ + back), h = new Vector3(wx + run, y1, faceZ);
                            if (rise) AddQuad(verts, normals, colors, uvs, top, tris, h, g, f, e, new Vector3(0f, s2, -s2), rim);
                            else AddQuad(verts, normals, colors, uvs, top, tris, e, f, g, h, new Vector3(0f, s2, s2), rim);
                        }
                    }
                }
            }
        }

        // The wall between a column and its +X (or +Z) neighbour, if their tops differ: height is the higher top,
        // neighbor the lower, type the higher column's block. rise = the neighbour is the higher one.
        static bool TryDrop(VoxelColumns columns, int x, int z, bool alongX, out short height, out short neighbor, out BlockType type, out bool rise)
        {
            short own = columns.TopUnits(x, z);
            int nx = alongX ? x + 1 : x;
            int nz = alongX ? z : z + 1;
            bool inside = columns.Inside(nx, nz);
            short other = inside ? columns.TopUnits(nx, nz) : own;
            rise = other > own;
            height = rise ? other : own;
            neighbor = rise ? own : other;
            type = rise ? columns.TypeAt(nx, nz) : columns.TypeAt(x, z);
            return other != own;
        }

        // faceTop: the height of the face's top edge (the column top), which the shader hangs the grass lip from.
        static void AddQuad(List<Vector3> verts, List<Vector3> normals, List<Color> colors, List<Vector2> uvs, float faceTop, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color color)
        {
            int i = verts.Count;
            for (int k = 0; k < 4; k++) uvs.Add(new Vector2(faceTop, 0f));
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
            // Unity's front face is clockwise seen from the front: a, c, b gives cross(c - a, b - a) along the
            // normal. (The first chunks wound a, b, c: every face pointed into the ground. The shader draws both
            // sides, so it only showed once the chunks became colliders and players fell through.)
            tris.Add(i);
            tris.Add(i + 2);
            tris.Add(i + 1);
            tris.Add(i);
            tris.Add(i + 3);
            tris.Add(i + 2);
        }
    }
}
