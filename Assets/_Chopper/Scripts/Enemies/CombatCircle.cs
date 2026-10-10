using System.Collections.Generic;
using Chopper.Player;
using UnityEngine;

namespace Chopper.Enemies
{
    // Server only. Who is fighting each player. Enemies close to a player share out the turns to attack, so only a
    // couple swing at once while the rest circle and wait, and each waiting enemy gets its own place on a ring round
    // the player, spread so they come at the sides and the back instead of queueing in front.
    public static class CombatCircle
    {
        public const int AttackersPerPlayer = 2;
        // The ring turns slowly, so the waiting enemies circle the player.
        private const float DriftDegreesPerSecond = 14f;

        private static readonly Dictionary<PlayerHealth, List<TreantAI>> _engaged = new Dictionary<PlayerHealth, List<TreantAI>>();
        private static readonly Dictionary<PlayerHealth, List<TreantAI>> _turns = new Dictionary<PlayerHealth, List<TreantAI>>();

        private static List<TreantAI> ListOf(Dictionary<PlayerHealth, List<TreantAI>> map, PlayerHealth target)
        {
            if (!map.TryGetValue(target, out List<TreantAI> list))
            {
                list = new List<TreantAI>();
                map[target] = list;
            }
            list.RemoveAll(ai => ai == null);
            return list;
        }

        // The enemy is now fighting this player (and no one else).
        public static void Engage(PlayerHealth target, TreantAI ai)
        {
            if (target == null || ai == null) return;
            List<TreantAI> list = ListOf(_engaged, target);
            if (list.Contains(ai)) return;
            Leave(ai);
            list.Add(ai);
        }

        // The enemy stopped fighting (it lost its target, went for the hut, or died).
        public static void Leave(TreantAI ai)
        {
            foreach (List<TreantAI> list in _engaged.Values) list.Remove(ai);
            foreach (List<TreantAI> list in _turns.Values) list.Remove(ai);
        }

        // True when the enemy may attack now: it already holds a turn, or a turn was free and it took it.
        public static bool TryTakeTurn(PlayerHealth target, TreantAI ai)
        {
            if (target == null || ai == null) return false;
            List<TreantAI> turns = ListOf(_turns, target);
            if (turns.Contains(ai)) return true;
            if (turns.Count >= AttackersPerPlayer) return false;
            turns.Add(ai);
            return true;
        }

        public static bool HasTurn(PlayerHealth target, TreantAI ai) => target != null && _turns.TryGetValue(target, out List<TreantAI> turns) && turns.Contains(ai);

        public static void EndTurn(TreantAI ai)
        {
            foreach (List<TreantAI> list in _turns.Values) list.Remove(ai);
        }

        // Where this enemy waits: its own spot on a ring of the given radius round the player. Spots are spread evenly
        // and turned so none is straight in front of the player (two waiting enemies take the flanks).
        public static Vector3 RingSpot(PlayerHealth target, TreantAI ai, float radius)
        {
            List<TreantAI> list = ListOf(_engaged, target);
            int count = Mathf.Max(1, list.Count);
            int slot = Mathf.Max(0, list.IndexOf(ai));
            float step = 360f / count;
            float angle = target.transform.eulerAngles.y + step * 0.5f + step * slot + Time.time * DriftDegreesPerSecond;
            return target.transform.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
        }

        // A fresh server (new game, game over) starts with nobody fighting.
        public static void Clear()
        {
            _engaged.Clear();
            _turns.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();
    }
}
