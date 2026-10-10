using Chopper.World;
using UnityEngine;

namespace Chopper.Audio
{
    // What a foot lands on, for footstep sounds: water when wading, grass where the grass carpet is thick, otherwise
    // bare ground (paths, the yard, forest floor, ash, rock).
    public static class GroundSurface
    {
        public enum Kind { Grass, Dirt, Water }

        private const float GrassThreshold = 0.35f;

        public static Kind At(Vector3 position)
        {
            if (Chopper.World.Generation.CryptWorld.IsUnderground(position)) return Kind.Dirt;
            if (WaterSystem.DepthAt(position, out _) >= WaterSystem.WadeDepth) return Kind.Water;
            GrassCarpet carpet = GrassCarpet.Instance;
            if (carpet == null) return Kind.Grass;
            return carpet.DensityAt(position.x, position.z) >= GrassThreshold ? Kind.Grass : Kind.Dirt;
        }
    }
}
