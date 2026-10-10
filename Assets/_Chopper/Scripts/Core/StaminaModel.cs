using UnityEngine;

namespace Chopper.Core
{
    // Pure stamina rules (Valheim style). Actions spend stamina, regeneration starts after a short delay, and
    // hitting zero leaves the player exhausted until stamina recovers past a threshold, so sprinting and chopping
    // cannot be spammed at the edge of empty.
    public class StaminaModel
    {
        private readonly float _regenPerSecond;
        private readonly float _regenDelay;
        private readonly float _exhaustedRecoverAt;
        private float _delayLeft;

        public float Max { get; private set; }
        public float Current { get; private set; }
        public bool Exhausted { get; private set; }

        public float Fraction => Max > 0f ? Current / Max : 0f;
        public bool CanSprint => !Exhausted && Current > 0f;

        public StaminaModel(float max, float regenPerSecond, float regenDelay, float exhaustedRecoverAt)
        {
            Max = Mathf.Max(1f, max);
            _regenPerSecond = Mathf.Max(0f, regenPerSecond);
            _regenDelay = Mathf.Max(0f, regenDelay);
            _exhaustedRecoverAt = Mathf.Clamp(exhaustedRecoverAt, 0f, Max);
            Current = Max;
        }

        // Changes the maximum (a permanent boon). Keeps the current fraction so nothing is lost or gained for free.
        public void SetMax(float max)
        {
            float fraction = Fraction;
            Max = Mathf.Max(1f, max);
            Current = Mathf.Clamp(fraction * Max, 0f, Max);
        }

        // For one off actions (swing, jump). Returns false and spends nothing if the player cannot afford it.
        public bool TryUse(float amount)
        {
            if (Exhausted || Current < amount) return false;
            Spend(amount);
            return true;
        }

        // For continuous actions (sprinting). Spends what is left rather than failing.
        public void Drain(float amount)
        {
            if (Current <= 0f) return;
            Spend(Mathf.Min(amount, Current));
        }

        // regenMultiplier slows getting stamina back (being wet).
        public void Tick(float deltaTime, float regenMultiplier = 1f)
        {
            if (_delayLeft > 0f) _delayLeft -= deltaTime;
            else Current = Mathf.Min(Max, Current + _regenPerSecond * regenMultiplier * deltaTime);

            if (Exhausted && Current >= _exhaustedRecoverAt) Exhausted = false;
        }

        public void Refill()
        {
            Current = Max;
            Exhausted = false;
            _delayLeft = 0f;
        }

        private void Spend(float amount)
        {
            Current = Mathf.Max(0f, Current - amount);
            _delayLeft = _regenDelay;
            if (Current <= 0f) Exhausted = true;
        }
    }

    // Pure crafting check: enough of the ingredients, and room in the stack.
    public static class CraftRules
    {
        public static bool CanCraft(int wood, int woodCost, int have, int stackCap)
        {
            return woodCost >= 0 && wood >= woodCost && have < stackCap;
        }

        // Two ingredients, such as a bandage from herbs and fabric.
        public static bool CanCraft(int first, int firstCost, int second, int secondCost, int have, int stackCap)
        {
            return CanCraft(first, firstCost, have, stackCap) && secondCost >= 0 && second >= secondCost;
        }
    }
}
