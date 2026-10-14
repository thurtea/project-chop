using System.Collections.Generic;
using UnityEngine;

namespace Chopper.World
{
    // Where the water is and how deep it is at a spot: every pool registers a WaterSurface (its circle and surface
    // height), and depth is the surface height minus the terrain height there. The player wades in shallows and
    // swims once the water is deep enough.
    public static class WaterSystem
    {
        // Deeper than this and the player swims instead of walking.
        public const float SwimDepth = 1.25f;
        public const float WadeDepth = 0.18f;
        // Body height that stays under the surface while swimming (feet this far below the surface).
        public const float SwimFloatDepth = 0.70f;
        // Feet higher than this over the surface are in the air, not in the water (a fall or a jump over a pond).
        public const float SurfaceReach = 0.3f;
        public const float WadeSpeedMultiplier = 0.82f;

        // Water depth over the terrain at this position, and the surface height. Zero when dry.
        public static float DepthAt(Vector3 position, out float surfaceY)
        {
            surfaceY = 0f;
            bool hasGround = Chopper.World.WorldGround.Exists;
            // The crypts lie under the land, ponds included: no water down there.
            if (!hasGround || Chopper.World.Generation.CryptWorld.IsUnderground(position)) return 0f;

            float best = 0f;
            for (int i = 0; i < WaterSurface.All.Count; i++)
            {
                WaterSurface water = WaterSurface.All[i];
                Vector3 center = water.transform.position;
                float dx = position.x - center.x;
                float dz = position.z - center.z;
                float distanceSqr = dx * dx + dz * dz;
                if (distanceSqr > water.Radius * water.Radius || distanceSqr < water.InnerRadius * water.InnerRadius) continue;

                float ground = Chopper.World.WorldGround.HeightAt(position);
                float depth = water.SurfaceY - ground;
                if (depth > best)
                {
                    best = depth;
                    surfaceY = water.SurfaceY;
                }
            }
            return best;
        }

        public static bool IsSwimming(Vector3 position) => DepthAt(position, out float surface) >= SwimDepth && position.y <= surface + SurfaceReach;

        public static bool IsWading(Vector3 position)
        {
            float depth = DepthAt(position, out _);
            return depth >= WadeDepth && depth < SwimDepth;
        }
    }
}
