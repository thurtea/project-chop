using UnityEngine;

namespace Chopper.Core
{
    public static class SteerRules
    {
        public const float Acceleration = 60f;
        public const float Deceleration = 160f;
        public const float SwimAcceleration = 18f;
        public const float SwimDeceleration = 30f;
        public const float FacingTurnRate = 1080f;
        public const float SwimFacingTurnRate = 600f;
        public const float ImpulseDecay = 24f;

        public static Vector3 Step(Vector3 current, Vector3 target, float acceleration, float deceleration, float dt)
        {
            current.y = 0f;
            target.y = 0f;
            if (target.sqrMagnitude < 0.0001f) return Vector3.MoveTowards(current, Vector3.zero, deceleration * dt);
            Vector3 heading = target.normalized;
            float along = Mathf.Max(0f, Vector3.Dot(current, heading));
            float rate = along > target.magnitude ? deceleration : acceleration;
            return Vector3.MoveTowards(heading * along, target, rate * dt);
        }

        public static float StopDistance(float speed, float deceleration)
        {
            return deceleration <= 0f ? float.PositiveInfinity : speed * speed / (2f * deceleration);
        }

        public static float StopSeconds(float speed, float deceleration)
        {
            return deceleration <= 0f ? float.PositiveInfinity : speed / deceleration;
        }
    }
}
