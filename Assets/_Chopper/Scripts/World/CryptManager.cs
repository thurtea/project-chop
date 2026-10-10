using System.Collections.Generic;
using Chopper.Enemies;
using Chopper.Player;
using Chopper.World.Generation;
using FishNet.Object;
using UnityEngine;

namespace Chopper.World
{
    // The crypts' changing state, on the server: who has looted which chest, and which crypts have woken their
    // guardians. The stone itself is built from the seed on every peer (WorldBuilder, CryptWorld). Chests are looted
    // per player, so every co-op partner finds their own iron and shards. Guardians wake the first time anyone
    // goes down, stay through dawn, and do not come back once felled. A game over resets it all.
    public class CryptManager : NetworkBehaviour
    {
        // Chest id to the profile ids of the players who emptied it (kept in the world save).
        private readonly Dictionary<int, HashSet<string>> _looted = new Dictionary<int, HashSet<string>>();
        private readonly HashSet<int> _awake = new HashSet<int>();

        public static CryptManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool IsAwake(int crypt) => _awake.Contains(crypt);

        public bool HasLooted(int chest, PlayerCrypt player) =>
            _looted.TryGetValue(chest, out HashSet<string> who) && who.Contains(PlayerIdentity.KeyOf(player));

        // Server only. Takes the player down into the crypt whose door they stand at.
        public bool ServerTryEnter(PlayerCrypt player)
        {
            int crypt = CryptWorld.DoorInReach(player.transform.position);
            if (crypt < 0) return false;
            CryptPlan plan = CryptWorld.Plans[crypt];
            ServerWakeGuardians(crypt);
            player.ServerTeleport(plan.ArrivalSpot + Vector3.up * 0.1f, plan.ArrivalYaw);
            return true;
        }

        // Server only. Brings the player back up to the entrance from the foot of the stairs.
        public bool ServerTryExit(PlayerCrypt player)
        {
            int crypt = CryptWorld.ExitInReach(player.transform.position);
            if (crypt < 0) return false;
            CryptPlan plan = CryptWorld.Plans[crypt];
            player.ServerTeleport(plan.ReturnSpot, plan.ReturnYaw);
            return true;
        }

        // Server only. Opens the nearest chest in reach for this player, once.
        public bool ServerTryOpenChest(PlayerCrypt player)
        {
            int chest = CryptWorld.ChestInReach(player.transform.position);
            if (chest < 0) return false;
            if (HasLooted(chest, player))
            {
                return false;
            }
            if (!_looted.TryGetValue(chest, out HashSet<string> who))
            {
                who = new HashSet<string>();
                _looted[chest] = who;
            }
            who.Add(PlayerIdentity.KeyOf(player));

            CryptChest loot = CryptWorld.Chests[chest].Chest;
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null) inventory.ServerAddRelics(loot.Iron, loot.Shards);
            player.ServerChestOpened(chest, loot.Iron, loot.Shards);
            return true;
        }

        // Server only. The first visit wakes every guardian of the crypt where it stands watch.
        public void ServerWakeGuardians(int crypt)
        {
            if (_awake.Contains(crypt) || EnemyDirector.Instance == null) return;
            _awake.Add(crypt);
            CryptPlan plan = CryptWorld.Plans[crypt];
            foreach (CryptGuardian guardian in plan.Guardians)
            {
                Treant treant = EnemyDirector.Instance.ServerSpawn(plan.Site.Zone, guardian.Size, false, guardian.Position);
                treant.ServerMakeGuardian(guardian.Position);
                treant.ServerActivate();
            }
        }

        // Server only. Every guardian standing in this crypt right now.
        public static int CountGuardians(int crypt)
        {
            int count = 0;
            foreach (Treant t in Treant.All)
            {
                if (t != null && t.IsGuardian && CryptWorld.CryptAt(t.transform.position) == crypt) count++;
            }
            return count;
        }

        // ---------- Saving ----------

        public IEnumerable<int> AwakeCrypts => _awake;

        public IEnumerable<(int chest, string player)> LootedPairs()
        {
            foreach (KeyValuePair<int, HashSet<string>> pair in _looted)
            {
                foreach (string player in pair.Value) yield return (pair.Key, player);
            }
        }

        // Server only. Loading a saved world: which crypts are awake (their surviving guardians are spawned by the
        // loader) and who emptied which chest.
        public void ServerLoad(IEnumerable<int> awake, IEnumerable<(int chest, string player)> looted)
        {
            _awake.Clear();
            _looted.Clear();
            foreach (int crypt in awake) _awake.Add(crypt);
            foreach ((int chest, string player) in looted)
            {
                if (!_looted.TryGetValue(chest, out HashSet<string> who))
                {
                    who = new HashSet<string>();
                    _looted[chest] = who;
                }
                who.Add(player);
            }
        }

        // Server only. Tells a player which chests they have already emptied, so those show as open for them.
        public void ServerSendLooted(PlayerCrypt player)
        {
            string key = PlayerIdentity.KeyOf(player);
            var chests = new List<int>();
            foreach (KeyValuePair<int, HashSet<string>> pair in _looted) if (pair.Value.Contains(key)) chests.Add(pair.Key);
            if (chests.Count > 0) player.ServerMarkOpened(chests.ToArray());
        }

        // Server only. Game over: chests full again and guardians asleep (EnemyDirector despawns the enemies).
        public void ServerReset()
        {
            _looted.Clear();
            _awake.Clear();
            ResetObserversRpc();
        }

        [ObserversRpc]
        private void ResetObserversRpc() => CryptWorld.ClearOpenedHere();
    }
}
