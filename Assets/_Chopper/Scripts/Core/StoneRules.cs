using UnityEngine;

namespace Chopper.Core
{
    public static class StoneRules
    {
        public const float Reach = 1.6f;
        public const int CarryCap = 200;

        public static int DailyYield(float rockSize) => Mathf.Clamp(Mathf.RoundToInt(rockSize * 2f), 2, 8);

        public static int PerStrike(float rockSize) => rockSize >= 2.5f ? 2 : 1;

        public static int Take(int takenToday, float rockSize)
        {
            int left = DailyYield(rockSize) - Mathf.Max(0, takenToday);
            return Mathf.Clamp(PerStrike(rockSize), 0, Mathf.Max(0, left));
        }

        public static bool InReach(Vector3 player, Vector3 aim, Vector3 rock, float rockSize)
        {
            Vector3 to = rock - player;
            to.y = 0f;
            float distance = to.magnitude - rockSize * 0.5f;
            if (distance > Reach) return false;
            if (to.sqrMagnitude < 0.0001f) return true;
            Vector3 flatAim = new Vector3(aim.x, 0f, aim.z);
            return flatAim.sqrMagnitude < 0.0001f || Vector3.Angle(flatAim, to) <= 60f;
        }
    }
}
