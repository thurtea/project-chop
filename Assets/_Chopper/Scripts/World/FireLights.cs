using System.Collections.Generic;
using Chopper.Core;
using UnityEngine;

namespace Chopper.World
{
    public class FireLights : MonoBehaviour
    {
        private const float ScanInterval = 2f;
        private const float GlowScale = 0.32f;

        private sealed class Fire
        {
            public Light Light;
            public float Base;
            public float BaseRange;
            public float Seed;
            public Transform Glow;
            public Renderer GlowRenderer;
        }

        public static FireLights Instance { get; private set; }
        public static int Count => Instance != null ? Instance._fires.Count : 0;
        public static int GlowCount { get; private set; }

        private readonly List<Fire> _fires = new List<Fire>();
        private readonly HashSet<Light> _known = new HashSet<Light>();
        private float _nextScan;
        private Material _glowMaterial;
        private Mesh _quad;
        private MaterialPropertyBlock _block;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        public static bool IsFire(Light light)
        {
            if (light == null || light.type != LightType.Point) return false;
            if (light.GetComponentInParent<Chopper.Player.HeldTorch>() != null || light.GetComponentInParent<ElementAura>() != null) return false;
            Color c = light.color;
            return c.r > 0.85f && c.g < 0.85f && c.b < 0.55f && c.r > c.b * 1.6f;
        }

        private void Awake()
        {
            Instance = this;
            _block = new MaterialPropertyBlock();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Scan()
        {
            _fires.RemoveAll(f => f.Light == null);
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (_known.Contains(light) || !IsFire(light)) continue;
                _known.Add(light);
                var fire = new Fire { Light = light, Base = light.intensity, BaseRange = light.range, Seed = Random.value * 100f };
                MakeGlow(fire);
                _fires.Add(fire);
            }
            GlowCount = 0;
            foreach (Fire f in _fires) if (f.Glow != null) GlowCount++;
        }

        private void MakeGlow(Fire fire)
        {
            if (_glowMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) return;
                _glowMaterial = new Material(shader) { name = "FireGlow", renderQueue = 3100 };
                _glowMaterial.SetFloat("_Surface", 1f);
                _glowMaterial.SetFloat("_Blend", 2f);
                _glowMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                _glowMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                _glowMaterial.SetFloat("_ZWrite", 0f);
                _glowMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _glowMaterial.SetTexture("_BaseMap", RadialTexture());
                _quad = BuildQuad();
            }
            var go = new GameObject("FireGlow");
            go.transform.SetParent(fire.Light.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _glowMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.transform.localScale = Vector3.one * fire.BaseRange * GlowScale;
            fire.Glow = go.transform;
            fire.GlowRenderer = mr;
        }

        private static Texture2D RadialTexture()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FireGlowTex" };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * a;
                    px[y * size + x] = new Color(a, a, a, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static Mesh BuildQuad()
        {
            var m = new Mesh { name = "FireGlowQuad" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + ScanInterval;
                Scan();
            }
            Camera cam = Camera.main;
            float darkness = DayNightCycle.Instance != null ? Mathf.Clamp01(1f - RenderSettings.ambientSkyColor.grayscale * 2.2f) : 0.5f;
            float t = Time.time;
            foreach (Fire f in _fires)
            {
                if (f.Light == null) continue;
                float a = Mathf.PerlinNoise(t * 7f, f.Seed);
                float b = Mathf.PerlinNoise(t * 19f, f.Seed + 7f);
                bool on = f.Light.enabled && f.Light.gameObject.activeInHierarchy;
                f.Light.intensity = LightGrade.Flicker(f.Base, a, b);
                f.Light.range = f.BaseRange * (0.95f + 0.08f * a);
                if (f.Glow == null) continue;
                f.GlowRenderer.enabled = on;
                if (!on) continue;
                if (cam != null) f.Glow.rotation = cam.transform.rotation;
                float alpha = Mathf.Lerp(0.06f, 0.28f, darkness) * (0.85f + 0.3f * a);
                Color c = f.Light.color * alpha;
                f.GlowRenderer.GetPropertyBlock(_block);
                _block.SetColor(ColorId, new Color(c.r, c.g, c.b, alpha));
                f.GlowRenderer.SetPropertyBlock(_block);
            }
        }
    }
}