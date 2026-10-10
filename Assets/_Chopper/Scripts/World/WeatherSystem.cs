using Chopper.Core;
using Chopper.Player;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Chopper.World
{
    // Sun, cloud and rain. The server picks the weather spell by spell (WeatherRules); every client eases its look in
    // over WeatherRules.BlendSeconds: DayNightCycle dims the sun, the light and the sky by Look, ZoneTracker thickens
    // and greys the fog, rain falls around the local player, and the wind (the grass sway and the rain's slant) picks up
    // with worse weather. Roofs keep the rain off: drops stop on anything solid, and nobody under a roof gets wet
    // (Sheltered). Crypts are left as they are.
    public class WeatherSystem : NetworkBehaviour
    {
        private const float RainHeight = 14f;
        private const float RainBox = 36f;
        private const float RainPerSecond = 2600f;
        private const int RainMaxParticles = 5000;
        private static readonly Color RainColor = new Color(0.75f, 0.8f, 0.88f, 0.55f);
        private static readonly int WindId = Shader.PropertyToID("_ChopperWind");
        // How far up a roof is looked for, from the top of the head.
        private const float ShelterReach = 40f;
        private const float HeadHeight = 1.9f;
        private static readonly RaycastHit[] RoofHits = new RaycastHit[8];

        private readonly SyncVar<int> _kind = new SyncVar<int>();
        private readonly System.Random _rng = new System.Random();
        private float _nextChange;
        private ParticleSystem _rain;
        private WeatherLook _look = WeatherRules.Look(WeatherKind.Sunny);

        public static WeatherSystem Instance { get; private set; }

        public WeatherKind Kind => (WeatherKind)_kind.Value;

        // The look right now, eased toward the current weather (sunny before the world is up).
        public static WeatherLook Look { get; private set; } = WeatherRules.Look(WeatherKind.Sunny);

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Look = WeatherRules.Look(WeatherKind.Sunny);
            if (_rain != null) Destroy(_rain.gameObject);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _kind.Value = (int)WeatherKind.Sunny;
            _nextChange = Time.time + WeatherRules.Duration(WeatherKind.Sunny, _rng.NextDouble());
        }

        // Server only. Sets the weather now (developer console, tests); the next change comes after a full spell.
        public void ServerSet(WeatherKind kind)
        {
            _kind.Value = (int)kind;
            _nextChange = Time.time + WeatherRules.Duration(kind, _rng.NextDouble());
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Development: jump straight to the current weather's look instead of easing (screenshot tours).
        public void DebugSnapLook() => _look = WeatherRules.Look(Kind);
#endif

        private void Update()
        {
            if (IsServerInitialized && Time.time >= _nextChange)
            {
                WeatherKind next = WeatherRules.Next(Kind, _rng.NextDouble());
                _kind.Value = (int)next;
                _nextChange = Time.time + WeatherRules.Duration(next, _rng.NextDouble());
            }
            if (!IsClientInitialized) return;

            float ease = Mathf.Clamp01(Time.deltaTime / WeatherRules.BlendSeconds * 3f);
            _look = WeatherLook.Lerp(_look, WeatherRules.Look(Kind), ease);
            PlayerWood local = PlayerWood.Local;
            bool below = local != null && Chopper.World.Generation.CryptWorld.IsUnderground(local.transform.position);
            Look = below ? WeatherRules.Look(WeatherKind.Sunny) : _look;
            Shader.SetGlobalFloat(WindId, Look.Wind);
            UpdateRain(local, below ? 0f : _look.Rain, Look.Wind);
        }

        // True when something solid is overhead (the hut's roof, a built roof, a crypt ceiling): rain does not wet
        // whoever stands there. The player's own colliders do not count.
        public static bool Sheltered(Vector3 feet, Transform self)
        {
            int count = Physics.RaycastNonAlloc(feet + Vector3.up * HeadHeight, Vector3.up, RoofHits, ShelterReach, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = RoofHits[i].collider;
                if (hit == null || hit is CharacterController) continue;
                if (self != null && hit.transform.IsChildOf(self)) continue;
                return true;
            }
            return false;
        }

        private void UpdateRain(PlayerWood local, float strength, float wind)
        {
            if (strength < 0.02f || local == null)
            {
                if (_rain != null && _rain.isEmitting) _rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }
            if (_rain == null) BuildRain();
            if (_rain == null) return;
            _rain.transform.position = local.transform.position + Vector3.up * RainHeight;
            var emission = _rain.emission;
            emission.rateOverTime = RainPerSecond * strength;
            // Slanted along the wind, harder the stronger it blows.
            Vector2 dir = WeatherRules.WindDirection;
            var velocity = _rain.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(dir.x * 0.6f * wind, dir.x * 1.2f * wind);
            velocity.z = new ParticleSystem.MinMaxCurve(dir.y * 0.6f * wind, dir.y * 1.2f * wind);
            if (!_rain.isEmitting) _rain.Play();
        }

        // Long thin streaks falling fast in a box over the player, drawn with the shared soft particle material.
        private void BuildRain()
        {
            if (CombatFx.BaseMaterial == null) return;
            var go = new GameObject("Rain");
            _rain = go.AddComponent<ParticleSystem>();
            _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _rain.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = RainMaxParticles;
            main.startLifetime = 1.1f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.04f);
            main.startColor = RainColor;
            main.gravityModifier = 0f;
            var velocity = _rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-1.2f, -0.6f);
            velocity.y = new ParticleSystem.MinMaxCurve(-17f, -14f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            // Drops stop on roofs, pieces and the ground, so none fall through into a room. Exact collision: the cheap
            // voxel qualities let fast drops slip through a thin roof (measured in the hut). Static colliders only.
            var collision = _rain.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.High;
            collision.enableDynamicColliders = false;
            collision.lifetimeLoss = 1f;
            collision.bounce = 0f;
            var shape = _rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(RainBox, 0.5f, RainBox);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.045f;
            renderer.lengthScale = 1f;
            var material = new Material(CombatFx.BaseMaterial);
            if (CombatFx.SoftTexture != null)
            {
                material.SetTexture("_BaseMap", CombatFx.SoftTexture);
                material.mainTexture = CombatFx.SoftTexture;
            }
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
