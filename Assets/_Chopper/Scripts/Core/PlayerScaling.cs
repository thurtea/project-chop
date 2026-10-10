using UnityEngine;

namespace Chopper.Core
{
    // multiplier(n) = 1 + coefficient * log2(min(n, maxPlayers)), with n clamped to at least 1.
    // Coefficients and the player cap come from GameConfig.
    public static class PlayerScaling
    {
        public static float Multiplier(int playerCount, float coefficient, int maxPlayers)
        {
            int n = Mathf.Clamp(playerCount, 1, Mathf.Max(1, maxPlayers));
            return 1f + coefficient * Mathf.Log(n, 2f);
        }

        public static float EnemyMultiplier(GameConfig config, int playerCount)
        {
            return Multiplier(playerCount, config.EnemyScalingCoefficient, config.MaxScaledPlayers);
        }

        public static float TreeDensityMultiplier(GameConfig config, int playerCount)
        {
            return Multiplier(playerCount, config.TreeDensityScalingCoefficient, config.MaxScaledPlayers);
        }

        public static float SaplingMultiplier(GameConfig config, int playerCount)
        {
            return Multiplier(playerCount, config.SaplingScalingCoefficient, config.MaxScaledPlayers);
        }
    }
}
