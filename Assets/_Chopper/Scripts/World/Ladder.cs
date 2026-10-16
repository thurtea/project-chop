using System.Collections.Generic;
using UnityEngine;

namespace Chopper.World
{
    public static class Ladder
    {
        public const float ClimbSpeed = 2.4f;
        private static readonly List<(Bounds zone, Vector3 inward)> Zones = new List<(Bounds, Vector3)>();

        public static void Clear() => Zones.Clear();

        public static void Add(Bounds zone, Vector3 inward) => Zones.Add((zone, inward.normalized));

        public static int Count => Zones.Count;

        public static bool At(Vector3 point, out Vector3 inward, out float top)
        {
            foreach (var z in Zones)
            {
                if (z.zone.Contains(point)) { inward = z.inward; top = z.zone.max.y; return true; }
            }
            inward = Vector3.zero;
            top = 0f;
            return false;
        }

        public static float Climb(Vector3 input, Vector3 inward)
        {
            if (input.sqrMagnitude < 0.01f) return 0f;
            return Vector3.Dot(input.normalized, inward) < -0.3f ? -ClimbSpeed : ClimbSpeed;
        }
    }
}
