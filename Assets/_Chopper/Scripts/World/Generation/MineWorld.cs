using System.Collections.Generic;
using UnityEngine;

namespace Chopper.World.Generation
{
    // The mines of the current world, once WorldBuilder has made them: their plans, every vein by one id across all
    // mines, and which veins this player has already worked today. Answers the reach questions the player and the
    // interface ask: a portal, the ladder out or a vein in front of you.
    public static class MineWorld
    {
        public const float DoorReach = 2.4f;
        public const float ExitReach = 1.8f;
        // A vein is struck from up to this far, and only if it is roughly in front.
        public const float VeinReach = 2.1f;
        private const float VeinFacing = 0.25f;

        public struct VeinRef
        {
            public int Mine;
            public MineVein Vein;
        }

        private static readonly List<MinePlan> _plans = new List<MinePlan>();
        private static readonly List<VeinRef> _veins = new List<VeinRef>();
        private static readonly HashSet<int> _spentHere = new HashSet<int>();

        public static IReadOnlyList<MinePlan> Plans => _plans;
        public static IReadOnlyList<VeinRef> Veins => _veins;

        // Raised on this peer when a vein's worked state changes here (the vein views dim or light up again).
        public static event System.Action Changed;

        public static void Set(IEnumerable<MinePlan> plans)
        {
            Reset();
            foreach (MinePlan plan in plans)
            {
                _plans.Add(plan);
                foreach (MineVein vein in plan.Veins) _veins.Add(new VeinRef { Mine = plan.Index, Vein = vein });
            }
        }

        public static void Reset()
        {
            _plans.Clear();
            _veins.Clear();
            _spentHere.Clear();
        }

        private static bool Near(Vector3 a, Vector3 b, float reach)
        {
            if (Mathf.Abs(a.y - b.y) > 2.5f) return false;
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude <= reach * reach;
        }

        public static int DoorInReach(Vector3 position)
        {
            for (int i = 0; i < _plans.Count; i++) if (Near(position, _plans[i].DoorSpot, DoorReach)) return i;
            return -1;
        }

        public static int ExitInReach(Vector3 position)
        {
            if (position.y > MinePlan.FloorY + 20f) return -1;
            for (int i = 0; i < _plans.Count; i++) if (Near(position, _plans[i].ExitSpot, ExitReach)) return i;
            return -1;
        }

        // The mine holding this position, or -1.
        public static int MineAt(Vector3 position)
        {
            if (position.y > MinePlan.FloorY + 20f) return -1;
            for (int i = 0; i < _plans.Count; i++) if (_plans[i].Contains(position)) return i;
            return -1;
        }

        // The vein in reach and in front of a player at position looking along forward, or -1. Spent veins count too
        // (the caller decides), so the prompt can say it is worked out.
        public static int VeinInReach(Vector3 position, Vector3 forward)
        {
            if (position.y > MinePlan.FloorY + 20f) return -1;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _veins.Count; i++)
            {
                Vector3 v = _veins[i].Vein.Position;
                Vector3 to = v - position;
                to.y = 0f;
                float sqr = to.sqrMagnitude;
                if (sqr > VeinReach * VeinReach || sqr >= bestSqr) continue;
                if (sqr > 0.25f && Vector3.Dot(to.normalized, forward) < VeinFacing) continue;
                if (Mathf.Abs(v.y - position.y) > 2.6f) continue;
                best = i;
                bestSqr = sqr;
            }
            return best;
        }

        // Server and owner checks: a vein in reach of a position, facing or not (for validating a strike).
        public static bool VeinNear(int vein, Vector3 position, float slack)
        {
            if (vein < 0 || vein >= _veins.Count) return false;
            return Near(position, _veins[vein].Vein.Position, VeinReach + slack);
        }

        // ---------- Worked veins, as seen by this player ----------

        public static bool SpentHere(int vein) => _spentHere.Contains(vein);

        public static void MarkSpentHere(int vein)
        {
            if (vein < 0 || vein >= _veins.Count || !_spentHere.Add(vein)) return;
            Changed?.Invoke();
        }

        public static void ClearSpentHere()
        {
            if (_spentHere.Count == 0) return;
            _spentHere.Clear();
            Changed?.Invoke();
        }
    }
}
