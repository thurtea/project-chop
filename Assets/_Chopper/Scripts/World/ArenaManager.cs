using System.Collections.Generic;
using Chopper.Core;
using Chopper.Enemies;
using Chopper.Player;
using Chopper.World.Generation;
using FishNet.Object;
using UnityEngine;

namespace Chopper.World
{
    // The arena under each crypt's vault, on the server. Entering starts the fight: three waves of the crypt's
    // guardians (more each wave and for more players), then the crypt's boss. When the boss falls, everyone in the
    // arena gets essence of the crypt's element (for elemental wards and stones at the workbench) plus rune shards and
    // iron. A beaten arena stays quiet until the next dawn, then its dead rise again, so its rewards can be farmed. If
    // nobody is left standing in the arena, the fight resets.
    public class ArenaManager : NetworkBehaviour
    {
        public const int Waves = 3;
        private const float FirstWaveDelay = 2.5f;
        private const float BetweenWaves = 3f;
        private const float ResetWhenEmptyFor = 6f;
        private const int MaxPerWave = 8;
        public const int EssenceReward = 3;
        public const int ShardReward = 2;
        public const int IronReward = 2;

        private enum State { Idle, Fighting, Boss, Cleared }

        private sealed class Arena
        {
            public State State;
            public int Wave;
            public float NextWaveAt = -1f;
            public float EmptySince = -1f;
            public readonly List<Treant> Alive = new List<Treant>();
        }

        private readonly Dictionary<int, Arena> _arenas = new Dictionary<int, Arena>();

        // Client: the line to show for each crypt's arena ("Wave 2 of 3"), and which arenas are already won.
        private static readonly Dictionary<int, string> _status = new Dictionary<int, string>();
        private static readonly HashSet<int> _clearedHere = new HashSet<int>();

        public static ArenaManager Instance { get; private set; }

        public static string StatusOf(int crypt) => _status.TryGetValue(crypt, out string text) ? text : string.Empty;
        public static bool ClearedOnClient(int crypt) => _clearedHere.Contains(crypt);

        private void Awake()
        {
            Instance = this;
            _status.Clear();
            _clearedHere.Clear();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            DayNightCycle.ServerPhaseChanged += OnPhaseChanged;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            DayNightCycle.ServerPhaseChanged -= OnPhaseChanged;
        }

        // Dawn: every beaten arena can be fought again.
        private void OnPhaseChanged(DayPhase phase)
        {
            if (phase != DayPhase.Dawn) return;
            foreach (KeyValuePair<int, Arena> pair in new List<KeyValuePair<int, Arena>>(_arenas))
            {
                if (pair.Value.State == State.Cleared) ServerResetArena(pair.Key);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private Arena Get(int crypt)
        {
            if (!_arenas.TryGetValue(crypt, out Arena arena))
            {
                arena = new Arena();
                _arenas[crypt] = arena;
            }
            return arena;
        }

        public bool IsCleared(int crypt) => _arenas.TryGetValue(crypt, out Arena a) && a.State == State.Cleared;
        public bool IsFighting(int crypt) => _arenas.TryGetValue(crypt, out Arena a) && (a.State == State.Fighting || a.State == State.Boss);
        public int WaveOf(int crypt) => _arenas.TryGetValue(crypt, out Arena a) ? a.Wave : 0;
        public bool BossUp(int crypt) => _arenas.TryGetValue(crypt, out Arena a) && a.State == State.Boss;
        public int AliveIn(int crypt) => _arenas.TryGetValue(crypt, out Arena a) ? a.Alive.Count : 0;

        // ---------- Entering and leaving ----------

        // Server only. Takes the player from the vault archway down into the arena, starting the fight if it is new.
        public bool ServerTryEnter(PlayerCrypt player)
        {
            int crypt = CryptWorld.ArenaDoorInReach(player.transform.position);
            if (crypt < 0) return false;
            CryptPlan plan = CryptWorld.Plans[crypt];
            player.ServerTeleport(plan.ArenaEntry + Vector3.up * 0.1f, plan.ArenaEntryYaw);
            Arena arena = Get(crypt);
            if (arena.State == State.Cleared)
            {
                // Already won: nothing waits here.
            }
            else if (arena.State == State.Idle)
            {
                arena.State = State.Fighting;
                arena.Wave = 0;
                arena.NextWaveAt = Time.time + FirstWaveDelay;
                arena.EmptySince = -1f;
                Broadcast(crypt, string.Empty);
            }
            return true;
        }

        // Server only. Back up to the vault from the archway behind the arrival point.
        public bool ServerTryExit(PlayerCrypt player)
        {
            int crypt = CryptWorld.ArenaExitInReach(player.transform.position);
            if (crypt < 0) return false;
            CryptPlan plan = CryptWorld.Plans[crypt];
            player.ServerTeleport(plan.VaultReturnSpot + Vector3.up * 0.1f, plan.VaultReturnYaw);
            return true;
        }

        // ---------- The fight ----------

        private void Update()
        {
            if (!IsServerInitialized) return;
            foreach (KeyValuePair<int, Arena> pair in _arenas)
            {
                Arena arena = pair.Value;
                if (arena.State != State.Fighting && arena.State != State.Boss) continue;
                int crypt = pair.Key;
                CryptPlan plan = CryptWorld.Plans[crypt];

                if (PlayersInArena(plan, standingOnly: true).Count == 0)
                {
                    if (arena.EmptySince < 0f) arena.EmptySince = Time.time;
                    else if (Time.time - arena.EmptySince > ResetWhenEmptyFor)
                    {
                        ServerResetArena(crypt);
                        return;
                    }
                }
                else
                {
                    arena.EmptySince = -1f;
                }

                arena.Alive.RemoveAll(t => t == null || !t.IsSpawned);
                if (arena.Alive.Count > 0) continue;

                if (arena.NextWaveAt < 0f)
                {
                    if (arena.State == State.Boss)
                    {
                        Victory(crypt);
                        return;
                    }
                    arena.NextWaveAt = Time.time + BetweenWaves;
                    Broadcast(crypt, string.Empty);
                }
                else if (Time.time >= arena.NextWaveAt)
                {
                    arena.NextWaveAt = -1f;
                    if (arena.Wave < Waves) SpawnWave(crypt, ++arena.Wave);
                    else SpawnBoss(crypt);
                }
            }
        }

        private static List<PlayerHealth> PlayersInArena(CryptPlan plan, bool standingOnly)
        {
            var list = new List<PlayerHealth>();
            foreach (PlayerHealth player in PlayerHealth.All)
            {
                if (player == null || (standingOnly && player.IsDown)) continue;
                if (plan.InArena(player.transform.position)) list.Add(player);
            }
            return list;
        }

        // How many enemies a wave brings: more for later waves, bigger crypts and more players in the arena.
        public static int WaveSize(int tier, int wave, int players)
        {
            return Mathf.Clamp(tier + wave + Mathf.Max(0, players - 1), 2, MaxPerWave);
        }

        private void SpawnWave(int crypt, int wave)
        {
            Arena arena = Get(crypt);
            CryptPlan plan = CryptWorld.Plans[crypt];
            int count = WaveSize(plan.Site.Tier, wave, PlayersInArena(plan, true).Count);
            for (int i = 0; i < count; i++)
            {
                Vector3 at = plan.ArenaSpawns[(i * 3 + wave) % plan.ArenaSpawns.Count];
                // Bigger enemies in the last wave and in the deeper crypts.
                int size = Mathf.Clamp((plan.Site.Tier >= 3 ? 1 : 0) + (wave == Waves && i % 2 == 0 ? 1 : 0), 0, 2);
                arena.Alive.Add(SpawnGuardian(crypt, plan, (TreeSize)size, false, at));
            }
            Broadcast(crypt, $"Wave {wave}/{Waves}");
        }

        private void SpawnBoss(int crypt)
        {
            Arena arena = Get(crypt);
            CryptPlan plan = CryptWorld.Plans[crypt];
            arena.State = State.Boss;
            arena.Alive.Add(SpawnGuardian(crypt, plan, TreeSize.Large, true, plan.BossSpot));
            Broadcast(crypt, "Guardian");
        }

        private Treant SpawnGuardian(int crypt, CryptPlan plan, TreeSize size, bool boss, Vector3 at)
        {
            Treant treant = EnemyDirector.Instance.ServerSpawn(plan.Site.Zone, size, boss, at);
            // They head for the middle of the hall when nobody is in reach, so the fight finds the players.
            treant.ServerMakeGuardian(boss ? plan.BossSpot : plan.ArenaCenter, crypt);
            treant.ServerActivate();
            return treant;
        }

        // Server only, from Treant: an arena enemy has fallen.
        public void ServerOnArenaEnemyDown(Treant treant)
        {
            if (_arenas.TryGetValue(treant.Arena, out Arena arena)) arena.Alive.Remove(treant);
        }

        private void Victory(int crypt)
        {
            Arena arena = Get(crypt);
            CryptPlan plan = CryptWorld.Plans[crypt];
            arena.State = State.Cleared;
            Element element = plan.Site.Zone;
            foreach (PlayerHealth player in PlayersInArena(plan, standingOnly: false))
            {
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                if (inventory != null)
                {
                    inventory.ServerAddEssence(element, EssenceReward);
                    inventory.ServerAddRelics(IronReward, ShardReward);
                }
                // The boss also leaves fishing gear: the next rod or lure.
                PlayerFishing fishing = player.GetComponent<PlayerFishing>();
                if (fishing != null) fishing.ServerGrantGear();
            }
            Broadcast(crypt, string.Empty, cleared: true);
        }

        // Server only. Everyone left or fell: the fight starts over the next time someone comes down.
        public void ServerResetArena(int crypt)
        {
            Arena arena = Get(crypt);
            foreach (Treant t in arena.Alive) if (t != null && t.IsSpawned) t.ServerDespawn();
            arena.Alive.Clear();
            arena.State = State.Idle;
            arena.Wave = 0;
            arena.NextWaveAt = -1f;
            arena.EmptySince = -1f;
            Broadcast(crypt, string.Empty);
        }

        // ---------- Status for the players ----------

        private void Broadcast(int crypt, string text, bool cleared = false)
        {
            StatusObserversRpc(crypt, text, cleared || IsCleared(crypt));
        }

        [ObserversRpc(BufferLast = false)]
        private void StatusObserversRpc(int crypt, string text, bool cleared)
        {
            _status[crypt] = text;
            if (cleared) _clearedHere.Add(crypt);
            else _clearedHere.Remove(crypt);
        }

        // ---------- Saving and restarts ----------

        public List<int> ClearedArenas()
        {
            var list = new List<int>();
            foreach (KeyValuePair<int, Arena> pair in _arenas) if (pair.Value.State == State.Cleared) list.Add(pair.Key);
            return list;
        }

        public void ServerLoad(IEnumerable<int> cleared)
        {
            foreach (int crypt in new List<int>(_arenas.Keys)) ServerResetArena(crypt);
            if (cleared == null) return;
            foreach (int crypt in cleared)
            {
                if (crypt < 0 || crypt >= CryptWorld.Plans.Count) continue;
                Get(crypt).State = State.Cleared;
                Broadcast(crypt, string.Empty, cleared: true);
            }
        }

        // Server only. Game over: every arena is new again.
        public void ServerReset()
        {
            foreach (int crypt in new List<int>(_arenas.Keys)) ServerResetArena(crypt);
            _arenas.Clear();
        }

        // A player who joins later learns which arenas are already won.
        public override void OnSpawnServer(FishNet.Connection.NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            foreach (int crypt in ClearedArenas()) ClearedTargetRpc(connection, crypt);
        }

        [TargetRpc]
        private void ClearedTargetRpc(FishNet.Connection.NetworkConnection connection, int crypt) => _clearedHere.Add(crypt);
    }
}
