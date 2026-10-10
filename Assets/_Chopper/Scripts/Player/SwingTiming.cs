namespace Chopper.Player
{
    // Pure cooldown rules for axe swings. The server accepts a swing slightly early (latency tolerance)
    // because RPCs sent a cooldown apart can arrive closer together.
    public static class SwingTiming
    {
        public static bool IsServerSwingAllowed(float now, float nextAllowedTime, float latencyTolerance)
        {
            return now >= nextAllowedTime - latencyTolerance;
        }
    }
}
