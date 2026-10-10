using System.Collections.Generic;
using Chopper.Core;
using Chopper.Player;
using Chopper.World.Generation;
using FishNet.Object;
using UnityEngine;

namespace Chopper.World
{
    // The mines' changing state, on the server: which veins each player has worked today. The rock is built from the
    // seed on every peer (WorldBuilder, MineWorld). Every player may work every vein once a day, so co-op partners
    // never take each other's finds; all veins come back at dawn. Nothing here is saved: a loaded world starts with
    // every vein whole.
    public class MineManager : NetworkBehaviour
    {
        // Vein id to the profile ids of the players who worked it today.
        private readonly Dictionary<int, HashSet<string>> _spent = new Dictionary<int, HashSet<string>>();

        public static MineManager Instance { get; private set; }

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
            DayNightCycle.ServerPhaseChanged += OnPhaseChanged;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            DayNightCycle.ServerPhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(DayPhase phase)
        {
            if (phase == DayPhase.Dawn) ServerRefill();
        }

        public bool HasWorked(int vein, NetworkBehaviour player) =>
            _spent.TryGetValue(vein, out HashSet<string> who) && who.Contains(PlayerIdentity.KeyOf(player));

        // Server only. Takes the player down into the mine whose portal they stand at.
        public bool ServerTryEnter(PlayerCrypt player)
        {
            int mine = MineWorld.DoorInReach(player.transform.position);
            if (mine < 0) return false;
            MinePlan plan = MineWorld.Plans[mine];
            player.ServerTeleport(plan.ArrivalSpot + Vector3.up * 0.1f, plan.ArrivalYaw);
            return true;
        }

        // Server only. Brings the player back up to the portal from the foot of the ladder.
        public bool ServerTryExit(PlayerCrypt player)
        {
            int mine = MineWorld.ExitInReach(player.transform.position);
            if (mine < 0) return false;
            MinePlan plan = MineWorld.Plans[mine];
            player.ServerTeleport(plan.ReturnSpot, plan.ReturnYaw);
            return true;
        }

        // Server only. The vein is worked out for this player until dawn.
        public void ServerMarkWorked(int vein, NetworkBehaviour player)
        {
            if (!_spent.TryGetValue(vein, out HashSet<string> who))
            {
                who = new HashSet<string>();
                _spent[vein] = who;
            }
            who.Add(PlayerIdentity.KeyOf(player));
        }

        // Server only. Tells a joining player which veins they already worked today.
        public void ServerSendWorked(PlayerMining player)
        {
            string key = PlayerIdentity.KeyOf(player);
            var veins = new List<int>();
            foreach (KeyValuePair<int, HashSet<string>> pair in _spent) if (pair.Value.Contains(key)) veins.Add(pair.Key);
            if (veins.Count > 0) player.ServerMarkWorked(veins.ToArray());
        }

        // Server only. Dawn (and a game over): every vein is whole again for everyone.
        public void ServerRefill()
        {
            _spent.Clear();
            RefillObserversRpc();
        }

        [ObserversRpc]
        private void RefillObserversRpc() => MineWorld.ClearSpentHere();
    }
}
