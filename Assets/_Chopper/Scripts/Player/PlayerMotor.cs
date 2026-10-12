using Chopper.Core;
using Chopper.UI;
using Chopper.World;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Chopper.Player
{
    // Owner-authoritative movement (plan D2). Only the owning client reads input and moves the
    // CharacterController; the client authoritative NetworkTransform replicates the result.
    // The NetworkTransform also disables the CharacterController on every non-owner copy.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : NetworkBehaviour
    {
        private const string MoveActionName = "Player/Move";
        private const string JumpActionName = "Player/Jump";
        // Below this input magnitude the body keeps its current facing.
        private const float TurnInputThreshold = 0.0001f;

        [SerializeField] private GameConfig _config;
        [SerializeField] private PlayerCamera _playerCamera;

        // Sprint stops when the player has not been moving for this long, so it does not stay on while idle.
        private const float SprintIdleCancelSeconds = 0.3f;
        private const float MoveInputDeadZone = 0.1f;

        // Free flight (developer console "fly"): Jump rises, Crouch sinks, holding Sprint goes much faster.
        private const float FlySpeed = 16f;
        private const float FlyFastSpeed = 60f;
        private const float FlyAcceleration = 80f;
        private const float LandingClearance = 0.2f;

        // Ground steeper than the controller's slope limit is a mountainside: the player slides off it at this speed.
        private const float SteepSlideSpeed = GameConfig.SteepSlideSpeed;
        private const float SteepProbeLength = 0.6f;
        // Swimming into a bank: rise at this speed while the block ahead tops out no higher than this over the water.
        private const float SwimClimbSpeed = 3f;
        private const float SwimClimbMaxAboveSurface = 0.75f;

        // Double jump: one more jump in the air, as high as the first and for the same stamina. It comes back on landing.
        private const int AirJumps = 1;

        private CharacterController _controller;
        private readonly SyncVar<bool> _crouchSync = new SyncVar<bool>();
        private bool _sprintToggled;
        private bool _crouching;
        private bool _crouchToggled;
        private float _idleTime;
        private float _standingHeight;
        private Vector3 _standingCenter;
        private PlayerHealth _health;
        private PlayerStamina _stamina;
        private PlayerCombat _combat;
        private PlayerDevTools _devTools;
        private PlayerFishing _fishing;
        private bool _flyingHere;
        private int _airJumpsLeft;
        private Vector3 _flyVelocity;
        private Vector3 _rollDirection = Vector3.forward;
        private float _rollUntil;
        private Vector3 _lastInput = Vector3.forward;
        private Vector3 _inputDirection;
        private InputAction _move;
        private PlayerRest _rest;
        private InputAction _jump;
        private float _verticalVelocity;
        private Vector3 _horizontalVelocity;
        private float _wetSeconds;
        private bool _swimming;

        // Owner only. Seconds of Wet left (WetRules): 0 is dry.
        public float WetSeconds => _wetSeconds;
        public bool IsWet => WetRules.IsWet(_wetSeconds);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Development: start dry or soaked (the weather tour).
        public void DebugSetWet(float seconds) => _wetSeconds = seconds;
        public void DebugSetCrouch(bool crouch) => _crouchToggled = crouch;
#endif
        public bool IsSwimming => _swimming;

        // True while sprint is toggled on (owner only; others infer it from speed).
        public bool IsSprinting => IsOwner && _sprintToggled;

        // Crouch is held by the owner and mirrored to everyone through a server owned SyncVar.
        public bool IsCrouching => IsOwner ? _crouching : _crouchSync.Value;

        public bool IsFlying => _devTools != null && _devTools.Flying;
        // Air jumps made since spawn (the double jump tour checks it).
        public int AirJumpCount { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _standingHeight = _controller.height;
            _standingCenter = _controller.center;
            _health = GetComponent<PlayerHealth>();
            _stamina = GetComponent<PlayerStamina>();
            _combat = GetComponent<PlayerCombat>();
            _devTools = GetComponent<PlayerDevTools>();
            _fishing = GetComponent<PlayerFishing>();
        }

        // The way a roll goes: where the stick points, or straight ahead when it is centered.
        // With nothing held the roll hops backward, like a dodge out of a guard.
        public Vector3 RollDirection()
        {
            Vector3 flat = _inputDirection;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.04f) flat = -transform.forward;
            return flat.normalized;
        }

        private bool JumpPressed() => Keybinds.WasPressed(KeyAction.Jump);

        // While guarding the body faces where the camera looks, but the walking legs only play a forward stride, so
        // moving sideways at a full right angle looks like skating. The body therefore turns a little toward the way it
        // moves (up to BlockStrafeDegrees), and fades back to squarely forward as the movement turns into backing away,
        // smoothly, so a thumbstick near the sideways or backward line never makes it flick between two facings.
        private const float BlockStrafeDegrees = 40f;
        private const float BlockBackpedalStart = 100f;
        private const float BlockBackpedalEnd = 150f;

        // Development and screenshots: when set (not NaN), the guard faces this yaw instead of the camera's, so the
        // guard can be photographed from the front.
        public static float DebugBlockFacing = float.NaN;

        private float BlockFacingYaw(Vector3 direction)
        {
            if (!float.IsNaN(DebugBlockFacing)) return DebugBlockFacing;
            float cameraYaw = _playerCamera.Yaw;
            if (direction.sqrMagnitude < TurnInputThreshold) return cameraYaw;
            float moveYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float offset = Mathf.DeltaAngle(cameraYaw, moveYaw);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BlockBackpedalStart, BlockBackpedalEnd, Mathf.Abs(offset)));
            return cameraYaw + Mathf.Clamp(offset, -BlockStrafeDegrees, BlockStrafeDegrees) * fade;
        }

        // A short forward push (the combo finisher). Eased out by the normal deceleration.
        public void Lunge(Vector3 direction, float speed)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            _horizontalVelocity += direction.normalized * speed;
        }

        public void BeginRoll(Vector3 direction, float seconds)
        {
            _rollDirection = direction.normalized;
            _rollUntil = Time.time + seconds;
            _sprintToggled = false;
            _horizontalVelocity = _rollDirection * _config.RollSpeed;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner) return;

            _move = InputSystem.actions.FindAction(MoveActionName, true);
            _jump = InputSystem.actions.FindAction(JumpActionName, true);
            _move.Enable();
            _jump.Enable();
            _sprintToggled = false;
            _crouching = false;
            _crouchToggled = false;
            _verticalVelocity = 0f;
            _horizontalVelocity = Vector3.zero;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _move = null;
            _jump = null;
        }

        // Shift toggles sprint, Ctrl (held) crouches. Crouching and standing still both end a sprint.
        private void UpdateStance(float inputMagnitude)
        {
            bool blockedInput = UiState.GameplayBlocked;
            // Both toggles follow the bindings, on the keyboard and on the gamepad.
            bool crouchPressed = !blockedInput && Keybinds.WasPressed(KeyAction.Crouch);
            bool sprintPressed = !blockedInput && Keybinds.WasPressed(KeyAction.Sprint);
            if (crouchPressed) _crouchToggled = !_crouchToggled;
            // Sprinting stands you up; being knocked down ends the crouch.
            if (sprintPressed) _crouchToggled = false;
            if (_health != null && _health.IsDown) _crouchToggled = false;
            bool wantsCrouch = _crouchToggled;
            if (sprintPressed)
            {
                _sprintToggled = !_sprintToggled && (_stamina == null || _stamina.CanSprint);
            }

            _idleTime = inputMagnitude > MoveInputDeadZone ? 0f : _idleTime + Time.deltaTime;
            if (_sprintToggled && _stamina != null)
            {
                if (!_stamina.CanSprint) _sprintToggled = false;
                else if (inputMagnitude > MoveInputDeadZone) _stamina.Drain(_config.SprintStaminaPerSecond * Time.deltaTime);
            }
            if (_idleTime > SprintIdleCancelSeconds || wantsCrouch) _sprintToggled = false;

            if (wantsCrouch != _crouching)
            {
                _crouching = wantsCrouch;
                float height = _crouching ? _config.CrouchHeight : _standingHeight;
                _controller.height = height;
                _controller.center = new Vector3(_standingCenter.x, height * 0.5f, _standingCenter.z);
                if (IsClientInitialized) SetCrouchServerRpc(_crouching);
            }
        }

        // Fall damage: the highest point since leaving the ground, measured on landing. Water breaks a fall; a teleport
        // (a jump of many meters in one frame) or leaving free flight starts over.
        private const float TeleportStep = 10f;
        private bool _airborne;
        private bool _fallIgnored;
        private float _fallTopY;
        private float _lastFallY;

        private void TrackFall(bool swimming)
        {
            float y = transform.position.y;
            bool teleported = Mathf.Abs(y - _lastFallY) > TeleportStep;
            _lastFallY = y;
            if (swimming || teleported || (_health != null && _health.IsDown))
            {
                _airborne = false;
                _fallIgnored = false;
                return;
            }
            if (!_controller.isGrounded)
            {
                _fallTopY = _airborne ? Mathf.Max(_fallTopY, y) : y;
                _airborne = true;
                return;
            }
            // A landing in water never hurts (the arrival drop can reach the pond floor in one slow frame).
            bool intoWater = WaterSystem.DepthAt(transform.position, out _) >= WaterSystem.WadeDepth;
            if (_airborne && !_fallIgnored && !intoWater)
            {
                float height = _fallTopY - y;
                if (FallRules.Damage(height, 100) > 0) FallServerRpc(height);
            }
            _airborne = false;
            _fallIgnored = false;
        }

        [ServerRpc]
        private void FallServerRpc(float height)
        {
            if (_health != null) _health.ServerFall(height);
        }

        [ServerRpc]
        private void SetCrouchServerRpc(bool crouching)
        {
            _crouchSync.Value = crouching;
        }

        private void Update()
        {
            if (!IsOwner || _move == null) return;
            if (IsFlying || _flyingHere)
            {
                UpdateFlight();
                _fallIgnored = true;
                return;
            }
            if (!_controller.enabled) return;

            // With the rod out the player stands still (PlayerFishing ends it on a jump, roll or guard).
            if (_rest == null) _rest = GetComponent<PlayerRest>();
            bool blocked = UiState.GameplayBlocked || (_health != null && _health.IsDown) || (_fishing != null && _fishing.Busy)
                           || (_rest != null && _rest.InBed);
            Vector2 input = blocked ? Vector2.zero : _move.ReadValue<Vector2>();
            Quaternion yaw = Quaternion.Euler(0f, _playerCamera.Yaw, 0f);
            Vector3 direction = Vector3.ClampMagnitude(yaw * new Vector3(input.x, 0f, input.y), 1f);

            if (direction.sqrMagnitude > 0.01f) _lastInput = direction;
            bool rolling = Time.time < _rollUntil;
            bool blocking = _combat != null && _combat.IsBlocking;
            // Block plus jump is the dodge (PlayerCombat turns it into a roll), so it must not also jump.
            bool guardUp = _combat != null && _combat.GuardRaised;
            _inputDirection = direction;
            float waterDepth = WaterSystem.DepthAt(transform.position, out float surfaceY);
            // Only in the water, not anywhere over it: the arrival drop (W9-01) falls 22 m into the pond, and over deep
            // water the swim buoyancy used to catch the player in mid air and lower them slowly.
            if (waterDepth > 0f && transform.position.y > surfaceY + WaterSystem.SurfaceReach) waterDepth = 0f;
            bool swimming = waterDepth >= WaterSystem.SwimDepth;
            bool wading = !swimming && waterDepth >= WaterSystem.WadeDepth;
            _swimming = swimming;
            UpdateWet(waterDepth);

            Vector3 downhill = Vector3.zero;
            if (swimming)
            {
                // Buoyancy: float with the chest in the water instead of sinking or standing on the bottom.
                float floatY = surfaceY - WaterSystem.SwimFloatDepth;
                _verticalVelocity = Mathf.Clamp((floatY - transform.position.y) * 4f, -3f, 3f);
                // Climbing out: swimming into a bank block whose top is near the surface lifts the swimmer over its
                // edge (the feet float 1.05 m down, far below the step height), like pulling up onto a ledge.
                if (direction.sqrMagnitude > TurnInputThreshold && BankAhead(direction, surfaceY))
                    _verticalVelocity = SwimClimbSpeed;
            }
            else
            {
                // Too steep to stand on (a mountainside): no jumping, and slide down, so cliffs cannot be hopped up.
                if (_controller.isGrounded && OnSteepGround(out downhill))
                {
                    _verticalVelocity = Mathf.Min(_verticalVelocity, 0f);
                }
                else if (_controller.isGrounded)
                {
                    if (_verticalVelocity < 0f) _verticalVelocity = -_config.PlayerGroundedStickSpeed;
                    _airJumpsLeft = AirJumps;
                    // TryUse only runs when jump was pressed (short circuit), so stamina is not drained every frame.
                    if (!blocked && !rolling && !guardUp && JumpPressed() && (_stamina == null || _stamina.TryUse(_config.JumpStamina)))
                    {
                        _verticalVelocity = _config.JumpLaunchSpeed;
                    }
                }
                else if (_airJumpsLeft > 0 && !blocked && !rolling && !guardUp && JumpPressed()
                         && (_stamina == null || _stamina.TryUse(_config.JumpStamina)))
                {
                    // The second jump replaces the fall speed, so it lifts the same amount on the way up or down.
                    _airJumpsLeft--;
                    _verticalVelocity = _config.JumpLaunchSpeed;
                    AirJumpCount++;
                }
                _verticalVelocity -= _config.PlayerGravity * Time.deltaTime;
            }

            // Weight: ramp horizontal velocity toward the target instead of setting it instantly.
            UpdateStance(direction.magnitude);
            if (swimming || blocking) _sprintToggled = false;
            float topSpeed = _crouching ? _config.PlayerCrouchSpeed : (_sprintToggled ? _config.PlayerSprintSpeed : _config.PlayerMoveSpeed);
            if (swimming)
            {
                // Slow, and every stroke costs stamina; out of breath the player only paddles (SwimRules).
                if (direction.sqrMagnitude > TurnInputThreshold && _stamina != null) _stamina.Drain(SwimRules.StaminaPerSecond * Time.deltaTime);
                topSpeed = SwimRules.Speed(_config.PlayerMoveSpeed, _stamina != null && _stamina.Exhausted);
            }
            else if (wading) topSpeed *= WaterSystem.WadeSpeedMultiplier;
            if (!swimming && IsWet) topSpeed *= WetRules.RunSpeedMultiplier;
            if (!swimming && HandsFull()) topSpeed *= CarryRules.EquippedSpeedMultiplier;
            if (blocking) topSpeed *= _config.BlockMoveMultiplier;
            Vector3 targetVelocity = direction * topSpeed;
            float rate = direction.sqrMagnitude > TurnInputThreshold ? _config.PlayerAcceleration : _config.PlayerDeceleration;
            if (swimming) rate *= 0.5f;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetVelocity, rate * Time.deltaTime);
            // A roll overrides everything for its short duration: a fast dash that eases out at the end.
            if (rolling)
            {
                float left = Mathf.Clamp01((_rollUntil - Time.time) / Mathf.Max(0.05f, _config.RollSeconds));
                _horizontalVelocity = _rollDirection * _config.RollSpeed * Mathf.Lerp(0.35f, 1f, left);
            }

            Vector3 velocity = _horizontalVelocity;
            if (downhill != Vector3.zero)
            {
                // No walking up it, only across or down, plus the slide.
                float uphill = Vector3.Dot(velocity, -downhill);
                if (uphill > 0f) velocity += downhill * uphill;
                velocity += downhill * SteepSlideSpeed;
            }
            velocity.y = _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
            TrackFall(swimming);

            if (rolling)
            {
                transform.rotation = Quaternion.LookRotation(_rollDirection, Vector3.up);
            }
            else if (blocking)
            {
                // Guarding keeps the raised axe toward where the camera looks.
                Quaternion facing = Quaternion.Euler(0f, BlockFacingYaw(direction), 0f);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, _config.PlayerTurnSpeed * 1.5f * Time.deltaTime);
            }
            else if (direction.sqrMagnitude > TurnInputThreshold)
            {
                Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _config.PlayerTurnSpeed * Time.deltaTime);
            }
        }

        private PlayerAxe _axe;
        private PlayerInventory _inventory;

        // Something in hand: the axe drawn or a torch lit.
        private bool HandsFull()
        {
            if (_axe == null) _axe = GetComponent<PlayerAxe>();
            if (_inventory == null) _inventory = GetComponent<PlayerInventory>();
            return (_axe != null && _axe.IsDrawn) || (_inventory != null && _inventory.TorchLit);
        }

        // Soaked by deep water or rain, dried by a campfire (WetRules). Underground or under a roof there is no rain.
        private void UpdateWet(float waterDepth)
        {
            Vector3 position = transform.position;
            bool inRain = WeatherSystem.Look.Rain > RainSoakThreshold && !Chopper.World.Generation.CryptWorld.IsUnderground(position)
                          && !WeatherSystem.Sheltered(position, transform);
            _wetSeconds = WetRules.Step(_wetSeconds, waterDepth >= WetRules.SoakDepth, inRain, Campfire.IsWarmAt(position), Time.deltaTime);
        }

        private const float RainSoakThreshold = 0.3f;

        // A bank block just ahead (past the capsule's front) that is above the feet but low enough to pull up onto.
        private bool BankAhead(Vector3 direction, float surfaceY)
        {
            if (Chopper.World.Voxel.VoxelColumns.Current == null) return false;
            Vector3 ahead = transform.position + direction.normalized * (_controller.radius + 0.3f);
            float top = WorldGround.HeightAt(ahead);
            return top > transform.position.y + _controller.stepOffset && top <= surfaceY + SwimClimbMaxAboveSurface;
        }

        // True when the terrain under the feet is steeper than the controller may climb; downhill is the flat
        // direction down the slope. The slope is the terrain's own smoothed steepness at the feet, not a contact
        // normal: a sphere touching the seam between two terrain triangles reports edge normals far steeper than the
        // hill, which slid players about on ordinary slopes. Standing on a rock or log (not the terrain) never slides.
        private bool OnSteepGround(out Vector3 downhill)
        {
            downhill = Vector3.zero;
            // Block ground (W5-08 step 5): every top is flat, and risers above one block already stop the controller
            // (stepOffset clears one 0.5 m step), so nothing slides. The smooth-hill rule below only runs without blocks.
            if (Chopper.World.Voxel.VoxelColumns.Current != null) return false;
            if (!WorldGround.Exists || !WorldGround.Covers(transform.position)) return false;
            // Only on the ground itself: above it (on a rock or a log) or below it (in a crypt) there is no slope.
            float ground = WorldGround.HeightAt(transform.position);
            if (Mathf.Abs(transform.position.y - ground) > SteepProbeLength) return false;
            if (WorldGround.SteepnessAt(transform.position) <= _controller.slopeLimit + 2f) return false;
            Vector3 normal = WorldGround.NormalAt(transform.position);
            downhill = new Vector3(normal.x, 0f, normal.z).normalized;
            return downhill != Vector3.zero;
        }

        // Owner only. Flies through everything with the CharacterController off (it would stop at the ground and
        // trees). When flight is switched off the controller comes back on, above the ground if the player was inside
        // the hill, and gravity takes over.
        private void UpdateFlight()
        {
            if (!IsFlying)
            {
                Land();
                return;
            }
            if (!_flyingHere)
            {
                _flyingHere = true;
                _flyVelocity = Vector3.zero;
                _sprintToggled = false;
                _crouchToggled = false;
                _controller.enabled = false;
            }

            bool blocked = UiState.GameplayBlocked;
            Vector2 input = blocked ? Vector2.zero : _move.ReadValue<Vector2>();
            Quaternion yaw = Quaternion.Euler(0f, _playerCamera.Yaw, 0f);
            Vector3 direction = Vector3.ClampMagnitude(yaw * new Vector3(input.x, 0f, input.y), 1f);
            float vertical = 0f;
            if (!blocked && Keybinds.IsHeld(KeyAction.Jump)) vertical += 1f;
            if (!blocked && Keybinds.IsHeld(KeyAction.Crouch)) vertical -= 1f;
            float speed = !blocked && Keybinds.IsHeld(KeyAction.Sprint) ? FlyFastSpeed : FlySpeed;
            Vector3 target = (direction + Vector3.up * vertical) * speed;
            _flyVelocity = Vector3.MoveTowards(_flyVelocity, target, FlyAcceleration * (speed / FlySpeed) * Time.deltaTime);
            transform.position += _flyVelocity * Time.deltaTime;
            _inputDirection = direction;

            if (direction.sqrMagnitude > TurnInputThreshold)
            {
                Quaternion facing = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, _config.PlayerTurnSpeed * 2f * Time.deltaTime);
            }
        }

        private void Land()
        {
            _flyingHere = false;
            _verticalVelocity = 0f;
            _horizontalVelocity = Vector3.zero;
            Vector3 position = transform.position;
            // Underground (the crypts) the land is far overhead, so only lift the player out of the hill above ground.
            if (WorldGround.Exists && !Chopper.World.Generation.CryptWorld.IsUnderground(position))
            {
                float ground = WorldGround.HeightAt(position) + LandingClearance;
                if (position.y < ground) transform.position = new Vector3(position.x, ground, position.z);
            }
            _controller.enabled = true;
        }
    }
}
