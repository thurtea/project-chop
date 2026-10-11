using Chopper.Audio;
using System.Collections.Generic;
using Chopper.Core;
using Chopper.UI;
using Chopper.World;
using Chopper.World.Building;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using FishNet.Connection;
using UnityEngine.InputSystem;

namespace Chopper.Player
{
    // Wood the player carries (plan D15). Server owned SyncVar; the owner presses Interact near the hut to
    // deposit it and repair. Everything is a plain press: picking a plant or a branch drops it on the ground, and it is
    // taken by walking over it (GroundItem), the way Valheim does it.
    public class PlayerWood : NetworkBehaviour
    {
        private const string InteractActionName = "Player/Interact";
        private const int DebugWoodAmount = 40;

        [SerializeField] private GameConfig _config;

        private readonly SyncVar<int> _carried = new SyncVar<int>();

        private InputAction _interact;

        private static readonly List<PlayerWood> _active = new List<PlayerWood>();

        public static PlayerWood Local { get; private set; }

        // Every player visible on this client (used by the map).
        public static IReadOnlyList<PlayerWood> Active => _active;

        public int Carried => _carried.Value;
        public int Cap => _config.WoodCarryCap;

        private const float GrassPushRadius = 1.25f;

        public override void OnStartClient()
        {
            base.OnStartClient();
            GrassPushers.Add(transform, GrassPushRadius);
            _active.Add(this);
            if (!IsOwner) return;
            _carried.OnChange += OnCarriedChanged;

            Local = this;
            _interact = InputSystem.actions.FindAction(InteractActionName, true);
            _interact.Enable();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            GrassPushers.Remove(transform);
            _active.Remove(this);
            _carried.OnChange -= OnCarriedChanged;
            if (Local == this) Local = null;
            _interact = null;
        }

        private void OnCarriedChanged(int previous, int next, bool asServer)
        {
            // One call per machine: the host reacts to the server side report, everyone else to the client side one.
            if (asServer != IsServerInitialized) return;
            if (next > previous)
            {
                Sfx.Play(ProceduralSfx.Pickup, transform.position + Vector3.up, 0.7f, 1f, 0.05f);
                if (IsOwner) PickupFeed.Add(PickupFeed.Kind.Wood, "Wood", next - previous, next);
            }
            else if (next < previous)
            {
                Sfx.Play(ProceduralSfx.Deposit, transform.position, 0.9f);
            }
        }

        private void Update()
        {
            if (!IsOwner || _interact == null) return;
            if (!UiState.GameplayBlocked && InteractPressed() && TryPickUp()) { }
            else if (!UiState.GameplayBlocked && InteractPressed())
            {
                Sign sign = Sign.Nearest(transform.position);
                Hut hutHere = Hut.Instance;
                PlayerCrypt crypt = GetComponent<PlayerCrypt>();
                // Not a pattern match: a missing Unity component compares equal to null but is not a C# null.
                PlayerMining mining = GetComponent<PlayerMining>();
                PlayerFishing fishing = GetComponent<PlayerFishing>();
                PlayerRest rest = GetComponent<PlayerRest>();
                Station station = StationHere(transform.position);
                PlayerBuilder builder = GetComponent<PlayerBuilder>();
                // With the hammer out, interact on a placed piece takes it down.
                if (builder != null && builder.TryRemoveAimed()) { }
                else if (crypt != null && crypt.TryInteract()) { }
                else if (mining != null && mining.TryInteract()) { }
                // At a shore with a rod, interact takes it out (and puts it away again).
                else if (fishing != null && fishing.TryToggle()) { }
                // The house: the workbench and the chest open their menus, the bed lies down (from dusk on).
                else if (station == Station.Workbench) UiState.RequestWorkbench();
                else if (station == Station.Chest) UiState.RequestChest();
                else if (station == Station.Bed)
                {
                    // In bed, interact gets up (PlayerRest). By day there is no sleeping: a dull click says so.
                    if (rest != null && rest.InBed) { }
                    else if (rest == null || !rest.RequestSleep()) Sfx.Play(ProceduralSfx.UiClick, transform.position + Vector3.up, 0.5f, 0.6f);
                }
                // A sign is read on interact, unless the player stands at the hut with wood to put away.
                else if (sign != null && (Carried == 0 || hutHere == null || !NearHut(hutHere)))
                {
                    if (sign.IsGuide) UiState.RequestHowToPlay();
                    else UiState.RequestReadSign(sign.Title, sign.Body);
                }
                else InteractServerRpc();
            }
            // The Repair key: the hut when near it, and pieces near a workbench. With the hammer out it turns the piece.
            if (!UiState.GameplayBlocked && !PlayerBuilder.LocalBuilding && Keybinds.WasPressed(KeyAction.Repair)
                && ((Hut.Instance != null && NearHut(Hut.Instance)) || Workbench.AnyInReach(transform.position))) RepairServerRpc();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame) DebugWoodServerRpc();
#endif
        }

        private enum Pickup { None, Herb, Pile, Branch }

        public enum Station { None, Workbench, Chest, Bed }

        // The house furniture in reach, nearest first in that order (they stand apart, so at most one is in reach).
        public static Station StationHere(Vector3 position)
        {
            if (Workbench.AnyInReach(position)) return Station.Workbench;
            if (StorageChest.AnyInReach(position)) return Station.Chest;
            if (Bed.InReach(position) != null) return Station.Bed;
            return Station.None;
        }

        // What interact would pick here. Plants first, then a wood pile (only with room to carry it), then a branch. A
        // sign or a shore claims the interact for itself.
        private Pickup PickupHere()
        {
            Vector3 at = transform.position;
            if (HerbManager.Instance != null && HerbManager.Instance.HerbInReach(at)) return Pickup.Herb;
            if (Sign.Nearest(at) != null || PlayerFishing.AtWater(at)) return Pickup.None;
            if (Carried < Cap && WoodPile.Nearest(at) != null) return Pickup.Pile;
            BranchManager branches = BranchManager.Instance;
            if (branches != null && branches.BranchInReach(at)) return Pickup.Branch;
            return Pickup.None;
        }

        // A press picks, like Valheim: a plant or a branch drops its item on the ground (GroundItem), which is taken by
        // walking over it; a wood pile is emptied straight into the arms. No holds.
        // A press that picks something by hand (a plant or a branch), for the bend down (PlayerAnimator).
        public event System.Action Gathered;

        private bool TryPickUp()
        {
            Pickup pickup = PickupHere();
            if (pickup == Pickup.None) return false;
            if (pickup != Pickup.Pile) Gathered?.Invoke();
            if (pickup == Pickup.Herb) PickHerbServerRpc();
            else if (pickup == Pickup.Pile) TakePileServerRpc();
            else PickBranchServerRpc();
            return true;
        }

        [ServerRpc]
        private void PickHerbServerRpc() => ServerPickHerb();

        [ServerRpc]
        private void PickBranchServerRpc() => ServerPickBranch();

        [ServerRpc]
        private void TakePileServerRpc() => ServerTakePile();

        // Server only. Takes wood back from a pile left where someone fell.
        public int ServerTakePile()
        {
            WoodPile pile = WoodPile.Nearest(transform.position);
            return pile != null ? pile.ServerTake(this) : 0;
        }

        // Server only. Picks up the nearest fallen branch in reach as wood.
        public bool ServerPickBranch()
        {
            return BranchManager.Instance != null && BranchManager.Instance.ServerTryPick(transform.position, this);
        }

        // Server only. Picks the nearest herb in reach into the inventory.
        public bool ServerPickHerb()
        {
            return HerbManager.Instance != null && HerbManager.Instance.ServerTryPick(transform.position, GetComponent<PlayerInventory>());
        }

        // Development only: F6 gives wood so upgrades can be tried without a long chop.
        [ServerRpc]
        private void DebugWoodServerRpc()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ServerAddWood(DebugWoodAmount);
#endif
        }

        private bool InteractPressed() => Keybinds.WasPressed(KeyAction.Interact);

        // Server only.
        public void ServerAddWood(int amount)
        {
            _carried.Value = WoodRules.AddCarried(_carried.Value, amount, _config.WoodCarryCap);
        }

        // Server only. Adds (or removes) carried wood within the cap; returns how much changed.
        public int ServerChange(int delta)
        {
            int before = _carried.Value;
            _carried.Value = Mathf.Clamp(before + delta, 0, _config.WoodCarryCap);
            return _carried.Value - before;
        }

        // Server only. Removes wood for crafting; returns false if the player does not carry enough.
        public bool ServerTrySpend(int amount)
        {
            if (amount < 0 || _carried.Value < amount) return false;
            _carried.Value -= amount;
            return true;
        }

        // Server only. Empties the carried wood and returns how much it was.
        public int ServerTakeAll()
        {
            int taken = _carried.Value;
            _carried.Value = 0;
            return taken;
        }

        [ServerRpc]
        private void InteractServerRpc() => ServerInteract();

        // Server only. The interact button at the hut: deposit carried wood, which repairs it while it is still broken.
        public void ServerInteract()
        {
            Hut hut = Hut.Instance;
            if (hut == null || !NearHut(hut)) return;
            hut.ServerDeposit(this);
        }

        [ServerRpc]
        private void RepairServerRpc() => ServerRepairAll();

        // Server only. Fixes everything it can at once: a broken hut takes the carried wood first, then the hut store
        // and any wood left restore the hut's health.
        public void ServerRepairAll()
        {
            Hut hut = Hut.Instance;
            int restored = 0;
            int woodSpent = 0;
            // At a workbench the Repair key also mends the building pieces around it.
            BuildManager pieces = BuildManager.Instance;
            if (pieces != null) restored += pieces.ServerRepairNear(gameObject);
            if (hut != null && NearHut(hut))
            {
                if (!hut.IsRepaired && Carried > 0) hut.ServerDeposit(this);
                // Still short: the prompt over the hut already shows how much wood it has and needs.
                if (hut.IsRepaired) restored += hut.ServerRepair(this, out woodSpent);
            }
            if (Owner.IsValid) RepairResultTargetRpc(Owner, restored, woodSpent);
        }

        [TargetRpc]
        private void RepairResultTargetRpc(NetworkConnection connection, int restored, int woodSpent)
        {
            // Heard, not read: the hammer chime when something was mended, a dull click when nothing needed it.
            if (restored > 0) Sfx.Play(ProceduralSfx.Upgrade, transform.position + Vector3.up, 0.8f);
            else Sfx.Play(ProceduralSfx.UiClick, transform.position + Vector3.up, 0.5f, 0.6f);
        }

        private bool NearHut(Hut hut)
        {
            Vector3 offset = hut.transform.position - transform.position;
            offset.y = 0f;
            return offset.magnitude <= _config.HutInteractRadius;
        }
    }
}
