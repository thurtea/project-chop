using System.Collections.Generic;
using Chopper.Audio;
using Chopper.Core;
using Chopper.Player;
using Chopper.Trees;
using Chopper.World;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.AI;

namespace Chopper.Enemies
{
    public enum TreantState
    {
        // Rooted on the ground, harmless. Any axe hit removes it (boss saplings need to grow first).
        Sapling = 0,
        // Awake and walking toward the hut. Only exists at night.
        Active = 1
    }

    // One networked creature that is a sapling by day and an enemy at night (plan D7). Size sets hits to kill
    // (D18) and the element sets its color and which buffs beat it. Boss saplings grow over game days and must be
    // chopped down, which queues a boss for the next night (plan D21).
    // The server owns state and health; clients only build a visual from the synced state. An awake forest enemy
    // stands up as a wood creature (GolemSetup: trunk limbs, a foliage crown, walk, punch, hit and death) instead of
    // a pine that slides along the ground. Crypt guardians and arena enemies look like a draugr. Saplings stay pines.
    public class Treant : NetworkBehaviour
    {
        private const float BobHeight = 0.12f;
        private const float BobSpeed = 9f;
        private const float MovingThreshold = 0.25f;
        private const float LungeDuration = 0.3f;
        private const float LungeDistance = 0.7f;
        private const float SaplingHeight = 0.9f;
        private const float BossSaplingStartHeight = 0.9f;
        private const float SaplingHitRadius = 0.5f;
        private const float NavMeshSnapDistance = 6f;
        private const float BossMoveSpeed = 2.4f;
        private const float BossTrunkFraction = 0.12f;
        private const float GrassPushRadiusBase = 0.8f;

        private static readonly List<Treant> _all = new List<Treant>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        // Awake enemies glow softly in their element's color so they can be seen and told apart at night; plain ones
        // give off a faint dull light so they do not vanish in the dark.
        private const float ElementGlow = 0.55f;
        private const float DraugrGlowBoost = 1.8f;
        private static readonly Color PlainGlow = new Color(0.16f, 0.13f, 0.1f);

        public static IReadOnlyList<Treant> All => _all;

        private static readonly List<Treant> _clientAll = new List<Treant>();

        // Every treant this client knows about (the map shows the awake ones).
        public static IReadOnlyList<Treant> ClientAll => _clientAll;

        [SerializeField] private GameConfig _config;
        [SerializeField] private ElementPalette _palette;
        [Tooltip("Model used for each element, indexed by the Element value (Neutral, Water, Fire, Earth, Wind).")]
        [SerializeField] private GameObject[] _modelsByElement;
        [Tooltip("The crypt enemy's look: an animated humanoid with a sword (built by DraugrSetup).")]
        [SerializeField] private GameObject _draugrPrefab;
        [Tooltip("The night forest creature (built by GolemSetup). Shown while a non-guardian enemy is awake.")]
        [SerializeField] private GameObject _golemPrefab;

        private readonly SyncVar<int> _state = new SyncVar<int>();
        private readonly SyncVar<int> _element = new SyncVar<int>();
        private readonly SyncVar<int> _size = new SyncVar<int>();
        private readonly SyncVar<bool> _boss = new SyncVar<bool>();
        private readonly SyncVar<float> _bornDays = new SyncVar<float>();
        // Crypt guardians stay in their crypt, ignore the hut and the night cycle, and are not counted in the night cap.
        private readonly SyncVar<bool> _guardian = new SyncVar<bool>();
        // The grove warden (zone boss): its phase 1 to 3, or 0 for every other enemy. Shielded while its guards stand.
        private readonly SyncVar<int> _wardenPhase = new SyncVar<int>();
        private readonly SyncVar<bool> _shielded = new SyncVar<bool>();
        private Vector3 _home;
        // Server: a leashed enemy (the warden and its guards) only fights within this many meters of _home.
        private float _leashRadius;
        private float _maxHealth = 1f;
        private float _wardenHealth;
        // Server: the crypt whose arena this enemy fights in, or -1.
        private int _arena = -1;

        private NavMeshAgent _agent;
        private float _health;
        private Transform _visualRoot;
        private Transform _model;
        private float _modelBaseHeight = 1f;
        private Vector3 _lastPosition;
        private float _lungeStart = float.NegativeInfinity;
        private float _bobPhase;
        private float _staggerUntil;
        private Vector3 _knockRemaining;
        private float _windupStart = float.NegativeInfinity;
        private float _hitStart = float.NegativeInfinity;
        private Vector3 _hitDirection = Vector3.back;
        private float _flashUntil;
        private const float RecoilSeconds = 0.26f;
        private const float RecoilDistance = 0.42f;
        private const float KnockSharpness = 16f;
        private const float DraugrModelHeight = CharacterSizes.Player;
        private const float DraugrCorpseSeconds = 4f;
        private static readonly Color DraugrSkin = new Color(0.95f, 1.05f, 1f);
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int AttackParam = Animator.StringToHash("Attack");
        private static readonly int HitParam = Animator.StringToHash("Hit");
        private static readonly int DeathParam = Animator.StringToHash("Death");
        // Client: the glowing material copies made for this enemy's look, destroyed with it.
        private readonly List<Material> _glowMaterials = new List<Material>();
        // The wood creature's bark: it takes only a share of the glow, so the bark grain shows. The full glow on the
        // whole body flattened it into one even color, a tan or orange silhouette (2026-10-05).
        private readonly HashSet<Material> _barkMaterials = new HashSet<Material>();
        private const float BarkGlowShare = 0.15f;
        // The leaf crown keeps more of the glow (it says the element), but not all of it, or the leaves turn into a
        // flat colored blob.
        private const float LeafGlowShare = 0.45f;
        private readonly HashSet<Material> _crownMaterials = new HashSet<Material>();
        // Client: the draugr's animator when this enemy shows as one, else null.
        private Animator _draugr;
        private float _draugrSpeed;

        public TreantState State => (TreantState)_state.Value;
        public Element Element => (Element)_element.Value;
        public TreeSize Size => (TreeSize)_size.Value;
        public bool IsBoss => _boss.Value;
        public bool IsActive => State == TreantState.Active;
        public float BornDays => _bornDays.Value;
        public bool IsGuardian => _guardian.Value;
        // Server: where a guardian goes back to when nobody is near.
        public Vector3 Home => _home;
        public int Arena => _arena;
        public NavMeshAgent Agent => _agent;
        public bool IsWarden => _wardenPhase.Value > 0;
        public int WardenPhase => _wardenPhase.Value;
        public bool IsShielded => _shielded.Value;
        public bool HasLeash => _leashRadius > 0f;
        public float LeashRadius => _leashRadius;
        // Server: 0 to 1 of the health it woke with.
        public float HealthFraction => Mathf.Clamp01(_health / Mathf.Max(0.0001f, _maxHealth));
        // Server: when it woke, so the warden can rise out of the ground before it fights.
        public float ActivatedAt { get; private set; }
        // Server: stunned by a hit or a parry, so it cannot walk or attack for a moment.
        public bool IsStaggered => Time.time < _staggerUntil;
        // Walking speed before the long distance charge bonus.
        public float BaseSpeed { get; private set; }

        // 0 to 1 for a boss sapling. Other treants report 1.
        public float Growth
        {
            get
            {
                if (!IsBoss || State != TreantState.Sapling) return 1f;
                DayNightCycle cycle = DayNightCycle.Instance;
                if (cycle == null) return 0f;
                return Mathf.Clamp01((cycle.WorldDays - _bornDays.Value) / _config.BossGrowDays);
            }
        }

        public bool IsGrown => Growth >= 1f;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        // ---------- Server ----------

        // Server only, before Spawn.
        public void ServerInit(Element element, TreeSize size, bool boss, float bornDays)
        {
            _element.Value = (int)element;
            _size.Value = (int)size;
            _boss.Value = boss;
            _bornDays.Value = bornDays;
            _state.Value = (int)TreantState.Sapling;
            _health = boss ? _config.BossChopHits : 1f;
            _wardenPhase.Value = 0;
            _shielded.Value = false;
            _leashRadius = 0f;
            _wardenHealth = 0f;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _all.Add(this);
            if (_agent != null) _agent.enabled = false;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _all.Remove(this);
        }

        // Server only, after Spawn and before ServerActivate. Makes this enemy a crypt guardian that keeps to its room.
        public void ServerMakeGuardian(Vector3 home, int arena = -1)
        {
            _guardian.Value = true;
            _home = home;
            _arena = arena;
        }

        // Server only, after Spawn and before ServerActivate. Keeps this enemy within radius of home: it fights only
        // players inside that circle and walks back when nobody is there.
        public void ServerLeash(Vector3 home, float radius)
        {
            _home = home;
            _leashRadius = Mathf.Max(1f, radius);
        }

        // Server only, after Spawn and before ServerActivate. Makes this boss the grove warden of the grove at home.
        public void ServerMakeWarden(Vector3 home, float leashRadius, float health)
        {
            ServerLeash(home, leashRadius);
            _wardenHealth = Mathf.Max(1f, health);
            _wardenPhase.Value = 1;
        }

        // Server only. Everyone left the warden's grove: it heals and starts over at phase 1.
        public void ServerResetWarden()
        {
            if (!IsWarden) return;
            _health = _maxHealth;
            _wardenPhase.Value = 1;
            _shielded.Value = false;
        }

        public void ServerSetShield(bool on)
        {
            if (_shielded.Value != on) _shielded.Value = on;
        }

        // Server only. The night roll decides whether a waking sapling is elemental or plain.
        public void ServerSetElement(Element element)
        {
            if (State == TreantState.Sapling && !IsBoss) _element.Value = (int)element;
        }

        // Server only. Wakes a sapling up as an enemy. Returns false if it could not be placed on the NavMesh.
        public bool ServerActivate()
        {
            if (State == TreantState.Active) return true;
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, NavMeshSnapDistance, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[Treant] No NavMesh near {transform.position:F1}; removing it.");
                ServerDespawn();
                return false;
            }

            transform.position = hit.position;
            _health = IsWarden ? _wardenHealth : IsBoss ? _config.BossHealth : _config.GetTreantHealth(Size);
            _maxHealth = _health;
            ActivatedAt = Time.time;
            _agent.enabled = true;
            BaseSpeed = IsBoss ? BossMoveSpeed : _config.GetTreantSpeed(Size);
            _agent.speed = BaseSpeed;
            _state.Value = (int)TreantState.Active;
            return true;
        }

        // Server only. Stuns the enemy and shoves it away from fromPosition. Big enemies and bosses resist.
        public void ServerStagger(float seconds, Vector3 fromPosition, bool strong = false)
        {
            if (!IsActive) return;
            _staggerUntil = Mathf.Max(_staggerUntil, Time.time + CombatRules.StaggerSeconds(seconds, (int)Size, IsBoss));
            if (IsBoss || _agent == null || !_agent.enabled) return;
            Vector3 away = transform.position - fromPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
            float distance = _config.TreantKnockback * (strong ? 1.6f : 1f) * (1f - 0.2f * (int)Size);
            _knockRemaining = away.normalized * distance;
        }

        // Server only. The attack wind up: the enemy rears back before the hit lands.
        public void ServerPlayWindup() => WindupObserversRpc();

        // Server only. units is the swing's damage in axe hit units (1 is one normal hit).
        public void ServerHit(float units, PlayerBuffs attackerBuffs, PlayerWood attackerWood, Vector3? from = null)
        {
            if (State == TreantState.Sapling)
            {
                // Ordinary saplings cannot be destroyed: they wake up as enemies at night. Young boss trees shrug
                // off the axe until fully grown; a grown one can be chopped down.
                if (!IsBoss || !IsGrown) return;
                _health -= 1f;
                if (_health > 0f) return;

                if (IsBoss && EnemyDirector.Instance != null) EnemyDirector.Instance.ServerOnBossTreeFelled(transform.position, Element);
                ServerDespawn();
                return;
            }

            _health -= ZoneBossRules.DamageTaken(units, IsWarden && IsShielded);
            if (_health > 0f)
            {
                if (IsWarden)
                {
                    int phase = ZoneBossRules.PhaseFor(HealthFraction);
                    if (phase > WardenPhase)
                    {
                        _wardenPhase.Value = phase;
                        PhaseObserversRpc(transform.position);
                        if (EnemyDirector.Instance != null) EnemyDirector.Instance.ServerOnWardenPhase(this, phase);
                    }
                }
                Vector3 source = from ?? (transform.position + transform.forward);
                ServerStagger(_config.TreantStaggerSeconds, source);
                Vector3 push = transform.position - source;
                push.y = 0f;
                HitObserversRpc(transform.position, (int)Size, push.sqrMagnitude > 0.0001f ? push.normalized : Vector3.back);
                return;
            }

            if (attackerBuffs != null) attackerBuffs.ServerAwardKill(Element);
            // Arena enemies report to their arena; only the world's own boss trees count toward the boss milestone.
            if (_arena >= 0 && ArenaManager.Instance != null) ArenaManager.Instance.ServerOnArenaEnemyDown(this);
            else if (IsWarden && EnemyDirector.Instance != null) EnemyDirector.Instance.ServerOnWardenKilled(this);
            else if (IsBoss && !IsGuardian && EnemyDirector.Instance != null) EnemyDirector.Instance.ServerOnBossKilled();
            Vector3 fallFrom = from ?? (transform.position + transform.forward);
            Vector3 fall = transform.position - fallFrom;
            fall.y = 0f;
            DeathObserversRpc(transform.position, (int)Size, fall.sqrMagnitude > 0.0001f ? fall.normalized : Vector3.back);
            ServerDespawn();
        }

        public void ServerDespawn()
        {
            if (!IsSpawned) return;
            ServerManager.Despawn(NetworkObject, FishNet.Object.DespawnType.Destroy);
        }

        // Server only. Plays the lunge animation on every client.
        public void ServerPlayAttack()
        {
            AttackObserversRpc();
        }

        // Server only. Boss ground slam: shakes the cameras of everyone near it.
        public void ServerPlaySlam()
        {
            SlamObserversRpc();
        }

        // Server only. The warden heaves itself out of the ground.
        public void ServerPlayRise() => RiseObserversRpc(transform.position);

        // Server only. Roots are about to burst out of the ground at position: a warning everyone can see.
        public void ServerWarnRoots(Vector3 position, float radius) => RootWarningObserversRpc(position, radius);

        public void ServerBurstRoots(Vector3 position) => RootBurstObserversRpc(position);

        [ObserversRpc]
        private void RiseObserversRpc(Vector3 position)
        {
            Sfx.Play(ProceduralSfx.Fell, position, 1f, 0.5f);
            CombatFx.Dust(position + Vector3.up * 0.3f, new Color(0.4f, 0.3f, 0.2f, 0.85f), 24);
            Shake(position, 0.5f);
        }

        [ObserversRpc]
        private void PhaseObserversRpc(Vector3 position)
        {
            Sfx.Play(ProceduralSfx.HutHit, position + Vector3.up * 2f, 1f, 0.55f);
            Color color = _palette != null && Element != Element.Neutral ? _palette.GetBaseColor(Element) : new Color(0.6f, 0.8f, 0.3f);
            CombatFx.Burst(position + Vector3.up * 2.5f, color * 1.5f, 30, 6f, 0.16f, 1.1f, 0.4f, true);
            CombatFx.Dust(position + Vector3.up * 0.3f, new Color(0.4f, 0.3f, 0.2f, 0.85f), 16);
            Shake(position, 0.4f);
        }

        [ObserversRpc]
        private void RootWarningObserversRpc(Vector3 position, float radius)
        {
            // A ring of churned earth and a low crack: the player has a moment to get out of it.
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * 0.2f;
                Vector3 edge = position + new Vector3(Mathf.Cos(angle), 0.1f, Mathf.Sin(angle)) * radius;
                CombatFx.Dust(edge, new Color(0.35f, 0.25f, 0.15f, 0.9f), 2);
            }
            Sfx.Play(ProceduralSfx.Chop, position, 0.7f, 0.55f, 0.05f);
        }

        [ObserversRpc]
        private void RootBurstObserversRpc(Vector3 position)
        {
            CombatFx.Chips(position + Vector3.up * 0.4f, new Color(0.45f, 0.3f, 0.18f, 1f));
            CombatFx.Burst(position, new Color(0.4f, 0.28f, 0.16f, 1f), 18, 7f, 0.14f, 0.8f, 2.4f, false, Vector3.up);
            Sfx.Play(ProceduralSfx.HutHit, position, 1f, 0.7f, 0.08f);
            Shake(position, 0.25f);
        }

        private void Shake(Vector3 position, float amount)
        {
            PlayerCamera cam = PlayerCamera.Local;
            if (cam == null) return;
            float strength = Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, position) / _config.FeelFalloffDistance);
            cam.AddShake(amount * strength);
        }

        [ObserversRpc]
        private void HitObserversRpc(Vector3 position, int sizeIndex, Vector3 pushDirection)
        {
            // Not the plain chop of felling a tree: a deeper crack with a hollow knock under it.
            Sfx.Play(ProceduralSfx.Chop, position + Vector3.up, 1f, 0.95f - 0.1f * sizeIndex, 0.08f);
            Sfx.Play(ProceduralSfx.Block, position + Vector3.up, 0.85f, 0.7f - 0.06f * sizeIndex, 0.06f);
            _hitStart = Time.time;
            _hitDirection = pushDirection;
            _flashUntil = Time.time + 0.12f;
            if (_draugr != null) _draugr.SetTrigger(HitParam);
            else if (_model != null) TreeTint.Flash(_model.gameObject);
            Vector3 chest = position + Vector3.up * (0.9f + 0.4f * sizeIndex);
            CombatFx.Chips(chest, new Color(0.55f, 0.36f, 0.2f, 1f));
            if (Element != Element.Neutral && _palette != null) CombatFx.Sparks(chest, _palette.GetBaseColor(Element) * 1.6f, 8);
            if (PlayerCamera.Local != null) PlayerCamera.Local.AddShake(0.05f);
        }

        [ObserversRpc]
        private void WindupObserversRpc()
        {
            _windupStart = Time.time;
            if (_draugr != null) _draugr.SetTrigger(AttackParam);
            // A low whoosh as the branch comes back: the attack can be heard coming.
            Sfx.Play(ProceduralSfx.Swing, transform.position + Vector3.up * 1.5f, 0.75f, 0.55f, 0.05f);
        }

        [ObserversRpc]
        private void DeathObserversRpc(Vector3 position, int sizeIndex, Vector3 fallDirection)
        {
            _hitDirection = fallDirection;
            // A heavy wooden crack, no voice: the enemies make no growls, roars or death cries.
            Sfx.Play(ProceduralSfx.HutHit, position + Vector3.up, 1f, 0.8f - 0.1f * sizeIndex, 0.06f);
            // A draugr falls where it stood: its body is left behind for a moment while the network object goes.
            if (_draugr != null && _visualRoot != null)
            {
                _visualRoot.SetParent(null, true);
                Transform bossLight = _visualRoot.Find("BossLight");
                if (bossLight != null) Destroy(bossLight.gameObject);
                _draugr.SetTrigger(DeathParam);
                Destroy(_visualRoot.gameObject, DraugrCorpseSeconds);
                ClearGlow(DraugrCorpseSeconds + 0.1f);
                _visualRoot = null;
                _draugr = null;
            }
            // A tree enemy topples away from the blow and sinks, instead of vanishing.
            else if (_visualRoot != null && State == TreantState.Active)
            {
                Transform body = _visualRoot;
                body.SetParent(null, true);
                body.localScale = Vector3.one;
                FallingCorpse.Begin(body, _hitDirection, CurrentHeight());
                ClearGlow(2f);
                _visualRoot = null;
                CombatFx.Chips(position + Vector3.up * (0.9f + 0.4f * sizeIndex), new Color(0.55f, 0.36f, 0.2f, 1f));
            }
        }

        [ObserversRpc]
        private void AttackObserversRpc()
        {
            _lungeStart = Time.time;
        }

        [ObserversRpc]
        private void SlamObserversRpc()
        {
            _lungeStart = Time.time;
            Sfx.Play(ProceduralSfx.Fell, transform.position, 1f, 0.6f);
            PlayerCamera cam = PlayerCamera.Local;
            if (cam == null) return;
            float strength = Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, transform.position) / _config.FeelFalloffDistance);
            cam.AddShake(0.35f * strength);
        }

        // Server only. Hit strip test used by PlayerAxe. Returns the nearest treant in the swing strip.
        public static Treant ServerFindTarget(Vector3 origin, Vector3 aim, float range, float radius, out float distance)
        {
            Treant best = null;
            distance = float.MaxValue;
            for (int i = 0; i < _all.Count; i++)
            {
                Treant t = _all[i];
                // Ordinary saplings are not targets, so the axe passes through them and hits the tree behind.
                if (!t.IsActive && !t.IsBoss) continue;
                float d = TreeTargeting.HitDistance(origin, aim, range, radius, t.transform.position, t.HitRadius);
                if (d >= 0f && d < distance)
                {
                    best = t;
                    distance = d;
                }
            }
            return best;
        }

        public float HitRadius
        {
            get
            {
                if (State == TreantState.Sapling && !IsBoss) return SaplingHitRadius;
                float height = CurrentHeight();
                return IsBoss ? Mathf.Max(SaplingHitRadius, height * BossTrunkFraction) : _config.TreantHitRadiusBase * (1f + (int)Size * 0.5f);
            }
        }

        // ---------- Client visuals ----------

        public override void OnStartClient()
        {
            base.OnStartClient();
            _state.OnChange += OnStateChanged;
            _element.OnChange += OnElementChanged;
            _guardian.OnChange += OnGuardianChanged;
            _wardenPhase.OnChange += OnWardenPhaseChanged;
            _shielded.OnChange += OnShieldChanged;
            GrassPushers.Add(transform, GrassPushRadiusBase * (1f + (int)Size * 0.4f));
            _clientAll.Add(this);
            BuildVisual();
            _lastPosition = transform.position;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _state.OnChange -= OnStateChanged;
            _element.OnChange -= OnElementChanged;
            _guardian.OnChange -= OnGuardianChanged;
            _wardenPhase.OnChange -= OnWardenPhaseChanged;
            _shielded.OnChange -= OnShieldChanged;
            GrassPushers.Remove(transform);
            _clientAll.Remove(this);
            ClearGlow(0f);
        }

        private void OnStateChanged(int previous, int next, bool asServer)
        {
            if (!asServer) BuildVisual();
        }

        private void OnElementChanged(int previous, int next, bool asServer)
        {
            if (asServer == IsServerInitialized && previous != next) BuildVisual();
        }

        // Host: the server side call, whose arguments are trustworthy (see PlayerWood).
        private void OnGuardianChanged(bool previous, bool next, bool asServer)
        {
            if (asServer == IsServerInitialized && previous != next) BuildVisual();
        }

        private void OnWardenPhaseChanged(int previous, int next, bool asServer)
        {
            if (asServer == IsServerInitialized && previous != next) RefreshWardenLook();
        }

        private void OnShieldChanged(bool previous, bool next, bool asServer)
        {
            if (asServer == IsServerInitialized && previous != next) RefreshWardenLook();
        }

        // The warden's bark glows hard while its guards shield it, and burns hotter and grows when it is enraged.
        private void RefreshWardenLook()
        {
            if (_draugr != null)
            {
                if (IsWarden && !IsGuardian)
                {
                    ApplyGolemScale();
                    TintGlow(GolemGlow());
                }
                return;
            }
            if (!IsWarden) return;
            ApplyScale();
            TintGlow(TreeGlow());
        }

        private void TintGlow(Color glow)
        {
            foreach (Material material in _glowMaterials)
            {
                if (material == null) continue;
                float share = _barkMaterials.Contains(material) ? BarkGlowShare : _crownMaterials.Contains(material) ? LeafGlowShare : 1f;
                material.SetColor(EmissionId, glow * share);
            }
        }

        private Color TreeGlow()
        {
            Color baseGlow = Element == Element.Neutral || _palette == null ? PlainGlow : _palette.GetBaseColor(Element) * ElementGlow;
            if (IsShielded) return baseGlow * 3f + new Color(0.25f, 0.2f, 0.1f);
            if (WardenPhase >= 3) return Color.Lerp(baseGlow, new Color(1f, 0.35f, 0.1f), 0.5f) * 1.6f;
            return baseGlow;
        }

        private bool ShowsDraugr => IsGuardian && State == TreantState.Active && _draugrPrefab != null;

        public bool ShowsGolem => !IsGuardian && State == TreantState.Active && _golemPrefab != null;

        // Ordinary draugr are a man's height, bigger ones a little taller, and a boss towers over the player.
        // The crypt boss stands nearly twice a guardian's height (it was 3.1 m and read as just another guardian in
        // the dark arena).
        private float DraugrHeight() => IsBoss ? CharacterSizes.CryptBoss : CharacterSizes.Draugr(Size);

        private float ActiveHeight() => CharacterSizes.Elemental(Size);

        public float CurrentHeight()
        {
            if (State == TreantState.Active) return IsBoss ? CharacterSizes.Warden * (WardenPhase >= 3 ? 1.12f : 1f) : ActiveHeight();
            if (IsBoss) return Mathf.Lerp(BossSaplingStartHeight, CharacterSizes.Warden, Growth);
            return SaplingHeight * (1f + (int)Size * 0.25f);
        }

        private void BuildVisual()
        {
            ClearGlow(0f);
            if (_visualRoot != null) Destroy(_visualRoot.gameObject);
            var root = new GameObject("Visual");
            root.transform.SetParent(transform, false);
            _visualRoot = root.transform;
            _draugr = null;

            if (ShowsDraugr)
            {
                BuildDraugr();
                return;
            }

            if (ShowsGolem)
            {
                BuildGolem();
                return;
            }

            int elementIndex = Mathf.Clamp(_element.Value, 0, _modelsByElement.Length - 1);
            GameObject model = Instantiate(_modelsByElement[elementIndex], _visualRoot);
            model.transform.localPosition = Vector3.zero;
            _model = model.transform;
            _modelBaseHeight = MeasureHeight(model);
            TreeTint.ApplyTreant(model, Element);
            ApplyScale();
            if (State == TreantState.Active) ApplyGlow(model);
            RefreshWardenLook();
        }

        // Lets every material of the look give off its element's color (copies, so other enemies keep their own).
        private void ApplyGlow(GameObject model)
        {
            Color glow = Element == Element.Neutral || _palette == null ? PlainGlow : _palette.GetBaseColor(Element) * ElementGlow;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.materials;
                foreach (Material material in materials)
                {
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    material.SetColor(EmissionId, glow);
                    _glowMaterials.Add(material);
                }
            }
        }

        // Frees the glowing copies, after a delay when a body is left behind for a moment.
        private void ClearGlow(float delay)
        {
            foreach (Material material in _glowMaterials)
            {
                if (material != null) Destroy(material, delay);
            }
            _glowMaterials.Clear();
            _barkMaterials.Clear();
            _crownMaterials.Clear();
        }

        private void BuildGolem()
        {
            GameObject model = Instantiate(_golemPrefab, _visualRoot);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            _model = model.transform;
            _draugr = model.GetComponentInChildren<Animator>();
            ShowElementCrown(model);
            _modelBaseHeight = MeasureEnabledHeight(model);
            ApplyGolemScale();
            MakeGolemGlow(model);
        }

        // The prefab carries one crown per element (Leaves0 is neutral). Only this enemy's element is shown.
        private void ShowElementCrown(GameObject model)
        {
            string keep = "Leaves" + Mathf.Clamp(_element.Value, 0, 4);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.gameObject.name.StartsWith("Leaves")) continue;
                renderer.enabled = renderer.gameObject.name.StartsWith(keep);
            }
        }

        private void ApplyGolemScale()
        {
            if (_model == null) return;
            _model.localScale = Vector3.one * (CurrentHeight() / Mathf.Max(0.1f, _modelBaseHeight));
        }

        // Copies of the bark and leaves, so this creature can glow on its own. The bark takes a little of the element.
        private void MakeGolemGlow(GameObject model)
        {
            Color element = Element == Element.Neutral || _palette == null ? Color.white : _palette.GetBaseColor(Element);
            Color glow = GolemGlow();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                bool bark = !renderer.gameObject.name.StartsWith("Leaves");
                Material[] materials = renderer.materials;
                foreach (Material material in materials)
                {
                    if (bark) material.SetColor(BaseColorId, Color.Lerp(Color.white, element, 0.12f));
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    material.SetColor(EmissionId, glow * (bark ? BarkGlowShare : LeafGlowShare));
                    _glowMaterials.Add(material);
                    if (bark) _barkMaterials.Add(material);
                    else _crownMaterials.Add(material);
                }
            }
        }

        private Color GolemGlow()
        {
            Color glow = Element == Element.Neutral || _palette == null ? PlainGlow : _palette.GetBaseColor(Element) * ElementGlow;
            if (!IsWarden) return glow;
            if (IsShielded) return glow * 3f + new Color(0.25f, 0.2f, 0.1f);
            if (WardenPhase >= 3) return Color.Lerp(glow, new Color(1f, 0.35f, 0.1f), 0.5f) * 1.6f;
            return glow;
        }

        private static float MeasureEnabledHeight(GameObject model)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return Mathf.Max(0.1f, bounds.size.y);
        }

        private void BuildDraugr()
        {
            GameObject model = Instantiate(_draugrPrefab, _visualRoot);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one * (DraugrHeight() / DraugrModelHeight);
            _model = model.transform;
            _draugr = model.GetComponentInChildren<Animator>();

            // Dressed like a man of the village, then drained of color: ashen skin and cloth. An elemental draugr is
            // washed in its element's color instead, so it reads at a glance (no glowing eyes).
            CharacterOutfitter outfitter = model.GetComponentInChildren<CharacterOutfitter>();
            if (outfitter != null)
            {
                outfitter.WearChopperClothes = false;
                outfitter.ApplyChopper();
            }
            Color element = Element == Element.Neutral || _palette == null ? Color.white : _palette.GetBaseColor(Element);
            Color tint = DraugrSkin * Color.Lerp(Color.white, element, 0.35f);
            // The glow goes in the same property block as the tint. Copying the materials to make them glow (as the
            // trees do) left every draugr body invisible in builds; the character materials keep emission switched on
            // (black) so the block can light it.
            // Brighter than the trees: the crypts are dark and a draugr must read against the stone.
            Color glow = (Element == Element.Neutral || _palette == null ? PlainGlow : element * ElementGlow) * DraugrGlowBoost;
            var block = new MaterialPropertyBlock();
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                for (int i = 0; i < skin.sharedMaterials.Length; i++)
                {
                    skin.GetPropertyBlock(block, i);
                    Color current = block.isEmpty ? Color.white : block.GetColor(BaseColorId);
                    if (current.maxColorComponent <= 0f) current = Color.white;
                    // Keep a little of each part's own shade so the clothes still read, then wash it out.
                    block.SetColor(BaseColorId, Color.Lerp(Color.white, current, 0.4f) * tint);
                    block.SetColor(EmissionId, glow);
                    skin.SetPropertyBlock(block, i);
                }
            }

            // The crypt boss carries its own light in its element's color, so it reads the moment it rises in the dim
            // arena. No shadows: one light, cheap.
            if (IsBoss)
            {
                var lightObject = new GameObject("BossLight");
                lightObject.transform.SetParent(_visualRoot, false);
                lightObject.transform.localPosition = new Vector3(0f, DraugrHeight() * 0.75f, 0.9f);
                Light bossLight = lightObject.AddComponent<Light>();
                bossLight.type = LightType.Point;
                bossLight.color = Element == Element.Neutral || _palette == null ? new Color(1f, 0.82f, 0.6f) : Color.Lerp(Color.white, element, 0.6f);
                bossLight.range = 9f;
                bossLight.intensity = 3.2f;
                bossLight.shadows = LightShadows.None;
            }
        }

        private void ApplyScale()
        {
            if (_model == null || _draugr != null) return;
            _model.localScale = Vector3.one * (CurrentHeight() / _modelBaseHeight);
        }

        private static float MeasureHeight(GameObject model)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return Mathf.Max(0.1f, bounds.size.y);
        }

        private float _nextGrowthRefresh;

        // The animator walks the draugr; only the hit shove is added on top.
        private void UpdateDraugr()
        {
            Vector3 delta = transform.position - _lastPosition;
            delta.y = 0f;
            _lastPosition = transform.position;
            float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            _draugrSpeed = Mathf.Lerp(_draugrSpeed, speed, 1f - Mathf.Exp(-10f * Time.deltaTime));
            _draugr.SetFloat(SpeedParam, _draugrSpeed);

            Vector3 local = Vector3.zero;
            float sinceHit = Time.time - _hitStart;
            if (sinceHit >= 0f && sinceHit < RecoilSeconds)
            {
                float k = 1f - sinceHit / RecoilSeconds;
                local += transform.InverseTransformDirection(_hitDirection) * (RecoilDistance * 0.5f * k * k);
            }
            _visualRoot.localPosition = local;
        }

        // Server: slides the enemy along the shove from the last hit instead of snapping it.
        private void ServerKnockback()
        {
            if (_knockRemaining.sqrMagnitude < 0.0004f || !IsServerInitialized) return;
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            {
                _knockRemaining = Vector3.zero;
                return;
            }
            Vector3 step = _knockRemaining * Mathf.Min(1f, KnockSharpness * Time.deltaTime);
            _agent.Move(step);
            _knockRemaining -= step;
        }

        private void Update()
        {
            ServerKnockback();
            if (_visualRoot == null) return;
            if (_flashUntil > 0f && Time.time >= _flashUntil)
            {
                _flashUntil = 0f;
                if (_model != null && _draugr == null) TreeTint.ApplyTreant(_model.gameObject, Element);
            }
            if (_draugr != null)
            {
                UpdateDraugr();
                return;
            }

            if (IsBoss && State == TreantState.Sapling && Time.time >= _nextGrowthRefresh)
            {
                _nextGrowthRefresh = Time.time + 0.5f;
                ApplyScale();
            }

            Vector3 delta = transform.position - _lastPosition;
            delta.y = 0f;
            bool moving = State == TreantState.Active && delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f) > MovingThreshold;
            _lastPosition = transform.position;

            Vector3 local = Vector3.zero;
            if (moving)
            {
                _bobPhase += Time.deltaTime * BobSpeed;
                local.y = Mathf.Abs(Mathf.Sin(_bobPhase)) * BobHeight * (1f + (int)Size * 0.3f);
                _visualRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_bobPhase) * 3f);
            }
            else
            {
                _visualRoot.localRotation = Quaternion.identity;
            }

            float lunge = (Time.time - _lungeStart) / LungeDuration;
            if (lunge >= 0f && lunge < 1f) local.z = Mathf.Sin(lunge * Mathf.PI) * LungeDistance * (1f + (int)Size * 0.3f);

            // Hit reaction: a shove away from the blow and a squash, easing back.
            float sinceHit = Time.time - _hitStart;
            Vector3 squash = Vector3.one;
            if (sinceHit >= 0f && sinceHit < RecoilSeconds)
            {
                float k = 1f - sinceHit / RecoilSeconds;
                k *= k;
                local += transform.InverseTransformDirection(_hitDirection) * (RecoilDistance * k);
                squash = new Vector3(1f + 0.12f * k, 1f - 0.14f * k, 1f + 0.12f * k);
            }

            // Wind up: rear back and lean away before the swing, so the player can read it.
            float windup = (Time.time - _windupStart) / Mathf.Max(0.1f, _config.TreantWindupSeconds);
            if (windup >= 0f && windup < 1f)
            {
                float ease = Mathf.Sin(windup * Mathf.PI * 0.5f);
                local.z -= 0.5f * ease * (1f + (int)Size * 0.2f);
                _visualRoot.localRotation = Quaternion.Euler(-16f * ease, 0f, 0f) * _visualRoot.localRotation;
                squash.y *= 1f + 0.06f * ease;
            }
            _visualRoot.localScale = squash;
            _visualRoot.localPosition = local;
        }
    }
}
