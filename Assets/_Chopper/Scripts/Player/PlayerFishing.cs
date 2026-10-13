using Chopper.Audio;
using Chopper.Core;
using Chopper.UI;
using Chopper.World;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Chopper.Player
{
    // Fishing from any shore: the hub pond, marsh pools, the deep lakes in the other forests, and the sea. With a rod,
    // interact at the bank (or press the rod key) to take it out; the player
    // stands still while it is out. Hold attack to charge a cast and let go to throw it where the camera looks (a ring
    // on the water shows where it will land). The lure lands near a few fish you can see (LureRules, the Ocarina of
    // Time side): tap attack to twitch it, hold to reel it in slowly. Fish that notice it follow it, and the keenest
    // takes it; twitching too fast spooks them, and a lure left still is taken only late. When the float goes under,
    // strike with attack. Then the New World side: hold attack to reel and let go to ease off; reeling tightens the
    // line, most of all while the fish thrashes, and a heavy fish pulls harder. Land it before the line snaps or the
    // fish takes all the line. Raising the guard, rolling, jumping, interacting again or opening a menu ends it.
    // The owner plays the minigame; the server rolls the school, moves the lure and the fish, decides the bite, checks
    // every step, and keeps the catch, the weight records, the experience, the rod and lure levels (one reward per
    // cast, never more).
    public class PlayerFishing : NetworkBehaviour
    {
        public enum Stage { Off, Ready, Charging, Waiting, Bite, Fighting }
        public enum Outcome { None, Caught, Snapped, Escaped, Missed, TooSoon, Cancelled }

        private const float ResultSeconds = 2.4f;
        private const float RodButtLength = 1.05f;
        private const float RodTipLength = 0.75f;
        private const float RodUpDegrees = 38f;
        private const float FlightSeconds = 0.55f;
        private const int LinePoints = 9;
        private const float ReelTickSeconds = 0.16f;
        private const float WarningBeepSeconds = 0.45f;
        // A lure left still is taken this many times later than the fish's usual bite time.
        private const float LazyBiteFactor = 2.5f;
        private const float HoldToReelSeconds = 0.3f;
        private const float SchoolSendSeconds = 0.16f;
        private const float MinTwitchGap = 0.08f;

        [SerializeField] private GameConfig _config;

        private readonly SyncVar<int> _rod = new SyncVar<int>();
        // The rod wears with every catch and more with a snapped line (ToolRules); at zero it cannot cast until it is
        // repaired at a workbench.
        private readonly SyncVar<int> _rodDurability = new SyncVar<int>();
        // A meal of fish: extra stamina for a while (FoodRules), shown by the stamina bar.
        private readonly SyncVar<int> _fedBonus = new SyncVar<int>();
        private readonly SyncVar<int> _fedSecondsLeft = new SyncVar<int>();
        private float _serverFedSeconds;
        private int _serverFedBonus;
        private float _nextFedSync;
        private readonly SyncVar<int> _lure = new SyncVar<int>();
        private readonly SyncList<int> _fish = new SyncList<int>();
        private readonly SyncVar<int> _xp = new SyncVar<int>();
        // The heaviest catch of each kind, in kilograms (0 for none yet).
        private readonly SyncList<float> _records = new SyncList<float>();
        // Seen by everyone: the rod is out, where the bobber floats, and the fight on the line (-1 none, else FishPhase).
        private readonly SyncVar<bool> _out = new SyncVar<bool>();
        private readonly SyncVar<Vector3> _bobber = new SyncVar<Vector3>();
        private readonly SyncVar<int> _phase = new SyncVar<int>(-1);

        public static PlayerFishing Local { get; private set; }

        public int Rod => _rod.Value;
        public int RodDurability => _rodDurability.Value;
        public int RodMaxDurability => _config.RodDurability;
        public bool RodBroken => _rod.Value > 0 && ToolRules.Broken(_rodDurability.Value);
        // A rod that is whole: the only kind that casts.
        public bool CanFish => _rod.Value > 0 && !ToolRules.Broken(_rodDurability.Value);
        public int FedBonus => _fedBonus.Value;
        public int FedSecondsLeft => _fedSecondsLeft.Value;
        public int RodWear => ToolRules.ToWear(_rodDurability.Value, _config.RodDurability);
        public int Lure => _lure.Value;
        public int FishPacked => FishingRules.PackLegacy(CopyCounts());
        public int Xp => _xp.Value;
        public int Level => FishingRules.Level(_xp.Value, _config.FishingLevelXp);
        public int FishCount(FishKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < _fish.Count ? _fish[i] : 0;
        }
        public int FishTotal
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _fish.Count; i++) if (_fish[i] > 0) total += _fish[i];
                return total;
            }
        }
        public int[] CopyCounts()
        {
            var counts = new int[FishingRules.Kinds];
            for (int i = 0; i < counts.Length && i < _fish.Count; i++) counts[i] = _fish[i];
            return counts;
        }
        public float Record(FishKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < _records.Count ? _records[i] : 0f;
        }
        public float[] CopyRecords()
        {
            var records = new float[FishingRules.Kinds];
            for (int i = 0; i < records.Length && i < _records.Count; i++) records[i] = _records[i];
            return records;
        }
        public FishSpecies SpeciesOf(FishKind kind) => FishingRules.Species(_config.Fish, kind);
        public FishSpecies[] SpeciesTable => _config.Fish;

        // Owner: where the minigame is.
        public Stage CurrentStage => _stage;
        public bool Busy => _stage != Stage.Off;
        public float Power => _power;
        public Fight CurrentFight => _fight;
        public FishPhase Phase => _fishPhase;
        public bool ThrashComing => _thrashComing;
        public bool InHotspot => _hotspot;
        // Owner: a fish is following the lure (the school view saw it).
        public bool FishFollowing => _schoolView != null && _schoolView.AnyFollowing;
        public bool Reeling => _reeling;
        public float CastPreviewDistance => FishingRules.CastDistance(_power, _config.MinCastDistance, _config.MaxCastDistance);
        public Outcome LastOutcome => Time.time < _resultUntil ? _outcome : Outcome.None;
        public string ResultText => Time.time < _resultUntil ? _resultText : string.Empty;
        // One word that pops up and fades (New World style): Hooked!, Caught!, Snapped, Got away, Missed. Owner only.
        public string CueWord { get; private set; } = string.Empty;
        public bool CueGood { get; private set; }
        public float CueAt { get; private set; } = float.NegativeInfinity;

        private void Cue(string word, bool good)
        {
            CueWord = word;
            CueGood = good;
            CueAt = Time.time;
        }

        // Development: tours and the console can force these.
        public static float DebugResistance = 1f;
        public static bool DebugShowValues;

        // Owner state.
        private Stage _stage;
        private float _power;
        private float _castDistance;
        private float _castAt;
        private int _castId = -1;
        private float _biteEnds;
        private FishKind _kind;
        private float _kg;
        private float _pressAt;
        private bool _reeling;
        private bool _hotspot;
        private Fight _fight;
        private FishPhase _fishPhase;
        private bool _thrashComing;
        private Outcome _outcome;
        private string _resultText = string.Empty;
        private float _resultUntil;
        private int _toggledFrame = -1;
        private float _nextReelTick;
        private float _nextWarning;
        private float _nextSplash;

        // Server state.
        private readonly System.Random _rng = new System.Random();
        private int _serverCast;
        private float _serverCastAt;
        private float _serverBiteDelay;
        private float _serverWindow;
        private FishKind _serverKind;
        private int _serverSpot = -1;
        private float _serverCastDistance;
        private bool _serverHooked;
        private float _serverHookAt;
        private int _forcedKind = -1;
        // The school under the current cast, the lure on the water, and where the player stands (the lure comes home there).
        private SchoolFish[] _school = new SchoolFish[0];
        private Vector2 _serverLure;
        private Vector2 _serverShore;
        private FishWater _serverWater;
        private bool _serverHotspot;
        private bool _serverReeling;
        private float _serverLastAction;
        private float _serverLastTwitch;
        private float _serverLazyBiteAt;
        // When the fish took the lure, or -1 while none has.
        private float _serverBiteAt = -1f;
        private float _serverKg;
        private float _nextSchoolSend;
        private System.Func<Vector2, bool> _canSwim;
        // Every client: the school's fish drawn in the water.
        private FishSchoolView _schoolView;

        private PlayerHealth _health;
        private PlayerCombat _combat;
        private PlayerAxe _axe;
        private PlayerWood _wood;
        private PlayerInventory _inventory;

        // Visuals, on every client.
        private Transform _hand;
        private HeldItemFrame _handFrame;
        private const float RodGripBack = 0.12f;
        private Transform _butt;
        private Transform _tip;
        private Transform _bobberView;
        private Transform _bobberCap;
        private Transform _fishShadow;
        private Transform _aimRing;
        private LineRenderer _line;
        private readonly Vector3[] _linePoints = new Vector3[LinePoints];
        private Vector3 _lastBobberTarget;
        private Vector3 _shownLure;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color Water = new Color(0.7f, 0.82f, 0.95f, 0.85f);

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _combat = GetComponent<PlayerCombat>();
            _axe = GetComponent<PlayerAxe>();
            _wood = GetComponent<PlayerWood>();
            _inventory = GetComponent<PlayerInventory>();
            _canSwim = CanSwim;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            EnsureFish();
            _rod.Value = Mathf.Max(1, _rod.Value);
            _rodDurability.Value = _config.RodDurability;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _fish.OnChange += OnFishChanged;
            _rod.OnChange += OnGearChanged;
            _lure.OnChange += OnGearChanged;
            _bobber.OnChange += OnBobberChanged;
            if (IsOwner) Local = this;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _fish.OnChange -= OnFishChanged;
            _rod.OnChange -= OnGearChanged;
            _lure.OnChange -= OnGearChanged;
            _bobber.OnChange -= OnBobberChanged;
            if (Local == this) Local = null;
            DestroyGear();
        }

        // Once per change, with trustworthy arguments (see PlayerWood).
        private bool OwnChange(bool asServer) => IsOwner && asServer == IsServerInitialized;

        private void OnFishChanged(SyncListOperation op, int index, int previous, int next, bool asServer)
        {
            if (!OwnChange(asServer) || op != SyncListOperation.Set || next != previous + 1) return;
            if (index < 0 || index >= FishingRules.Kinds) return;
            var kind = (FishKind)index;
            PickupFeed.Add(PickupFeed.Kind.Fish, SpeciesOf(kind).Name, 1, next);
        }

        private void EnsureFish()
        {
            while (_fish.Count < FishingRules.Kinds) _fish.Add(0);
            while (_records.Count < FishingRules.Kinds) _records.Add(0f);
        }

        private void ChangeFish(FishKind kind, int delta) => ServerChangeFish(kind, delta);

        // Server only. Adds (or removes) fish of a kind; returns how many actually changed (the chest uses it).
        public int ServerChangeFish(FishKind kind, int delta)
        {
            EnsureFish();
            int i = (int)kind;
            if (i < 0 || i >= _fish.Count) return 0;
            int before = _fish[i];
            int after = Mathf.Clamp(before + delta, 0, FishingRules.MaxStack);
            if (after != before) _fish[i] = after;
            return after - before;
        }

        private void OnGearChanged(int previous, int next, bool asServer)
        {
            // Only a better rod or lure is news; the rod everyone starts with is not.
            if (!OwnChange(asServer) || next <= previous || (previous <= 0 && next == 1)) return;
            PickupFeed.Add(PickupFeed.Kind.Gear, FishingRules.RodName(_rod.Value) + ", " + FishingRules.LureName(_lure.Value), 1, 1);
        }

        // A bobber hitting the water splashes for everyone (the owner's own lands after its flight, see LateUpdate).
        private void OnBobberChanged(Vector3 previous, Vector3 next, bool asServer)
        {
            if (asServer != IsServerInitialized || IsOwner || next == Vector3.zero) return;
            CombatFx.Splash(next, 0.4f);
            Sfx.Play(ProceduralSfx.Splash, next, 0.45f, 1.3f, 0.08f);
        }

        // ---------- Where ----------

        // True on a bank where a cast can land, and not while swimming or underground.
        public static bool AtWater(Vector3 position)
        {
            Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
            if (gen == null || Chopper.World.Generation.CryptWorld.IsUnderground(position) || WaterSystem.IsSwimming(position)) return false;
            return gen.TryFishingSpot(new Vector2(position.x, position.z), out _, out _, out _);
        }

        // The hub pond's bank. Forest lakes, marsh pools and the sea are AtWater but not AtPond.
        public static bool AtPond(Vector3 position)
        {
            Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
            if (gen == null || Chopper.World.Generation.CryptWorld.IsUnderground(position) || WaterSystem.IsSwimming(position)) return false;
            return gen.TryFishingSpot(new Vector2(position.x, position.z), out FishWater water, out _, out _) && water == FishWater.Pond;
        }

        // Keeps a cast in the water the player is standing beside.
        private Vector3 ClampCast(Vector3 target)
        {
            Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
            if (gen == null)
            {
                target.y = Chopper.World.Generation.WorldGen.WaterHeight;
                return target;
            }
            return gen.ClampBobber(transform.position, target);
        }

        // Where a cast with the current charge would land.
        public Vector3 AimPoint()
        {
            Quaternion yaw = Quaternion.Euler(0f, PlayerCamera.Local != null ? PlayerCamera.Local.Yaw : transform.eulerAngles.y, 0f);
            return ClampCast(transform.position + yaw * Vector3.forward * CastPreviewDistance);
        }

        // ---------- Owner ----------

        // Interact or the rod key: puts the rod away while fishing, or takes it out on the bank. False if neither
        // applies. Interact and the rod key can share a button, so a second call in the same frame is ignored.
        public bool TryToggle()
        {
            if (!IsOwner) return false;
            if (_toggledFrame == Time.frameCount) return true;
            if (Busy)
            {
                _toggledFrame = Time.frameCount;
                Stop(Outcome.Cancelled);
                return true;
            }
            if (!CanFish || !AtWater(transform.position) || (_health != null && _health.IsDown)) return false;
            _toggledFrame = Time.frameCount;
            _stage = Stage.Ready;
            _castId = -1;
            StartServerRpc();
            return true;
        }

        private void Stop(Outcome outcome)
        {
            if (_stage == Stage.Off) return;
            if (_stage >= Stage.Waiting && outcome == Outcome.Cancelled) ShowResult(Outcome.Cancelled, "Reeled in");
            _stage = Stage.Off;
            _castId = -1;
            _reeling = false;
            StopServerRpc();
        }

        private void ShowResult(Outcome outcome, string text)
        {
            _outcome = outcome;
            _resultText = text;
            switch (outcome)
            {
                case Outcome.Caught: Cue("Caught!", true); break;
                case Outcome.Snapped: Cue("Snapped", false); break;
                case Outcome.Escaped: Cue("Got away", false); break;
                case Outcome.Missed: Cue("Missed", false); break;
            }
            _resultUntil = Time.time + ResultSeconds;
        }

        // The cast is over (caught, lost or missed); the rod stays out for the next one.
        private void EndCast(Outcome outcome, string text)
        {
            ShowResult(outcome, text);
            _stage = Stage.Ready;
            _castId = -1;
            _reeling = false;
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (!UiState.GameplayBlocked)
            {
                if (Keybinds.WasPressed(KeyAction.Food)) EatServerRpc();
                if (Keybinds.WasPressed(KeyAction.Rod) || (Busy && Keybinds.WasPressed(KeyAction.Unequip))) TryToggle();
            }
            if (_stage == Stage.Off) return;

            // Anything else the player does puts the rod away.
            bool other = (_combat != null && (_combat.GuardRaised || _combat.IsRolling)) || Keybinds.WasPressed(KeyAction.Jump);
            if (other || (_health != null && _health.IsDown) || !AtWater(transform.position) || !CanFish)
            {
                Stop(Outcome.Cancelled);
                return;
            }
            // A menu opened mid cast lets the fish go but keeps the rod out.
            if (UiState.GameplayBlocked)
            {
                if (_stage >= Stage.Waiting)
                {
                    LostServerRpc(_castId);
                    EndCast(Outcome.Cancelled, "Reeled in");
                }
                else if (_stage == Stage.Charging) _stage = Stage.Ready;
                return;
            }

            bool pressed = Keybinds.WasPressed(KeyAction.Attack) && Time.frameCount != _toggledFrame;
            bool held = Keybinds.IsHeld(KeyAction.Attack);
            switch (_stage)
            {
                case Stage.Ready:
                    if (pressed)
                    {
                        _stage = Stage.Charging;
                        _power = 0f;
                    }
                    break;
                case Stage.Charging:
                    _power = Mathf.Clamp01(_power + Time.deltaTime / _config.CastChargeSeconds);
                    if (!held) Cast();
                    break;
                case Stage.Waiting:
                    UpdateWaiting(pressed, held);
                    break;
                case Stage.Bite:
                    if (pressed) Hook();
                    else if (Time.time > _biteEnds)
                    {
                        LostServerRpc(_castId);
                        EndCast(Outcome.Missed, "Missed the bite");
                        Sfx.Play(ProceduralSfx.Splash, BobberPoint(), 0.3f, 0.8f);
                    }
                    break;
                case Stage.Fighting:
                    UpdateFight(held);
                    break;
            }
        }

        private void Cast()
        {
            _castDistance = CastPreviewDistance;
            Vector3 target = AimPoint();
            Vector3 flat = target - transform.position;
            flat.y = 0f;
            _castDistance = Mathf.Max(_config.MinCastDistance, flat.magnitude);
            _castAt = Time.time;
            _castId = -1;
            _stage = Stage.Waiting;
            _hotspot = false;
            _reeling = false;
            Sfx.Play(ProceduralSfx.Swing, transform.position + Vector3.up, 0.7f, 1.25f);
            CastServerRpc(target, _power);
        }

        // Working the lure: a tap twitches it, holding reels it in slowly. The server moves it and the fish, and says
        // when one bites (BiteTargetRpc).
        private void UpdateWaiting(bool pressed, bool held)
        {
            if (_castId < 0 || Time.time < _castAt + FlightSeconds) return;
            if (pressed)
            {
                _pressAt = Time.time;
                TwitchServerRpc(_castId);
                Sfx.Play(ProceduralSfx.UiClick, BobberPoint(), 0.25f, 1.5f, 0.1f);
            }
            bool reel = held && Time.time - _pressAt >= HoldToReelSeconds;
            if (reel != _reeling)
            {
                _reeling = reel;
                ReelServerRpc(_castId, reel);
            }
            if (reel && Time.time >= _nextReelTick)
            {
                _nextReelTick = Time.time + ReelTickSeconds * 1.6f;
                Sfx.Play(ProceduralSfx.UiClick, transform.position + Vector3.up, 0.12f, 0.8f, 0.05f);
            }
        }

        private void Hook()
        {
            HookServerRpc(_castId);
            _fight = FishingRules.Begin(_kind, SpeciesOf(_kind), _castDistance, _kg);
            _fishPhase = FishPhase.Calm;
            _stage = Stage.Fighting;
            Cue("Hooked!", true);
            Sfx.Play(ProceduralSfx.Block, transform.position + Vector3.up, 0.7f, 1.4f);
            Rumble(0.6f, 0.12f);
        }

        private void UpdateFight(bool reeling)
        {
            FishSpecies species = SpeciesOf(_fight.Kind);
            float castFactor = FishingRules.CastFactor(_castDistance, _config.MinCastDistance, _config.MaxCastDistance);
            FishPhase before = _fishPhase;
            float dt = Time.deltaTime * (reeling ? DebugResistance : 1f);
            FightResult result = FishingRules.Step(ref _fight, species, reeling, Mathf.Max(1, _rod.Value), castFactor, _config.LongCastTensionModifier, dt);
            _fishPhase = FishingRules.PhaseAt(species, _fight.Elapsed, out _thrashComing);
            if (_fishPhase != before)
            {
                PhaseServerRpc((int)_fishPhase);
                if (_fishPhase == FishPhase.Thrashing) Rumble(0.5f, 0.2f);
            }

            if (reeling && Time.time >= _nextReelTick)
            {
                _nextReelTick = Time.time + ReelTickSeconds;
                Sfx.Play(ProceduralSfx.UiClick, transform.position + Vector3.up, 0.18f, 0.7f + 0.6f * _fight.Tension, 0.05f);
            }
            if (FishingRules.Zone(_fight.Tension) == TensionZone.Critical && Time.time >= _nextWarning)
            {
                _nextWarning = Time.time + WarningBeepSeconds;
                Sfx.Play(ProceduralSfx.UiHover, transform.position + Vector3.up, 0.6f, 1.9f);
            }

            switch (result)
            {
                case FightResult.Landed:
                    CatchServerRpc(_castId);
                    bool record = LureRules.IsRecord(_fight.Kg, Record(_fight.Kind));
                    EndCast(Outcome.Caught, $"{species.Name}  {_fight.Kg:F2} kg  ({FishingRules.RarityName(species.Rarity)}){(record ? "  New record!" : string.Empty)}");
                    CombatFx.Splash(transform.position + transform.forward * 1.2f + Vector3.up * 0.3f, 0.6f);
                    Sfx.Play(ProceduralSfx.Upgrade, transform.position + Vector3.up, 0.9f);
                    break;
                case FightResult.Snapped:
                    SnappedServerRpc(_castId);
                    EndCast(Outcome.Snapped, "The line snapped");
                    Sfx.Play(ProceduralSfx.Parry, transform.position + Vector3.up, 0.7f, 1.7f);
                    Rumble(0.8f, 0.2f);
                    break;
                case FightResult.Escaped:
                    LostServerRpc(_castId);
                    EndCast(Outcome.Escaped, "It got away");
                    Sfx.Play(ProceduralSfx.Splash, BobberPoint(), 0.5f, 0.7f);
                    break;
            }
        }

        private static void Rumble(float strength, float seconds)
        {
            Gamepad pad = Gamepad.current;
            if (pad == null || !InputHints.UsingGamepad) return;
            pad.SetMotorSpeeds(strength * 0.5f, strength);
            RumbleStopAt = Time.unscaledTime + seconds;
        }

        private static float RumbleStopAt = -1f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Development: lets tours and the console play the minigame without input.
        public void DebugCast(float power)
        {
            if (!IsOwner || _stage != Stage.Ready) return;
            _power = power;
            Cast();
        }

        public void DebugBiteNow()
        {
            if (IsOwner && _stage == Stage.Waiting) DebugBiteServerRpc();
        }

        public void DebugTwitch()
        {
            if (IsOwner && _stage == Stage.Waiting && _castId >= 0) TwitchServerRpc(_castId);
        }

        public void DebugHook()
        {
            if (IsOwner && _stage == Stage.Bite) Hook();
        }

        public void DebugSnap()
        {
            if (IsOwner && _stage == Stage.Fighting) _fight.Tension = 1.1f;
        }

        public void DebugSetTension(float tension, float elapsed)
        {
            if (!IsOwner || _stage != Stage.Fighting) return;
            _fight.Tension = tension;
            _fight.Elapsed = elapsed;
        }
#endif

        // ---------- Server ----------

        // Development builds only (the RPC exists in every build so the two sides always agree): the fish bites now.
        [ServerRpc]
        private void DebugBiteServerRpc()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _serverLazyBiteAt = Time.time;
#endif
        }

        [ServerRpc]
        private void StartServerRpc()
        {
            if (!CanFish || !AtWater(transform.position)) return;
            _out.Value = true;
            _bobber.Value = Vector3.zero;
            _phase.Value = -1;
        }

        [ServerRpc]
        private void StopServerRpc() => ServerStop();

        // Server only. The rod is put away (also on death, restarts and tests).
        public void ServerStop()
        {
            _out.Value = false;
            _phase.Value = -1;
            _bobber.Value = Vector3.zero;
            _serverHooked = false;
            _serverCast++;
            ServerClearSchool();
        }

        private void ServerClearSchool()
        {
            if (_school.Length == 0) return;
            _school = new SchoolFish[0];
            _serverReeling = false;
            _serverBiteAt = -1f;
            if (IsServerInitialized) SchoolClearObserversRpc();
        }

        // Fish stay in the water they were found in.
        private bool CanSwim(Vector2 p)
        {
            Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
            if (gen == null) return true;
            if (_serverWater == FishWater.Sea) return p.magnitude > Chopper.World.Generation.WorldGen.SeaInnerRadius + 3f;
            return gen.InWater(p, 0.85f);
        }

        [ServerRpc]
        private void CastServerRpc(Vector3 target, float power) => ServerCast(target, power);

        // Server only. A cast lands: the fish, its bite time and the hotspot are decided here. Returns the cast id.
        public int ServerCast(Vector3 target, float power)
        {
            if (!_out.Value || !CanFish || !AtWater(transform.position)) return -1;
            _serverCast++;
            _serverCastAt = Time.time;
            Vector3 spot = ClampCast(target);
            Vector3 flat = spot - transform.position;
            flat.y = 0f;
            _serverCastDistance = Mathf.Max(_config.MinCastDistance, flat.magnitude);
            FishingHotspots hotspots = FishingHotspots.Instance;
            _serverSpot = hotspots != null ? hotspots.SpotAt(spot) : -1;
            bool hotspot = _serverSpot >= 0;
            FishWater water = FishWater.Pond;
            Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
            if (gen != null && gen.TryFishingSpot(new Vector2(transform.position.x, transform.position.z), out FishWater found, out _, out _))
                water = found;
            WeatherKind weather = WeatherSystem.Instance != null ? WeatherSystem.Instance.Kind : WeatherKind.Sunny;
            DayPhase phase = DayNightCycle.Instance != null ? DayNightCycle.Instance.Phase : DayPhase.Day;
            _serverWater = water;
            _serverHotspot = hotspot;

            // The school under the cast: each fish rolled like a bite, with its own weight. The first is the one a still
            // lure would bring (the cast's power favors rarer fish).
            int count = LureRules.SchoolSize(_rng.NextDouble(), hotspot);
            // A glowworm carried is bait: the cast takes one, and the rarer fish come to it (a mining find).
            PlayerMining mining = GetComponent<PlayerMining>();
            bool baited = _forcedKind < 0 && mining != null && mining.ServerTakeGlowworm();
            float rareMultiplier = (hotspot ? _config.HotspotRareMultiplier : 1f) * (baited ? MiningRules.GlowwormRareMultiplier : 1f);
            _school = new SchoolFish[count];
            var flatSpot = new Vector2(spot.x, spot.z);
            for (int i = 0; i < count; i++)
            {
                FishKind kind = _forcedKind >= 0
                    ? (FishKind)_forcedKind
                    : FishingRules.Roll(_config.Fish, _rng.NextDouble(), _lure.Value, Level, hotspot || baited, rareMultiplier, i == 0 ? power : 0.5f, water, weather, phase);
                FishSpecies s = SpeciesOf(kind);
                Vector2 at = flatSpot;
                for (int tries = 0; tries < 6; tries++)
                {
                    float a = (float)_rng.NextDouble() * Mathf.PI * 2f;
                    Vector2 p = flatSpot + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Lerp(1.2f, LureRules.SchoolRadius, (float)_rng.NextDouble());
                    if (!CanSwim(p)) continue;
                    at = p;
                    break;
                }
                _school[i] = new SchoolFish { Kind = kind, Kg = LureRules.Weigh(s, _rng.NextDouble()), Position = at, Home = at, Shyness = LureRules.Shyness(s.Rarity) };
            }
            _serverKind = _school[0].Kind;
            _serverKg = _school[0].Kg;
            FishSpecies species = SpeciesOf(_serverKind);
            // Fish bite sooner in the rain.
            float biteScale = WeatherRules.BiteMultiplier(weather);
            _serverBiteDelay = FishingRules.BiteDelay(species, _rng.NextDouble(), _lure.Value, hotspot, Level, _config.HotspotBiteMultiplier) * biteScale;
            _serverLazyBiteAt = Time.time + FlightSeconds + _serverBiteDelay * LazyBiteFactor;
            _serverBiteAt = -1f;
            _serverWindow = FishingRules.HookWindow(species, _lure.Value) + _config.HookGraceSeconds;
            _serverHooked = false;
            _serverReeling = false;
            _serverLure = flatSpot;
            _serverShore = new Vector2(transform.position.x, transform.position.z);
            // The splash of the cast itself does not count as working the lure.
            _serverLastAction = -100f;
            _serverLastTwitch = -10f;
            _bobber.Value = spot;
            _phase.Value = -1;
            var kinds = new int[count];
            var kgs = new float[count];
            for (int i = 0; i < count; i++)
            {
                kinds[i] = (int)_school[i].Kind;
                kgs[i] = _school[i].Kg;
            }
            Vector3[] snapshot = SchoolSnapshot();
            foreach (NetworkConnection connection in Chopper.Networking.NearbyClients.Within(ServerManager, transform.position))
            {
                SchoolTargetRpc(connection, _serverCast, hotspot, kinds, kgs, snapshot);
            }
            return _serverCast;
        }

        // Positions and states for the views: x, z and the state, one per fish.
        private Vector3[] SchoolSnapshot()
        {
            var snapshot = new Vector3[_school.Length];
            for (int i = 0; i < _school.Length; i++) snapshot[i] = new Vector3(_school[i].Position.x, _school[i].Position.y, (int)_school[i].State);
            return snapshot;
        }

        // The school is drawn only for players near the caster (always including the caster). Clearing it goes to
        // everyone, so nobody who walked away keeps a stale school.
        [TargetRpc]
        private void SchoolTargetRpc(NetworkConnection connection, int cast, bool hotspot, int[] kinds, float[] kgs, Vector3[] fish)
        {
            if (_schoolView == null) _schoolView = new FishSchoolView();
            _schoolView.Begin(kinds, kgs, fish);
            if (!IsOwner || _stage != Stage.Waiting) return;
            _castId = cast;
            _hotspot = hotspot;
        }

        [TargetRpc]
        private void SchoolMoveTargetRpc(NetworkConnection connection, Vector3[] fish)
        {
            if (_schoolView != null) _schoolView.Move(fish);
        }

        [ObserversRpc]
        private void SchoolClearObserversRpc()
        {
            if (_schoolView != null) _schoolView.Clear();
        }

        [ServerRpc]
        private void TwitchServerRpc(int cast) => ServerTwitch(cast);

        // Server only. The lure hops toward the shore; the fish that see it take an interest, or flee if it splashes
        // about too often.
        public bool ServerTwitch(int cast)
        {
            if (cast != _serverCast || _serverHooked || _serverBiteAt >= 0f || _school.Length == 0) return false;
            float since = Time.time - _serverLastTwitch;
            if (since < MinTwitchGap) return false;
            LureRules.OnTwitch(_school, _serverLure, since, LureRules.NoticeRadius(_lure.Value, _serverHotspot));
            _serverLure = LureRules.Twitch(_serverLure, _serverShore);
            _serverLastTwitch = Time.time;
            _serverLastAction = Time.time;
            ServerMoveLure();
            return true;
        }

        [ServerRpc]
        private void ReelServerRpc(int cast, bool on)
        {
            if (cast == _serverCast) _serverReeling = on;
        }

        private void ServerMoveLure()
        {
            Vector3 lure = new Vector3(_serverLure.x, Chopper.World.Generation.WorldGen.WaterHeight, _serverLure.y);
            if ((lure - _bobber.Value).sqrMagnitude > 0.0004f) _bobber.Value = lure;
        }

        // Server: the lure and the school while waiting for a bite.
        private void ServerSchoolTick()
        {
            if (!_out.Value || _school.Length == 0 || _serverHooked || _serverBiteAt >= 0f) return;
            if (Time.time < _serverCastAt + FlightSeconds) return;
            if (_serverReeling)
            {
                _serverLure = LureRules.Reel(_serverLure, _serverShore, Time.deltaTime);
                _serverLastAction = Time.time;
                ServerMoveLure();
            }
            if (LureRules.Home(_serverLure, _serverShore))
            {
                // Reeled all the way in without a bite.
                int cast = _serverCast;
                _serverCast++;
                _bobber.Value = Vector3.zero;
                ServerClearSchool();
                if (Owner.IsValid) CastOverTargetRpc(Owner, cast);
                return;
            }
            int biter = LureRules.Step(_school, _serverLure, Time.time - _serverLastAction, _lure.Value, _serverHotspot, Time.deltaTime, _canSwim, _rng);
            if (biter < 0 && Time.time >= _serverLazyBiteAt)
            {
                biter = LureRules.LazyBiter(_school);
                if (biter >= 0) _school[biter].State = SchoolFishState.Biting;
            }
            if (biter >= 0) ServerBite(biter);
            if (Time.time >= _nextSchoolSend || biter >= 0)
            {
                _nextSchoolSend = Time.time + SchoolSendSeconds;
                Vector3[] snapshot = SchoolSnapshot();
                foreach (NetworkConnection connection in Chopper.Networking.NearbyClients.Within(ServerManager, transform.position))
                {
                    SchoolMoveTargetRpc(connection, snapshot);
                }
            }
        }

        // Server only. A fish of the school takes the lure: the strike window opens now.
        private void ServerBite(int index)
        {
            SchoolFish fish = _school[index];
            _school[index].Position = _serverLure;
            _serverKind = fish.Kind;
            _serverKg = fish.Kg;
            _serverBiteAt = Time.time;
            _serverReeling = false;
            _serverWindow = FishingRules.HookWindow(SpeciesOf(fish.Kind), _lure.Value) + _config.HookGraceSeconds;
            if (Owner.IsValid) BiteTargetRpc(Owner, _serverCast, (int)fish.Kind, fish.Kg);
        }

        // Server only, tests: makes the waiting fish bite now. False when there is no cast waiting.
        public bool ServerBiteNow(int cast)
        {
            if (cast != _serverCast || _school.Length == 0 || _serverHooked || _serverBiteAt >= 0f) return false;
            int biter = LureRules.LazyBiter(_school);
            if (biter < 0) biter = 0;
            _school[biter].State = SchoolFishState.Biting;
            ServerBite(biter);
            return true;
        }

        // The owner hears what took the lure: the float goes under now.
        [TargetRpc]
        private void BiteTargetRpc(NetworkConnection connection, int cast, int kind, float kg)
        {
            if (_stage != Stage.Waiting) return;
            _castId = cast;
            _kind = (FishKind)kind;
            _kg = kg;
            _reeling = false;
            _stage = Stage.Bite;
            _biteEnds = Time.time + FishingRules.HookWindow(SpeciesOf(_kind), _lure.Value) + _config.HookGraceSeconds;
            Vector3 bobber = BobberPoint();
            CombatFx.Splash(bobber, 0.8f);
            Sfx.Play(ProceduralSfx.Splash, bobber, 0.9f, 0.85f);
            Sfx.Play(ProceduralSfx.Pickup, transform.position + Vector3.up, 0.8f, 1.6f);
            Rumble(0.35f, 0.15f);
        }

        // The lure came all the way home without a bite.
        [TargetRpc]
        private void CastOverTargetRpc(NetworkConnection connection, int cast)
        {
            if (_stage == Stage.Waiting && (cast == _castId || _castId < 0)) EndCast(Outcome.Cancelled, "Reeled in");
        }

        [ServerRpc]
        private void HookServerRpc(int cast) => ServerHook(cast, false);

        // Server only. The strike counts if it came while the bobber was down (with room for the trip there and back).
        public bool ServerHook(int cast, bool ignoreTiming)
        {
            if (cast != _serverCast || _serverHooked) return false;
            const float latencySlack = 1.2f;
            if (!ignoreTiming && (_serverBiteAt < 0f || Time.time > _serverBiteAt + _serverWindow + latencySlack)) return false;
            _serverHooked = true;
            ServerClearSchool();
            _serverHookAt = Time.time;
            _phase.Value = (int)FishPhase.Calm;
            return true;
        }

        [ServerRpc]
        private void PhaseServerRpc(int phase)
        {
            if (_serverHooked) _phase.Value = Mathf.Clamp(phase, 0, 3);
        }

        [ServerRpc]
        private void CatchServerRpc(int cast) => ServerCatch(cast, false);

        // Server only. The fish is landed: it goes in the bag, with its experience, if the fight lasted a believable
        // time. The cast is spent either way, so it can never pay twice.
        public bool ServerCatch(int cast, bool ignoreTiming)
        {
            if (cast != _serverCast || !_serverHooked) return false;
            FishSpecies species = SpeciesOf(_serverKind);
            if (!ignoreTiming && Time.time - _serverHookAt < FishingRules.MinFightSeconds(species, _serverCastDistance)) return false;
            _serverHooked = false;
            _serverCast++;
            _phase.Value = -1;
            _bobber.Value = Vector3.zero;
            ChangeFish(_serverKind, 1);
            ServerWearRod(1);
            int k = (int)_serverKind;
            if (k >= 0 && k < _records.Count && LureRules.IsRecord(_serverKg, _records[k])) _records[k] = _serverKg;
            bool hotspot = _serverSpot >= 0;
            _xp.Value += Mathf.RoundToInt(species.Xp * (hotspot ? _config.HotspotXpMultiplier : 1f));
            if (hotspot && FishingHotspots.Instance != null) FishingHotspots.Instance.ServerConsume(_serverSpot);
            // Now and then something else comes up with the fish: a geode, rarely an old pick head (mining finds).
            PlayerMining mining = GetComponent<PlayerMining>();
            if (mining != null) mining.ServerAfterCatch();
            return true;
        }

        // Server only, tests: the fish and hotspot waiting on the current cast.
        public FishKind ServerPendingKind => _serverKind;
        public float ServerPendingKg => _serverKg;
        public int ServerSchoolSize => _school.Length;
        public Vector3 ServerLure => new Vector3(_serverLure.x, Chopper.World.Generation.WorldGen.WaterHeight, _serverLure.y);
        public int ServerPendingSpot => _serverSpot;

        // Server only, development: the next casts hook this fish (-1 rolls again).
        public void ServerForceKind(int kind) => _forcedKind = kind >= 0 && kind < FishingRules.Kinds ? kind : -1;

        // The line snapped: the fish is lost and the rod takes more wear than a catch.
        [ServerRpc]
        private void SnappedServerRpc(int cast)
        {
            if (cast != _serverCast || !_serverHooked) return;
            ServerWearRod(_config.RodSnapWear);
            ServerLose();
        }

        // Server only. Wears the rod; at zero it breaks and cannot cast until it is repaired.
        public void ServerWearRod(int amount)
        {
            if (_rod.Value <= 0 || _rodDurability.Value <= 0) return;
            _rodDurability.Value = ToolRules.Wear(_rodDurability.Value, amount);
            if (ToolRules.Broken(_rodDurability.Value)) RodBrokeObserversRpc();
        }

        [ObserversRpc]
        private void RodBrokeObserversRpc()
        {
            Sfx.Play(ProceduralSfx.Parry, transform.position + Vector3.up, 0.7f, 1.4f);
        }

        // Server only. Mended at a workbench.
        public void ServerRepairRod()
        {
            _rod.Value = Mathf.Max(1, _rod.Value);
            _rodDurability.Value = _config.RodDurability;
        }

        // Server only. Loading a saved player: how worn the rod was (0 is new, as in older saves).
        public void ServerLoadRodWear(int wear) => _rodDurability.Value = ToolRules.FromWear(wear, _config.RodDurability);

        [ServerRpc]
        private void LostServerRpc(int cast)
        {
            if (cast != _serverCast) return;
            ServerLose();
        }

        private void ServerLose()
        {
            _serverHooked = false;
            _serverCast++;
            _phase.Value = -1;
            _bobber.Value = Vector3.zero;
            ServerClearSchool();
        }

        [ServerRpc]
        private void EatServerRpc() => ServerEat();

        // The inventory's Eat button: this kind of fish.
        public void RequestEat(FishKind kind)
        {
            if (IsOwner) EatKindServerRpc((int)kind);
        }

        [ServerRpc]
        private void EatKindServerRpc(int kind) => ServerEat((FishKind)Mathf.Clamp(kind, 0, FishingRules.Kinds - 1));

        // Server only. Eats one fish of this kind: a meal that raises the stamina maximum for a while (FoodRules).
        public bool ServerEat(FishKind kind)
        {
            if (_health == null || _health.IsDown || FishCount(kind) <= 0) return false;
            ChangeFish(kind, -1);
            ServerFeed(SpeciesOf(kind).Heal);
            return true;
        }

        // Server only. Eats the smallest fish carried, if any (the Eat fish key).
        public bool ServerEat()
        {
            if (_health == null || _health.IsDown) return false;
            if (!FishingRules.NextToEat(CopyCounts(), _config.Fish, out FishKind kind)) return false;
            ChangeFish(kind, -1);
            ServerFeed(SpeciesOf(kind).Heal);
            return true;
        }

        private void ServerFeed(int fishValue)
        {
            int bonus = FoodRules.StaminaBonus(fishValue, _config.FishStaminaPerPoint);
            FoodRules.Eat(ref _serverFedBonus, ref _serverFedSeconds, bonus, _config.FedSeconds);
            _fedBonus.Value = _serverFedBonus;
            _fedSecondsLeft.Value = Mathf.CeilToInt(_serverFedSeconds);
            AteObserversRpc();
        }

        [ObserversRpc]
        private void AteObserversRpc()
        {
            Sfx.Play(ProceduralSfx.Pickup, transform.position + Vector3.up * 1.4f, 0.6f, 0.8f);
        }

        private void ServerFedTick()
        {
            if (_serverFedSeconds <= 0f) return;
            FoodRules.Tick(ref _serverFedBonus, ref _serverFedSeconds, Time.deltaTime);
            if (_serverFedSeconds <= 0f || Time.time >= _nextFedSync)
            {
                _nextFedSync = Time.time + 1f;
                _fedSecondsLeft.Value = Mathf.CeilToInt(_serverFedSeconds);
                _fedBonus.Value = _serverFedBonus;
            }
        }

        // Server only. A crypt arena boss's gift: the next rod or lure, alternating so both improve. False when both
        // are already the best there is.
        public bool ServerGrantGear()
        {
            if (_rod.Value <= 0)
            {
                _rod.Value = 1;
                return true;
            }
            bool lureBehind = _lure.Value < _rod.Value && _lure.Value < FishingRules.MaxLureLevel;
            if (lureBehind || _rod.Value >= FishingRules.MaxRodLevel)
            {
                if (_lure.Value >= FishingRules.MaxLureLevel) return false;
                _lure.Value++;
                return true;
            }
            _rod.Value++;
            return true;
        }

        // Server only, development: set the rod and lure directly.
        public void ServerSetGear(int rod, int lure)
        {
            _rod.Value = Mathf.Clamp(rod, 0, FishingRules.MaxRodLevel);
            _lure.Value = Mathf.Clamp(lure, 0, FishingRules.MaxLureLevel);
        }

        // Server only. Loading a saved player. counts is the full bag; when it is missing, fish is the old packed int
        // (the first four kinds only).
        public void ServerLoad(int rod, int lure, int fish, int xp) => ServerLoad(rod, lure, fish, null, xp);

        public void ServerLoad(int rod, int lure, int fish, int[] counts, int xp)
        {
            // Everyone has a rod now; older saves had none until one was crafted.
            _rod.Value = Mathf.Clamp(rod, 1, FishingRules.MaxRodLevel);
            _lure.Value = Mathf.Clamp(lure, 0, FishingRules.MaxLureLevel);
            _xp.Value = Mathf.Max(0, xp);
            int[] bag = counts != null && counts.Length > 0 ? counts : FishingRules.UnpackLegacy(fish);
            EnsureFish();
            for (int i = 0; i < FishingRules.Kinds; i++)
            {
                int value = i < bag.Length ? Mathf.Clamp(bag[i], 0, FishingRules.MaxStack) : 0;
                if (_fish[i] != value) _fish[i] = value;
            }
        }

        // Server only. Loading a saved player's weight records (missing in older saves).
        public void ServerLoadRecords(float[] records)
        {
            EnsureFish();
            for (int i = 0; i < FishingRules.Kinds; i++)
            {
                float value = records != null && i < records.Length ? Mathf.Max(0f, records[i]) : 0f;
                if (_records[i] != value) _records[i] = value;
            }
        }

        // Server only. The game over restart. The weight records are the player's own and stay.
        public void ServerReset()
        {
            ServerStop();
            _rod.Value = 1;
            _rodDurability.Value = _config.RodDurability;
            _lure.Value = 0;
            _serverFedBonus = 0;
            _serverFedSeconds = 0f;
            _fedBonus.Value = 0;
            _fedSecondsLeft.Value = 0;
            EnsureFish();
            for (int i = 0; i < _fish.Count; i++) if (_fish[i] != 0) _fish[i] = 0;
            _xp.Value = 0;
        }

        // ---------- Visuals ----------

        // Where the bobber is right now (the owner's own may be flying or dragged in).
        public Vector3 BobberWorld => BobberPoint();

        private Vector3 BobberPoint() => _bobberView != null && _bobberView.gameObject.activeSelf ? _bobberView.position : _bobber.Value;

        private void LateUpdate()
        {
            if (IsServerInitialized)
            {
                ServerSchoolTick();
                ServerFedTick();
            }
            if (_schoolView != null) _schoolView.Update(Time.deltaTime, _bobber.Value);
            if (RumbleStopAt > 0f && Time.unscaledTime >= RumbleStopAt && Gamepad.current != null)
            {
                Gamepad.current.SetMotorSpeeds(0f, 0f);
                RumbleStopAt = -1f;
            }

            bool show = _out.Value || (IsOwner && Busy);
            if (_axe != null) _axe.SetStowed(show);
            if (!show)
            {
                SetGearActive(false);
                return;
            }
            if (_butt == null) BuildGear();
            if (_butt == null) return;
            SetGearActive(true);

            bool fighting = IsOwner ? _stage == Stage.Fighting : _phase.Value >= 0;
            float tension = IsOwner ? (fighting ? _fight.Tension : 0f) : (fighting ? 0.55f : 0f);
            FishPhase phase = IsOwner ? _fishPhase : (FishPhase)Mathf.Max(0, _phase.Value);
            bool thrashing = fighting && phase == FishPhase.Thrashing;
            bool bite = IsOwner && _stage == Stage.Bite;

            // The rod: a butt in the hand and a tip that bends down with the strain (and jerks while the fish thrashes).
            // In the hand along the grip's head axis (the AxeGrip on Weapon_R), so the rod follows the arm through the
            // cast; without the grip, a fixed angle in front of the body as before.
            Vector3 grip = _hand != null ? _hand.position : transform.position + Vector3.up * 1.1f + transform.forward * 0.3f;
            Vector3 along = _handFrame != null
                ? _hand.TransformDirection(_handFrame.Head).normalized
                : Quaternion.AngleAxis(-RodUpDegrees, transform.right) * transform.forward;
            // The butt sticks out a little behind the hand, like a real grip.
            grip -= along * RodGripBack;
            _butt.SetPositionAndRotation(grip + along * (RodButtLength * 0.5f), Quaternion.FromToRotation(Vector3.up, along));
            Vector3 joint = grip + along * RodButtLength;
            float bend = (bite ? 0.3f : 0f) + tension * 0.8f + (thrashing ? Mathf.Sin(Time.time * 26f) * 0.15f : 0f);
            Vector3 tipAlong = Quaternion.AngleAxis(Mathf.Clamp01(bend) * 55f, transform.right) * along;
            _tip.SetPositionAndRotation(joint + tipAlong * (RodTipLength * 0.5f), Quaternion.FromToRotation(Vector3.up, tipAlong));
            Vector3 tip = joint + tipAlong * RodTipLength;

            // The cast's landing ring on the water while charging.
            bool aiming = IsOwner && _stage == Stage.Charging;
            _aimRing.gameObject.SetActive(aiming);
            if (aiming) _aimRing.position = AimPoint() + Vector3.up * 0.03f;

            Vector3 rest = _bobber.Value;
            bool floating = rest != Vector3.zero && !(IsOwner && (_stage == Stage.Ready || _stage == Stage.Charging));
            _bobberView.gameObject.SetActive(floating);
            _fishShadow.gameObject.SetActive(floating && fighting);
            if (!floating)
            {
                DrawLine(tip, tip + Vector3.down * 0.7f, 0f, false);
                return;
            }

            Vector3 bobber = rest;
            float bob = Mathf.Sin(Time.time * 2.4f) * 0.025f;
            if (IsOwner && _stage == Stage.Waiting)
            {
                // In flight: an arc from the rod tip to the water, then a splash where it lands.
                float flight = (Time.time - _castAt) / FlightSeconds;
                if (flight < 1f)
                {
                    bobber = Vector3.Lerp(tip, rest, flight) + Vector3.up * Mathf.Sin(flight * Mathf.PI) * 2.2f;
                    bob = 0f;
                }
                else if (_lastBobberTarget != rest)
                {
                    _lastBobberTarget = rest;
                    CombatFx.Splash(rest, 0.45f);
                    Sfx.Play(ProceduralSfx.Splash, rest, 0.5f, 1.3f, 0.08f);
                }
            }
            // A fish nosing at the lure makes the float dip now and then.
            if (!fighting && _schoolView != null && _schoolView.NearLure(rest, 1.2f) && Mathf.Sin(Time.time * 5.3f) > 0.85f) bob = -0.05f;
            // The float glides after the lure as it is worked, rather than jumping with each update.
            if (!fighting && !(IsOwner && _stage == Stage.Waiting && Time.time - _castAt < FlightSeconds))
            {
                _shownLure = _shownLure == Vector3.zero || (_shownLure - rest).sqrMagnitude > 25f ? rest : Vector3.Lerp(_shownLure, rest, 1f - Mathf.Exp(-10f * Time.deltaTime));
                bobber = _shownLure;
            }
            // A bite pulls the float right under: the only sign there is, so it has to be unmistakable.
            if (bite) bob = -0.42f + Mathf.Sin(Time.time * 14f) * 0.04f;
            if (fighting)
            {
                // The fish drags the bobber about: steady while calm, hard to one side while pulling, wild when it thrashes.
                float sway = phase == FishPhase.Pulling ? 0.9f : phase == FishPhase.Thrashing ? 0.5f + Mathf.Sin(Time.time * 17f) * 0.4f : 0.25f;
                Vector3 side = Vector3.Cross(Vector3.up, (rest - transform.position).normalized);
                bobber = rest + side * Mathf.Sin(Time.time * (phase == FishPhase.Thrashing ? 9f : 1.6f)) * sway;
                if (IsOwner)
                {
                    Vector3 shore = new Vector3(transform.position.x, rest.y, transform.position.z);
                    Vector3 toBobber = bobber - shore;
                    float max = Mathf.Max(0.1f, toBobber.magnitude);
                    bobber = shore + toBobber / max * Mathf.Min(max, Mathf.Max(0.8f, _fight.Distance * max / Mathf.Max(0.1f, _fight.StartDistance)));
                }
                bob = phase == FishPhase.Thrashing ? -0.08f + Mathf.Sin(Time.time * 30f) * 0.05f : -0.04f;
                if (thrashing && Time.time >= _nextSplash)
                {
                    _nextSplash = Time.time + 0.3f;
                    CombatFx.Burst(bobber + Vector3.up * 0.05f, Water, 7, 2.2f, 0.08f, 0.5f, 2.5f);
                    Sfx.Play(ProceduralSfx.Splash, bobber, 0.35f, 1.1f, 0.15f);
                }
                Vector3 under = bobber - transform.position;
                under.y = 0f;
                Vector3 away = under.sqrMagnitude > 0.01f ? under.normalized : transform.forward;
                Vector3 heading = -away + Vector3.Cross(Vector3.up, away) * Mathf.Sin(Time.time * 4f) * 0.4f;
                // Just under the surface, seen through the water.
                Vector3 shadowAt = bobber + away * 0.6f;
                shadowAt.y = Chopper.World.Generation.WorldGen.WaterHeight - 0.12f;
                _fishShadow.SetPositionAndRotation(shadowAt, Quaternion.LookRotation(heading));
            }
            _bobberView.position = bobber + Vector3.up * (0.06f + bob);
            DrawLine(tip, _bobberView.position, fighting ? Mathf.Clamp01(0.4f + tension) : 0.15f, thrashing);
        }

        // The line from the rod tip to the bobber: it sags when slack, runs straight when taut, and shivers when the
        // fish thrashes.
        private void DrawLine(Vector3 from, Vector3 to, float taut, bool shiver)
        {
            float length = Vector3.Distance(from, to);
            float sag = (1f - taut) * Mathf.Min(1.2f, length * 0.12f);
            Vector3 across = Vector3.Cross(Vector3.up, (to - from).normalized);
            for (int i = 0; i < LinePoints; i++)
            {
                float t = i / (float)(LinePoints - 1);
                Vector3 p = Vector3.Lerp(from, to, t) + Vector3.down * (sag * 4f * t * (1f - t));
                if (shiver) p += across * (Mathf.Sin(Time.time * 60f + t * 12f) * 0.03f * Mathf.Sin(t * Mathf.PI));
                _linePoints[i] = p;
            }
            _line.SetPositions(_linePoints);
        }

        private void SetGearActive(bool on)
        {
            if (_butt == null) return;
            if (_butt.gameObject.activeSelf != on) _butt.gameObject.SetActive(on);
            if (_tip.gameObject.activeSelf != on) _tip.gameObject.SetActive(on);
            if (on) return;
            _bobberView.gameObject.SetActive(false);
            _fishShadow.gameObject.SetActive(false);
            _aimRing.gameObject.SetActive(false);
        }

        private void DestroyGear()
        {
            if (_butt != null) Destroy(_butt.gameObject);
            if (_tip != null) Destroy(_tip.gameObject);
            if (_bobberView != null) Destroy(_bobberView.gameObject);
            if (_fishShadow != null) Destroy(_fishShadow.gameObject);
            if (_aimRing != null) Destroy(_aimRing.gameObject);
            if (_schoolView != null) _schoolView.Destroy();
            _schoolView = null;
            _butt = null;
        }

        private static Transform Piece(PrimitiveType type, string name, Vector3 scale, Material material, Color tint)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, tint);
            renderer.SetPropertyBlock(block);
            return go.transform;
        }

        // Rod, line, bobber, the fish's shadow and the aim ring, from primitives and the character's gear material
        // (it is in every build and takes a tint).
        private void BuildGear()
        {
            CharacterCatalog catalog = Resources.Load<CharacterCatalog>("CharacterCatalog");
            Material material = catalog != null ? catalog.GearMaterial : null;
            if (material == null) return;
            // Chopper's rig holds things on AxeGrip (Weapon_R) with a HeldItemFrame; the old rig had a bone hand_r.
            // The lookup for hand_r alone found nothing on Chopper, so the rod floated in front of the body.
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "AxeGrip" && t.GetComponent<HeldItemFrame>() != null)
                {
                    _hand = t;
                    _handFrame = t.GetComponent<HeldItemFrame>();
                    break;
                }
                if (t.name == "hand_r" && _hand == null) _hand = t;
            }

            _butt = Piece(PrimitiveType.Cylinder, "RodButt", new Vector3(0.04f, RodButtLength * 0.5f, 0.04f), material, new Color(0.42f, 0.28f, 0.15f));
            _tip = Piece(PrimitiveType.Cylinder, "RodTip", new Vector3(0.022f, RodTipLength * 0.5f, 0.022f), material, new Color(0.55f, 0.4f, 0.22f));
            // The line hangs off its own object so it stays drawn whichever way the rod pieces turn.
            var lineHolder = new GameObject("FishingLine");
            lineHolder.transform.SetParent(_butt, false);
            _line = lineHolder.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = LinePoints;
            _line.startWidth = 0.02f;
            _line.endWidth = 0.014f;
            _line.sharedMaterial = material;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var lineBlock = new MaterialPropertyBlock();
            lineBlock.SetColor(BaseColorId, new Color(0.92f, 0.9f, 0.82f));
            _line.SetPropertyBlock(lineBlock);

            // A red float with a white cap, easy to read on the water.
            _bobberView = Piece(PrimitiveType.Sphere, "Bobber", Vector3.one * 0.24f, material, new Color(0.88f, 0.12f, 0.08f));
            _bobberCap = Piece(PrimitiveType.Sphere, "BobberCap", Vector3.one * 0.6f, material, new Color(0.96f, 0.96f, 0.92f));
            _bobberCap.SetParent(_bobberView, false);
            _bobberCap.localPosition = new Vector3(0f, 0.32f, 0f);
            _fishShadow = Piece(PrimitiveType.Sphere, "FishShadow", new Vector3(0.28f, 0.008f, 0.75f), material, new Color(0.05f, 0.09f, 0.13f));
            _aimRing = Piece(PrimitiveType.Cylinder, "CastAim", new Vector3(0.9f, 0.01f, 0.9f), material, new Color(1f, 0.85f, 0.45f));
            SetGearActive(false);
        }
    }
}
