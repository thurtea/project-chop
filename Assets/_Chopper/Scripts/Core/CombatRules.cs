using UnityEngine;

namespace Chopper.Core
{
    public enum HitOutcome
    {
        Hit = 0,
        Blocked = 1,
        Parried = 2,
        Dodged = 3
    }

    // Pure combat math: blocking cuts damage, a block raised just before a hit parries it completely, and a roll
    // makes the player untouchable for a short window.
    public static class CombatRules
    {
        // Damage that gets through a block. At least 1 while any damage was dealt, like armor.
        public static int BlockedDamage(int raw, float reduction)
        {
            if (raw <= 0) return 0;
            return Mathf.Max(1, Mathf.CeilToInt(raw * (1f - Mathf.Clamp01(reduction))));
        }

        public static bool IsParryWindow(float now, float blockStartedAt, float window)
        {
            return now - blockStartedAt <= Mathf.Max(0f, window);
        }

        public static bool IsInvulnerable(float now, float invulnerableUntil) => now < invulnerableUntil;

        // Decides what happens when a hit of raw damage lands on a player. Returns the damage to apply.
        public static int Resolve(int raw, bool blocking, float blockStartedAt, float invulnerableUntil, float now,
            float reduction, float parryWindow, out HitOutcome outcome)
        {
            if (raw <= 0)
            {
                outcome = HitOutcome.Hit;
                return 0;
            }
            if (IsInvulnerable(now, invulnerableUntil))
            {
                outcome = HitOutcome.Dodged;
                return 0;
            }
            if (blocking)
            {
                if (IsParryWindow(now, blockStartedAt, parryWindow))
                {
                    outcome = HitOutcome.Parried;
                    return 0;
                }
                outcome = HitOutcome.Blocked;
                return BlockedDamage(raw, reduction);
            }
            outcome = HitOutcome.Hit;
            return raw;
        }

        // Stagger shrinks for big and boss enemies so they cannot be stun locked.
        public static float StaggerSeconds(float baseSeconds, int sizeIndex, bool boss)
        {
            if (boss) return baseSeconds * 0.3f;
            return Mathf.Max(0.05f, baseSeconds * (1f - 0.18f * sizeIndex));
        }

        // Every charged axe hits harder, whatever it is fighting (the element counter bonus is on top of this).
        public static float EnchantMultiplier(bool enchanted, float bonus) => enchanted ? 1f + Mathf.Max(0f, bonus) : 1f;
    }

    // The three hit axe combo used against enemies and empty air (trees always get the plain chop). Each swing within
    // the window after the last one moves to the next step: a slash, a backhand, then a heavy finisher.
    public static class ComboRules
    {
        public const int Steps = 3;
        public const float WindowSeconds = 0.55f;
        public const float FinisherDamage = 1.6f;
        public const float FinisherStagger = 1.0f;
        public const float FinisherRecovery = 1.3f;
        public const float FinisherLunge = 4.5f;

        // The step the next swing is. last is the previous step (-1 when none or after a tree chop), sinceLast the
        // seconds since that swing began and cooldown the normal swing cooldown.
        public static int NextStep(int last, float sinceLast, float cooldown)
        {
            if (last < 0) return 0;
            return sinceLast <= cooldown + WindowSeconds ? (last + 1) % Steps : 0;
        }

        public static float Damage(int step) => step == Steps - 1 ? FinisherDamage : (step == 1 ? 1.15f : 1f);

        // The finisher takes longer to recover from, so the combo is a rhythm and not a button mash.
        public static float CooldownScale(int step) => step == Steps - 1 ? FinisherRecovery : 1f;
    }
}
