using System.Collections.Generic;
using UnityEngine;

namespace Chopper.World.Generation
{
    public static class CaveShell
    {
        public const float Cut = 2.4f;

        public static void Append(List<CryptBox> boxes, int group, Vector3 center, Vector3 size, Quaternion rotation, CryptMaterial material)
        {
            boxes.Add(new CryptBox { Center = center, Size = size, Rotation = rotation, Material = material, Group = group });
        }

        public static void Chamfers(List<CryptBox> boxes, int group, Vector3 center, float half, float height, float cut)
        {
            foreach (float sx in new[] { 1f, -1f })
            {
                foreach (float sz in new[] { 1f, -1f })
                {
                    Vector3 outward = new Vector3(sx, 0f, sz).normalized;
                    float y = height * 0.5f;
                    Vector3 corner = center + new Vector3(sx * half, y, sz * half);
                    Vector3 mid = center + new Vector3(sx * (half - cut * 0.5f), y, sz * (half - cut * 0.5f));
                    float span = cut * 1.41421356f;
                    float depth = Vector3.Distance(corner, mid) + 0.08f;
                    Append(boxes, group, (corner + mid) * 0.5f, new Vector3(span, height, depth), Quaternion.LookRotation(outward), CryptMaterial.Wall);
                }
            }
        }

        public static void FloorRelief(List<CryptBox> boxes, int group, Vector3 origin, float half, int seed, float pathHalf, float inset, System.Func<Vector3, bool> skip)
        {
            for (float x = -half + 1.4f; x <= half - 1.4f; x += 1.6f)
            {
                for (float z = -half + 1.15f; z <= half - 1.15f; z += 1.35f)
                {
                    int h = Hash(seed, Mathf.RoundToInt(x * 10f), Mathf.RoundToInt(z * 10f));
                    float ox = ((h % 5) - 2) * 0.18f;
                    float oz = (((h / 5) % 5) - 2) * 0.14f;
                    var local = new Vector3(x + ox, 0f, z + oz);
                    float ax = Mathf.Abs(local.x);
                    float az = Mathf.Abs(local.z);
                    bool centre = ax < pathHalf && az < pathHalf;
                    bool againstWall = ax > half - 3.1f || az > half - 3.1f;
                    bool doorway = (az > half - 2.4f && ax < 2.6f) || (ax > half - 2.4f && az < 2.6f);
                    bool propCorner = ax > half - inset && az > half - inset;
                    if (centre || !againstWall || doorway || propCorner) continue;
                    if (skip != null && skip(local)) continue;
                    float rise = (h % 3) == 0 ? 0.48f : 0.24f;
                    float w = (h % 2) == 0 ? 1.15f : 0.72f;
                    float d = (h % 2) == 0 ? 0.78f : 1.05f;
                    Append(boxes, group, origin + new Vector3(local.x, rise * 0.5f, local.z), new Vector3(w, rise, d), Quaternion.identity, CryptMaterial.Floor);
                }
            }
        }

        public static void WallRelief(List<CryptBox> boxes, int group, Vector3 origin, float half, float height, int seed, System.Func<Vector2Int, bool> solid)
        {
            Vector2Int[] sides = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            foreach (Vector2Int side in sides)
            {
                if (!solid(side)) continue;
                Vector3 n = new Vector3(side.x, 0f, side.y);
                Vector3 t = new Vector3(side.y, 0f, -side.x);
                for (int i = -3; i <= 3; i++)
                {
                    int h = Hash(seed, side.x * 11 + i, side.y * 17);
                    if (i != 0 && (h % 5) == 0) continue;
                    float along = i * 1.7f + ((h % 5) - 2) * 0.12f;
                    if (Mathf.Abs(along) > half - Cut - 0.4f) continue;
                    float depth = (h % 3) == 0 ? 0.85f : 0.55f;
                    float bh = 0.9f + (h % 4) * 0.55f;
                    float by = i == 0 ? 1.65f : (h % 3) * 0.35f;
                    if (by + bh > height - 0.5f) bh = height - 0.6f - by;
                    if (bh < 0.4f) continue;
                    Vector3 at = origin + n * (half - depth * 0.5f) + t * along + Vector3.up * (by + bh * 0.5f);
                    float yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg + ((h % 5) - 2) * 5f;
                    Append(boxes, group, at, new Vector3(0.85f + (h % 3) * 0.28f, bh, depth), Quaternion.Euler(0f, yaw, 0f), CryptMaterial.Wall);
                }
            }
        }

        public static void StoneCourses(List<CryptBox> boxes, int group, Vector3 origin, float half, float height, int seed, float pathHalf, System.Func<Vector3, bool> skip)
        {
            const float cell = 0.5f;
            for (float x = -half + 0.75f; x <= half - 0.75f; x += cell)
            {
                for (float z = -half + 0.75f; z <= half - 0.75f; z += cell)
                {
                    var local = new Vector3(x, 0f, z);
                    float ax = Mathf.Abs(x);
                    float az = Mathf.Abs(z);
                    bool centre = ax < pathHalf && az < pathHalf;
                    bool againstWall = ax > half - 1.55f || az > half - 1.55f;
                    bool doorway = (az > half - 2.2f && ax < 2.4f) || (ax > half - 2.2f && az < 2.4f);
                    if (centre || !againstWall || doorway) continue;
                    if (skip != null && skip(local)) continue;
                    int h = Hash(seed, Mathf.RoundToInt(x * 8f), Mathf.RoundToInt(z * 8f));
                    float rise = (h % 5) == 0 ? 1.0f : 0.5f;
                    Append(boxes, group, origin + new Vector3(x, rise * 0.5f, z), new Vector3(cell, rise, cell), Quaternion.identity, CryptMaterial.Floor);
                    if ((h % 4) != 0) continue;
                    float wy = 1.25f + (h % 3) * 0.5f;
                    if (wy + cell > height - 0.45f) continue;
                    float nx = ax > az ? Mathf.Sign(x) * (half - 0.35f) : x;
                    float nz = az >= ax ? Mathf.Sign(z) * (half - 0.35f) : z;
                    Append(boxes, group, origin + new Vector3(nx, wy, nz), new Vector3(cell, cell, cell), Quaternion.identity, CryptMaterial.Wall);
                }
            }
        }

        public static void CeilingNibs(List<CryptBox> boxes, int group, Vector3 origin, float half, float height, float cut)
        {
            foreach (float sx in new[] { 1f, -1f })
            {
                foreach (float sz in new[] { 1f, -1f })
                {
                    Vector3 at = origin + new Vector3(sx * (half - cut * 0.65f), height - 0.4f, sz * (half - cut * 0.65f));
                    Append(boxes, group, at, new Vector3(1.8f, 0.8f, 1.5f), Quaternion.identity, CryptMaterial.Ceiling);
                }
            }
        }

        public static void CeilingCourses(List<CryptBox> boxes, int group, Vector3 origin, float half, float height)
        {
            float bottom = 3.2f;
            float top = height - 0.2f;
            float hang = Mathf.Max(0.8f, top - bottom);
            float y = bottom + hang * 0.5f;
            Vector2Int[] sides = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            foreach (Vector2Int side in sides)
            {
                Vector3 n = new Vector3(side.x, 0f, side.y);
                Vector3 t = new Vector3(side.y, 0f, -side.x);
                foreach (float s in new[] { 1f, -1f })
                {
                    float span = half * 0.42f;
                    Vector3 at = origin + n * (half - 1.15f) + t * s * (half * 0.40f) + Vector3.up * y;
                    Vector3 size = new Vector3(Mathf.Abs(n.z) * span + Mathf.Abs(n.x) * 1.5f, hang, Mathf.Abs(n.x) * span + Mathf.Abs(n.z) * 1.5f);
                    Append(boxes, group, at, size, Quaternion.identity, CryptMaterial.Ceiling);
                }
            }
        }

        public static void MouthSteps(List<CryptBox> boxes, int group, Vector3 mouth, Vector3 tangent, Vector3 normal, float width, float openingHeight)
        {
            for (int step = 0; step < 3; step++)
            {
                float inset = 0.85f + step * 0.4f;
                float drop = 0.9f + step * 0.45f;
                float y = openingHeight - drop * 0.5f;
                if (y - drop * 0.5f < 1.55f) continue;
                foreach (float s in new[] { 1f, -1f })
                {
                    Vector3 at = mouth + tangent * s * (width * 0.5f - inset * 0.5f) + normal * 0.2f + Vector3.up * y;
                    Vector3 size = new Vector3(
                        Mathf.Abs(tangent.x) * inset + Mathf.Abs(normal.x) * 0.85f,
                        drop,
                        Mathf.Abs(tangent.z) * inset + Mathf.Abs(normal.z) * 0.85f);
                    Append(boxes, group, at, size, Quaternion.identity, CryptMaterial.Wall);
                }
            }
        }

        public static void CorridorJog(List<CryptBox> boxes, int group, Vector3 mid, Vector3 tangent, Vector3 along, float length, float width, float height)
        {
            Vector3 pinch = mid + along * (length * 0.12f);
            foreach (float s in new[] { 1f, -1f })
            {
                Vector3 at = pinch + tangent * s * (width * 0.5f - 0.42f) + Vector3.up * (height * 0.58f);
                float bh = Mathf.Max(1.1f, height * 0.55f);
                Vector3 size = new Vector3(
                    Mathf.Abs(along.x) * 1.35f + Mathf.Abs(tangent.x) * 0.85f,
                    bh,
                    Mathf.Abs(along.z) * 1.35f + Mathf.Abs(tangent.z) * 0.85f);
                Append(boxes, group, at, size, Quaternion.identity, CryptMaterial.Wall);
            }
            float drop = 1.05f;
            Vector3 ceil = mid + Vector3.up * (height - drop * 0.35f);
            Vector3 csize = new Vector3(
                Mathf.Abs(along.x) * (length * 0.42f) + Mathf.Abs(tangent.x) * (width * 0.62f),
                drop,
                Mathf.Abs(along.z) * (length * 0.42f) + Mathf.Abs(tangent.z) * (width * 0.62f));
            Append(boxes, group, ceil, csize, Quaternion.identity, CryptMaterial.Ceiling);
        }

        public static int Hash(int a, int b, int c)
        {
            unchecked
            {
                int h = a * 73856093 ^ b * 19349663 ^ c * 83492791;
                return h & 0x7fffffff;
            }
        }
    }
}
