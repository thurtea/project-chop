using System.Collections.Generic;
using Chopper.World.Voxel;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chopper.World.Generation
{
    public static class CaveVoxel
    {
        public const float Cell = 0.5f;

        private const byte Empty = 0;
        private const byte Stone = 1;
        private const byte Dirt = 2;
        private const byte Path = 3;
        private const byte Timber = 4;
        private const byte Shadow = 5;
        private const byte Blocked = 255;

        public static Mesh Build(List<CryptBox> boxes) => Build(boxes, out _);

        public static Mesh Build(List<CryptBox> boxes, out Mesh crystals) => Build(boxes, out crystals, true);

        public static Mesh Build(List<CryptBox> boxes, out Mesh crystals, bool rough)
        {
            crystals = null;
            if (boxes == null || boxes.Count == 0) return null;
            BlockPalette.Ensure();
            Bounds bounds = BoundsOf(boxes);
            bounds.Expand(Cell * 4f);
            Vector3 min = bounds.min;
            float floorTop = float.MinValue;
            foreach (CryptBox box in boxes)
                if (box.Material == CryptMaterial.Floor) floorTop = Mathf.Max(floorTop, box.Center.y + box.Size.y * 0.5f);
            if (floorTop > float.MinValue) min.y = floorTop - Cell * Mathf.Ceil((floorTop - min.y) / Cell);
            int nx = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / Cell));
            int ny = Mathf.Max(1, Mathf.CeilToInt((bounds.max.y - min.y) / Cell) + 1);
            int nz = Mathf.Max(1, Mathf.CeilToInt(bounds.size.z / Cell));
            var grid = new byte[nx, ny, nz];
            foreach (CryptBox box in boxes)
            {
                if (box.Material == CryptMaterial.Void && Mathf.Min(box.Size.x, Mathf.Min(box.Size.y, box.Size.z)) < 0.2f)
                {
                    Stamp(grid, min, nx, ny, nz, box, Shadow);
                    continue;
                }
                if (box.Material == CryptMaterial.Void)
                {
                    Stamp(grid, min, nx, ny, nz, box, Blocked);
                    continue;
                }
                byte kind = KindOf(box.Material, box.Center);
                Stamp(grid, min, nx, ny, nz, box, kind);
            }
            if (!rough)
            {
                crystals = new Mesh();
                return MeshGrid(grid, nx, ny, nz, min);
            }
            Terrace(grid, nx, ny, nz);
            Roughen(grid, nx, ny, nz, min);
            Roughen(grid, nx, ny, nz, min);
            Roughen(grid, nx, ny, nz, min);
            crystals = Crystals(grid, nx, ny, nz, min);
            return MeshGrid(grid, nx, ny, nz, min);
        }

        public static Mesh BuildOre(Vector3 origin, Vector3 normal, Color glow, int seed)
        {
            BlockPalette.Ensure();
            var verts = new List<Vector3>(256);
            var normals = new List<Vector3>(256);
            var colors = new List<Color>(256);
            var uvs = new List<Vector2>(256);
            var tris = new List<int>(384);
            Vector3 right = Vector3.Cross(Vector3.up, normal);
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(normal, right).normalized;
            float stoneAlpha = (int)BlockType.Stone / 8f + 1f / 16f;
            Color iron = new Color(0.07f, 0.09f, 0.12f, stoneAlpha);
            Color ironBlue = new Color(0.10f, 0.20f, 0.30f, stoneAlpha);
            Color shine = new Color(0.30f, 0.90f, 1.00f, stoneAlpha);
            for (int ix = -2; ix <= 2; ix++)
            {
                for (int iy = -1; iy <= 1; iy++)
                {
                    int h = CaveShell.Hash(seed + ix * 19, iy * 23, 3);
                    if (Mathf.Abs(ix) == 2 && (h % 3) == 0) continue;
                    Vector3 center = origin + right * (ix * 0.34f) + up * (iy * 0.3f + ix * 0.06f) + normal * 0.08f;
                    bool streak = ((ix + iy + 4) % 3) == 0 || (h % 5) == 0;
                    Color color = streak ? shine : ((h % 2) == 0 ? ironBlue : iron);
                    AddCube(verts, normals, colors, uvs, tris, center, 0.32f, color);
                }
            }
            int[] crystal = { 3, 5, 2 };
            for (int c = 0; c < crystal.Length; c++)
            {
                Vector3 foot = origin + right * (0.95f + c * 0.15f) + up * (-0.25f + (c % 2) * 0.12f) + normal * 0.2f;
                for (int k = 0; k < crystal[c]; k++)
                    AddCube(verts, normals, colors, uvs, tris, foot + normal * (k * 0.12f) + up * (k * 0.05f), 0.13f, shine);
            }
            if (verts.Count == 0) return null;
            var mesh = new Mesh { name = "VeinOre", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Bounds BoundsOf(List<CryptBox> boxes)
        {
            Bounds b = new Bounds(boxes[0].Center, Vector3.zero);
            foreach (CryptBox box in boxes)
                b.Encapsulate(new Bounds(box.Center, box.Size));
            return b;
        }

        private static byte KindOf(CryptMaterial material, Vector3 at)
        {
            int h = CaveShell.Hash(Mathf.RoundToInt(at.x * 8f), Mathf.RoundToInt(at.y * 8f), Mathf.RoundToInt(at.z * 8f));
            if (material == CryptMaterial.Timber) return Timber;
            if (material == CryptMaterial.Floor) return (h % 6) == 0 ? Dirt : Stone;
            return (h % 9) == 0 ? Dirt : Stone;
        }

        private static void Stamp(byte[,,] grid, Vector3 min, int nx, int ny, int nz, CryptBox box, byte kind)
        {
            Vector3 half = box.Size * 0.5f;
            Vector3[] corners = new Vector3[8];
            int i = 0;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        corners[i++] = box.Center + box.Rotation * new Vector3(sx * half.x, sy * half.y, sz * half.z);
            Vector3 bmin = corners[0];
            Vector3 bmax = corners[0];
            for (int c = 1; c < 8; c++)
            {
                bmin = Vector3.Min(bmin, corners[c]);
                bmax = Vector3.Max(bmax, corners[c]);
            }
            int x0 = Mathf.Max(0, Mathf.FloorToInt((bmin.x - min.x) / Cell));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((bmin.y - min.y) / Cell));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((bmin.z - min.z) / Cell));
            int x1 = Mathf.Min(nx - 1, Mathf.FloorToInt((bmax.x - min.x) / Cell));
            int y1 = Mathf.Min(ny - 1, Mathf.FloorToInt((bmax.y - min.y) / Cell));
            int z1 = Mathf.Min(nz - 1, Mathf.FloorToInt((bmax.z - min.z) / Cell));
            Quaternion inv = Quaternion.Inverse(box.Rotation);
            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    for (int z = z0; z <= z1; z++)
                    {
                        Vector3 world = min + new Vector3((x + 0.5f) * Cell, (y + 0.5f) * Cell, (z + 0.5f) * Cell);
                        Vector3 local = inv * (world - box.Center);
                        if (Mathf.Abs(local.x) > half.x + Slack(half.x) || Mathf.Abs(local.y) > half.y + Slack(half.y) || Mathf.Abs(local.z) > half.z + Slack(half.z))
                            continue;
                        if (kind == Blocked) { grid[x, y, z] = Blocked; continue; }
                        if (grid[x, y, z] == Blocked || grid[x, y, z] != Empty) continue;
                        grid[x, y, z] = kind;
                    }
                }
            }
        }

        private static float Slack(float half) => half * 2f < Cell * 1.5f ? Cell * 0.5f - 0.02f : 0.01f;

        private static void Roughen(byte[,,] grid, int nx, int ny, int nz, Vector3 min)
        {
            var extra = new List<(int x, int y, int z, byte k)>();
            var cut = new List<(int x, int y, int z)>();
            for (int x = 1; x < nx - 1; x++)
            {
                for (int y = 1; y < ny - 1; y++)
                {
                    for (int z = 1; z < nz - 1; z++)
                    {
                        byte here = grid[x, y, z];
                        int h = CaveShell.Hash(x * 13, y * 17, z * 19);
                        if (here == Empty)
                        {
                            bool wall = Solid(grid[x + 1, y, z]) || Solid(grid[x - 1, y, z]) || Solid(grid[x, y, z + 1]) || Solid(grid[x, y, z - 1]);
                            bool floor = Solid(grid[x, y - 1, z]);
                            bool high = y >= 4 && !Solid(grid[x, y - 1, z]) && !Solid(grid[x, y - 2, z]) && !Solid(grid[x, y - 3, z]) && !Solid(grid[x, y - 4, z]);
                            if (wall && high && (h % 3) != 0)
                                extra.Add((x, y, z, (h % 5) == 0 ? Dirt : Stone));
                            continue;
                        }
                        if (here == Blocked) continue;
                        if ((h % 7) != 0) continue;
                        if (!OpenTowardEmpty(grid, x, y, z)) continue;
                        cut.Add((x, y, z));
                    }
                }
            }
            foreach (var e in extra)
                if (grid[e.x, e.y, e.z] == Empty) grid[e.x, e.y, e.z] = e.k;
            cut.Clear();
        }

        private static void Terrace(byte[,,] grid, int nx, int ny, int nz)
        {
            var add = new List<(int x, int y, int z)>();
            for (int x = 3; x < nx - 3; x++)
            {
                for (int z = 3; z < nz - 3; z++)
                {
                    for (int y = 1; y < ny - 4; y++)
                    {
                        if (!Solid(grid[x, y, z]) || grid[x, y + 1, z] != Empty || grid[x, y + 2, z] != Empty || grid[x, y + 3, z] != Empty) continue;
                        int near = 9;
                        for (int dx = -3; dx <= 3; dx++)
                            for (int dz = -3; dz <= 3; dz++)
                                if (Solid(grid[x + dx, y + 2, z + dz])) near = Mathf.Min(near, Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)));
                        if (near > 1) continue;
                        int steps = (CaveShell.Hash(x, y, z) % 2) == 0 ? 2 : 1;
                        for (int s = 1; s <= steps; s++) add.Add((x, y + s, z));
                    }
                }
            }
            foreach (var a in add) if (grid[a.x, a.y, a.z] == Empty) grid[a.x, a.y, a.z] = Stone;
        }

        private static bool OpenTowardEmpty(byte[,,] grid, int x, int y, int z)
        {
            int open = 0;
            if (grid[x + 1, y, z] == Empty) open++;
            if (grid[x - 1, y, z] == Empty) open++;
            if (grid[x, y, z + 1] == Empty) open++;
            if (grid[x, y, z - 1] == Empty) open++;
            return open >= 1 && open <= 3;
        }

        private static bool Solid(byte v) => v != Empty && v != Blocked;

        private static Mesh MeshGrid(byte[,,] grid, int nx, int ny, int nz, Vector3 min)
        {
            var verts = new List<Vector3>(4096);
            var normals = new List<Vector3>(4096);
            var colors = new List<Color>(4096);
            var uvs = new List<Vector2>(4096);
            var tris = new List<int>(8192);
            Vector3[] dir = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            int[,] step = { { 1, 0, 0 }, { -1, 0, 0 }, { 0, 1, 0 }, { 0, -1, 0 }, { 0, 0, 1 }, { 0, 0, -1 } };
            for (int x = 0; x < nx; x++)
            {
                for (int y = 0; y < ny; y++)
                {
                    for (int z = 0; z < nz; z++)
                    {
                        byte here = grid[x, y, z];
                        if (!Solid(here)) continue;
                        for (int d = 0; d < 6; d++)
                        {
                            int nx2 = x + step[d, 0];
                            int ny2 = y + step[d, 1];
                            int nz2 = z + step[d, 2];
                            bool outside = nx2 < 0 || ny2 < 0 || nz2 < 0 || nx2 >= nx || ny2 >= ny || nz2 >= nz;
                            if (!outside && Solid(grid[nx2, ny2, nz2])) continue;
                            Vector3 n = dir[d];
                            Vector3 center = min + new Vector3((x + 0.5f) * Cell, (y + 0.5f) * Cell, (z + 0.5f) * Cell);
                            Vector3 face = center + n * (Cell * 0.5f);
                            Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, n);
                            Vector3 v = Vector3.Cross(n, u);
                            float e = Cell * 0.5f;
                            Vector3 a = face + (-u - v) * e;
                            Vector3 b = face + (u - v) * e;
                            Vector3 c = face + (u + v) * e;
                            Vector3 d4 = face + (-u + v) * e;
                            Color color = FaceColor(here, n);
                            float faceTop = center.y + Cell * 0.5f;
                            AddQuad(verts, normals, colors, uvs, tris, a, b, c, d4, n, color, faceTop);
                        }
                    }
                }
            }
            if (verts.Count == 0) return null;
            var mesh = new Mesh { name = "CaveVoxel", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static readonly Color CaveTop = new Color(0.12f, 0.23f, 0.23f);
        public static readonly Color CaveSide = new Color(0.07f, 0.15f, 0.16f);
        public static readonly Color CaveUnder = new Color(0.05f, 0.10f, 0.11f);
        public static readonly Color CaveDirt = new Color(0.10f, 0.16f, 0.15f);
        public static readonly Color CrystalColor = new Color(0.10f, 0.85f, 0.92f);

        private static Color FaceColor(byte kind, Vector3 n)
        {
            Color rgb = kind == Timber ? (n.y > 0.5f ? new Color(0.62f, 0.36f, 0.16f) : new Color(0.48f, 0.26f, 0.11f))
                : kind == Shadow ? new Color(0.012f, 0.014f, 0.016f)
                : kind == Dirt ? CaveDirt : (n.y > 0.5f ? CaveTop : (n.y < -0.5f ? CaveUnder : CaveSide));
            rgb.a = (int)BlockType.Stone / 8f + 1f / 16f;
            return rgb;
        }

        private static Mesh Crystals(byte[,,] grid, int nx, int ny, int nz, Vector3 min)
        {
            var verts = new List<Vector3>(1024);
            var normals = new List<Vector3>(1024);
            var colors = new List<Color>(1024);
            var uvs = new List<Vector2>(1024);
            var tris = new List<int>(2048);
            Color shine = CrystalColor;
            shine.a = (int)BlockType.Stone / 8f + 1f / 16f;
            for (int x = 1; x < nx - 1; x++)
            {
                for (int y = 1; y < ny - 2; y++)
                {
                    for (int z = 1; z < nz - 1; z++)
                    {
                        if (!Solid(grid[x, y, z]) || grid[x, y + 1, z] != Empty || grid[x, y + 2, z] != Empty) continue;
                        int h = CaveShell.Hash(x * 7, y * 11, z * 5);
                        if ((h % 17) != 0) continue;
                        Vector3 top = min + new Vector3((x + 0.5f) * Cell, (y + 1f) * Cell, (z + 0.5f) * Cell);
                        int count = 2 + (h % 3);
                        for (int c = 0; c < count; c++)
                        {
                            int hc = CaveShell.Hash(h + c * 31, c, 9);
                            float ox = ((hc % 5) - 2) * 0.07f;
                            float oz = (((hc / 5) % 5) - 2) * 0.07f;
                            int tall = 1 + (hc % 3);
                            for (int k = 0; k < tall; k++)
                                AddCube(verts, normals, colors, uvs, tris, top + new Vector3(ox, 0.05f + k * 0.1f, oz), 0.1f, shine);
                        }
                    }
                }
            }
            if (verts.Count == 0) return null;
            var mesh = new Mesh { name = "CaveCrystals", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCube(List<Vector3> verts, List<Vector3> normals, List<Color> colors, List<Vector2> uvs, List<int> tris, Vector3 center, float size, Color color)
        {
            Vector3[] dir = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            float e = size * 0.5f;
            foreach (Vector3 n in dir)
            {
                Vector3 face = center + n * e;
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, n);
                Vector3 v = Vector3.Cross(n, u);
                AddQuad(verts, normals, colors, uvs, tris,
                    face + (-u - v) * e, face + (u - v) * e, face + (u + v) * e, face + (-u + v) * e,
                    n, color, center.y + e);
            }
        }

        private static void AddQuad(List<Vector3> verts, List<Vector3> normals, List<Color> colors, List<Vector2> uvs, List<int> tris,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Color color, float faceTop)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            normals.Add(n); normals.Add(n); normals.Add(n); normals.Add(n);
            colors.Add(color); colors.Add(color); colors.Add(color); colors.Add(color);
            Vector2 uv = new Vector2(faceTop, 0f);
            uvs.Add(uv); uvs.Add(uv); uvs.Add(uv); uvs.Add(uv);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
            tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
        }
    }
}
