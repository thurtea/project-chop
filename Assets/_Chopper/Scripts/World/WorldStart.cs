using Chopper.World.Generation;
using UnityEngine;

namespace Chopper.World
{
    // How a player arrives (W9-01, user 2026-10-12): dropped from the sky into the hub pond with no outfit and no axe,
    // swims to the shore, and finds the outfit and the axe lying on the bank toward the hut. A fallen player arrives
    // the same way; their own outfit and axe lie where they fell.
    public static class WorldStart
    {
        // Height of the drop over the water surface.
        public const float DropHeight = 22f;
        // The kit lies on the bank toward the hut, turned this far from the fishing sign (straight toward the hut) and
        // the pond campfire (35 degrees either side).
        private const float KitTurn = -18f;
        private const float KitBank = 1.5f;

        // Above the middle of the hub pond, a little off centre per player so two arrivals do not land on each other.
        public static Vector3 DropPoint(int seat)
        {
            WorldGen gen = WorldSession.Gen;
            if (gen == null) return Vector3.up * DropHeight;
            float angle = seat * 137.5f * Mathf.Deg2Rad;
            Vector2 at = gen.PondCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (seat == 0 ? 0f : 3f);
            return new Vector3(at.x, WorldGen.WaterHeight + DropHeight, at.y);
        }

        // Where the start kit lies: on the bank toward the hut. The two pieces lie a metre apart.
        public static Vector3 KitSpot(int piece)
        {
            WorldGen gen = WorldSession.Gen;
            if (gen == null) return Vector3.zero;
            Vector2 toHut = gen.PondCenter.sqrMagnitude > 0.01f ? -gen.PondCenter.normalized : Vector2.up;
            Vector2 bank = gen.PondCenter + (Vector2)(Quaternion.Euler(0f, 0f, KitTurn) * toHut) * (WorldGen.PondOuterRadius + KitBank);
            float side = piece == 0 ? -0.75f : piece == 1 ? 0.75f : 0f;
            Vector2 along = new Vector2(-toHut.y, toHut.x) * side;
            Vector3 spot = new Vector3(bank.x + along.x, 0f, bank.y + along.y);
            spot.y = WorldGround.HeightAt(spot);
            return spot;
        }
    }
}
