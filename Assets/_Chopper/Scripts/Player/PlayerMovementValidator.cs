using Chopper.Core;
using FishNet.Component.Transforming;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace Chopper.Player
{
    // Server-side sanity check for owner-authoritative movement (plan D2, T06).
    // Every tick the server samples the player position and compares it with the one SpeedCheckWindowTicks ago.
    // Too fast sideways (over MaxAllowedPlayerSpeed, which already covers sprinting or rolling down a slope too steep
    // to stand on) or rising too fast (over MaxAllowedRiseSpeed; falling is never limited) logs a warning and snaps
    // the owner back to the older position with a TargetRpc. Measuring over a window rather than a single tick keeps
    // packet bunching and NetworkTransform smoothing from causing false positives.
    // Known fast moves are allowed explicitly: the server grants a short burst (ServerAllowBurst) when it accepts a
    // roll or a combo finisher lunge, and every move the server makes itself (respawn, crypt doors, arenas, loading a
    // saved spot, the console's tp) goes through ServerNotifyTeleport first. Free flight is the host's alone, and the
    // host's own player is never checked.
    public class PlayerMovementValidator : NetworkBehaviour
    {
        // A granted burst also covers the window it is measured over plus some latency, since the owner moved before
        // the server heard about it.
        private const float BurstLatencySeconds = 0.5f;

        [SerializeField] private GameConfig _config;
        [SerializeField] private NetworkTransform _networkTransform;

        private CharacterController _controller;
        private PlayerDevTools _devTools;

        // Server state. Ring buffer of the last SpeedCheckWindowTicks + 1 positions.
        private Vector3[] _samples;
        private int _sampleCount;
        private int _newest;
        private int _graceTicksLeft;
        private Vector3 _correctionTarget;
        private float _burstSpeed;
        private float _burstUntil;
        private string _burstReason;
        private bool _warnedFlying;

        // Server side, for tests and logs.
        public int ServerCorrections { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _devTools = GetComponent<PlayerDevTools>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _samples = new Vector3[_config.SpeedCheckWindowTicks + 1];
            ResetSamples(transform.position);
            _graceTicksLeft = 0;
            TimeManager.OnPostTick += ServerOnPostTick;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (TimeManager != null) TimeManager.OnPostTick -= ServerOnPostTick;
        }

        private void ServerOnPostTick()
        {
            // The host's own player is the server's: there is nothing to check it against, and a false snap back is a
            // freeze and a teleport for the person hosting. This is also why the host's free flight needs no exception.
            if (Owner.IsLocalClient) return;
            Vector3 position = transform.position;
            float tickSeconds = (float)TimeManager.TickDelta;

            // Flight is a host tool (PlayerDevTools refuses it for joined players). If the flag is ever set on a joined
            // player anyway, say so once and keep checking.
            if (_devTools != null && _devTools.Flying && !_warnedFlying)
            {
                _warnedFlying = true;
                Debug.LogWarning($"[MovementCheck] Client {Owner.ClientId} object {ObjectId} is flagged as flying, " +
                                 "which only the host may do; its movement is still checked.");
            }

            // After a correction or a teleport, wait for the owner's new position to replace packets already in flight.
            if (_graceTicksLeft > 0)
            {
                _graceTicksLeft--;
                if (_graceTicksLeft > 0) return;

                float graceSeconds = _config.SpeedCheckGraceTicks * tickSeconds;
                if (SpeedCheck.IsTooFast(_correctionTarget, position, graceSeconds, HorizontalLimit()))
                {
                    float distance = SpeedCheck.HorizontalDistance(_correctionTarget, position);
                    ServerCorrect(_correctionTarget, $"ignored the last correction or teleport: still {distance:F2} m " +
                                                     $"from it after {graceSeconds:F2} s");
                    return;
                }
                ResetSamples(position);
                return;
            }

            _newest = (_newest + 1) % _samples.Length;
            _samples[_newest] = position;
            if (_sampleCount < _samples.Length) _sampleCount++;
            if (_sampleCount < _samples.Length) return;

            // With a full buffer the oldest sample sits right after the newest.
            Vector3 oldest = _samples[(_newest + 1) % _samples.Length];
            float windowSeconds = _config.SpeedCheckWindowTicks * tickSeconds;

            float horizontalLimit = HorizontalLimit();
            if (SpeedCheck.IsTooFast(oldest, position, windowSeconds, horizontalLimit))
            {
                float distance = SpeedCheck.HorizontalDistance(oldest, position);
                ServerCorrect(oldest, $"horizontal speed: moved {distance:F2} m in {windowSeconds:F2} s " +
                                      $"({distance / windowSeconds:F1} m/s, limit {horizontalLimit:F1}{BurstNote()})");
                return;
            }

            float riseLimit = RiseLimit();
            if (SpeedCheck.IsRisingTooFast(oldest, position, windowSeconds, riseLimit))
            {
                float rise = SpeedCheck.Rise(oldest, position);
                ServerCorrect(oldest, $"vertical speed: rose {rise:F2} m in {windowSeconds:F2} s " +
                                      $"({rise / windowSeconds:F1} m/s, limit {riseLimit:F1}{BurstNote()})");
            }
        }

        private bool BurstActive => Time.time < _burstUntil;
        private float BurstAllowance => BurstActive ? _burstSpeed * _config.SpeedCheckTolerance : 0f;
        private float HorizontalLimit() => _config.MaxAllowedPlayerSpeed + BurstAllowance;
        private float RiseLimit() => _config.MaxAllowedRiseSpeed + BurstAllowance;
        private string BurstNote() => BurstActive ? $", with the {_burstReason} allowance" : string.Empty;

        // Server only. A known fast move was accepted (a roll, the combo finisher lunge): for the next few moments both
        // limits rise by extraSpeed. The largest burst still running wins.
        public void ServerAllowBurst(float extraSpeed, float seconds, string reason)
        {
            if (extraSpeed <= 0f || _config == null) return;
            float windowSeconds = _config.SpeedCheckWindowTicks * (float)TimeManager.TickDelta;
            float until = Time.time + seconds + windowSeconds + BurstLatencySeconds;
            if (BurstActive && _burstSpeed > extraSpeed)
            {
                _burstUntil = Mathf.Max(_burstUntil, until);
                return;
            }
            _burstSpeed = extraSpeed;
            _burstUntil = until;
            _burstReason = reason;
        }

        // Server only. Call before moving a player on purpose (respawn, crypt doors, loading) so the check does not flag
        // the jump. Every server-side move of a player must come through here.
        public void ServerNotifyTeleport(Vector3 target)
        {
            _correctionTarget = target;
            _graceTicksLeft = _config.SpeedCheckGraceTicks;
            ResetSamples(target);
        }

        private void ServerCorrect(Vector3 target, string reason)
        {
            ServerCorrections++;
            Debug.LogWarning($"[MovementCheck] Correction {ServerCorrections}: client {Owner.ClientId} object {ObjectId} " +
                             $"tick {TimeManager.Tick} at {transform.position:F2}, {reason}; snapping back to {target:F2}.");
            _correctionTarget = target;
            _graceTicksLeft = _config.SpeedCheckGraceTicks;
            ResetSamples(target);
            if (Owner.IsValid) SnapBackTargetRpc(Owner, target);
        }

        private void ResetSamples(Vector3 position)
        {
            for (int i = 0; i < _samples.Length; i++) _samples[i] = position;
            _sampleCount = 1;
            _newest = 0;
        }

        [TargetRpc]
        private void SnapBackTargetRpc(NetworkConnection conn, Vector3 position)
        {
            MoveOwnerTo(position);
        }

        // The CharacterController overrides transform writes while enabled, so toggle it around the move.
        // Teleport() tells observers to jump to the next position instead of interpolating across the map.
        private void MoveOwnerTo(Vector3 position)
        {
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = wasEnabled;
            _networkTransform.Teleport();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Debug only: F8 on the owner simulates a speed hack by jumping forward without telling observers.
        private void Update()
        {
            if (!IsOwner || Keyboard.current == null || !Keyboard.current.f8Key.wasPressedThisFrame) return;
            DebugTeleportForward();
        }

        public void DebugTeleportForward()
        {
            if (!IsOwner) return;
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.position += transform.forward * _config.DebugTeleportDistance;
            _controller.enabled = wasEnabled;
            Debug.Log($"[MovementCheck] Debug teleport to {transform.position:F2}.");
        }
#endif
    }
}
