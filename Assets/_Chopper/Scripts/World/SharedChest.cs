using Chopper.Core;
using Chopper.Player;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Chopper.World
{
    // The storage chest in the house: one store for every player, saved with the world. It holds any carried item
    // (ItemKind: wood, herbs, fiber, bandages, torches, crypt iron, rune shards, essence and fish), one count per kind,
    // up to ItemRules.ChestCap each. The server owns the counts; a player standing at the chest (StorageChest) asks to
    // move items between it and their bag, and the server moves what fits.
    public class SharedChest : NetworkBehaviour
    {
        [SerializeField] private GameConfig _config;

        private readonly SyncList<int> _counts = new SyncList<int>();

        public static SharedChest Instance { get; private set; }

        // Raised on clients whenever a count changes, so an open chest menu redraws.
        public event System.Action Changed;

        public int Count(int kind) => kind >= 0 && kind < _counts.Count ? _counts[kind] : 0;

        public int Total
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _counts.Count; i++) total += _counts[i];
                return total;
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerReset();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _counts.OnChange += OnCountsChanged;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _counts.OnChange -= OnCountsChanged;
        }

        private void OnCountsChanged(SyncListOperation op, int index, int previous, int next, bool asServer)
        {
            if (asServer != IsServerInitialized) return;
            Changed?.Invoke();
        }

        private void Ensure()
        {
            while (_counts.Count < ItemRules.Count) _counts.Add(0);
        }

        // Server only. Empty (a new world, or the game over restart).
        public void ServerReset()
        {
            Ensure();
            for (int i = 0; i < _counts.Count; i++) if (_counts[i] != 0) _counts[i] = 0;
        }

        // Server only. Loading a saved world (counts in ItemKind order; missing kinds are empty).
        public void ServerLoad(int[] counts)
        {
            Ensure();
            for (int i = 0; i < _counts.Count; i++)
            {
                int value = counts != null && i < counts.Length ? Mathf.Clamp(counts[i], 0, ItemRules.ChestCap) : 0;
                if (_counts[i] != value) _counts[i] = value;
            }
        }

        public int[] ServerCapture()
        {
            Ensure();
            var copy = new int[_counts.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = _counts[i];
            return copy;
        }

        // Owner side: ask to move items. toChest puts them in, otherwise they are taken out.
        public void RequestMove(int kind, int amount, bool toChest) => MoveServerRpc(kind, amount, toChest);

        [ServerRpc(RequireOwnership = false)]
        private void MoveServerRpc(int kind, int amount, bool toChest, NetworkConnection sender = null)
        {
            NetworkObject player = sender != null ? sender.FirstObject : null;
            if (player == null || !StorageChest.AnyInReach(player.transform.position)) return;
            ServerMove(player.gameObject, kind, amount, toChest);
        }

        // Server only. Moves up to amount of a kind between a player's bag and the chest, as much as the giver has and
        // the receiver has room for. Returns how many moved.
        public int ServerMove(GameObject player, int kind, int amount, bool toChest)
        {
            if (player == null || !ItemRules.Valid(kind) || amount <= 0) return 0;
            Ensure();
            int carried = PlayerItems.Get(player, kind);
            int stored = _counts[kind];
            int moved = toChest
                ? ItemRules.Movable(carried, stored, ItemRules.ChestCap, amount)
                : ItemRules.Movable(stored, carried, PlayerItems.Cap(_config, kind), amount);
            if (moved <= 0) return 0;
            // The bag changes first; the chest follows by what the bag really took or gave.
            int changed = PlayerItems.ServerChange(player, kind, toChest ? -moved : moved);
            moved = Mathf.Abs(changed);
            _counts[kind] = toChest ? stored + moved : stored - moved;
            return moved;
        }
    }
}
