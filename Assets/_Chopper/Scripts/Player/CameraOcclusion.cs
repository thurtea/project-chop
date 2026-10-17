using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chopper.Player
{
    public static class CameraOcclusion
    {
        private static readonly Dictionary<Renderer, ShadowCastingMode> Hidden = new Dictionary<Renderer, ShadowCastingMode>();
        private static readonly HashSet<Renderer> Now = new HashSet<Renderer>();
        private static readonly List<Renderer> Restore = new List<Renderer>();
        public static int HiddenCount => Hidden.Count;

        public static bool Fades(Collider c, Transform self, Transform hut)
        {
            if (c == null || c is CharacterController || c is TerrainCollider) return false;
            Transform t = c.transform;
            if (self != null && t.IsChildOf(self)) return false;
            if (hut != null && t.IsChildOf(hut)) return false;
            if (t.name.StartsWith("Chunk_") || t.name.StartsWith("Block")) return false;
            Bounds b = c.bounds;
            return b.size.y > 1.6f && b.size.x < 14f && b.size.z < 14f;
        }

        public static void Tick(Vector3 eye, Vector3 target, Transform self)
        {
            Now.Clear();
            Transform hut = Chopper.World.Hut.Instance != null ? Chopper.World.Hut.Instance.transform : null;
            Vector3 d = target - eye;
            float len = d.magnitude;
            if (len > 0.05f)
            {
                foreach (RaycastHit h in Physics.SphereCastAll(eye, 0.25f, d / len, len - 0.3f, ~0, QueryTriggerInteraction.Collide))
                {
                    if (!Fades(h.collider, self, hut)) continue;
                    Transform t = h.collider.transform;
                    if (t.GetComponentInChildren<Renderer>() == null && t.parent != null) t = t.parent;
                    foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
                    {
                        if (r.bounds.size.magnitude > 40f) continue;
                        Now.Add(r);
                        if (!Hidden.ContainsKey(r)) { Hidden[r] = r.shadowCastingMode; r.shadowCastingMode = ShadowCastingMode.ShadowsOnly; }
                    }
                }
            }
            Restore.Clear();
            foreach (var kv in Hidden) if (kv.Key == null || !Now.Contains(kv.Key)) Restore.Add(kv.Key);
            foreach (Renderer r in Restore)
            {
                if (r != null) r.shadowCastingMode = Hidden[r];
                Hidden.Remove(r);
            }
        }
    }
}
