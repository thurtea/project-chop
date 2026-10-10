using UnityEngine;

namespace Chopper.Trees
{
    // Which way a felled tree comes down, the Valheim way: the last blow pushes the trunk over, so on level ground it
    // falls away from the chopper (give or take a little), and the ground decides the rest. On a slope gravity wins
    // and the tree comes down the hill whichever side it was cut from. So chop a tree on a hillside from above it:
    // cut from the downhill side, it can come straight back down on you. Trees stand straight; nothing on the tree
    // gives its fall away, only the land around it.
    public static class TreeFallRules
    {
        // How hard the slope pulls compared with the push of the blow (which counts 1). Downhill is the flat direction
        // the ground falls away and its length is the slope (rise over run), so a 1 in 4 hillside already pulls about
        // as hard as the blow pushes, and a 1 in 2 one wins outright.
        private const float SlopeWeight = 4f;
        private const float MaxSlopePull = 3f;
        // How far a fall can stray from the blended direction (a knot in the wood, an uneven cut).
        private const float JitterDegrees = 25f;

        // Blends the push of the blow (away from the chopper) with the downhill pull, then turns it a little.
        public static Vector3 Choose(Vector3 downhill, Vector3 awayFromChopper, System.Random random)
        {
            Vector3 away = Flat(awayFromChopper);
            Vector3 slope = new Vector3(downhill.x, 0f, downhill.z);
            float pull = Mathf.Min(MaxSlopePull, slope.magnitude * SlopeWeight);
            Vector3 blend = away + (slope.sqrMagnitude > 1e-6f ? slope.normalized * pull : Vector3.zero);
            if (blend.sqrMagnitude < 1e-6f) blend = away;
            float jitter = ((float)random.NextDouble() * 2f - 1f) * JitterDegrees;
            return Quaternion.Euler(0f, jitter, 0f) * blend.normalized;
        }

        // The first large tree is an early warning that trees are dangerous: it drops straight toward the player who
        // fells it. Later trees use Choose.
        public static Vector3 TowardPlayer(Vector3 treePosition, Vector3 playerPosition, Vector3 fallback)
        {
            Vector3 toward = playerPosition - treePosition;
            toward.y = 0f;
            return toward.sqrMagnitude > 1e-6f ? toward.normalized : Flat(fallback);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }
    }
}
