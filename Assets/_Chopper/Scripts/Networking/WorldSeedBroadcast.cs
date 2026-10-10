using FishNet.Broadcast;

namespace Chopper.Networking
{
    // Sent by the server to each player as soon as they are authenticated, before the World scene loads, so their
    // WorldBuilder makes the same world as the host. TreeChecksum lets the client confirm its forest matches.
    public struct WorldSeedBroadcast : IBroadcast
    {
        public int Seed;
        public int TreeChecksum;
    }
}
