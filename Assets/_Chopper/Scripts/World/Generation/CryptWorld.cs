using System.Collections.Generic;
using UnityEngine;

namespace Chopper.World.Generation
{
    // The crypts of the current world, once WorldBuilder has made them: their plans, every chest by one id across all
    // crypts, and the chest views so the open or closed look can be switched. Also answers the questions the player
    // and the interface ask: is a door, a way out or a chest in reach, and is a position underground.
    public static class CryptWorld
    {
        // Anything this far below the hut floor is inside a crypt (the land never goes below about -12 m).
        public const float UndergroundY = -100f;
        public const float DoorReach = 2.6f;
        public const float ExitReach = 2.4f;
        public const float ChestReach = 2.2f;

        public struct ChestRef
        {
            public int Crypt;
            public CryptChest Chest;
        }

        private static readonly List<CryptPlan> _plans = new List<CryptPlan>();
        private static readonly List<ChestRef> _chests = new List<ChestRef>();
        private static readonly List<GameObject> _chestGlints = new List<GameObject>();
        private static readonly HashSet<int> _openedHere = new HashSet<int>();

        public static IReadOnlyList<CryptPlan> Plans => _plans;
        public static IReadOnlyList<ChestRef> Chests => _chests;

        public static bool IsUnderground(Vector3 position) => position.y < UndergroundY;

        public static void Set(IEnumerable<CryptPlan> plans)
        {
            Reset();
            foreach (CryptPlan plan in plans)
            {
                _plans.Add(plan);
                foreach (CryptChest chest in plan.Chests)
                {
                    _chests.Add(new ChestRef { Crypt = plan.Index, Chest = chest });
                    _chestGlints.Add(null);
                }
            }
        }

        // The glow over an unopened chest, hidden once this player has looted it.
        public static void SetChestGlint(int chest, GameObject glint)
        {
            if (chest >= 0 && chest < _chestGlints.Count) _chestGlints[chest] = glint;
        }

        public static void Reset()
        {
            _plans.Clear();
            _chests.Clear();
            _chestGlints.Clear();
            _openedHere.Clear();
        }

        // ---------- Reach tests ----------

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
            if (!IsUnderground(position)) return -1;
            for (int i = 0; i < _plans.Count; i++) if (Near(position, _plans[i].ExitSpot, ExitReach)) return i;
            return -1;
        }

        public static int ChestInReach(Vector3 position)
        {
            if (!IsUnderground(position)) return -1;
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _chests.Count; i++)
            {
                Vector3 c = _chests[i].Chest.Position;
                if (!Near(position, c, ChestReach)) continue;
                float sqr = (position - c).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best;
        }

        // The vault archway down to the arena, in reach.
        public static int ArenaDoorInReach(Vector3 position)
        {
            if (!IsUnderground(position)) return -1;
            for (int i = 0; i < _plans.Count; i++) if (Near(position, _plans[i].ArenaDoorSpot, DoorReach)) return i;
            return -1;
        }

        // The archway back up from the arena, in reach.
        public static int ArenaExitInReach(Vector3 position)
        {
            if (!IsUnderground(position)) return -1;
            for (int i = 0; i < _plans.Count; i++) if (Near(position, _plans[i].ArenaExitSpot, ExitReach)) return i;
            return -1;
        }

        // The crypt whose arena hall holds this position, or -1.
        public static int ArenaAt(Vector3 position)
        {
            for (int i = 0; i < _plans.Count; i++) if (_plans[i].InArena(position)) return i;
            return -1;
        }

        // The crypt whose rooms hold this position, or -1 on the surface.
        public static int CryptAt(Vector3 position)
        {
            if (!IsUnderground(position)) return -1;
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _plans.Count; i++)
            {
                Vector3 o = _plans[i].Origin;
                float sqr = new Vector2(position.x - o.x, position.z - o.z).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best;
        }

        // ---------- Opened chests, as seen by this player ----------

        public static bool OpenedHere(int chest) => _openedHere.Contains(chest);

        public static void MarkOpenedHere(int chest)
        {
            if (chest < 0 || chest >= _chests.Count) return;
            _openedHere.Add(chest);
            if (_chestGlints[chest] != null) _chestGlints[chest].SetActive(false);
        }

        public static void ClearOpenedHere()
        {
            _openedHere.Clear();
            foreach (GameObject glint in _chestGlints) if (glint != null) glint.SetActive(true);
        }
    }
}
