using UnityEngine;

namespace Chopper.Core
{
    public static class SkillRules
    {
        public const int MaxSkill = 100;

        public const float LogChopMin = 0.6f;
        public const float LogChopMax = 2.0f;
        public const float LogSwingMin = 0.8f;
        public const float MaxTreeChopPower = 4.5f;
        public const int LogGainPerChop = 1;

        public const float MineProgressMin = 0.5f;
        public const float MineProgressMax = 1.8f;
        public const int MineMinStrikes = 2;
        public const int MineGainPerFind = 4;

        public const float FishEaseMin = 1.0f;
        public const float FishEaseMax = 1.6f;
        public const int FishGainPerCatch = 4;

        public static int Clamp(int skill) => Mathf.Clamp(skill, 0, MaxSkill);

        public static int Gain(int skill, int amount) => Mathf.Clamp(skill + Mathf.Max(0, amount), 0, MaxSkill);

        public static float Normalized(int skill) => Mathf.Clamp01(Clamp(skill) / (float)MaxSkill);

        public static float LogChopMultiplier(int skill) => Mathf.Lerp(LogChopMin, LogChopMax, Normalized(skill));

        public static float LogSwingMultiplier(int skill) => Mathf.Lerp(1f, LogSwingMin, Normalized(skill));

        public static float TreeChopPower(float gearMultiplier, int logSkill) =>
            Mathf.Min(MaxTreeChopPower, Mathf.Max(0f, gearMultiplier) * LogChopMultiplier(logSkill));

        public static float MineProgressMultiplier(int skill) => Mathf.Lerp(MineProgressMin, MineProgressMax, Normalized(skill));

        public static bool MineFrees(float progress, int strikes) => progress >= MiningRules.FreeAt && strikes >= MineMinStrikes;

        public static float FishFightEase(int skill) => Mathf.Lerp(FishEaseMin, FishEaseMax, Normalized(skill));
    }
}
