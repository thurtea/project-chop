using System.Collections.Generic;
using Chopper.Core;
using Chopper.Player;
using Chopper.Trees;
using Chopper.World;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace Chopper.Enemies
{
    // Server side director for the whole day and night loop (plan T19, T24, T25, T29, T30):
    // - rolls sapling drops on every accepted tree hit (by day only),
    // - wakes saplings up at night, a few at a time up to a player scaled cap,
    // - spawns queued bosses at nightfall,
    // - at dawn withers the survivors, resets buffs, heals players and regrows trees,
    // - restarts the run when the hut falls.
    public class EnemyDirector : NetworkBehaviour
    {
        private const int Seed = 90210;
        private const float DropMinRadius = 0.9f;
        private const float MessageSeconds = 6f;
        private const int DebugSaplingCount = 8;
        private const float DebugSaplingRadius = 6f;

        public static EnemyDirector Instance { get; private set; }

        // Client side banner text shown by the HUD.
        public static string Message { get; private set; } = string.Empty;
        public static float MessageUntil { get; private set; }

        [SerializeField] private GameConfig _config;
        [SerializeField] private NetworkObject _treantPrefab;
        [Tooltip("The pile of wood a player leaves where they fell.")]
        [SerializeField] private NetworkObject _woodPilePrefab;
        [Tooltip("Something lying on the ground (a picked herb, a fern's fiber, a branch), taken by walking over it.")]
        [SerializeField] private NetworkObject _groundItemPrefab;
        [SerializeField] private SpawnTable[] _spawnTables;

        private struct PendingBoss
        {
            public Vector3 Position;
            public Element Element;
        }

        private readonly List<PendingBoss> _pendingBosses = new List<PendingBoss>();
        private System.Random _rng;
        private float _nextActivate;
        // Share of tonight's awake enemies that are elemental, rolled at nightfall.
        private float _nightShare = 0.5f;
        private Element _nightDominant = Element.Neutral;
        // Boss milestone: trees felled since the last boss, bosses beaten so far, and whether one is growing or on its way.
        private int _treesFelled;
        private int _bossesDefeated;
        private bool _bossPlanted;
        private float _lockedShare = -1f;
        private bool _gameOverRunning;
        // Zone boss: where forest trees were felled (a grove is many of them close together), the grove warden that is
        // awake (at most one), and the guards it called.
        private readonly List<Vector3> _groveFelled = new List<Vector3>();
        private Treant _warden;
        private readonly List<Treant> _wardenGuards = new List<Treant>();

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
            _rng = new System.Random(Seed);
            _pendingBosses.Clear();
            DayNightCycle.ServerPhaseChanged += OnPhaseChanged;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            DayNightCycle.ServerPhaseChanged -= OnPhaseChanged;
        }

        private int PlayerCount => Mathf.Max(1, ServerManager.Clients.Count);

        public static int CountActive()
        {
            int count = 0;
            foreach (Treant t in Treant.All) if (t.IsActive && !t.IsBoss && !t.IsGuardian) count++;
            return count;
        }

        public static int CountSaplings()
        {
            int count = 0;
            foreach (Treant t in Treant.All) if (!t.IsActive && !t.IsBoss) count++;
            return count;
        }

        // ---------- Drops ----------

        public float NightElementalShare => _nightShare;
        public Element NightDominant => _nightDominant;

        // Server only, tests: makes every night use this elemental share instead of rolling one. Negative unlocks.
        public void ServerLockNightShare(float share) => _lockedShare = share;
        public int TreesFelled => _treesFelled;
        public int BossesDefeated => _bossesDefeated;
        public bool BossPlanted => _bossPlanted;
        // Server: the awake grove warden, or null.
        public Treant Warden => _warden != null && _warden.IsSpawned ? _warden : null;
        public int WardenGuardsAlive => CountGuards();
        public IReadOnlyList<Vector3> GroveFelled => _groveFelled;

        // Server only. Called by TreeManager when a tree is FELLED by day: it plants 1-2 (small), 2-3 (medium) or
        // 3-4 (large) saplings around the stump. Each sapling is rolled from the forest's table: the forest's element
        // or a plain one. The swinger is told what sprouted. Trees in the hub plant nothing (NightRules.PlantsSaplings).
        public void ServerOnTreeHit(Vector3 treePosition, Element zone, TreeSize size, PlayerWood swinger = null)
        {
            DayNightCycle cycle = DayNightCycle.Instance;
            if (cycle == null || cycle.IsNight || !NightRules.PlantsSaplings(zone)) return;

            float multiplier = PlayerScaling.Multiplier(PlayerCount, _config.SaplingScalingCoefficient, _config.MaxScaledPlayers);
            _config.GetSaplingRange(size, out int min, out int max);
            int count = DropRules.RollSaplings(_rng, min, max, multiplier);
            int planted = 0;
            for (int i = 0; i < count; i++)
            {
                if (Treant.All.Count >= _config.MaxSaplings) break;
                // Bosses rise from felled groves (see ServerOnTreeFelled), not from saplings.
                Element element = RollElement(zone);
                ServerSpawn(element, size, false, ScatterAround(treePosition, _config.SaplingScatterRadius));
                planted++;
            }
            if (planted > 0)
            {
                foreach (NetworkConnection connection in Chopper.Networking.NearbyClients.Within(ServerManager, treePosition))
                {
                    SproutTargetRpc(connection, treePosition, (int)zone);
                }
            }
        }

        // Everyone nearby sees the ground stir where the saplings were planted.
        [TargetRpc]
        private void SproutTargetRpc(NetworkConnection connection, Vector3 position, int zone)
        {
            Color color = zone == (int)Element.Neutral ? new Color(0.5f, 0.75f, 0.3f, 0.9f) : _paletteColor((Element)zone);
            CombatFx.Burst(position + Vector3.up * 0.2f, color, 14, 1.8f, 0.14f, 0.9f, 0.3f);
            CombatFx.Burst(position + Vector3.up * 0.1f, new Color(0.45f, 0.32f, 0.2f, 0.7f), 6, 1.2f, 0.3f, 0.8f, 0.2f);
        }

        private static Color _paletteColor(Element element)
        {
            return ZonePalette.Label(element);
        }

        private Element RollElement(Element zone)
        {
            if (zone == Element.Neutral || _spawnTables == null) return Element.Neutral;
            foreach (SpawnTable table in _spawnTables)
            {
                if (table != null && table.ZoneElement == zone) return table.Roll(_rng);
            }
            return zone;
        }

        private Vector3 ScatterAround(Vector3 center, float radius)
        {
            float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(DropMinRadius, Mathf.Max(DropMinRadius, radius), (float)_rng.NextDouble());
            Vector3 p = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            bool hasGround = Chopper.World.WorldGround.Exists;
            p.y = hasGround ? Chopper.World.WorldGround.HeightAt(p) : center.y;
            return p;
        }

        // Server only. bornOffsetDays makes a boss sapling that much older (used by tests).
        public Treant ServerSpawn(Element element, TreeSize size, bool boss, Vector3 position, float bornOffsetDays = 0f)
        {
            NetworkObject nob = NetworkManager.GetPooledInstantiated(_treantPrefab, position, Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f), true);
            Treant treant = nob.GetComponent<Treant>();
            float born = (DayNightCycle.Instance != null ? DayNightCycle.Instance.WorldDays : 0f) - bornOffsetDays;
            treant.ServerInit(element, size, boss, born);
            ServerManager.Spawn(nob, null, gameObject.scene);
            return treant;
        }

        // ---------- Saving ----------

        public int TreesFelledSinceBoss => _treesFelled;

        // Server only. The bosses queued for the coming night, as (position, element).
        public List<(Vector3 position, Element element)> ServerPendingBosses()
        {
            var list = new List<(Vector3, Element)>();
            foreach (PendingBoss boss in _pendingBosses) list.Add((boss.Position, boss.Element));
            return list;
        }

        // Server only. Loading a saved world: the boss milestone counters and the queued bosses.
        public void ServerLoadProgress(int treesFelled, int bossesDefeated, bool bossPlanted, List<(Vector3 position, Element element)> pending)
        {
            _treesFelled = Mathf.Max(0, treesFelled);
            _bossesDefeated = Mathf.Max(0, bossesDefeated);
            _bossPlanted = bossPlanted;
            _pendingBosses.Clear();
            if (pending == null) return;
            foreach ((Vector3 position, Element element) in pending) _pendingBosses.Add(new PendingBoss { Position = position, Element = element });
        }

        // Server only. Knocks an item loose from where it grew: it hops a little way toward the player who picked it and
        // lies on the ground until someone walks over it (GroundItem).
        public void ServerDropItem(Chopper.World.GroundItemKind kind, int amount, Vector3 source, Vector3 toward)
        {
            if (_groundItemPrefab == null || amount <= 0) return;
            Vector3 flat = toward - source;
            flat.y = 0f;
            Vector3 position = source + (flat.sqrMagnitude > 0.01f ? flat.normalized * Mathf.Min(DropHop, flat.magnitude * 0.5f) : Vector3.zero);
            bool hasGround = Chopper.World.WorldGround.Exists;
            if (hasGround && !Chopper.World.Generation.CryptWorld.IsUnderground(position))
            {
                position.y = Chopper.World.WorldGround.HeightAt(position);
            }
            NetworkObject nob = NetworkManager.GetPooledInstantiated(_groundItemPrefab, position, Quaternion.identity, true);
            nob.GetComponent<Chopper.World.GroundItem>().ServerSetup(kind, amount, source + Vector3.up * 0.4f);
            ServerManager.Spawn(nob, null, gameObject.scene);
        }

        private const float DropHop = 0.8f;

        // Server only. Leaves a fallen player's wood on the ground where they fell, for anyone to pick up.
        public void ServerDropWood(Vector3 position, int amount)
        {
            if (_woodPilePrefab == null || amount <= 0) return;
            bool hasGround = Chopper.World.WorldGround.Exists;
            // On the land the pile settles on the ground; in a crypt it stays on the floor where the player fell.
            if (hasGround && !Chopper.World.Generation.CryptWorld.IsUnderground(position))
            {
                position.y = Chopper.World.WorldGround.HeightAt(position);
            }
            NetworkObject nob = NetworkManager.GetPooledInstantiated(_woodPilePrefab, position, Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f), true);
            nob.GetComponent<Chopper.World.WoodPile>().ServerSetAmount(amount);
            ServerManager.Spawn(nob, null, gameObject.scene);
        }

        // ---------- Grove warden (zone boss) ----------

        // Server only. Called by TreeManager whenever a tree is felled. Forest trees are remembered where they stood;
        // when enough lie in one grove (and the first boss day has come), that grove's warden rises among them.
        public void ServerOnTreeFelled(Vector3 position, Element zone)
        {
            _treesFelled++;
            if (zone == Element.Neutral) return;
            ZoneBossRules.Remember(_groveFelled, position);
            ServerCheckWarden(position);
        }

        // Server only. Raises the warden of the grove around latest if that grove is ready.
        public Treant ServerCheckWarden(Vector3 latest)
        {
            DayNightCycle cycle = DayNightCycle.Instance;
            if (cycle == null) return null;
            int required = BossRules.RequiredTrees(_config.GroveTreesFelled, _config.BossTreesFelledGrowth, _bossesDefeated);
            bool ready = ZoneBossRules.TryFindGrove(_groveFelled, latest, _config.GroveRadius, required, out Vector3 center);
            if (!ZoneBossRules.ShouldRise(cycle.Day, _config.BossFirstDay, Warden != null, ready)) return null;
            Element zone = ZoneMap.ElementAt(center, _config.HubRadius);
            if (zone == Element.Neutral) zone = ZoneMap.ElementAt(latest, _config.HubRadius);
            Treant warden = ServerWakeWarden(center, zone);
            if (warden == null) warden = ServerWakeWarden(latest, zone);
            if (warden != null) ZoneBossRules.ForgetGrove(_groveFelled, center, _config.GroveRadius);
            return warden;
        }

        // Server only. A grove warden of element rises at position (also used by loading, tests and the dev console).
        public Treant ServerWakeWarden(Vector3 position, Element element)
        {
            if (Warden != null) return null;
            bool hasGround = Chopper.World.WorldGround.Exists;
            if (hasGround) position.y = Chopper.World.WorldGround.HeightAt(position);
            Treant warden = ServerSpawn(element, TreeSize.Large, true, position);
            float health = ZoneBossRules.Health(_config.WardenHealth, _config.WardenHealthPerExtraPlayer, PlayerCount);
            warden.ServerMakeWarden(position, _config.WardenLeashRadius, health);
            if (!warden.ServerActivate()) return null;
            _warden = warden;
            _wardenGuards.Clear();
            warden.ServerPlayRise();
            ServerAnnounce($"Too many trees have fallen. The warden of the {element} grove rises!");
            return warden;
        }

        // Server only. The warden was driven into a new phase: it calls guards whose bark shields it while they stand.
        public void ServerOnWardenPhase(Treant warden, int phase)
        {
            if (warden != Warden) return;
            int count = ZoneBossRules.GuardsFor(phase, PlayerCount);
            TreeSize size = phase >= 3 ? TreeSize.Small : TreeSize.Medium;
            bool hasGround = Chopper.World.WorldGround.Exists;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + (float)_rng.NextDouble() * 0.5f) / count * Mathf.PI * 2f;
                Vector3 at = warden.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 6f;
                if (hasGround) at.y = Chopper.World.WorldGround.HeightAt(at);
                Treant guard = ServerSpawn(warden.Element, size, false, at);
                guard.ServerLeash(warden.Home, warden.LeashRadius);
                if (guard.ServerActivate()) _wardenGuards.Add(guard);
            }
            warden.ServerSetShield(CountGuards() > 0);
            ServerAnnounce(phase >= 3 ? "The warden is enraged!" : "Roots tear at the ground. The warden calls its guards, and its bark hardens.");
        }

        // Server only. Nobody stayed to fight: the warden heals and its guards go back into the ground.
        public void ServerResetWarden(Treant warden)
        {
            if (warden != Warden) return;
            warden.ServerResetWarden();
            DespawnGuards();
        }

        // Server only. The warden is dead: it counts as a boss beaten, everyone gets a boon, the hut gets wood.
        public void ServerOnWardenKilled(Treant warden)
        {
            if (warden != _warden) return;
            _warden = null;
            DespawnGuards();
            _bossesDefeated++;
            Hut hut = Hut.Instance;
            if (hut != null) hut.ServerAddStoreWood(_config.WardenRewardWood);
            BoonKind kind = BoonRules.Pick(_rng);
            foreach (PlayerHealth player in PlayerHealth.All)
            {
                PlayerBoons boons = player.GetComponent<PlayerBoons>();
                if (boons != null) boons.ServerGrant(kind);
            }
            ServerAnnounce($"The warden falls! Everyone gains a permanent boon: {BoonRules.Name(kind)}.");
        }

        private int CountGuards()
        {
            _wardenGuards.RemoveAll(g => g == null || !g.IsSpawned);
            return _wardenGuards.Count;
        }

        private void DespawnGuards()
        {
            foreach (Treant guard in _wardenGuards) if (guard != null) guard.ServerDespawn();
            _wardenGuards.Clear();
        }

        // Server: the bark shield lasts exactly as long as a guard stands.
        private void ServerWardenTick()
        {
            Treant warden = Warden;
            if (warden == null)
            {
                _warden = null;
                return;
            }
            warden.ServerSetShield(CountGuards() > 0);
        }

        // Server only, loading: the felled trees still counting toward a grove.
        public void ServerLoadGroves(List<Vector3> felled)
        {
            _groveFelled.Clear();
            if (felled == null) return;
            foreach (Vector3 p in felled) ZoneBossRules.Remember(_groveFelled, p);
        }

        // ---------- Night ----------

        private void OnPhaseChanged(DayPhase phase)
        {
            if (!IsServerInitialized) return;
            if (phase == DayPhase.Dusk) ServerAnnounce("Dusk. Get back to the hut, night is coming!");
            else if (phase == DayPhase.Night) BeginNight();
            else if (phase == DayPhase.Dawn) EndNight();
        }

        private void BeginNight()
        {
            _nextActivate = 0f;
            _nightShare = _lockedShare >= 0f ? _lockedShare : NightRules.RollShare(_rng, _config.NightElementalShareMin, _config.NightElementalShareMax);
            foreach (PendingBoss pending in _pendingBosses)
            {
                Treant boss = ServerSpawn(pending.Element, TreeSize.Large, true, pending.Position);
                boss.ServerActivate();
            }
            _nightDominant = NightRules.RollDominant(_rng, CountSleepingByElement());
            ServerAnnounce(_pendingBosses.Count > 0 ? "Night has fallen and the boss tree has awoken!" : NightRules.DescribeTheme(_nightDominant, _nightShare));
            _pendingBosses.Clear();
        }

        private void EndNight()
        {
            foreach (Treant t in new List<Treant>(Treant.All))
            {
                // Rooted boss trees keep growing, crypt guardians keep watch and the warden holds its grove with its
                // guards; everything else withers with the dawn.
                if (t.IsBoss && !t.IsActive) continue;
                if (t.IsGuardian) continue;
                if (t.HasLeash) continue;
                if (t.IsBoss && t.IsActive)
                {
                    // A boss nobody beat slips back into the forest; the milestone plants a new one soon.
                    _bossPlanted = false;
                    ServerAnnounce("The boss tree slipped away into the forest. It will be back.");
                }
                t.ServerDespawn();
            }

            foreach (PlayerBuffs buffs in PlayerBuffs.All) buffs.ServerReset();
            foreach (PlayerHealth health in PlayerHealth.All) health.ServerHealFull();
            if (TreeManager.Instance != null) TreeManager.Instance.ServerRegrowAll();
            ServerAnnounce("Dawn. The night's enemies wither and the trees regrow.");
        }

        private void Update()
        {
            if (IsServerInitialized)
            {
                ServerActivationTick();
                ServerWardenTick();
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsClientInitialized && Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame && PlayerWood.Local != null)
            {
                DebugSpawnSaplingsServerRpc(PlayerWood.Local.transform.position);
            }
#endif
        }

        // Wakes one sapling at a time while the night is on and fewer than the cap are awake.
        private void ServerActivationTick()
        {
            DayNightCycle cycle = DayNightCycle.Instance;
            if (cycle == null || !cycle.IsNight || Time.time < _nextActivate) return;
            _nextActivate = Time.time + Mathf.Max(0.02f, _config.NightSpawnStagger);

            int cap = Mathf.CeilToInt(_config.EnemyCapBase * PlayerScaling.EnemyMultiplier(_config, PlayerCount));
            if (CountActive() >= cap) return;

            // Tonight's theme decides which element wakes next: the dominant one mostly, its counter now and then (so the
            // axe can be charged on it), plain ones by the elemental share. Saplings keep the element they were planted
            // with, so the Fire forest's trees stay Fire and the player has to carry the right enchantment to them.
            int[] sleeping = CountSleepingByElement();
            Element wake = NightRules.PickWake(_rng, _nightShare, _nightDominant, sleeping);
            var pool = new List<Treant>();
            foreach (Treant t in Treant.All) if (!t.IsActive && !t.IsBoss && t.Element == wake) pool.Add(t);
            if (pool.Count == 0) return;
            pool[_rng.Next(pool.Count)].ServerActivate();
        }

        // Sleeping (not yet awake) saplings per element, indexed by Element. Bosses are left out.
        private static int[] CountSleepingByElement()
        {
            var counts = new int[5];
            foreach (Treant t in Treant.All)
            {
                if (!t.IsActive && !t.IsBoss) counts[(int)t.Element]++;
            }
            return counts;
        }

        // ---------- Boss ----------

        // Server only. A grown boss tree was chopped down: a boss will rise at the next nightfall.
        public void ServerOnBossTreeFelled(Vector3 position, Element element)
        {
            _pendingBosses.Add(new PendingBoss { Position = position, Element = element });
            ServerAnnounce("The boss tree has fallen. Something will rise from it at nightfall.");
        }

        // Server only. The boss is dead: everyone gets the same permanent boon, plus some wood for the hut.
        public void ServerOnBossKilled()
        {
            _bossesDefeated++;
            _bossPlanted = false;
            _treesFelled = 0;

            Hut hut = Hut.Instance;
            if (hut != null) hut.ServerAddStoreWood(_config.BossRewardWood);

            BoonKind kind = BoonRules.Pick(_rng);
            foreach (PlayerHealth player in PlayerHealth.All)
            {
                PlayerBoons boons = player.GetComponent<PlayerBoons>();
                if (boons != null) boons.ServerGrant(kind);
                PlayerUpgrades upgrades = player.GetComponent<PlayerUpgrades>();
                if (upgrades != null) upgrades.ServerGrantArmourKit();
            }
            ServerAnnounce($"Boss defeated! Everyone gains a permanent boon: {BoonRules.Name(kind)}, and an armour kit (use it from the bag).");
        }

        // ---------- Game over ----------

        // Server only. Called when a player runs out of lives: if nobody is left standing the run restarts.
        public void ServerCheckAllPlayersOut()
        {
            if (PlayerHealth.All.Count == 0) return;
            foreach (PlayerHealth player in PlayerHealth.All)
            {
                if (!player.IsOut) return;
            }
            ServerGameOver();
        }

        // Server only. Called when the hut reaches zero health: everything resets to day one.
        public void ServerGameOver()
        {
            if (_gameOverRunning) return;
            _gameOverRunning = true;

            foreach (Treant t in new List<Treant>(Treant.All)) t.ServerDespawn();
            _pendingBosses.Clear();
            _treesFelled = 0;
            _bossPlanted = false;
            _warden = null;
            _wardenGuards.Clear();
            _groveFelled.Clear();
            foreach (PlayerBuffs buffs in PlayerBuffs.All) buffs.ServerReset();
            foreach (PlayerHealth health in PlayerHealth.All)
            {
                health.ServerHealFull();
                var upgrades = health.GetComponent<PlayerUpgrades>();
                if (upgrades != null) upgrades.ServerReset();
                var inventory = health.GetComponent<PlayerInventory>();
                if (inventory != null) inventory.ServerReset();
                var fishing = health.GetComponent<PlayerFishing>();
                if (fishing != null) fishing.ServerReset();
                var mining = health.GetComponent<PlayerMining>();
                if (mining != null) mining.ServerReset();
            }

            Hut hut = Hut.Instance;
            if (hut != null) hut.ServerReset();
            if (CryptManager.Instance != null) CryptManager.Instance.ServerReset();
            if (MineManager.Instance != null) MineManager.Instance.ServerRefill();
            if (ArenaManager.Instance != null) ArenaManager.Instance.ServerReset();
            if (TreeManager.Instance != null) TreeManager.Instance.ServerRegrowAll();
            if (DayNightCycle.Instance != null) DayNightCycle.Instance.ServerRestart();
            ServerAnnounce("The hut has fallen! The run restarts at day one.");
            _gameOverRunning = false;
        }

        // ---------- Announcements ----------

        public void ServerAnnounce(string text)
        {
            AnnounceObserversRpc(text);
        }

        [ObserversRpc]
        private void AnnounceObserversRpc(string text)
        {
            Message = text;
            MessageUntil = Time.time + MessageSeconds;
        }

        // Development only: F7 drops a ring of saplings around the caller so the night can be tried quickly.
        [ServerRpc(RequireOwnership = false)]
        private void DebugSpawnSaplingsServerRpc(Vector3 position)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            for (int i = 0; i < DebugSaplingCount; i++)
            {
                Element element = (Element)_rng.Next(0, 5);
                TreeSize size = (TreeSize)_rng.Next(0, 3);
                ServerSpawn(element, size, false, ScatterAround(position, DebugSaplingRadius));
            }
#endif
        }
    }
}
