using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Chopper.Editor
{
    // Lighting and post processing for the World scene and cameras (called from MeadowBuilder).
    // - URP asset: longer shadow range, four cascades, soft shadows, MSAA.
    // - A global Volume with ACES tonemapping, bloom, color grading and a light vignette. DayNightCycle blends
    //   its exposure, tint and bloom between day and night at runtime.
    // - Trilight ambient (sky, horizon, ground) instead of flat skybox ambient, for more contrast.
    public static class LightingSetup
    {
        private const string RenderPipelineAssetPath = "Assets/Settings/PC_RPAsset.asset";
        private const string ProfilePath = "Assets/_Chopper/World/Chopper_Lighting.asset";

        private const float ShadowDistance = 140f;
        private const int ShadowResolution = 4096;
        private const int MsaaSamples = 4;

        public static readonly Color DayAmbientSky = new Color(0.62f, 0.68f, 0.78f);
        public static readonly Color DayAmbientEquator = new Color(0.68f, 0.69f, 0.60f);
        public static readonly Color DayAmbientGround = new Color(0.42f, 0.40f, 0.30f);

        public static void ConfigurePipeline()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RenderPipelineAssetPath);
            if (asset == null)
            {
                Debug.LogWarning($"[LightingSetup] Missing {RenderPipelineAssetPath}; pipeline settings not changed.");
                return;
            }

            asset.shadowDistance = ShadowDistance;
            asset.shadowCascadeCount = 4;
            asset.cascade4Split = new Vector3(0.06f, 0.18f, 0.45f);
            asset.mainLightShadowmapResolution = ShadowResolution;
            var serialized = new SerializedObject(asset);
            SerializedProperty soft = serialized.FindProperty("m_SoftShadowsSupported");
            if (soft != null) soft.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            asset.msaaSampleCount = MsaaSamples;
            asset.supportsHDR = true;
            EditorUtility.SetDirty(asset);
            ConfigureAmbientOcclusion();
        }

        // Contact shadows in creases (under trees, along walls, around rocks) give the world weight.
        private static void ConfigureAmbientOcclusion()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (renderer == null) return;
            foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
            {
                if (feature == null || feature.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                feature.SetActive(true);
                var serialized = new SerializedObject(feature);
                SerializedProperty settings = serialized.FindProperty("m_Settings");
                if (settings != null)
                {
                    SerializedProperty intensity = settings.FindPropertyRelative("Intensity");
                    SerializedProperty radius = settings.FindPropertyRelative("Radius");
                    if (intensity != null) intensity.floatValue = 0.9f;
                    if (radius != null) radius.floatValue = 0.4f;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(feature);
            }
            EditorUtility.SetDirty(renderer);
        }

        public static void ApplyToScene()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = DayAmbientSky;
            RenderSettings.ambientEquatorColor = DayAmbientEquator;
            RenderSettings.ambientGroundColor = DayAmbientGround;
            RenderSettings.ambientIntensity = 1f;
            // Low sky reflections: bright reflections on low poly surfaces are what made everything look like plastic.
            RenderSettings.reflectionIntensity = 0.2f;

            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = BuildProfile();
        }

        // Turns on post processing and anti aliasing for a camera created by script.
        public static void EnableCameraEffects(Camera camera)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.renderShadows = true;
        }

        private static VolumeProfile BuildProfile()
        {
            if (File.Exists(ProfilePath)) AssetDatabase.DeleteAsset(ProfilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var tonemapping = AddComponent<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var bloom = AddComponent<Bloom>(profile);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.6f);

            var color = AddComponent<ColorAdjustments>(profile);
            color.postExposure.Override(0.45f);
            color.contrast.Override(0f);
            color.saturation.Override(-8f);

            // Warm highlights; shadows stay neutral so dark browns do not turn maroon under the day desaturation.
            var smh = AddComponent<ShadowsMidtonesHighlights>(profile);
            smh.shadows.Override(new Vector4(1f, 1f, 1f, 0f));
            smh.highlights.Override(new Vector4(1.10f, 1.02f, 0.88f, 0.05f));

            var balance = AddComponent<WhiteBalance>(profile);
            balance.temperature.Override(6f);

            var grain = AddComponent<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.05f);
            grain.response.Override(0.8f);

            var vignette = AddComponent<Vignette>(profile);
            vignette.intensity.Override(0.25f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T AddComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
