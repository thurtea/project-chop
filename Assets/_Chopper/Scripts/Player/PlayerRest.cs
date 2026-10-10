using System.Collections.Generic;
using Chopper.Core;
using Chopper.UI;
using Chopper.World;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Chopper.Player
{
    // Sleeping in the bed, Valheim style. From dusk on, interact at the bed lies the player down (they stop moving and
    // the screen darkens); moving, jumping or interacting again gets up. When every player is in bed the night is
    // skipped to the morning (DayNightCycle.ServerSleepToMorning) and everyone wakes. The respawn point is beside the bed
    // already, so sleeping does not need to set it.
    public class PlayerRest : NetworkBehaviour
    {
        private const float WakeInput = 0.5f;
        // A player who drifts this far from the bed (pushed, teleported) is out of it.
        private const float LeaveDistance = 3.5f;

        private readonly SyncVar<bool> _inBed = new SyncVar<bool>();

        private static readonly List<PlayerRest> _all = new List<PlayerRest>();
        private PlayerHealth _health;
        private Vector3 _bedAt;
        private int _lieFrame = -1;
        private UnityEngine.InputSystem.InputAction _move;

        public static PlayerRest Local { get; private set; }

        public bool InBed => _inBed.Value;

        // How many players are in bed, and how many there are (for the "In bed 1/2" line).
        public static int InBedCount
        {
            get
            {
                int count = 0;
                foreach (PlayerRest rest in _all) if (rest != null && rest.InBed) count++;
                return count;
            }
        }

        public static int PlayerCount => _all.Count;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!_all.Contains(this)) _all.Add(this);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _all.Remove(this);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner) return;
            Local = this;
            _move = UnityEngine.InputSystem.InputSystem.actions.FindAction("Player/Move", true);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Local == this) Local = null;
        }

        // Owner: interact at the bed. False when sleeping is not possible now (by day), so the caller can say so.
        public bool RequestSleep()
        {
            if (!IsOwner) return false;
            DayNightCycle cycle = DayNightCycle.Instance;
            if (cycle == null || !SleepRules.CanSleep(cycle.Phase)) return false;
            _lieFrame = Time.frameCount;
            SleepServerRpc(true);
            return true;
        }

        public void RequestWake()
        {
            if (IsOwner && InBed) SleepServerRpc(false);
        }

        [ServerRpc]
        private void SleepServerRpc(bool sleep)
        {
            if (sleep) ServerLieDown();
            else _inBed.Value = false;
        }

        // Server only. Into the bed if one is in reach, it is dusk or night and the player is up.
        public bool ServerLieDown()
        {
            DayNightCycle cycle = DayNightCycle.Instance;
            Bed bed = Bed.InReach(transform.position);
            if (bed == null || cycle == null || !SleepRules.CanSleep(cycle.Phase) || (_health != null && _health.IsDown)) return false;
            _bedAt = transform.position;
            _inBed.Value = true;
            ServerCheckEveryone();
            return true;
        }

        private void Update()
        {
            if (IsServerInitialized) ServerTick();
            if (!IsOwner || !InBed || _lieFrame == Time.frameCount || UiState.GameplayBlocked) return;
            // Any move, a jump or interact gets out of bed.
            bool move = _move != null && _move.ReadValue<Vector2>().magnitude > WakeInput;
            if (move || Keybinds.WasPressed(KeyAction.Jump) || Keybinds.WasPressed(KeyAction.Interact)) RequestWake();
        }

        private void ServerTick()
        {
            if (!_inBed.Value) return;
            DayNightCycle cycle = DayNightCycle.Instance;
            Vector3 offset = transform.position - _bedAt;
            offset.y = 0f;
            if ((_health != null && _health.IsDown) || cycle == null || !SleepRules.CanSleep(cycle.Phase) || offset.magnitude > LeaveDistance)
                _inBed.Value = false;
        }

        // Server only. Everyone is in bed: skip to the morning and wake them all.
        private static void ServerCheckEveryone()
        {
            if (!SleepRules.Everyone(InBedCount, _all.Count)) return;
            DayNightCycle cycle = DayNightCycle.Instance;
            if (cycle == null) return;
            cycle.ServerSleepToMorning();
            foreach (PlayerRest rest in _all) if (rest != null) rest._inBed.Value = false;
        }

        // Server only, tests: whether everyone being in bed has skipped the night.
        public static void ServerCheckForTests() => ServerCheckEveryone();
    }
}
