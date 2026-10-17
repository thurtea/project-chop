using UnityEngine;
using UnityEngine.Serialization;

namespace Chopper.Core
{
    // Every gameplay tunable lives here. Field initializers are only the defaults for a new asset;
    // the SO_GameConfig asset is the source of truth at runtime.
    [CreateAssetMenu(fileName = "SO_GameConfig", menuName = "Chopper/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Day and night (seconds)")]
        [SerializeField, Min(1f)] private float _dayDuration = 330f;
        [SerializeField, Min(1f)] private float _duskDuration = 15f;
        [SerializeField, Min(1f)] private float _nightDuration = 150f;
        [SerializeField, Min(1f)] private float _dawnDuration = 15f;

        [Header("Trees")]
        // Swings with a plain axe; a better axe needs fewer (ChopRules). Raised from 1, 2 and 3 (2026-10-05): trees fell
        // too easily to feel like work.
        [SerializeField, Min(1)] private int _hitsToFellSmall = 4;
        [SerializeField, Min(1)] private int _hitsToFellMedium = 6;
        [SerializeField, Min(1)] private int _hitsToFellLarge = 9;
        // An awake forest enemy's health by its size, in plain axe hits. It used to be the hits to fell, so raising
        // those made enemies tougher too; it stays at what it was.
        [SerializeField, Min(1)] private int _treantHealthSmall = 1;
        [SerializeField, Min(1)] private int _treantHealthMedium = 2;
        [SerializeField, Min(1)] private int _treantHealthLarge = 3;
        [SerializeField, FormerlySerializedAs("_seedlingsSmall"), Min(0)] private int _saplingsSmall = 1;
        [SerializeField, FormerlySerializedAs("_seedlingsMedium"), Min(0)] private int _saplingsMedium = 2;
        [SerializeField, FormerlySerializedAs("_seedlingsLarge"), Min(0)] private int _saplingsLarge = 3;
        [SerializeField, FormerlySerializedAs("_seedlingScatterRadius"), Min(0f)] private float _saplingScatterRadius = 2f;

        [Header("Player")]
        [SerializeField, Min(0f)] private float _playerMoveSpeed = 6f;
        [Tooltip("Speed while sprint is toggled on (Shift).")]
        [SerializeField, Min(0f)] private float _playerSprintSpeed = 9.5f;
        [Tooltip("Speed while crouching (hold Ctrl).")]
        [SerializeField, Min(0f)] private float _playerCrouchSpeed = 2.6f;
        [Tooltip("Character controller height while crouching, in meters.")]
        [SerializeField, Min(0.6f)] private float _crouchHeight = 1.2f;
        [SerializeField, Min(0f)] private float _playerTurnSpeed = 480f;
        [Tooltip("Meters per second squared while input is held. Higher feels snappier, lower feels heavier.")]
        [SerializeField, Min(0.1f)] private float _playerAcceleration = 16f;
        [Tooltip("Meters per second squared when input is released or reversed.")]
        [SerializeField, Min(0.1f)] private float _playerDeceleration = 22f;
        [SerializeField, Min(0f)] private float _playerJumpHeight = 1.05f;
        [SerializeField, Min(0f)] private float _playerGravity = 26f;
        [SerializeField, Min(0f)] private float _playerGroundedStickSpeed = 2f;
        [Tooltip("A fall from at least this many meters plants the player for a moment on landing.")]
        [SerializeField, Min(0f)] private float _landingRecoveryHeight = 3f;
        [SerializeField, Min(0f)] private float _landingRecoverySeconds = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _landingRecoverySpeedMultiplier = 0.35f;
        [SerializeField, Min(1f)] private float _speedCheckTolerance = 1.5f;
        [Tooltip("Ticks of history the server speed check measures over. Longer windows absorb packet bunching.")]
        [SerializeField, Min(1)] private int _speedCheckWindowTicks = 10;
        [Tooltip("Ticks the server waits after a correction for the owner's snapped position to arrive.")]
        [SerializeField, Min(1)] private int _speedCheckGraceTicks = 15;
        [Tooltip("Editor and development builds only: distance of the debug teleport key.")]
        [SerializeField, Min(0f)] private float _debugTeleportDistance = 50f;

        [Header("Player camera")]
        [SerializeField, Min(0f)] private float _cameraDistance = 2.5f;
        [SerializeField, Min(0f)] private float _cameraPivotHeight = 1.6f;
        [SerializeField, Min(0f)] private float _cameraCrouchPivotHeight = 1.0f;
        [SerializeField, Range(-89f, 89f)] private float _cameraDefaultPitch = 10f;
        [SerializeField, Range(-89f, 89f)] private float _cameraMinPitch = -20f;
        [SerializeField, Range(-89f, 89f)] private float _cameraMaxPitch = 70f;
        [SerializeField, Min(0f)] private float _mouseLookSensitivity = 0.15f;
        [SerializeField, Min(0f)] private float _stickLookSpeed = 180f;
        [Tooltip("Closest and farthest the mouse wheel can zoom the camera, in meters behind the player.")]
        [SerializeField, Min(0.5f)] private float _cameraMinDistance = 2.5f;
        [SerializeField, Min(1f)] private float _cameraMaxDistance = 12f;
        [SerializeField, Min(0.1f)] private float _cameraZoomStep = 1f;
        [SerializeField, Min(0.1f)] private float _cameraZoomSmoothing = 10f;
        [Tooltip("How tightly the camera follows the player. Lower trails further behind, higher is rigid.")]
        [SerializeField, Min(0.5f)] private float _cameraFollowSpeed = 9f;

        [Header("Axe")]
        [SerializeField, Min(0f)] private float _swingCooldown = 0.6f;
        [SerializeField, Min(0f)] private float _swingRange = 2.2f;
        [SerializeField, Min(0f)] private float _swingRadius = 0.8f;
        [SerializeField, Min(0f)] private float _swingLatencyTolerance = 0.1f;
        [Tooltip("Seconds the local chop animation takes. Keep at or below the swing cooldown.")]
        [SerializeField, Min(0.01f)] private float _swingAnimationDuration = 0.35f;
        [Tooltip("Degrees the axe rotates down from its rest pose at the bottom of the chop.")]
        [SerializeField] private float _swingArcDegrees = 100f;
        [Tooltip("Share of the swing cooldown before the blow lands.")]
        [SerializeField, Range(0.05f, 0.9f)] private float _swingStrikeFraction = 0.4f;
        [Tooltip("Share of the swing cooldown the swing holds the player: slower, no jumping, only guard or roll cancel it.")]
        [SerializeField, Range(0f, 1f)] private float _swingCommitFraction = 0.85f;
        [SerializeField, Range(0f, 1f)] private float _swingMoveMultiplier = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _swingTurnMultiplier = 0.35f;
        [Tooltip("Meters per second of forward step on the strike frame of a combat swing.")]
        [SerializeField, Min(0f)] private float _swingStrikeStep = 2.5f;

        [Header("Chop feel (local only)")]
        [Tooltip("Camera shake strength in meters for a hit and for a felled tree, at point blank range.")]
        [SerializeField, Min(0f)] private float _shakeOnHit = 0.06f;
        [SerializeField, Min(0f)] private float _shakeOnFell = 0.2f;
        [Tooltip("Shake and sound fade out over this distance in meters.")]
        [SerializeField, Min(1f)] private float _feelFalloffDistance = 25f;
        [SerializeField, Min(0f)] private float _shakeRecoverySpeed = 0.8f;
        [Tooltip("Seconds the hitter's and the target's animators hold when a swing connects, and for a finisher or kill.")]
        [SerializeField, Min(0f)] private float _hitStopSeconds = 0.07f;
        [SerializeField, Min(0f)] private float _hitStopHeavySeconds = 0.09f;
        [Tooltip("Camera kick in meters when a swing connects, and for a finisher or kill.")]
        [SerializeField, Min(0f)] private float _hitKick = 0.05f;
        [SerializeField, Min(0f)] private float _hitKickHeavy = 0.12f;
        [SerializeField, Min(0)] private int _chipsPerHit = 14;
        [SerializeField, Min(0)] private int _leavesOnFell = 40;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 0.8f;

        [Header("Zones")]
        [Tooltip("Radius of the safe hub around the hut in meters. Forests start beyond it.")]
        [SerializeField, Min(5f)] private float _hubRadius = 110f;

        [Header("Stamina")]
        [SerializeField, Min(10f)] private float _staminaMax = 140f;
        [SerializeField, Min(0f)] private float _staminaRegenPerSecond = 26f;
        [Tooltip("Seconds after spending stamina before it starts to refill.")]
        [SerializeField, Min(0f)] private float _staminaRegenDelay = 1.0f;
        [Tooltip("After running dry the player stays exhausted until stamina reaches this.")]
        [SerializeField, Min(0f)] private float _staminaExhaustedRecoverAt = 20f;
        [SerializeField, Min(0f)] private float _sprintStaminaPerSecond = 6f;
        [Tooltip("Only paid when an awake enemy is in front of the swing; chopping trees is free.")]
        [SerializeField, Min(0f)] private float _swingStamina = 6f;
        [SerializeField, Min(0f)] private float _jumpStamina = 8f;

        [Header("Crafting (torches at a workbench, bandages anywhere; paid with carried items)")]
        [SerializeField, Min(0)] private int _torchWoodCost = 3;
        [SerializeField, Min(0)] private int _boatWoodCost = 20;
        [SerializeField, Min(0)] private int _boatFiberCost = 6;
        [SerializeField, Range(0.05f, 0.9f)] private float _sharkCentreBand = 0.25f;
        [SerializeField, Range(0.3f, 1f)] private float _sharkLoseOffset = 0.95f;
        [SerializeField, Min(0.01f)] private float _sharkStaminaDrain = 0.12f;
        [SerializeField, Min(0f)] private float _sharkStaminaRecover = 0.05f;
        [SerializeField, Min(0f)] private float _sharkDriftSpeed = 0.9f;
        [SerializeField, Min(0.1f)] private float _sharkSteerSpeed = 1.6f;
        [SerializeField, Min(0f)] private float _sharkTowSpeed = 3f;
        [SerializeField] private SharkRodUnlock _sharkRodUnlock = SharkRodUnlock.Craft;
        [SerializeField, Min(0)] private int _sharkRodFishToUnlock = 10;
        [SerializeField, Range(10f, 40f)] private float _sharkHookDistance = 25f;
        [SerializeField, Min(0)] private int _sharkRodWoodCost = 12;
        [SerializeField, Min(0)] private int _sharkRodFiberCost = 8;
        [SerializeField, Min(0)] private int _sharkRodIronCost = 4;
        [Header("Healing herbs and fiber")]
        [Tooltip("Healing herbs growing in the world (leafy plants with a yellow flower). Bandages are made from them and fiber.")]
        [SerializeField, Min(0)] private int _herbCount = 240;
        [SerializeField, Min(1)] private int _herbCarryCap = 20;
        [Tooltip("Ferns growing in the world. Picking one gives fiber.")]
        [SerializeField, Min(0)] private int _fabricBushCount = 160;
        [SerializeField, Min(1)] private int _fabricCarryCap = 20;
        [SerializeField, Min(1)] private int _bandageHerbCost = 2;
        [SerializeField, Min(1)] private int _bandageFabricCost = 1;
        [SerializeField, Min(1)] private int _craftStackCap = 9;
        [Tooltip("Seconds one torch burns while lit.")]
        [SerializeField, Min(10f)] private float _torchBurnSeconds = 240f;
        [SerializeField, Min(1)] private int _salveHeal = 50;

        [Header("Starting kit")]
        [SerializeField, Min(0)] private int _startTorches = 1;
        [SerializeField, Min(0)] private int _startSalves = 1;

        [Header("Tools wear out (repaired at a workbench)")]
        [Tooltip("Hits an axe takes before it breaks (trees and enemies alike).")]
        [SerializeField, Min(1)] private int _axeDurability = 400;
        [Tooltip("Catches a rod lands before it breaks. A snapped line costs RodSnapWear.")]
        [SerializeField, Min(1)] private int _rodDurability = 40;
        [SerializeField, Min(1)] private int _rodSnapWear = 3;
        [Tooltip("Carried wood to repair the axe to full.")]
        [SerializeField, Min(0)] private int _axeRepairWood = 10;
        [Tooltip("Carried wood and fiber to repair the rod to full.")]
        [SerializeField, Min(0)] private int _rodRepairWood = 4;
        [SerializeField, Min(0)] private int _rodRepairFabric = 2;

        [Header("Food")]
        [Tooltip("Stamina added to the maximum per point of a fish's food value.")]
        [SerializeField, Min(0f)] private float _fishStaminaPerPoint = 0.5f;
        [Tooltip("Seconds a meal of fish lasts.")]
        [SerializeField, Min(10f)] private float _fedSeconds = 300f;

        [Header("Building")]
        [Tooltip("How far from the player a piece can be placed.")]
        [SerializeField, Min(2f)] private float _buildRange = 8f;
        [Tooltip("Pieces marked Needs Workbench need a workbench within this distance; basic pieces go up anywhere.")]
        [SerializeField, Min(2f)] private float _buildWorkbenchRadius = 20f;
        [SerializeField, Min(1)] private int _maxPieces = 600;

        [Header("Combat")]
        [Tooltip("Axe damage in hit units before upgrades. Enemies have 1, 2 or 3 units of health by size.")]
        [SerializeField, Min(0.1f)] private float _axeBaseDamage = 2f;
        [Tooltip("Extra damage against everything while the axe is enchanted (after killing any elemental enemy).")]
        [SerializeField, Min(0f)] private float _enchantDamageBonus = 0.5f;
        [Tooltip("Enemies raise their arms this long before a hit lands, so a roll or block can be timed.")]
        [SerializeField, Min(0.1f)] private float _treantWindupSeconds = 0.65f;
        [SerializeField, Min(0f)] private float _treantStaggerSeconds = 0.55f;
        [SerializeField, Min(0f)] private float _treantKnockback = 1.4f;
        [Range(0f, 1f), SerializeField] private float _blockReduction = 0.75f;
        [Tooltip("A block raised this recently before a hit parries it: no damage and the attacker is staggered.")]
        [SerializeField, Min(0f)] private float _parryWindow = 0.25f;
        [SerializeField, Min(0f)] private float _blockStaminaPerHit = 10f;
        [SerializeField, Min(0f)] private float _blockStaminaPerSecond = 2f;
        [SerializeField, Range(0.1f, 1f)] private float _blockMoveMultiplier = 0.5f;
        [SerializeField, Min(0f)] private float _rollStamina = 18f;
        [SerializeField, Min(0.1f)] private float _rollSeconds = 0.5f;
        [SerializeField, Min(1f)] private float _rollSpeed = 9f;
        [Tooltip("Untouchable time at the start of a roll.")]
        [SerializeField, Min(0f)] private float _rollInvulnSeconds = 0.4f;
        [SerializeField, Min(0f)] private float _rollCooldown = 0.7f;

        [Header("Start of the game")]
        [Tooltip("The hut starts broken (a ruin). Players rebuild it with wood.")]
        [SerializeField] private bool _hutStartsRuined = true;
        [Tooltip("Wood that must be deposited to repair the hut at the start.")]
        [SerializeField, Min(1)] private int _hutRepairWood = 10;
        [Tooltip("A bare fist punch as a fraction of an axe hit. Trees count a hit every 1 / this many punches.")]
        [SerializeField, Range(0.05f, 1f)] private float _fistDamageMultiplier = 0.34f;

        [Header("Night mix")]
        [Tooltip("Each night rolls the share of enemies that are elemental between these two values.")]
        [SerializeField, Range(0f, 1f)] private float _nightElementalShareMin = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _nightElementalShareMax = 0.85f;

        [Header("Boss milestones")]
        [Tooltip("No grove warden rises before this day.")]
        [SerializeField, Min(1)] private int _bossFirstDay = 3;
        [Tooltip("Each boss defeated raises the trees a grove needs for its warden by this fraction of the base.")]
        [SerializeField, Min(0f)] private float _bossTreesFelledGrowth = 0.5f;

        [Header("Boss boons (permanent, per level)")]
        [SerializeField, Min(0f)] private float _boonAxeDamage = 0.15f;
        [SerializeField, Min(0f)] private float _boonArmor = 0.08f;
        [SerializeField, Min(0f)] private float _boonTorchSeconds = 60f;
        [SerializeField, Min(0)] private int _boonSalveHeal = 25;
        [SerializeField, Min(0f)] private float _boonStamina = 20f;

        [Header("Wood and hut")]
        [SerializeField, Min(0)] private int _woodSmall = 3;
        [SerializeField, Min(0)] private int _woodMedium = 6;
        [SerializeField, Min(0)] private int _woodLarge = 12;
        [SerializeField, Min(1)] private int _woodCarryCap = 40;
        [Tooltip("Hut health restored by one wood.")]
        [SerializeField, Min(1)] private int _hutHealthPerWood = 10;
        [Tooltip("Fraction of max health the hut starts with, so there is something to repair.")]
        [SerializeField, Range(0.05f, 1f)] private float _hutStartHealthFraction = 1f;
        [Tooltip("How close a player must be to the hut to store wood and repair it.")]
        [SerializeField, Min(0f)] private float _hutInteractRadius = 12f;
        [Tooltip("Trunk radius per tree size in world meters, used for axe targeting and colliders.")]
        [SerializeField, Min(0.05f)] private float _trunkRadiusSmall = 0.3f;
        [SerializeField, Min(0.05f)] private float _trunkRadiusMedium = 0.45f;
        [SerializeField, Min(0.05f)] private float _trunkRadiusLarge = 0.65f;

        [Header("Saplings and night")]
        [Tooltip("Most saplings one accepted chop can drop, per tree size. The roll is 0 to this value.")]
        [SerializeField, Min(0)] private int _saplingMaxPerHitSmall = 1;
        [SerializeField, Min(0)] private int _saplingMaxPerHitMedium = 1;
        [SerializeField, Min(0)] private int _saplingMaxPerHitLarge = 2;
        [Tooltip("Saplings planted when a tree is FELLED (not per hit): small 1-2, medium 2-3, large 3-4.")]
        [SerializeField, Min(0)] private int _saplingsMinSmall = 1;
        [SerializeField, Min(0)] private int _saplingsMaxSmall = 2;
        [SerializeField, Min(0)] private int _saplingsMinMedium = 2;
        [SerializeField, Min(0)] private int _saplingsMaxMedium = 3;
        [SerializeField, Min(0)] private int _saplingsMinLarge = 3;
        [SerializeField, Min(0)] private int _saplingsMaxLarge = 4;
        [Tooltip("Saplings alive at once. Drops stop when this is reached.")]
        [SerializeField, Min(1)] private int _maxSaplings = 150;
        [Tooltip("Enemies awake at once at one player. Scaled by the player count.")]
        [SerializeField, Min(1)] private int _enemyCapBase = 40;
        [SerializeField, Min(0f)] private float _nightSpawnStagger = 0.15f;

        [Header("Treant combat")]
        [SerializeField, Min(0f)] private float _treantAggroRange = 40f;
        [SerializeField, Min(0.1f)] private float _treantMoveSpeed = 3.4f;
        [Tooltip("Enemies farther than this from their target run faster, so a night from the far forests still reaches the hut.")]
        [SerializeField, Min(0f)] private float _treantChargeDistance = 60f;
        [SerializeField, Min(1f)] private float _treantChargeMultiplier = 2f;
        [Tooltip("Speed lost per size step, so large enemies are slower.")]
        [SerializeField, Min(0f)] private float _treantSizeSlowdown = 0.5f;
        [SerializeField, Min(0)] private int _treantPlayerDamage = 4;
        [SerializeField, Min(0.1f)] private float _treantHitRadiusBase = 0.5f;

        [Header("Player health")]
        [SerializeField, Min(1)] private int _playerMaxHealth = 100;
        [Tooltip("Off: lives are infinite (going down only costs the respawn wait). On: a finite number of lives per night.")]
        [SerializeField] private bool _finiteLives = false;
        [Tooltip("Lives per night in the finite lives mode. Each time a player goes down it costs one; dawn restores them.")]
        [SerializeField, Min(1)] private int _playerLives = 3;
        [SerializeField, Min(0f)] private float _playerRespawnSeconds = 3f;

        [Header("Boss")]
        [SerializeField, Min(0.1f)] private float _bossGrowDays = 2f;
        [SerializeField, Min(1)] private int _bossChopHits = 30;
        [Tooltip("Axe hit units the boss can take before it falls.")]
        [SerializeField, Min(1f)] private float _bossHealth = 30f;
        [SerializeField, Min(0.1f)] private float _bossSlamInterval = 6f;
        [SerializeField, Min(0f)] private float _bossSlamRadius = 7f;
        [SerializeField, Min(0)] private int _bossSlamPlayerDamage = 35;
        [SerializeField, Min(0)] private int _bossSlamHutDamage = 60;
        [SerializeField, Min(0f)] private float _bossScale = 2.6f;
        [Tooltip("Wood the killing player receives from a felled boss tree, and stored in the hut when the boss dies.")]
        [SerializeField, Min(0)] private int _bossRewardWood = 60;

        [Header("Grove warden (zone boss)")]
        [Tooltip("Felled forest trees within this many meters of each other count as one grove.")]
        [SerializeField, Min(5f)] private float _groveRadius = 35f;
        [Tooltip("Trees felled in one grove before its warden rises (raised after each boss like the old milestone).")]
        [SerializeField, Min(1)] private int _groveTreesFelled = 18;
        [Tooltip("Axe hit units the warden can take at one player.")]
        [SerializeField, Min(1f)] private float _wardenHealth = 90f;
        [Tooltip("Each player past the first adds this fraction of the warden's health.")]
        [SerializeField, Min(0f)] private float _wardenHealthPerExtraPlayer = 0.6f;
        [Tooltip("The warden fights only inside this many meters of where it rose, and heals if everyone leaves.")]
        [SerializeField, Min(5f)] private float _wardenLeashRadius = 45f;
        [SerializeField, Min(0.5f)] private float _wardenRootInterval = 7f;
        [SerializeField, Min(0.1f)] private float _wardenRootWarning = 1.3f;
        [SerializeField, Min(0.5f)] private float _wardenRootRadius = 2.4f;
        [SerializeField, Min(0)] private int _wardenRootDamage = 30;
        [SerializeField, Min(0)] private int _wardenRewardWood = 80;

        [Header("Upgrades (level 0 is the starting gear)")]
        [SerializeField] private int[] _axeUpgradeCosts = { 30, 60, 120 };
        [SerializeField] private float[] _axeCooldownMultipliers = { 1f, 0.85f, 0.72f, 0.6f };
        [SerializeField] private float[] _axeDamageMultipliers = { 1f, 1.25f, 1.5f, 2f };
        [SerializeField] private int[] _armorUpgradeCosts = { 30, 60, 120 };
        [SerializeField] private float[] _armorDamageReduction = { 0f, 0.15f, 0.3f, 0.45f };
        [SerializeField] private int[] _hutUpgradeCosts = { 40, 80, 150 };
        [SerializeField, Min(0)] private int _hutHealthPerLevel = 250;
        [Tooltip("Crypt iron for each Sturdy gear level. Sturdy gear is what lets a player live through a falling tree.")]
        [SerializeField] private int[] _sturdyIronCosts = { 4, 6, 10 };
        [Tooltip("Essence of an element for each level of that element's ward (from crypt arena bosses).")]
        [SerializeField] private int[] _wardEssenceCosts = { 2, 3 };
        [Tooltip("Share of damage from enemies of the warded element that a ward level removes.")]
        [SerializeField] private float[] _wardReduction = { 0f, 0.25f, 0.5f };
        [Tooltip("Essence of an element, and rune shards, to forge that element's stone: a permanent enchantment of the axe.")]
        [SerializeField, Min(0)] private int _stoneEssenceCost = 4;
        [SerializeField, Min(0)] private int _stoneShardCost = 3;
        [Header("Fishing")]
        [Tooltip("Every fish that can bite, in FishKind order: Perch, Pike, Golden carp, Old Mossback.")]
        [SerializeField] private FishSpecies[] _fishSpecies = FishSpecies.Defaults();
        [SerializeField, Min(0.5f)] private float _minCastDistance = 4f;
        [SerializeField, Min(1f)] private float _maxCastDistance = 14f;
        [Tooltip("Seconds of holding to charge a full cast.")]
        [SerializeField, Min(0.1f)] private float _castChargeSeconds = 1.1f;
        [Tooltip("Share of line strain a full length cast takes off (short casts get none).")]
        [SerializeField, Range(0f, 0.5f)] private float _longCastTensionModifier = 0.15f;
        [Tooltip("Extra seconds a strike still counts after the bobber comes back up.")]
        [SerializeField, Min(0f)] private float _hookGraceSeconds = 0.25f;
        [Tooltip("Bite delay is multiplied by this in a hotspot.")]
        [SerializeField, Range(0.2f, 1f)] private float _hotspotBiteMultiplier = 0.6f;
        [Tooltip("Uncommon and rarer fish are this much more likely in a hotspot.")]
        [SerializeField, Min(1f)] private float _hotspotRareMultiplier = 2.5f;
        [SerializeField, Min(1f)] private float _hotspotXpMultiplier = 1.5f;
        [Tooltip("Fish a hotspot gives before it goes quiet, and how long it takes to come back.")]
        [SerializeField, Min(1)] private int _hotspotCatches = 3;
        [SerializeField, Min(5f)] private float _hotspotRespawnSeconds = 150f;
        [Tooltip("Fishing experience needed for each level (the first is level 0).")]
        [SerializeField] private int[] _fishingLevelXp = { 0, 15, 40, 80, 140 };
        [Tooltip("Rune shards for each Rune axe level.")]
        [SerializeField] private int[] _runeShardCosts = { 3, 5, 7 };
        [Tooltip("Extra axe damage per Rune axe level, on top of the wood upgrades.")]
        [SerializeField] private float[] _runeDamageBonus = { 0f, 0.15f, 0.3f, 0.5f };

        [Header("Elemental charges")]
        [Tooltip("Axe hits an element charge lasts after killing an enemy of that element.")]
        [SerializeField, Min(0)] private int _chargesPerKill = 15;
        [SerializeField, Min(0)] private int _chargeCapPerElement = 15;
        [Tooltip("When on, only hits that use the buff (on enemies the element beats) spend a charge.")]
        [SerializeField] private bool _chargeOnlyOnEffectiveHits = false;
        [SerializeField, Min(0f)] private float _counterBonusPerElement = 0.5f;

        [Header("Treants")]
        [SerializeField, Min(0)] private int _treantHutDamage = 10;
        [SerializeField, Min(0.1f)] private float _treantAttackInterval = 2.2f;

        [Header("Hut")]
        [SerializeField, Min(1)] private int _hutMaxHealth = 500;

        [Header("Player count scaling")]
        [SerializeField, Min(1)] private int _maxScaledPlayers = 5;
        [SerializeField, Min(0f)] private float _enemyScalingCoefficient = 0.4f;
        [SerializeField, Min(0f)] private float _treeDensityScalingCoefficient = 0.4f;
        [SerializeField, FormerlySerializedAs("_seedlingScalingCoefficient"), Min(0f)] private float _saplingScalingCoefficient = 0.4f;

        public float DayDuration => _dayDuration;
        public float DuskDuration => _duskDuration;
        public float NightDuration => _nightDuration;
        public float DawnDuration => _dawnDuration;

        public float SaplingScatterRadius => _saplingScatterRadius;

        public float PlayerMoveSpeed => _playerMoveSpeed;
        public float PlayerSprintSpeed => _playerSprintSpeed;
        public float PlayerCrouchSpeed => _playerCrouchSpeed;
        public float CrouchHeight => _crouchHeight;
        public float PlayerTurnSpeed => _playerTurnSpeed;
        public float PlayerAcceleration => _playerAcceleration;
        public float PlayerDeceleration => _playerDeceleration;
        public float PlayerJumpHeight => _playerJumpHeight;
        public float PlayerGravity => _playerGravity;
        public float PlayerGroundedStickSpeed => _playerGroundedStickSpeed;
        public float LandingRecoveryHeight => _landingRecoveryHeight;
        public float LandingRecoverySeconds => _landingRecoverySeconds;
        public float LandingRecoverySpeedMultiplier => _landingRecoverySpeedMultiplier;
        public float SpeedCheckTolerance => _speedCheckTolerance;
        public int SpeedCheckWindowTicks => _speedCheckWindowTicks;
        public int SpeedCheckGraceTicks => _speedCheckGraceTicks;
        public float DebugTeleportDistance => _debugTeleportDistance;
        // The fastest a player can honestly go (sprinting or rolling, plus sliding down a slope too steep to stand on),
        // with the tolerance on top. Leaving the slide out snapped sprinting players back near every mountainside.
        // Short bursts on top of this (the combo finisher lunge) are granted by the server when it accepts them.
        public float MaxAllowedPlayerSpeed => (Mathf.Max(_playerMoveSpeed, Mathf.Max(_playerSprintSpeed, _rollSpeed)) + SteepSlideSpeed) * _speedCheckTolerance;
        public const float SteepSlideSpeed = 6f;
        // The fastest honest climb: a jump, or sprinting straight up the steepest walkable slope (45 degrees, so as fast
        // up as along). Falling is never limited.
        public float JumpLaunchSpeed => Mathf.Sqrt(2f * _playerJumpHeight * _playerGravity);
        public float MaxAllowedRiseSpeed => Mathf.Max(JumpLaunchSpeed, Mathf.Max(_playerSprintSpeed, _rollSpeed)) * _speedCheckTolerance;

        public float CameraDistance => _cameraDistance;
        public float CameraPivotHeight => _cameraPivotHeight;
        public float CameraCrouchPivotHeight => _cameraCrouchPivotHeight;
        public float CameraDefaultPitch => _cameraDefaultPitch;
        public float CameraMinPitch => _cameraMinPitch;
        public float CameraMaxPitch => _cameraMaxPitch;
        public float MouseLookSensitivity => _mouseLookSensitivity;
        public float StickLookSpeed => _stickLookSpeed;
        public float CameraMinDistance => _cameraMinDistance;
        public float CameraMaxDistance => _cameraMaxDistance;
        public float CameraZoomStep => _cameraZoomStep;
        public float CameraZoomSmoothing => _cameraZoomSmoothing;
        public float CameraFollowSpeed => _cameraFollowSpeed;

        public float ShakeOnHit => _shakeOnHit;
        public float ShakeOnFell => _shakeOnFell;
        public float FeelFalloffDistance => _feelFalloffDistance;
        public float ShakeRecoverySpeed => _shakeRecoverySpeed;
        public float HitStopSeconds => _hitStopSeconds;
        public float HitStopHeavySeconds => _hitStopHeavySeconds;
        public float HitKick => _hitKick;
        public float HitKickHeavy => _hitKickHeavy;
        public int ChipsPerHit => _chipsPerHit;
        public int LeavesOnFell => _leavesOnFell;
        public float SfxVolume => _sfxVolume;

        public float SwingCooldown => _swingCooldown;
        public float SwingRange => _swingRange;
        public float SwingRadius => _swingRadius;
        public float SwingLatencyTolerance => _swingLatencyTolerance;
        public float SwingAnimationDuration => _swingAnimationDuration;
        public float SwingArcDegrees => _swingArcDegrees;
        public float SwingStrikeFraction => _swingStrikeFraction;
        public float SwingCommitFraction => _swingCommitFraction;
        public float SwingMoveMultiplier => _swingMoveMultiplier;
        public float SwingTurnMultiplier => _swingTurnMultiplier;
        public float SwingStrikeStep => _swingStrikeStep;

        public int ChargesPerKill => _chargesPerKill;
        public bool ChargeOnlyOnEffectiveHits => _chargeOnlyOnEffectiveHits;
        public int ChargeCapPerElement => _chargeCapPerElement;
        public float CounterBonusPerElement => _counterBonusPerElement;

        public int TreantHutDamage => _treantHutDamage;
        public float TreantAttackInterval => _treantAttackInterval;

        public int HutMaxHealth => _hutMaxHealth;
        public float HubRadius => _hubRadius;
        public bool FiniteLives => _finiteLives;
        public int PlayerLives => _playerLives;
        public int StartTorches => _startTorches;
        public int StartSalves => _startSalves;
        public int AxeDurability => _axeDurability;
        public int RodDurability => _rodDurability;
        public int RodSnapWear => _rodSnapWear;
        public int AxeRepairWood => _axeRepairWood;
        public int RodRepairWood => _rodRepairWood;
        public int RodRepairFabric => _rodRepairFabric;
        public float FishStaminaPerPoint => _fishStaminaPerPoint;
        public float FedSeconds => _fedSeconds;
        public float BuildRange => _buildRange;
        public float BuildWorkbenchRadius => _buildWorkbenchRadius;
        public int MaxPieces => _maxPieces;

        public float AxeBaseDamage => _axeBaseDamage;
        public float EnchantDamageBonus => _enchantDamageBonus;
        public float TreantWindupSeconds => _treantWindupSeconds;
        public float TreantStaggerSeconds => _treantStaggerSeconds;
        public float TreantKnockback => _treantKnockback;
        public float BlockReduction => _blockReduction;
        public float ParryWindow => _parryWindow;
        public float BlockStaminaPerHit => _blockStaminaPerHit;
        public float BlockStaminaPerSecond => _blockStaminaPerSecond;
        public float BlockMoveMultiplier => _blockMoveMultiplier;
        public float RollStamina => _rollStamina;
        public float RollSeconds => _rollSeconds;
        public float RollSpeed => _rollSpeed;
        public float RollInvulnSeconds => _rollInvulnSeconds;
        public float RollCooldown => _rollCooldown;
        public bool HutStartsRuined => _hutStartsRuined;
        public int HutRepairWood => _hutRepairWood;
        public float FistDamageMultiplier => _fistDamageMultiplier;
        public float NightElementalShareMin => _nightElementalShareMin;
        public float NightElementalShareMax => _nightElementalShareMax;
        public int BossFirstDay => _bossFirstDay;
        public float BossTreesFelledGrowth => _bossTreesFelledGrowth;
        public float BoonAxeDamage => _boonAxeDamage;
        public float BoonArmor => _boonArmor;
        public float BoonTorchSeconds => _boonTorchSeconds;
        public int BoonSalveHeal => _boonSalveHeal;
        public float BoonStamina => _boonStamina;

        public float StaminaMax => _staminaMax;
        public float StaminaRegenPerSecond => _staminaRegenPerSecond;
        public float StaminaRegenDelay => _staminaRegenDelay;
        public float StaminaExhaustedRecoverAt => _staminaExhaustedRecoverAt;
        public float SprintStaminaPerSecond => _sprintStaminaPerSecond;
        public float SwingStamina => _swingStamina;
        public float JumpStamina => _jumpStamina;

        public int TorchWoodCost => _torchWoodCost;
        public int BoatWoodCost => _boatWoodCost;
        public int BoatFiberCost => _boatFiberCost;
        public float SharkCentreBand => _sharkCentreBand;
        public float SharkLoseOffset => _sharkLoseOffset;
        public float SharkStaminaDrain => _sharkStaminaDrain;
        public float SharkStaminaRecover => _sharkStaminaRecover;
        public float SharkDriftSpeed => _sharkDriftSpeed;
        public float SharkSteerSpeed => _sharkSteerSpeed;
        public float SharkTowSpeed => _sharkTowSpeed;
        public SharkRodUnlock SharkRodUnlock => _sharkRodUnlock;
        public int SharkRodFishToUnlock => _sharkRodFishToUnlock;
        public float SharkHookDistance => _sharkHookDistance;
        public int SharkRodWoodCost => _sharkRodWoodCost;
        public int SharkRodFiberCost => _sharkRodFiberCost;
        public int SharkRodIronCost => _sharkRodIronCost;
        public int HerbCount => _herbCount;
        public int HerbCarryCap => _herbCarryCap;
        public int FabricBushCount => _fabricBushCount;
        public int FabricCarryCap => _fabricCarryCap;
        public int BandageHerbCost => _bandageHerbCost;
        public int BandageFabricCost => _bandageFabricCost;
        public int CraftStackCap => _craftStackCap;
        public float TorchBurnSeconds => _torchBurnSeconds;
        public int SalveHeal => _salveHeal;
        public int WoodCarryCap => _woodCarryCap;
        public int HutHealthPerWood => _hutHealthPerWood;
        public int HutStartHealth => Mathf.Max(1, Mathf.RoundToInt(_hutMaxHealth * _hutStartHealthFraction));
        public float HutInteractRadius => _hutInteractRadius;

        public int GetWoodForSize(TreeSize size)
        {
            switch (size)
            {
                case TreeSize.Small: return _woodSmall;
                case TreeSize.Medium: return _woodMedium;
                default: return _woodLarge;
            }
        }

        public float GetTrunkRadius(TreeSize size)
        {
            switch (size)
            {
                case TreeSize.Small: return _trunkRadiusSmall;
                case TreeSize.Medium: return _trunkRadiusMedium;
                default: return _trunkRadiusLarge;
            }
        }

        public int MaxScaledPlayers => _maxScaledPlayers;
        public float EnemyScalingCoefficient => _enemyScalingCoefficient;
        public float TreeDensityScalingCoefficient => _treeDensityScalingCoefficient;
        public float SaplingScalingCoefficient => _saplingScalingCoefficient;

        public void GetSaplingRange(TreeSize size, out int min, out int max)
        {
            switch (size)
            {
                case TreeSize.Small: min = _saplingsMinSmall; max = _saplingsMaxSmall; break;
                case TreeSize.Medium: min = _saplingsMinMedium; max = _saplingsMaxMedium; break;
                default: min = _saplingsMinLarge; max = _saplingsMaxLarge; break;
            }
        }

        public int GetSaplingMaxPerHit(TreeSize size)
        {
            switch (size)
            {
                case TreeSize.Small: return _saplingMaxPerHitSmall;
                case TreeSize.Medium: return _saplingMaxPerHitMedium;
                default: return _saplingMaxPerHitLarge;
            }
        }

        public int MaxSaplings => _maxSaplings;
        public int EnemyCapBase => _enemyCapBase;
        public float NightSpawnStagger => _nightSpawnStagger;

        public float TreantAggroRange => _treantAggroRange;
        public float TreantChargeDistance => _treantChargeDistance;
        public float TreantChargeMultiplier => _treantChargeMultiplier;
        public int TreantPlayerDamage => _treantPlayerDamage;
        public float TreantHitRadiusBase => _treantHitRadiusBase;

        public float GetTreantSpeed(TreeSize size) => Mathf.Max(0.5f, _treantMoveSpeed - _treantSizeSlowdown * (int)size);

        public int PlayerMaxHealth => _playerMaxHealth;
        public float PlayerRespawnSeconds => _playerRespawnSeconds;

        public float BossGrowDays => _bossGrowDays;
        public int BossChopHits => _bossChopHits;
        public float BossHealth => _bossHealth;
        public float BossSlamInterval => _bossSlamInterval;
        public float BossSlamRadius => _bossSlamRadius;
        public int BossSlamPlayerDamage => _bossSlamPlayerDamage;
        public int BossSlamHutDamage => _bossSlamHutDamage;
        public float BossScale => _bossScale;
        public int BossRewardWood => _bossRewardWood;
        public float GroveRadius => _groveRadius;
        public int GroveTreesFelled => _groveTreesFelled;
        public float WardenHealth => _wardenHealth;
        public float WardenHealthPerExtraPlayer => _wardenHealthPerExtraPlayer;
        public float WardenLeashRadius => _wardenLeashRadius;
        public float WardenRootInterval => _wardenRootInterval;
        public float WardenRootWarning => _wardenRootWarning;
        public float WardenRootRadius => _wardenRootRadius;
        public int WardenRootDamage => _wardenRootDamage;
        public int WardenRewardWood => _wardenRewardWood;

        public int[] AxeUpgradeCosts => _axeUpgradeCosts;
        public float[] AxeCooldownMultipliers => _axeCooldownMultipliers;
        public float[] AxeDamageMultipliers => _axeDamageMultipliers;
        public int[] ArmorUpgradeCosts => _armorUpgradeCosts;
        public float[] ArmorDamageReduction => _armorDamageReduction;
        public int[] HutUpgradeCosts => _hutUpgradeCosts;
        public int HutHealthPerLevel => _hutHealthPerLevel;
        public int[] SturdyIronCosts => _sturdyIronCosts;
        public int[] RuneShardCosts => _runeShardCosts;
        public int[] WardEssenceCosts => _wardEssenceCosts;
        public int StoneEssenceCost => _stoneEssenceCost;
        public FishSpecies[] Fish => _fishSpecies != null && _fishSpecies.Length >= FishingRules.Kinds ? _fishSpecies : (_fishSpecies = FishSpecies.Defaults());
        public float MinCastDistance => _minCastDistance;
        public float MaxCastDistance => Mathf.Max(_minCastDistance + 1f, _maxCastDistance);
        public float CastChargeSeconds => _castChargeSeconds;
        public float LongCastTensionModifier => _longCastTensionModifier;
        public float HookGraceSeconds => _hookGraceSeconds;
        public float HotspotBiteMultiplier => _hotspotBiteMultiplier;
        public float HotspotRareMultiplier => _hotspotRareMultiplier;
        public float HotspotXpMultiplier => _hotspotXpMultiplier;
        public int HotspotCatches => _hotspotCatches;
        public float HotspotRespawnSeconds => _hotspotRespawnSeconds;
        public int[] FishingLevelXp => _fishingLevelXp;
        public int StoneShardCost => _stoneShardCost;
        public float[] WardReduction => _wardReduction;
        public float[] RuneDamageBonus => _runeDamageBonus;

        public int GetHitsToFell(TreeSize size)
        {
            switch (size)
            {
                case TreeSize.Small: return _hitsToFellSmall;
                case TreeSize.Medium: return _hitsToFellMedium;
                default: return _hitsToFellLarge;
            }
        }

        public int GetTreantHealth(TreeSize size)
        {
            switch (size)
            {
                case TreeSize.Small: return _treantHealthSmall;
                case TreeSize.Medium: return _treantHealthMedium;
                default: return _treantHealthLarge;
            }
        }

        public int GetBaseSaplings(TreeSize size)
        {
            switch (size)
            {
                case TreeSize.Small: return _saplingsSmall;
                case TreeSize.Medium: return _saplingsMedium;
                default: return _saplingsLarge;
            }
        }
    }
}
