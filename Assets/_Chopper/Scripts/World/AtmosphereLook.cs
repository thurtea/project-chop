using Chopper.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Chopper.World
{
    public class AtmosphereLook : MonoBehaviour
    {
        public static bool Enabled = false;

        private Volume _volume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<AtmosphereLook>() != null) return;
            var go = new GameObject("AtmosphereLook");
            DontDestroyOnLoad(go);
            go.AddComponent<AtmosphereLook>();
        }

        private void Awake()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            SplitToning split = profile.Add<SplitToning>(true);
            split.shadows.Override(AtmosphereRules.ShadowTone);
            split.highlights.Override(AtmosphereRules.HighlightTone);
            split.balance.Override(AtmosphereRules.Balance);
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 20f;
            _volume.sharedProfile = profile;
            _volume.weight = 0f;
        }

        private void LateUpdate()
        {
            _volume.weight = Enabled ? AtmosphereRules.GradeWeight : 0f;
            if (!Enabled || ZoneTracker.FogEnabled) return;
            Camera cam = Camera.main;
            float far = cam != null ? cam.farClipPlane : 1000f;
            DayNightCycle cycle = DayNightCycle.Instance;
            float darkness = cycle != null ? cycle.Darkness : 0f;
            Color day = cycle != null ? cycle.HazeTint : Color.white;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = AtmosphereRules.HazeStart;
            RenderSettings.fogEndDistance = AtmosphereRules.LinearEnd(far);
            RenderSettings.fogColor = AtmosphereRules.HazeTint(day, darkness);
        }
    }
}