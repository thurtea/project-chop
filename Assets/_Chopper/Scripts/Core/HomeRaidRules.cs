using UnityEngine;

namespace Chopper.Core
{
    public static class HomeRaidRules
    {
        public static int Count(int basis, int perExtraPlayer, int players, int cap)
        {
            int extra = Mathf.Max(0, players - 1) * Mathf.Max(0, perExtraPlayer);
            return Mathf.Clamp(Mathf.Max(0, basis) + extra, 0, Mathf.Max(0, cap));
        }

        public static TreeSize Size(int index) => index % 3 == 2 ? TreeSize.Medium : TreeSize.Small;

        public static bool InReach(Vector3 raider, Vector3 heart, float reach)
        {
            Vector3 offset = heart - raider;
            offset.y = 0f;
            return offset.magnitude <= reach;
        }
    }
}
