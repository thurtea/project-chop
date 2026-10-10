using Chopper.Audio;
using Chopper.Core;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Chopper.World
{
    // Fishing hotspots in the hub pond: spots where bubbles rise and fish jump now and then. A cast that lands in one
    // gets faster bites, rarer fish and more fishing experience. Each gives a few fish, then goes quiet and comes back
    // after a while; at night the fish go deep and the spots are inactive. The places come from the world seed on
    // every peer; the server owns how many fish each has left and its state, so a spot is never emptied twice.
    public class FishingHotspots : NetworkBehaviour
    {
        public enum SpotState { Active, Depleted, Respawning, Inactive }

        public const int SpotCount = 2;
        public const float Radius = 2.4f;
        private const float RespawningShowSeconds = 30f;
        private const float BubbleInterval = 0.35f;
        private const float StateInterval = 0.5f;
        private static readonly Color Bubble = new Color(0.85f, 0.93f, 1f, 0.8f);

        [SerializeField] private GameConfig _config;

        private readonly SyncList<int> _states = new SyncList<int>();
        // Server: fish left in each spot and when a quiet one comes back.
        private readonly int[] _left = new int[SpotCount];
        private readonly float[] _returnAt = new float[SpotCount];
        private Vector3[] _spots;
        private float _nextState;
        private float _nextBubble;
        private float _nextJump;

        public static FishingHotspots Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Where the spots are: inside the pond, apart from each other, the same on every peer.
        private bool EnsureSpots()
        {
            if (_spots != null) return true;
            Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
            if (gen == null) return false;
            var rng = new System.Random(Chopper.World.Generation.WorldSession.Seed ^ 0x5F15);
            float reach = Chopper.World.Generation.WorldGen.PondOuterRadius * 0.55f;
            _spots = new Vector3[SpotCount];
            for (int i = 0; i < SpotCount; i++)
            {
                Vector2 p = gen.PondCenter;
                for (int tries = 0; tries < 50; tries++)
                {
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float r = Mathf.Lerp(reach * 0.3f, reach, (float)rng.NextDouble());
                    p = gen.PondCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                    bool apart = true;
                    for (int j = 0; j < i; j++) if (Vector2.Distance(p, new Vector2(_spots[j].x, _spots[j].z)) < 7f) apart = false;
                    if (apart) break;
                }
                _spots[i] = new Vector3(p.x, Chopper.World.Generation.WorldGen.WaterHeight, p.y);
            }
            return true;
        }

        public Vector3 Position(int spot) => EnsureSpots() && spot >= 0 && spot < SpotCount ? _spots[spot] : Vector3.zero;

        public SpotState State(int spot) => spot >= 0 && spot < _states.Count ? (SpotState)_states[spot] : SpotState.Inactive;

        // The active spot a bobber at this position floats in, or -1.
        public int SpotAt(Vector3 position)
        {
            if (!EnsureSpots()) return -1;
            for (int i = 0; i < SpotCount; i++)
            {
                Vector3 d = position - _spots[i];
                d.y = 0f;
                if (d.magnitude <= Radius && State(i) == SpotState.Active) return i;
            }
            return -1;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _states.Clear();
            for (int i = 0; i < SpotCount; i++)
            {
                _left[i] = _config.HotspotCatches;
                _states.Add((int)SpotState.Active);
            }
        }

        // Server only. A fish came out of this spot; the last one quiets it.
        public void ServerConsume(int spot)
        {
            if (spot < 0 || spot >= SpotCount || State(spot) != SpotState.Active) return;
            _left[spot]--;
            if (_left[spot] > 0) return;
            _returnAt[spot] = Time.time + _config.HotspotRespawnSeconds;
            _states[spot] = (int)SpotState.Depleted;
        }

        // Server only, development: empties an active spot or fills a quiet one.
        public void ServerToggle(int spot)
        {
            if (spot < 0 || spot >= SpotCount) return;
            if (State(spot) == SpotState.Active)
            {
                _left[spot] = 1;
                ServerConsume(spot);
            }
            else
            {
                _left[spot] = _config.HotspotCatches;
                _states[spot] = (int)SpotState.Active;
            }
        }

        private void Update()
        {
            if (IsServerInitialized && Time.time >= _nextState) ServerUpdateStates();
            if (IsClientInitialized) ClientEffects();
        }

        private void ServerUpdateStates()
        {
            _nextState = Time.time + StateInterval;
            bool night = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight;
            for (int i = 0; i < SpotCount && i < _states.Count; i++)
            {
                SpotState next;
                if (_left[i] <= 0)
                {
                    if (Time.time >= _returnAt[i]) _left[i] = _config.HotspotCatches;
                    next = _left[i] > 0 ? SpotState.Active : (_returnAt[i] - Time.time <= RespawningShowSeconds ? SpotState.Respawning : SpotState.Depleted);
                }
                else
                {
                    next = SpotState.Active;
                }
                // The fish go deep at night: an active spot rests until dawn.
                if (night && next == SpotState.Active) next = SpotState.Inactive;
                if (_states[i] != (int)next) _states[i] = (int)next;
            }
        }

        // Bubbles rise over an active spot and a fish jumps now and then; a spot about to come back stirs a little.
        private void ClientEffects()
        {
            if (Time.time < _nextBubble || !EnsureSpots()) return;
            _nextBubble = Time.time + BubbleInterval;
            bool jump = Time.time >= _nextJump;
            if (jump) _nextJump = Time.time + Random.Range(3f, 6f);
            Chopper.Player.PlayerWood local = Chopper.Player.PlayerWood.Local;
            for (int i = 0; i < SpotCount; i++)
            {
                SpotState state = State(i);
                if (state == SpotState.Depleted || state == SpotState.Inactive) continue;
                if (local != null && (local.transform.position - _spots[i]).sqrMagnitude > 90f * 90f) continue;
                if (state == SpotState.Respawning && Random.value > 0.3f) continue;
                Vector2 jitter = Random.insideUnitCircle * Radius * 0.8f;
                Vector3 at = _spots[i] + new Vector3(jitter.x, 0.05f, jitter.y);
                CombatFx.Burst(at, Bubble, state == SpotState.Active ? 4 : 2, 0.6f, 0.07f, 0.8f, -0.25f);
                if (jump && state == SpotState.Active && i == Mathf.FloorToInt(Time.time) % SpotCount)
                {
                    CombatFx.Burst(_spots[i] + new Vector3(jitter.y, 0.1f, jitter.x), new Color(0.75f, 0.85f, 0.95f, 0.9f), 12, 2.6f, 0.09f, 0.7f, 2.2f);
                    Sfx.Play(ProceduralSfx.Splash, _spots[i], 0.35f, 1.3f, 0.1f);
                }
            }
        }
    }
}
