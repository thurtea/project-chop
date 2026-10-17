using System;
using System.Collections.Generic;
using Chopper.World.Voxel;
using UnityEngine;

namespace Chopper.World
{
    public static class ScatterMask
    {
        public const float Margin = 0.4f;
        public const float HutMargin = 0.6f;
        public const float BelowReach = 1.5f;
        public const float AboveReach = 0.3f;

        private static readonly Dictionary<object, List<Bounds>> _zones = new Dictionary<object, List<Bounds>>();

        public static event Action<Bounds> Changed;

        public static int Count => _zones.Count;

        public static void Set(object owner, Bounds bounds) => Set(owner, new List<Bounds> { bounds });

        public static void Set(object owner, List<Bounds> parts)
        {
            if (owner == null || parts == null) return;
            Clear(owner);
            if (parts.Count == 0) return;
            _zones[owner] = parts;
            Changed?.Invoke(Union(parts));
        }

        public static void SetFrom(object owner, GameObject root) => Set(owner, PartsOf(root));

        public static void Clear(object owner)
        {
            if (owner == null || !_zones.TryGetValue(owner, out List<Bounds> old)) return;
            _zones.Remove(owner);
            Changed?.Invoke(Union(old));
        }

        public static Bounds Union(List<Bounds> parts)
        {
            Bounds u = parts[0];
            for (int i = 1; i < parts.Count; i++) u.Encapsulate(parts[i]);
            return u;
        }

        public static void ClearAll() => _zones.Clear();

        public static bool InHutFootprint(float x, float z) =>
            Mathf.Abs(x) < Generation.WorldGen.HutHalfWidth + HutMargin && Mathf.Abs(z) < Generation.WorldGen.HutHalfDepth + HutMargin;

        public static bool InZone(Bounds b, Vector3 p) =>
            p.x > b.min.x - Margin && p.x < b.max.x + Margin && p.z > b.min.z - Margin && p.z < b.max.z + Margin
            && p.y > b.min.y - BelowReach && p.y < b.max.y + AboveReach;

        public static bool Blocked(Vector3 p)
        {
            if (InHutFootprint(p.x, p.z)) return true;
            foreach (KeyValuePair<object, List<Bounds>> pair in _zones)
                foreach (Bounds b in pair.Value) if (InZone(b, p)) return true;
            return false;
        }

        public static bool PieceNear(Vector3 p, float radius)
        {
            foreach (KeyValuePair<object, List<Bounds>> pair in _zones)
                foreach (Bounds b in pair.Value)
                {
                    if (p.y < b.min.y - BelowReach || p.y > b.max.y + AboveReach) continue;
                    float dx = Mathf.Max(b.min.x - Margin - p.x, 0f, p.x - b.max.x - Margin);
                    float dz = Mathf.Max(b.min.z - Margin - p.z, 0f, p.z - b.max.z - Margin);
                    if (dx * dx + dz * dz < radius * radius) return true;
                }
            return false;
        }

        public static bool GrowsOn(BlockType type) => type == BlockType.Grass || type == BlockType.Dirt;

        public static bool Allows(Vector3 p, bool needsSoil)
        {
            if (Blocked(p)) return false;
            if (!needsSoil) return true;
            VoxelColumns columns = VoxelColumns.Current;
            return columns == null || !columns.TryTopAt(p.x, p.z, out _) || GrowsOn(columns.TypeAtWorld(p.x, p.z));
        }

        public static List<Bounds> PartsOf(GameObject root)
        {
            var parts = new List<Bounds>();
            if (root == null) return parts;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || !r.enabled) continue;
                parts.Add(r.bounds);
            }
            foreach (Collider c in root.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || !c.enabled) continue;
                parts.Add(c.bounds);
            }
            return parts;
        }
    }
}
