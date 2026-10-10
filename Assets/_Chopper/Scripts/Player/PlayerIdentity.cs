using System.Collections.Generic;
using Chopper.Core;
using FishNet.Object;
using UnityEngine;

namespace Chopper.Player
{
    // Who this player is across sessions: the owner sends its profile id when it spawns, and the server uses it to
    // give back the player's saved things and to remember which crypt chests they emptied. Two copies of the game
    // on one PC share a profile, so the second one gets a numbered id of its own.
    public class PlayerIdentity : NetworkBehaviour
    {
        private const int MaxIdLength = 64;

        private static readonly List<PlayerIdentity> _serverAll = new List<PlayerIdentity>();

        // Server: every player whose id has arrived.
        public static IReadOnlyList<PlayerIdentity> ServerAll => _serverAll;

        // Server: raised once a player's id has arrived, and just before a player leaves (to keep their record).
        public static event System.Action<PlayerIdentity> ServerIdentified;
        public static event System.Action<PlayerIdentity> ServerLeaving;

        // Server: the profile id, empty until the owner has sent it.
        public string Id { get; private set; } = string.Empty;

        // The key used for saves and chests: the profile id, or the object id until the profile id has arrived.
        public static string KeyOf(Component player)
        {
            PlayerIdentity identity = player != null ? player.GetComponent<PlayerIdentity>() : null;
            if (identity != null && !string.IsNullOrEmpty(identity.Id)) return identity.Id;
            NetworkObject nob = player != null ? player.GetComponent<NetworkObject>() : null;
            return nob != null ? "object-" + nob.ObjectId : "unknown";
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner) SubmitServerRpc(ProfileId.Get());
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (!_serverAll.Contains(this)) return;
            ServerLeaving?.Invoke(this);
            _serverAll.Remove(this);
        }

        [ServerRpc]
        private void SubmitServerRpc(string id)
        {
            if (!string.IsNullOrEmpty(Id)) return;
            if (string.IsNullOrEmpty(id) || id.Length > MaxIdLength) id = "object-" + ObjectId;
            string unique = id;
            for (int n = 2; _serverAll.Exists(p => p.Id == unique); n++) unique = id + "-" + n;
            Id = unique;
            _serverAll.Add(this);
            ServerIdentified?.Invoke(this);
        }
    }
}
