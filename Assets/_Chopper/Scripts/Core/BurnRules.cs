namespace Chopper.Core
{
    // Standing in a flame (W11-10). Damage lands on a tick and stops as soon as you step out. One tick is a scrape,
    // not a killing blow.
    public static class BurnRules
    {
        public const float TickSeconds = 0.5f;
        public const int DamagePerTick = 5;

        // elapsed is time toward the next tick. It is cleared on leaving, so a partial tick does not follow you out.
        public static int Tick(ref float elapsed, float deltaTime, bool inFlame)
        {
            if (!inFlame || deltaTime <= 0f)
            {
                elapsed = 0f;
                return 0;
            }
            elapsed += deltaTime;
            if (elapsed < TickSeconds) return 0;
            int ticks = (int)(elapsed / TickSeconds);
            elapsed -= ticks * TickSeconds;
            return ticks * DamagePerTick;
        }
    }
}
