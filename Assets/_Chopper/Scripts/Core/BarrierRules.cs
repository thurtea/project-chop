using UnityEngine;

namespace Chopper.Core
{
    public static class BarrierRules
    {
        public static bool IsUp(float charge) => charge > 0f;

        public static float Feed(float charge, float add, float max) => Mathf.Clamp(charge + Mathf.Max(0f, add), 0f, Mathf.Max(0f, max));

        public static float Burn(float charge, float burn) => Mathf.Max(0f, charge - Mathf.Max(0f, burn));

        public static bool Absorb(ref float charge, float drain)
        {
            if (!IsUp(charge)) return false;
            charge = Mathf.Max(0f, charge - Mathf.Max(0f, drain));
            return true;
        }

        public static bool Covers(Vector3 heart, Vector3 position, float radius)
        {
            Vector3 offset = position - heart;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius && Mathf.Abs(position.y - heart.y) < 10f;
        }

        public static int Nights(float charge, float burn) => burn <= 0f ? 0 : Mathf.FloorToInt(charge / burn);
    }
}
