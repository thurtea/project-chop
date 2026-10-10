using UnityEngine;

namespace Chopper.Core
{
    // Eating a fish is Valheim food: it raises the stamina maximum for a while instead of healing. A bigger fish (one
    // that used to heal more) gives a bigger boost. Eating again restarts the timer and keeps the better of the two
    // boosts while the old one lasts.
    public static class FoodRules
    {
        public static int StaminaBonus(int fishValue, float perPoint) => Mathf.Max(1, Mathf.RoundToInt(fishValue * Mathf.Max(0f, perPoint)));

        public static void Eat(ref int bonus, ref float secondsLeft, int newBonus, float duration)
        {
            bonus = secondsLeft > 0f ? Mathf.Max(bonus, newBonus) : newBonus;
            secondsLeft = Mathf.Max(0f, duration);
        }

        // Counts the boost down; it is gone at zero.
        public static void Tick(ref int bonus, ref float secondsLeft, float deltaTime)
        {
            if (secondsLeft <= 0f) return;
            secondsLeft -= deltaTime;
            if (secondsLeft > 0f) return;
            secondsLeft = 0f;
            bonus = 0;
        }
    }

    // Sleeping in the bed: allowed from dusk through the night, and the night is skipped once every player is in bed.
    public static class SleepRules
    {
        public static bool CanSleep(DayPhase phase) => phase == DayPhase.Dusk || phase == DayPhase.Night;

        public static bool Everyone(int inBed, int players) => players > 0 && inBed >= players;
    }
}
