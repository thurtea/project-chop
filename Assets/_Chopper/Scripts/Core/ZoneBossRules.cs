using System.Collections.Generic;
using UnityEngine;

namespace Chopper.Core
{
    // The grove warden, the zone boss. Felled forest trees are remembered where they stood; once enough of them lie
    // close together (one grove), a warden rises in the middle of them. Its fight has three phases by health:
    // 1, slams and swings; 2, roots burst under the players and it calls guards that harden its bark; 3, enraged,
    // faster and slamming more often.
    public static class ZoneBossRules
    {
        public const int MaxRemembered = 400;
        public const float PhaseTwoAt = 2f / 3f;
        public const float PhaseThreeAt = 1f / 3f;
        // Share of each blow that gets through the bark while the warden's guards stand.
        public const float ShieldedDamage = 0.25f;
        public const int MaxGuards = 8;

        // How many felled trees lie within radius of a spot (on the ground plane).
        public static int CountNear(IList<Vector3> felled, Vector3 at, float radius)
        {
            int count = 0;
            float sqr = radius * radius;
            for (int i = 0; i < felled.Count; i++)
            {
                if (FlatSqr(felled[i], at) <= sqr) count++;
            }
            return count;
        }

        // True when at least required felled trees lie within radius of the latest one. center is their middle.
        public static bool TryFindGrove(IList<Vector3> felled, Vector3 latest, float radius, int required, out Vector3 center)
        {
            center = latest;
            if (felled == null || required < 1) return false;
            float sqr = radius * radius;
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < felled.Count; i++)
            {
                if (FlatSqr(felled[i], latest) > sqr) continue;
                sum += felled[i];
                count++;
            }
            if (count < required) return false;
            center = sum / count;
            return true;
        }

        // Forgets the felled trees of a grove whose warden has risen, so the next one needs fresh felling.
        public static void ForgetGrove(List<Vector3> felled, Vector3 center, float radius)
        {
            float sqr = radius * radius;
            felled.RemoveAll(p => FlatSqr(p, center) <= sqr);
        }

        // Remembers one felled tree, dropping the oldest when the list is full.
        public static void Remember(List<Vector3> felled, Vector3 position)
        {
            felled.Add(position);
            if (felled.Count > MaxRemembered) felled.RemoveRange(0, felled.Count - MaxRemembered);
        }

        public static bool ShouldRise(int day, int firstDay, bool wardenAwake, bool groveReady)
        {
            return !wardenAwake && day >= firstDay && groveReady;
        }

        public static int PhaseFor(float healthFraction)
        {
            if (healthFraction > PhaseTwoAt) return 1;
            if (healthFraction > PhaseThreeAt) return 2;
            return 3;
        }

        // More players, more health: each player past the first adds perExtraPlayer of the base.
        public static float Health(float baseHealth, float perExtraPlayer, int players)
        {
            return baseHealth * (1f + Mathf.Max(0f, perExtraPlayer) * Mathf.Max(0, players - 1));
        }

        public static float DamageTaken(float units, bool shielded)
        {
            return Mathf.Max(0f, units) * (shielded ? ShieldedDamage : 1f);
        }

        // The guards called when the warden enters phase 2 (more) or phase 3 (fewer).
        public static int GuardsFor(int phase, int players)
        {
            int p = Mathf.Max(1, players);
            if (phase == 2) return Mathf.Min(MaxGuards, 2 + p);
            if (phase == 3) return Mathf.Min(MaxGuards, 1 + p);
            return 0;
        }

        public static float SpeedMultiplier(int phase) => phase >= 3 ? 1.4f : phase == 2 ? 1.1f : 1f;

        // Seconds between slams and between swings shrink when it is enraged.
        public static float IntervalMultiplier(int phase) => phase >= 3 ? 0.55f : 1f;

        public static bool RootsActive(int phase) => phase >= 2;

        private static float FlatSqr(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
