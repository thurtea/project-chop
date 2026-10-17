using UnityEngine;

namespace Chopper.Core
{
    public static class MoveRules
    {
        public const float LandClimb = 1.05f;
        public const float WaterClimb = 1.65f;

        public static float MaxAutoClimb(bool swimming) => swimming ? WaterClimb : LandClimb;

        public static bool Climbable(float rise, float maxRise) => rise > 0.02f && rise <= maxRise;

        public static bool FindLedge(Vector3 feet, Vector3 dir, float radius, float height, float maxRise, Transform self, out Vector3 top)
        {
            top = feet;
            for (int i = 0; i < 3; i++)
            {
                float reach = radius + 0.12f + i * 0.12f;
                Vector3 from = feet + dir * reach + Vector3.up * (maxRise + 0.05f);
                if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, maxRise + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (self != null && hit.transform.IsChildOf(self)) continue;
                if (hit.normal.y < 0.6f) continue;
                float rise = hit.point.y - feet.y;
                if (!Climbable(rise, maxRise)) continue;
                Vector3 stand = new Vector3(feet.x, hit.point.y + 0.03f, feet.z) + dir * Mathf.Min(reach, radius + 0.1f);
                float r = radius * 0.9f;
                Vector3 a = stand + Vector3.up * (r + 0.04f);
                Vector3 b = stand + Vector3.up * Mathf.Max(r + 0.05f, height - r);
                if (Physics.CheckCapsule(a, b, r, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    bool blockedBySelfOnly = true;
                    foreach (Collider c in Physics.OverlapCapsule(a, b, r, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        if (self == null || !c.transform.IsChildOf(self)) { blockedBySelfOnly = false; break; }
                    if (!blockedBySelfOnly) continue;
                }
                top = stand;
                return true;
            }
            return false;
        }
    }
}