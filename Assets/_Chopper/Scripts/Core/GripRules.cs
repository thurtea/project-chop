namespace Chopper.Core
{
    public static class GripRules
    {
        public const float GroundStopGrip = 2.5f;
        public const float SlipperyStopGrip = 0.4f;

        public static float StopGrip(bool slippery) => slippery ? SlipperyStopGrip : GroundStopGrip;

        public static float StopDistance(float speed, float deceleration, bool slippery)
        {
            float rate = deceleration * StopGrip(slippery);
            return rate <= 0f ? float.PositiveInfinity : speed * speed / (2f * rate);
        }
    }
}