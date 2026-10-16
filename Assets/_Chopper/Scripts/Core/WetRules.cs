using UnityEngine;

namespace Chopper.Core
{
    // Valheim's Wet: getting into water (or standing in the rain) soaks the player. Wet wears off slowly in the open
    // and quickly beside a campfire. While wet the player runs a little slower and gets stamina back more slowly.
    // Kept as seconds left: 0 is dry.
    public static class WetRules
    {
        public const float SoakSeconds = 90f;
        // Rain keeps the player at least this wet.
        public const float RainSoakSeconds = 30f;
        // Beside a fire, wet wears off this many times faster (90 s in about 10).
        public const float FireDryRate = 9f;
        // Water this deep at the feet (above the knees) soaks you; a splash through the shallows does not.
        public const float SoakDepth = 0.6f;
        public const float RunSpeedMultiplier = 0.9f;
        public const float StaminaRegenMultiplier = 0.7f;

        public static float Step(float wetSeconds, bool inWater, bool inRain, bool byFire, float deltaTime)
        {
            if (inWater) return SoakSeconds;
            if (byFire) return Mathf.Max(0f, wetSeconds - deltaTime * FireDryRate);
            float left = Mathf.Max(0f, wetSeconds - deltaTime);
            return inRain ? Mathf.Max(left, RainSoakSeconds) : left;
        }

        public static bool IsWet(float wetSeconds) => wetSeconds > 0f;
    }

    // Hands full (the axe drawn, a torch lit): the player runs a little slower than with empty hands, so putting the
    // axe away to travel is worth it.
    public static class CarryRules
    {
        public const float EquippedSpeedMultiplier = 0.92f;
    }

    // Swimming: slower than walking, and moving through water costs stamina. Out of stamina, the player only paddles.
    public static class SwimRules
    {
        public const float SpeedMultiplier = 0.5f;
        public const float SprintMultiplier = 1.6f;
        public const float StaminaPerSecond = 5f;
        public const float ExhaustedMultiplier = 0.5f;

        public static float Speed(float walkSpeed, bool exhausted) => Speed(walkSpeed, exhausted, false);

        public static float Speed(float walkSpeed, bool exhausted, bool sprinting) => walkSpeed * SpeedMultiplier * (exhausted ? ExhaustedMultiplier : (sprinting ? SprintMultiplier : 1f));

        public static bool KeepsSprint(bool swimming, bool blocking) => !blocking;

        public static bool NextCrouch(bool crouchToggled, bool crouchPressed, bool sprintPressed, bool down, bool swimming)
        {
            if (swimming || down || sprintPressed) return false;
            return crouchPressed ? !crouchToggled : crouchToggled;
        }
    }
}
