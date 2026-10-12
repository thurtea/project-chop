using Chopper.Audio;
using Chopper.Core;
using Chopper.Enemies;
using Chopper.UI;
using Chopper.World;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Chopper.Player
{
    // Blocking and dodge rolling.
    // Block (hold right mouse or LB): slows you, cuts damage, and a block raised just before a hit parries it: no
    // damage and the attacker is staggered. Holding it drains stamina; every blocked hit costs stamina, and running
    // out breaks your guard for a moment.
    // Roll (left Alt by default, R3 on a pad, or jump while the guard is up like Valheim's dodge): a quick dash with a short
// untouchable window at the start, for stamina. With no direction held it hops backward. All keys are rebindable.
    // The owner moves itself (PlayerMotor reads the roll state) and tells the server; the server decides what a hit
    // does (Mitigate) so the rules cannot be cheated from the client.
    public class PlayerCombat : NetworkBehaviour
    {
        private const float GuardBreakSeconds = 1.2f;
        private const float MinStaminaToBlock = 4f;

        private readonly SyncVar<bool> _blocking = new SyncVar<bool>();
        private readonly SyncVar<bool> _god = new SyncVar<bool>();

        [SerializeField] private GameConfig _config;

        private PlayerHealth _health;
        private PlayerUpgrades _upgrades;
        private PlayerStamina _stamina;
        private PlayerMotor _motor;
        private PlayerMovementValidator _validator;
        private PlayerCamera _camera;
        private float _blockStartedAt = float.NegativeInfinity;
        private float _invulnerableUntil;
        private float _serverNextRoll;
        private float _nextRoll;
        private float _guardBrokenUntil;
        private float _rollEndsAt;
        private bool _debugBlocking;
        private bool _guardRaised;

        public bool IsBlocking => _blocking.Value;
        // True the moment the block button is held (the synced IsBlocking lags a network round trip).
        public bool GuardRaised => _guardRaised;
        // Developer god mode: no damage and endless stamina.
        public bool God => _god.Value;

        // Server only.
        public void ServerSetGod(bool on) => _god.Value = on;
        public bool IsRolling => Time.time < _rollEndsAt;
        public bool GuardBroken => Time.time < _guardBrokenUntil;

        // Raised on every client when this player starts a roll (the animator plays it).
        public event System.Action RollPlayed;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _upgrades = GetComponent<PlayerUpgrades>();
            _stamina = GetComponent<PlayerStamina>();
            _motor = GetComponent<PlayerMotor>();
            _validator = GetComponent<PlayerMovementValidator>();
            _camera = GetComponent<PlayerCamera>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
        }

        private void Update()
        {
            if (!IsOwner) return;

            bool canAct = !UiState.GameplayBlocked && _health != null && !_health.IsDown;
            if (!canAct)
            {
                _guardRaised = false;
                SetBlocking(false);
                return;
            }

            // Guard.
            // With the hammer out the block button opens the piece list instead (PlayerBuilder).
            _guardRaised = (BlockHeld() && !PlayerBuilder.LocalBuilding) || _debugBlocking;
            bool wantsBlock = _guardRaised && !IsRolling && !GuardBroken && _stamina != null && _stamina.Current > MinStaminaToBlock
                              && !WaterSystem.IsSwimming(transform.position);
            if (wantsBlock && _stamina != null)
            {
                _stamina.Drain(_config.BlockStaminaPerSecond * Time.deltaTime);
                if (_stamina.Exhausted)
                {
                    _guardBrokenUntil = Time.time + GuardBreakSeconds;
                    wantsBlock = false;
                }
            }
            SetBlocking(wantsBlock);

            // Roll.
            if (RollPressed() && Time.time >= _nextRoll) TryRoll();
        }

        private static bool BlockHeld() => Keybinds.IsHeld(KeyAction.Block);

        private static bool RollPressed()
        {
            // The roll button on its own, or (as in Valheim) jump pressed while the guard is up.
            return Keybinds.WasPressed(KeyAction.Roll) || (BlockHeld() && Keybinds.WasPressed(KeyAction.Jump));
        }

        private void SetBlocking(bool value)
        {
            if (_blocking.Value == value) return;
            SetBlockingServerRpc(value);
        }

        [ServerRpc]
        private void SetBlockingServerRpc(bool value)
        {
            if (value && (_health == null || _health.IsDown)) return;
            if (value && !_blocking.Value) _blockStartedAt = Time.time;
            _blocking.Value = value;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Development only: lets screenshot and test tours raise the guard and roll without input.
        public void DebugSetBlocking(bool value) { if (IsOwner) _debugBlocking = value; }
        public void DebugRoll() { if (IsOwner) TryRoll(); }
#endif

        // ---------- Rolling ----------

        private void TryRoll()
        {
            if (IsRolling || _stamina == null || !_stamina.TryUse(_config.RollStamina)) return;
            if (WaterSystem.IsSwimming(transform.position)) return;

            _nextRoll = Time.time + _config.RollCooldown;
            _rollEndsAt = Time.time + _config.RollSeconds;
            Vector3 direction = _motor != null ? _motor.RollDirection() : transform.forward;
            if (_motor != null) _motor.BeginRoll(direction, _config.RollSeconds);
            RollPlayed?.Invoke();
            RollServerRpc();
        }

        [ServerRpc]
        private void RollServerRpc()
        {
            float now = Time.time;
            if (now < _serverNextRoll || (_health != null && _health.IsDown)) return;
            _serverNextRoll = now + _config.RollCooldown * 0.8f;
            _invulnerableUntil = now + _config.RollInvulnSeconds;
            // Rolling is no faster than sprinting today; if it is tuned faster, the speed check already knows.
            if (_validator != null) _validator.ServerAllowBurst(_config.RollSpeed - _config.PlayerSprintSpeed, _config.RollSeconds, "roll");
            RollObserversRpc();
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void RollObserversRpc()
        {
            _rollEndsAt = Time.time + _config.RollSeconds;
            RollPlayed?.Invoke();
        }

        // ---------- Taking hits (server) ----------

        // Server only. Returns the damage that gets through and shows the result to everyone.
        public int ServerMitigate(int raw, Treant source)
        {
            if (_god.Value) return 0;
            int damage = CombatRules.Resolve(raw, _blocking.Value, _blockStartedAt, _invulnerableUntil, Time.time,
                _config.BlockReduction, _config.ParryWindow, out HitOutcome outcome);
            if (outcome == HitOutcome.Hit) return damage;

            if (outcome == HitOutcome.Parried && source != null) source.ServerStagger(_config.TreantStaggerSeconds * 2.2f, transform.position, true);
            if (outcome == HitOutcome.Blocked && Owner.IsValid) BlockedTargetRpc(Owner);
            OutcomeObserversRpc((int)outcome);
            return damage;
        }

        [TargetRpc]
        private void BlockedTargetRpc(NetworkConnection connection)
        {
            if (_stamina == null) return;
            _stamina.Drain(_config.BlockStaminaPerHit);
            if (_stamina.Exhausted || _stamina.Current <= 0.5f) _guardBrokenUntil = Time.time + GuardBreakSeconds;
        }

        [ObserversRpc]
        private void OutcomeObserversRpc(int outcome)
        {
            Vector3 spot = transform.position + transform.forward * 0.6f + Vector3.up * 1.3f;
            switch ((HitOutcome)outcome)
            {
                case HitOutcome.Parried:
                    CombatFx.Sparks(spot, new Color(1.6f, 1.5f, 1f, 1f), 18);
                    Sfx.Play(ProceduralSfx.Parry, transform.position + Vector3.up, 1f, 1f, 0.05f);
                    PlayerAnimator parryAnim = GetComponent<PlayerAnimator>();
                    if (parryAnim != null) parryAnim.PlayParry();
                    break;
                case HitOutcome.Blocked:
                    CombatFx.Sparks(spot, new Color(1.4f, 1.1f, 0.6f, 1f), 9);
                    Sfx.Play(ProceduralSfx.Block, transform.position + Vector3.up, 0.9f, 1f, 0.08f);
                    break;
                case HitOutcome.Dodged:
                    CombatFx.Dust(transform.position + Vector3.up * 0.3f, new Color(0.9f, 0.9f, 0.85f, 0.5f), 4);
                    break;
            }
        }
    }
}
