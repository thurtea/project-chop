using System;
using Chopper.Core;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace Chopper.World
{
    // Day and night (plan T23). The server owns the clock and re-sends it once a second; clients keep a local
    // copy that runs between updates. The hub is safe by day; the night phase is when enemies wake up.
    public class DayNightCycle : NetworkBehaviour
    {
        private const float SyncInterval = 1f;
        private const float SunLowPitch = 30f;
        private const float SunHighPitch = 46f;
        private const float SunStartYaw = -70f;
        private const float SunEndYaw = 110f;
        private const float MoonPitch = 42f;
        private const float MoonYaw = 290f;
        private const float NoonIntensity = 1.45f;
        private const float LowSunIntensity = 1.1f;
        private const float LowSunSpan = 0.75f;
        private const float MoonIntensity = 1.0f;
        private const float NightSkyExposure = 0.08f;
        private const float DayExposure = 0.20f;
        private const float NightExposure = 0.15f;
        // Valheim grade: muted, desaturated colours.
        private const float DaySaturation = -34f;
        private const float NightSaturation = -18f;
        // W5-02b Valheim grade (was 0.22): a softer glow on the sunlit side.
        private const float DayBloom = 0.30f;
        private const float NightBloom = 0.85f;
        // W5-02b Valheim grade (was 1, 0.66, 0.42): a warmer, more golden low sun.
        private static readonly Color SunriseColor = new Color(1f, 0.60f, 0.34f);
        // W5-02b concept 1 look (was 1, 0.93, 0.80): a warmer sun.
        private static readonly Color NoonColor = new Color(1f, 0.87f, 0.68f);
        private static readonly Color MoonColor = new Color(0.50f, 0.62f, 1f);
        private static readonly Color NightFilter = new Color(0.66f, 0.76f, 1f);
        // W11-08: daylight fill follows the warm sky. Night stays blue below.
        private static readonly Color LightingSky = new Color(0.72f, 0.66f, 0.56f);
        private static readonly Color LightingEquator = new Color(0.70f, 0.62f, 0.50f);
        private static readonly Color LightingGround = new Color(0.42f, 0.40f, 0.30f);
        private static readonly Color NightSky = new Color(0.26f, 0.34f, 0.58f);
        private static readonly Color NightEquator = new Color(0.20f, 0.26f, 0.42f);
        private static readonly Color NightGround = new Color(0.10f, 0.12f, 0.20f);
        // Underground (crypts and mines): no sun or moon. Darker and warmer than the old grey fill.
        private static readonly Color CryptSky = new Color(0.18f, 0.10f, 0.06f);
        private static readonly Color CryptEquator = new Color(0.13f, 0.07f, 0.04f);
        private static readonly Color CryptGround = new Color(0.05f, 0.03f, 0.02f);
        private const float CryptExposure = 0.62f;
        private const float UndergroundBlendSpeed = 3f;

        public static DayNightCycle Instance { get; private set; }

        // Server only. Raised on every phase change.
        public static event Action<DayPhase> ServerPhaseChanged;

        [SerializeField] private GameConfig _config;

        private DayNightClock _clock;
        private float _nextSyncTime;
        private Light _sun;
        private Material _skyRuntime;
        private ColorAdjustments _colorAdjustments;
        private Bloom _bloom;
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int AtmosphereId = Shader.PropertyToID("_AtmosphereThickness");
        private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
        private static readonly int GroundColorId = Shader.PropertyToID("_GroundColor");
        private static readonly int SunSizeId = Shader.PropertyToID("_SunSize");
        // Low sun: a golden haze in the air and warmer fill light; high sun: clear, faintly cool air.
        private static readonly Color LowSunHaze = new Color(1.08f, 0.80f, 0.60f);
        private static readonly Color NoonHaze = new Color(1.04f, 0.96f, 0.84f);
        private static readonly Color LowSunEquator = new Color(0.80f, 0.64f, 0.50f);
        // Below the horizon the sky shows the color of the open sea, so the fogged sea and the sky meet without a seam.
        private static readonly Color SeaHorizon = new Color(0.30f, 0.38f, 0.40f);
        private static readonly Color OvercastSky = new Color(0.5f, 0.5f, 0.5f);

        // Multiplies the zone fog color (ZoneTracker), so the air carries the time of day.
        public Color HazeTint { get; private set; } = Color.white;
        // 0 on the surface, 1 inside a crypt (for the local player), eased so the change is not a hard cut.
        public static float UndergroundBlend { get; private set; }

        public DayPhase Phase => _clock.Phase;
        public int Day => _clock.Day;
        public bool IsNight => _clock.Phase == DayPhase.Night;
        public float Darkness => _clock.Darkness;
        public float WorldDays => _clock.WorldDays;
        public float Remaining => _clock.Remaining;

        private void Awake()
        {
            Instance = this;
            _clock = new DayNightClock(_config.DayDuration, _config.DuskDuration, _config.NightDuration, _config.DawnDuration);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_skyRuntime != null) Destroy(_skyRuntime);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _clock.Restart();
            _clock.PhaseChanged += OnServerClockPhaseChanged;
            SendSync();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _clock.PhaseChanged -= OnServerClockPhaseChanged;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _sun = RenderSettings.sun;
            FindVolume();
            if (RenderSettings.skybox != null)
            {
                _skyRuntime = new Material(RenderSettings.skybox);
                RenderSettings.skybox = _skyRuntime;
            }
        }

        // Server only. Jumps to the start of the next phase.
        public void ServerSkipPhase()
        {
            _clock.SkipPhase();
        }

        // Server only. Everyone is in bed: the clock runs on to the start of dawn, through the night if it is still dusk,
        // so every phase change (the night's start and its end at dawn, with its save) happens as usual.
        public void ServerSleepToMorning()
        {
            if (!SleepRules.CanSleep(_clock.Phase)) return;
            int guard = 0;
            while (_clock.Phase != DayPhase.Dawn && guard++ < 4) _clock.SkipPhase();
            SendSync();
        }

        // Server only. Development and screenshots: jumps the clock to a fraction of a phase and tells the clients.
        public void ServerDebugSetClock(DayPhase phase, float fraction)
        {
            _clock.Set(phase, _clock.Day, Mathf.Clamp01(fraction) * _clock.PhaseDuration(phase));
            SendSync();
        }

        // Seconds already gone in the current phase (for saving).
        public float PhaseElapsed => _clock.Elapsed;

        // Server only. The practice yard holds the clock still (it stays day); every client follows the server's sync.
        public bool ServerFrozen { get; set; }

        // Server only. Loading a saved world: puts the clock where the save left it and tells the clients.
        public void ServerLoad(DayPhase phase, int day, float elapsed)
        {
            _clock.Set(phase, Mathf.Max(1, day), Mathf.Clamp(elapsed, 0f, _clock.PhaseDuration(phase)));
            SendSync();
        }

        // Server only. Used by the game over restart.
        public void ServerRestart()
        {
            _clock.Restart();
            SendSync();
            ServerPhaseChanged?.Invoke(DayPhase.Day);
        }

        private void OnServerClockPhaseChanged(DayPhase phase)
        {
            SendSync();
            ServerPhaseChanged?.Invoke(phase);
        }

        private void Update()
        {
            if (_clock == null) return;

            if (IsServerInitialized)
            {
                if (!ServerFrozen) _clock.Advance(Time.deltaTime);
                if (Time.time >= _nextSyncTime) SendSync();
            }
            else
            {
                _clock.Advance(Time.deltaTime);
            }

            ApplyLighting();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsClientInitialized && Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame) DebugSkipServerRpc();
#endif
        }

        private void SendSync()
        {
            _nextSyncTime = Time.time + SyncInterval;
            SyncClockObserversRpc((int)_clock.Phase, _clock.Day, _clock.Elapsed);
        }

        // BufferLast keeps the most recent state for players who join later.
        [ObserversRpc(BufferLast = true)]
        private void SyncClockObserversRpc(int phase, int day, float elapsed)
        {
            if (IsServerInitialized) return;
            _clock.Set((DayPhase)phase, day, elapsed);
        }

        [ServerRpc(RequireOwnership = false)]
        private void DebugSkipServerRpc()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _clock.SkipPhase();
            Debug.Log($"[DayNight] Debug skip to {_clock.Phase}.");
#endif
        }

        private void ApplyLighting()
        {
            if (!IsClientInitialized) return;
            Chopper.Player.PlayerWood local = Chopper.Player.PlayerWood.Local;
            bool below = local != null && Chopper.World.Generation.CryptWorld.IsUnderground(local.transform.position);
            UndergroundBlend = Mathf.MoveTowards(UndergroundBlend, below ? 1f : 0f, UndergroundBlendSpeed * Time.deltaTime);
            float u = UndergroundBlend;
            // Night tinting is a surface thing; inside a crypt it is just dark stone and lantern light.
            float d = _clock.Darkness * (1f - u);
            if (_sun == null) _sun = RenderSettings.sun;
            // Cloud and rain dim the sun, the light and the sky (WeatherSystem; it reads clear weather in a crypt).
            WeatherLook weather = WeatherSystem.Look;

            // Sun arcs across the sky during the day (low at dawn and dusk, high at noon). At night the same
            // light becomes a dim blue moon on the opposite side, blended by darkness so nothing jumps.
            float dayT = _clock.Phase == DayPhase.Day ? _clock.Elapsed / Mathf.Max(0.01f, _clock.PhaseDuration(DayPhase.Day))
                : (_clock.Phase == DayPhase.Dawn ? 0f : 1f);
            float elevation = Mathf.Sin(dayT * Mathf.PI);
            float sunPitch = Mathf.Lerp(SunLowPitch, SunHighPitch, elevation);
            float sunYaw = Mathf.Lerp(SunStartYaw, SunEndYaw, dayT);

            if (_sun != null)
            {
                Color sunColor = Color.Lerp(SunriseColor, NoonColor, elevation);
                _sun.color = Color.Lerp(sunColor, MoonColor, d);
                _sun.intensity = Mathf.Lerp(Mathf.Lerp(LowSunIntensity, NoonIntensity, elevation), MoonIntensity, d) * (1f - u) * weather.Sun;
                _sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(sunPitch, MoonPitch, d), Mathf.Lerp(sunYaw, MoonYaw, d), 0f);
            }

            // How low the sun is, 1 at sunrise and sunset, 0 around midday.
            float lowSun = 1f - Mathf.SmoothStep(0f, 1f, elevation / LowSunSpan);
            HazeTint = Color.Lerp(Color.Lerp(NoonHaze, LowSunHaze, lowSun), Color.white, d);

            RenderSettings.ambientSkyColor = Color.Lerp(Color.Lerp(LightingSky, NightSky, d), CryptSky, u) * weather.Ambient;
            RenderSettings.ambientEquatorColor = Color.Lerp(Color.Lerp(Color.Lerp(LightingEquator, LowSunEquator, lowSun * 0.6f), NightEquator, d), CryptEquator, u) * weather.Ambient;
            RenderSettings.ambientGroundColor = Color.Lerp(Color.Lerp(LightingGround, NightGround, d), CryptGround, u) * weather.Ambient;
            if (_skyRuntime != null)
            {
                if (_skyRuntime.HasProperty(ExposureId)) _skyRuntime.SetFloat(ExposureId, Mathf.Lerp(1.3f, NightSkyExposure, d) * weather.Sky);
                // A thicker atmosphere at a low sun paints the long orange horizon of morning and evening.
                // W11-08: a thicker, warmer noon sky so the horizon is pale gold, not hard blue.
                if (_skyRuntime.HasProperty(AtmosphereId)) _skyRuntime.SetFloat(AtmosphereId, Mathf.Lerp(1.45f, 1.75f, lowSun));
                // An overcast sky loses its blue and its warm low sun tint and turns flat grey.
                Color tint = Color.Lerp(new Color(0.64f, 0.56f, 0.46f), new Color(0.62f, 0.48f, 0.36f), lowSun);
                if (_skyRuntime.HasProperty(SkyTintId)) _skyRuntime.SetColor(SkyTintId, Color.Lerp(tint, OvercastSky, weather.Overcast));
                if (_skyRuntime.HasProperty(GroundColorId)) _skyRuntime.SetColor(GroundColorId, Color.Lerp(SeaHorizon, SeaHorizon * 0.15f, d));
                // The visible sun is the block disc (BlockSky). This disc in the skybox stays a small glow behind it.
                if (_skyRuntime.HasProperty(SunSizeId)) _skyRuntime.SetFloat(SunSizeId, 0.012f);
            }

            if (_colorAdjustments != null)
            {
                _colorAdjustments.postExposure.value = Mathf.Lerp(Mathf.Lerp(DayExposure, NightExposure, d), CryptExposure, u);
                _colorAdjustments.colorFilter.value = Color.Lerp(Color.white, NightFilter, d);
                _colorAdjustments.saturation.value = Mathf.Lerp(DaySaturation, NightSaturation, d) + weather.Saturation;
            }
            if (_bloom != null) _bloom.intensity.value = Mathf.Lerp(DayBloom, NightBloom, d);
        }

        private void FindVolume()
        {
            Volume volume = FindAnyObjectByType<Volume>();
            if (volume == null) return;
            // Reading profile gives this scene its own copy, so the shared asset is never changed at runtime.
            volume.profile.TryGet(out _colorAdjustments);
            volume.profile.TryGet(out _bloom);
        }
    }
}
