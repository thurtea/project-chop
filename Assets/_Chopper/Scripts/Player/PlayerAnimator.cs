using Chopper.Audio;
using Chopper.Core;
using Chopper.World;
using UnityEngine;

namespace Chopper.Player
{
    // Drives the character Animator on every client from what can be observed: horizontal speed from position
    // changes (so remote players animate too), a ground check, the down state, and the swing event.
    // Locomotion is a blend tree on Speed; the chop plays on an upper body layer so legs keep walking.
    public class PlayerAnimator : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int DownId = Animator.StringToHash("Down");
        private static readonly int SwingId = Animator.StringToHash("Swing");
        private static readonly int CrouchingId = Animator.StringToHash("Crouching");
        private static readonly int SwingSpeedId = Animator.StringToHash("SwingSpeed");
        private static readonly int PunchId = Animator.StringToHash("Punch");
        private static readonly int Swing2Id = Animator.StringToHash("Swing2");
        private static readonly int Swing3Id = Animator.StringToHash("Swing3");
        private static readonly int Punch2Id = Animator.StringToHash("Punch2");
        private static readonly int RollId = Animator.StringToHash("Roll");
        private static readonly int RollSpeedId = Animator.StringToHash("RollSpeed");
        private static readonly int BlockingId = Animator.StringToHash("Blocking");
        private static readonly int SwimmingId = Animator.StringToHash("Swimming");
        private static readonly int HealId = Animator.StringToHash("Heal");
        private static readonly int ChopId = Animator.StringToHash("Chop");
        private static readonly int StowId = Animator.StringToHash("Stow");
        private static readonly int StrideRateId = Animator.StringToHash("StrideRate");
        private static readonly int LandHardId = Animator.StringToHash("LandHard");
        private static readonly int MineId = Animator.StringToHash("Mine");
        private static readonly int GatherId = Animator.StringToHash("Gather");
        private static readonly int FishStageId = Animator.StringToHash("FishStage");
        private static readonly int FishCastId = Animator.StringToHash("FishCast");
        private static readonly int FishCatchId = Animator.StringToHash("FishCatch");
        private static readonly int EmptyStateId = Animator.StringToHash("Empty");
        private static readonly int HitId = Animator.StringToHash("Hit");
        private static readonly int ParryId = Animator.StringToHash("Parry");
        // The action layers that rest in an Empty state. An Empty humanoid state can write the neutral (T) pose over
        // the body, so these layers are only weighted in while they are playing something.
        private static readonly string[] ActionLayerNames = { "Work", "Chop", "Stow", "CrouchArms" };
        private static readonly Color HealGlow = new Color(0.45f, 1.3f, 0.5f, 0.9f);
        // Chopper's own Roll take (animate_chopper ROLL_LEN, 24 frames at 30 fps).
        private const float RollClipSeconds = 24f / 30f;
        private const float WakeInterval = 0.22f;

        // Clip lengths in seconds (TreeChopping and the sword combo from the second library), and the fraction of a
        // swing cooldown one swing should fill.
        // The chop ends after its follow through, about half of the 0.97 s loop.
        // Chopper's own chops (animate_chopper.py): Attack_1 and Attack_2 22 frames, Attack_3 28 frames, at 30 fps. The tree
        // chop is Attack_1.
        private const float ChopClipSeconds = 22f / 30f;
        // The KayKit one-handed strikes (KayKitImport): diagonal slice, horizontal slice, overhead chop.
        private const float ComboAClipSeconds = 1.0f;
        private const float ComboBClipSeconds = 1.367f;
        private const float ComboCClipSeconds = 1.067f;
        private const float PunchClipSeconds = 0.87f;
        private const float ChopFillsCooldown = 0.95f;
        // The top of the locomotion blend (the sprint loop's speed in CharacterSetup); faster than this, the loop plays
        // faster so the feet keep pace.
        private const float TopBlendSpeed = 8.7f;
        private const float SpeedSmoothing = 6.5f;
        private const float GroundCheckHeight = 0.3f;
        private const float GroundCheckDistance = 0.55f;
        private const float MinFootstepSpeed = 0.7f;
        // Meters travelled between footsteps when crouching, walking and sprinting.
        private const float CrouchStride = 1.0f;
        private const float WalkStride = 1.7f;
        private const float SprintStride = 2.3f;
        private const float SprintSpeedThreshold = 7f;
        private const float FootstepVolumeOwner = 0.55f;
        private const float FootstepVolumeOthers = 0.4f;
        private const float CrouchVolumeScale = 0.45f;

        [SerializeField] private Animator _animator;
        [SerializeField] private GameConfig _config;

        private PlayerAxe _axe;
        private PlayerMining _mining;
        private GuardPose _guard;
        private PlayerHealth _health;
        private PlayerUpgrades _upgrades;
        private PlayerMotor _motor;
        private PlayerCombat _combat;
        private PlayerInventory _inventory;
        private PlayerWood _wood;
        private PlayerFishing _fishing;
        private PlayerFishing.Stage _lastFishStage;
        private PlayerFishing.Outcome _lastFishOutcome;
        private bool _airborne;
        private float _airTopY;
        private bool _wasInWater;
        private float _nextWake;
        private float _stepDistance;
        private int _stepIndex;
        private Vector3 _lastPosition;
        private float _speed;
        private int[] _actionLayers;

        private void Awake()
        {
            _axe = GetComponent<PlayerAxe>();
            _health = GetComponent<PlayerHealth>();
            _upgrades = GetComponent<PlayerUpgrades>();
            _motor = GetComponent<PlayerMotor>();
            _combat = GetComponent<PlayerCombat>();
            _lastPosition = transform.position;
        }

        private void OnEnable()
        {
            if (_axe == null) _axe = GetComponent<PlayerAxe>();
            if (_axe != null) _axe.SwingPlayed += OnSwingPlayed;
            if (_axe != null) _axe.StowPlayed += OnStowPlayed;
            if (_mining == null) _mining = GetComponent<PlayerMining>();
            if (_mining != null) _mining.StrikePlayed += OnMiningStrike;
            if (_combat == null) _combat = GetComponent<PlayerCombat>();
            if (_combat != null) _combat.RollPlayed += OnRollPlayed;
            if (_motor == null) _motor = GetComponent<PlayerMotor>();
            if (_motor != null) _motor.AirJumped += OnAirJumped;
            if (_inventory == null) _inventory = GetComponent<PlayerInventory>();
            if (_inventory != null) _inventory.Healed += OnHealed;
            if (_wood == null) _wood = GetComponent<PlayerWood>();
            if (_wood != null) _wood.Gathered += OnGathered;
        }

        private void OnGathered()
        {
            if (_animator != null) _animator.SetTrigger(GatherId);
        }

        // Wrapping a bandage: the hands come together in front of the chest (upper body only, so the legs can keep
        // walking), soft green motes rise around the player, and a gentle chime plays.
        private void OnHealed()
        {
            Vector3 chest = transform.position + Vector3.up * 1.1f;
            Sfx.Play(ProceduralSfx.Upgrade, chest, 0.45f, 0.8f, 0.05f);
            CombatFx.Burst(chest, HealGlow, 16, 1.1f, 0.09f, 1.1f, -0.6f, true, Vector3.up);
            CombatFx.Burst(transform.position + Vector3.up * 0.2f, HealGlow, 10, 0.8f, 0.07f, 0.9f, -0.9f, true, Vector3.up);
            if (_animator != null) _animator.SetTrigger(HealId);
        }

        private void OnRollPlayed()
        {
            Sfx.Play(ProceduralSfx.Swing, transform.position + Vector3.up, 0.5f, 0.6f, 0.05f);
            CombatFx.Dust(transform.position + Vector3.up * 0.15f, FootColor(), 8);
            if (_animator == null) return;
            _animator.SetFloat(RollSpeedId, RollClipSeconds / Mathf.Max(0.1f, _config.RollSeconds));
            _animator.SetTrigger(RollId);
        }

        // The double jump is a front flip: the model turns once round its hips while the body rises.
        private const float FlipSeconds = 0.5f;
        private const float FlipPivotHeight = 0.9f;
        private float _flipStart = float.NegativeInfinity;
        private bool _flipping;
        private Quaternion _flipBaseRotation;
        private Vector3 _flipBasePosition;

        public bool IsFlipping => _flipping;

        private void OnAirJumped()
        {
            _flipStart = Time.time;
            Sfx.Play(ProceduralSfx.Swing, transform.position + Vector3.up, 0.45f, 1.3f, 0.05f);
        }

        private void LateUpdate()
        {
            if (_animator == null || _animator.transform == transform) return;
            Transform model = _animator.transform;
            float t = (Time.time - _flipStart) / FlipSeconds;
            if (t >= 0f && t < 1f)
            {
                if (!_flipping)
                {
                    _flipBaseRotation = model.localRotation;
                    _flipBasePosition = model.localPosition;
                    _flipping = true;
                }
                Quaternion spin = Quaternion.AngleAxis(360f * (t * t * (3f - 2f * t)), Vector3.right);
                Vector3 pivot = Vector3.up * FlipPivotHeight;
                model.localRotation = spin * _flipBaseRotation;
                model.localPosition = _flipBasePosition + pivot - spin * pivot;
            }
            else if (_flipping)
            {
                model.localRotation = _flipBaseRotation;
                model.localPosition = _flipBasePosition;
                _flipping = false;
            }
        }

        private void OnDisable()
        {
            if (_axe != null) _axe.SwingPlayed -= OnSwingPlayed;
            if (_axe != null) _axe.StowPlayed -= OnStowPlayed;
            if (_mining != null) _mining.StrikePlayed -= OnMiningStrike;
            if (_combat != null) _combat.RollPlayed -= OnRollPlayed;
            if (_motor != null) _motor.AirJumped -= OnAirJumped;
            if (_inventory != null) _inventory.Healed -= OnHealed;
            if (_wood != null) _wood.Gathered -= OnGathered;
        }

        private void OnSwingPlayed(int step)
        {
            Sfx.Play(ProceduralSfx.Swing, transform.position + Vector3.up, 0.6f, 1f, 0.1f);
            if (_animator == null) return;
            // Fit the clip into the swing cooldown so faster axes chop faster.
            float cooldown = _config.SwingCooldown * (_upgrades != null ? _upgrades.SwingCooldownMultiplier : 1f) * SkillRules.LogSwingMultiplier(_axe != null ? _axe.LogSkill : 0);
            bool punch = _axe != null && !_axe.IsDrawn;
            // Step -1 is a tree chop. Bare fists alternate a jab and a cross; the axe in a fight runs the sword combo.
            int trigger;
            float clipSeconds;
            if (punch)
            {
                trigger = step % 2 == 1 ? Punch2Id : PunchId;
                clipSeconds = PunchClipSeconds;
            }
            else if (step < 0)
            {
                trigger = ChopId;
                clipSeconds = ChopClipSeconds;
            }
            else if (step == ComboRules.Steps - 1)
            {
                trigger = Swing3Id;
                clipSeconds = ComboCClipSeconds;
            }
            else if (step == 1)
            {
                trigger = Swing2Id;
                clipSeconds = ComboBClipSeconds;
            }
            else
            {
                trigger = SwingId;
                clipSeconds = ComboAClipSeconds;
            }
            _animator.SetFloat(SwingSpeedId, clipSeconds / Mathf.Max(0.1f, cooldown * ChopFillsCooldown));
            _animator.SetTrigger(trigger);
        }

        // A hit that got through, and a parry. The clips are in and still need polish.
        public void PlayHit() => PlayTrigger(HitId);
        public void PlayParry()
        {
            PlayTrigger(ParryId);
            GuardJolt(1f);
        }

        public void PlayBlocked() => GuardJolt(-0.5f);

        public void PlayHitStop(float delay, float seconds) => HitStop.Apply(_animator, delay, seconds);

        private void GuardJolt(float amount)
        {
            if (_guard == null && _animator != null) _guard = _animator.GetComponent<GuardPose>();
            if (_guard != null) _guard.Shove(amount);
        }

        private void PlayTrigger(int id)
        {
            if (_animator == null) return;
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.nameHash != id) continue;
                _animator.SetTrigger(id);
                return;
            }
        }

        // A pickaxe strike at a vein: the crafting pack's one handed blow into a wall (0.97 s), a little quicker.
        private const float MineClipSeconds = 0.97f;
        private const float MiningSwingSeconds = 0.6f;

        private void OnMiningStrike()
        {
            Sfx.Play(ProceduralSfx.Swing, transform.position + Vector3.up, 0.45f, 0.9f, 0.1f);
            if (_animator == null) return;
            _animator.SetFloat(SwingSpeedId, MineClipSeconds / MiningSwingSeconds);
            _animator.SetTrigger(MineId);
        }

        // Drawing the axe from the back or putting it there: the hand reaches back over the shoulder.
        private void OnStowPlayed()
        {
            if (_animator != null) _animator.SetTrigger(StowId);
        }

        private void Update()
        {
            if (_animator == null) return;

            Vector3 delta = transform.position - _lastPosition;
            delta.y = 0f;
            float measured = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
            // Teleports (respawn) would read as a huge speed for one frame; ignore anything unreasonable.
            if (measured > _config.PlayerMoveSpeed * 3f) measured = 0f;
            _lastPosition = transform.position;
            _speed = Mathf.Lerp(_speed, measured, 1f - Mathf.Exp(-SpeedSmoothing * Time.deltaTime));

            float waterDepth = WaterSystem.DepthAt(transform.position, out float surfaceY);
            bool swimming = waterDepth >= WaterSystem.SwimDepth;
            bool inWater = waterDepth >= WaterSystem.WadeDepth;
            bool grounded = swimming || Physics.Raycast(transform.position + Vector3.up * GroundCheckHeight, Vector3.down, GroundCheckDistance);
            _animator.SetBool(SwimmingId, swimming);
            bool guarding = _combat != null && _combat.IsBlocking;
            _animator.SetBool(BlockingId, guarding);
            if (_guard == null) _guard = _animator.GetComponent<GuardPose>();
            // Not while swimming or down: those have their own full body animations.
            if (_guard != null) _guard.Raised = guarding && !swimming && !(_health != null && _health.IsDown);
            TickWater(inWater, swimming, surfaceY, measured);
            _animator.SetFloat(SpeedId, _speed);
            _animator.SetFloat(StrideRateId, Mathf.Max(1f, _speed / TopBlendSpeed));
            _animator.SetBool(GroundedId, grounded);
            _animator.SetBool(DownId, _health != null && _health.IsDown);
            bool crouching = _motor != null && _motor.IsCrouching;
            _animator.SetBool(CrouchingId, crouching);
            TickActionLayers();
            TickLanding(grounded && !swimming, swimming);
            TickFishing();
            TickFootsteps(measured, grounded, crouching);
        }

        // Weight 1 while a layer plays or blends toward a motion, 0 while it rests in Empty. A trigger set this frame
        // starts its transition in the Animator update that follows, and the layer is weighted in from the next frame.
        private void TickActionLayers()
        {
            if (_actionLayers == null)
            {
                _actionLayers = new int[ActionLayerNames.Length];
                for (int i = 0; i < ActionLayerNames.Length; i++) _actionLayers[i] = _animator.GetLayerIndex(ActionLayerNames[i]);
            }
            foreach (int layer in _actionLayers)
            {
                if (layer < 0) continue;
                bool resting = !_animator.IsInTransition(layer) && _animator.GetCurrentAnimatorStateInfo(layer).shortNameHash == EmptyStateId;
                _animator.SetLayerWeight(layer, resting ? 0f : 1f);
            }
        }

        // A fall long enough to hurt ends in a hard landing (every machine sees it from the position alone).
        private void TickLanding(bool onGround, bool swimming)
        {
            float y = transform.position.y;
            if (swimming || (_health != null && _health.IsDown))
            {
                _airborne = false;
                return;
            }
            if (!onGround)
            {
                _airTopY = _airborne ? Mathf.Max(_airTopY, y) : y;
                _airborne = true;
                return;
            }
            if (_airborne && _airTopY - y > FallRules.SafeHeight) _animator.SetTrigger(LandHardId);
            _airborne = false;
        }

        // The rod: out or waiting (1), a fish on the line (2); the cast and a catch play once. The fishing stage is only
        // known on the fisher's own machine.
        private void TickFishing()
        {
            if (_fishing == null) _fishing = GetComponent<PlayerFishing>();
            if (_fishing == null) return;
            PlayerFishing.Stage stage = _fishing.CurrentStage;
            int value = stage == PlayerFishing.Stage.Off ? 0 : stage == PlayerFishing.Stage.Fighting ? 2 : 1;
            _animator.SetInteger(FishStageId, value);
            if (stage == PlayerFishing.Stage.Waiting && (_lastFishStage == PlayerFishing.Stage.Charging || _lastFishStage == PlayerFishing.Stage.Ready))
                _animator.SetTrigger(FishCastId);
            PlayerFishing.Outcome outcome = _fishing.LastOutcome;
            if (outcome == PlayerFishing.Outcome.Caught && _lastFishOutcome != PlayerFishing.Outcome.Caught) _animator.SetTrigger(FishCatchId);
            _lastFishStage = stage;
            _lastFishOutcome = outcome;
        }

        // Splash on entering the water, and a wake of droplets while moving through it.
        private void TickWater(bool inWater, bool swimming, float surfaceY, float measuredSpeed)
        {
            Vector3 surface = new Vector3(transform.position.x, surfaceY, transform.position.z);
            if (inWater && !_wasInWater)
            {
                float strength = Mathf.Clamp01(measuredSpeed / 8f + 0.3f);
                CombatFx.Splash(surface, strength);
            }
            _wasInWater = inWater;
        }

        private Color FootColor()
        {
            switch (ZoneMap.ElementAt(transform.position, _config.HubRadius))
            {
                case Element.Fire: return new Color(0.28f, 0.24f, 0.22f, 0.55f);
                case Element.Water: return new Color(0.4f, 0.5f, 0.44f, 0.5f);
                case Element.Earth: return new Color(0.34f, 0.28f, 0.16f, 0.5f);
                case Element.Wind: return new Color(0.82f, 0.74f, 0.5f, 0.45f);
                default: return new Color(0.45f, 0.42f, 0.28f, 0.45f);
            }
        }

        private void TickFootsteps(float measuredSpeed, bool grounded, bool crouching)
        {
            if (!grounded || _speed < MinFootstepSpeed)
            {
                _stepDistance = 0f;
                return;
            }

            _stepDistance += measuredSpeed * Time.deltaTime;
            float stride = crouching ? CrouchStride : (_speed > SprintSpeedThreshold ? SprintStride : WalkStride);
            if (_stepDistance < stride) return;

            _stepDistance = 0f;
            float volume = _motor != null && _motor.IsOwner ? FootstepVolumeOwner : FootstepVolumeOthers;
            if (crouching) volume *= CrouchVolumeScale;
            Sfx.Play(ProceduralSfx.FootstepOn(GroundSurface.At(transform.position), _stepIndex++), transform.position, volume, 1f, 0.12f);
        }
    }
}
