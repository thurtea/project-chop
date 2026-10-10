using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing.Server;
using FishNet.Object;
using UnityEngine;

namespace Chopper.Networking
{
    // Interest management for effects that only matter to whoever could see them (a tree shaking, the ground stirring
    // where saplings sprout, a fish school on the water). Objects that come and go use a NetworkObserver with a
    // distance condition instead (treants, wood piles, set up in MeadowBuilder); this is for RPCs on the long lived
    // managers, which every client observes. The server sends a TargetRpc to each client with a player near the spot.
    // Anything that changes what a client knows (a tree is down, an announcement) stays a global ObserversRpc.
    public static class NearbyClients
    {
        // How far away a player still sees and hears an effect. Treants use the same distance as their observer range.
        public const float CosmeticRange = 120f;
        // Wood piles are small and only matter to someone walking up to take one.
        public const float WoodPileRange = 80f;

        private static readonly List<NetworkConnection> _buffer = new List<NetworkConnection>();

        // Server only. The clients with an object of theirs (their player) within range of position. A player 200 m down
        // in a crypt is out of range of the surface above it. The list is reused: send to it straight away.
        public static List<NetworkConnection> Within(ServerManager server, Vector3 position, float range = CosmeticRange)
        {
            _buffer.Clear();
            if (server == null) return _buffer;
            float sqrRange = range * range;
            foreach (NetworkConnection connection in server.Clients.Values)
            {
                if (connection == null || !connection.IsActive) continue;
                foreach (NetworkObject owned in connection.Objects)
                {
                    if (owned == null || (owned.transform.position - position).sqrMagnitude > sqrRange) continue;
                    _buffer.Add(connection);
                    break;
                }
            }
            return _buffer;
        }
    }
}
