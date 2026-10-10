using System.Collections.Generic;
using Chopper.Core;
using Chopper.Player;
using UnityEngine;

namespace Chopper.World
{
    // Client side ambient particles that follow the local player and give every zone its own living air.
    // Several layered particle systems run at once (glowing motes, ash, leaves, wind streaks, rain, mist, smoke), and
    // each zone and time of day sets how many of each and how they move:
    //   Hub   pollen drifting in the sun, a few petals, green fireflies at night
    //   Water drizzle, rolling mist banks, dew, cold blue-green wisps after dark
    //   Earth drifting spores, slow falling leaves, dappled golden light, glowing fireflies
    //   Fire  rising embers, falling ash, dark smoke, a dull red glow in the air
    //   Wind  streaming leaves, dust streaks racing along the gusts, dandelion seeds
    public class ZoneAtmosphere : MonoBehaviour
    {
        private const float HeightAbovePlayer = 4f;
        private const int LayerMaxParticles = 1400;

        [SerializeField] private Material _material;

        // How one layer behaves in one zone. Rate 0 (or a layer a zone does not list) means off.
        private struct Spec
        {
            public float Rate;
            public Color Color0, Color1;
            public float SizeMin, SizeMax, LifeMin, LifeMax, SpeedMin, SpeedMax, Gravity;
            public Vector3 Drift, DriftJitter;
            public float Spin;
            public float Noise;
            public bool Shrink;
            public Vector3 Box;
            public float Height;
            public float Stretch;
        }

        private class Layer
        {
            public ParticleSystem System;
            public ParticleSystemRenderer Renderer;
            // Small sprites (embers, leaves, motes) are drawn bigger and denser so they read at game distance.
            public float SizeBoost = 1f;
            public float RateBoost = 1f;
        }

        private readonly Dictionary<string, Layer> _layers = new Dictionary<string, Layer>();
        private Element _zone = (Element)(-1);
        private bool _night;
        private bool _ambientOn;

        private void Awake()
        {
            Texture2D soft = MakeSoft();
            Texture2D leaf = MakeLeaf();
            Texture2D flake = MakeFlake();
            CombatFx.BaseMaterial = _material;
            CombatFx.SoftTexture = soft;

            AddLayer("soft", soft, false);
            AddLayer("glow", soft, true);
            AddLayer("haze", soft, true);
            AddLayer("leaf", leaf, false);
            AddLayer("flake", flake, false);
            AddLayer("streak", soft, false);
            AddLayer("mist", soft, false);
            AddLayer("smoke", soft, false);
        }

        private void AddLayer(string name, Texture2D texture, bool additive)
        {
            var go = new GameObject("Layer_" + name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = LayerMaxParticles;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;

            // Fade in and out so nothing ever pops.
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.72f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var material = new Material(_material);
            material.SetTexture("_BaseMap", texture);
            material.mainTexture = texture;
            if (additive)
            {
                material.SetFloat("_Blend", 2f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            }
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            ps.Play();
            bool big = name == "mist" || name == "smoke" || name == "haze" || name == "streak";
            _layers[name] = new Layer { System = ps, Renderer = renderer, SizeBoost = big ? 1f : 2.2f, RateBoost = big ? 1f : 1.6f };
        }

        private void Update()
        {
            PlayerWood local = PlayerWood.Local;
            if (local == null) return;

            transform.position = local.transform.position + Vector3.up * HeightAbovePlayer;
            DayNightCycle cycle = DayNightCycle.Instance;
            bool night = cycle != null && cycle.Darkness > 0.5f;
            // No pollen, embers or snow inside a crypt.
            bool ambient = Chopper.UI.UserSettings.AmbientParticles && !Chopper.World.Generation.CryptWorld.IsUnderground(local.transform.position);
            if (ZoneTracker.Current != _zone || night != _night || ambient != _ambientOn)
            {
                _zone = ZoneTracker.Current;
                _night = night;
                _ambientOn = ambient;
                Configure(_zone, _night);
            }
        }

        // ---------- Layer setup ----------

        private void Configure(Element zone, bool night)
        {
            var specs = new Dictionary<string, Spec>();
            if (_ambientOn) switch (zone)
            {
                case Element.Fire: FireSpecs(specs, night); break;
                case Element.Water: WaterSpecs(specs, night); break;
                case Element.Earth: EarthSpecs(specs, night); break;
                case Element.Wind: WindSpecs(specs, night); break;
                default: HubSpecs(specs, night); break;
            }

            foreach (KeyValuePair<string, Layer> pair in _layers)
            {
                if (specs.TryGetValue(pair.Key, out Spec spec)) Apply(pair.Value, spec);
                else Silence(pair.Value);
            }
        }

        private static void Silence(Layer layer)
        {
            var emission = layer.System.emission;
            emission.rateOverTime = 0f;
        }

        private static void Apply(Layer layer, Spec s)
        {
            ParticleSystem ps = layer.System;
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(s.Color0, s.Color1);
            main.startSize = new ParticleSystem.MinMaxCurve(s.SizeMin * layer.SizeBoost, s.SizeMax * layer.SizeBoost);
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.LifeMin, s.LifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(s.SpeedMin, s.SpeedMax);
            main.gravityModifier = s.Gravity;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = ps.emission;
            emission.rateOverTime = s.Rate * layer.RateBoost;

            var shape = ps.shape;
            shape.scale = s.Box == Vector3.zero ? new Vector3(38f, 14f, 38f) : s.Box;
            ps.transform.localPosition = new Vector3(0f, s.Height, 0f);

            var velocity = ps.velocityOverLifetime;
            bool drifting = s.Drift != Vector3.zero || s.DriftJitter != Vector3.zero;
            velocity.enabled = drifting;
            if (drifting)
            {
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(s.Drift.x - s.DriftJitter.x, s.Drift.x + s.DriftJitter.x);
                velocity.y = new ParticleSystem.MinMaxCurve(s.Drift.y - s.DriftJitter.y, s.Drift.y + s.DriftJitter.y);
                velocity.z = new ParticleSystem.MinMaxCurve(s.Drift.z - s.DriftJitter.z, s.Drift.z + s.DriftJitter.z);
            }

            var noise = ps.noise;
            noise.enabled = s.Noise > 0f;
            if (s.Noise > 0f)
            {
                noise.strength = s.Noise;
                noise.frequency = 0.35f;
                noise.scrollSpeed = 0.3f;
                noise.damping = true;
            }

            var spin = ps.rotationOverLifetime;
            spin.enabled = s.Spin > 0f;
            if (s.Spin > 0f) spin.z = new ParticleSystem.MinMaxCurve(-s.Spin, s.Spin);

            var size = ps.sizeOverLifetime;
            size.enabled = s.Shrink;
            if (s.Shrink) size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 0.65f), new Keyframe(1f, 0.05f)));

            if (s.Stretch > 0f)
            {
                layer.Renderer.renderMode = ParticleSystemRenderMode.Stretch;
                layer.Renderer.lengthScale = s.Stretch;
                layer.Renderer.velocityScale = 0.04f;
                layer.Renderer.cameraVelocityScale = 0f;
            }
            else
            {
                layer.Renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }
        }

        private static Spec Make(float rate, Color c0, Color c1, float size0, float size1, float life0, float life1, float speed0, float speed1, float gravity = 0f)
        {
            return new Spec
            {
                Rate = rate, Color0 = c0, Color1 = c1, SizeMin = size0, SizeMax = size1, LifeMin = life0, LifeMax = life1,
                SpeedMin = speed0, SpeedMax = speed1, Gravity = gravity
            };
        }

        private static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

        private void HubSpecs(Dictionary<string, Spec> s, bool night)
        {
            if (!night)
            {
                Spec pollen = Make(10f, C(1f, 0.98f, 0.8f, 0.55f), C(0.9f, 1f, 0.8f, 0.4f), 0.03f, 0.07f, 7f, 11f, 0.05f, 0.25f, -0.005f);
                pollen.Noise = 0.8f;
                s["soft"] = pollen;
                Spec petals = Make(1.6f, C(1f, 0.86f, 0.9f), C(0.95f, 0.95f, 0.6f), 0.14f, 0.2f, 9f, 13f, 0.1f, 0.4f, 0.06f);
                petals.Spin = 2f; petals.Noise = 0.9f; petals.Height = 6f;
                s["leaf"] = petals;
            }
            else
            {
                Spec flies = Make(11f, C(1.6f, 2f, 0.5f, 0.9f), C(1.1f, 1.8f, 0.4f, 0.8f), 0.1f, 0.15f, 4f, 8f, 0.1f, 0.4f);
                flies.Noise = 1.2f; flies.Box = new Vector3(34f, 6f, 34f); flies.Height = -2f;
                s["glow"] = flies;
            }
        }

        private void WaterSpecs(Dictionary<string, Spec> s, bool night)
        {
            Spec mist = Make(night ? 9f : 7f, C(0.78f, 0.9f, 0.92f, night ? 0.1f : 0.16f), C(0.65f, 0.8f, 0.85f, 0.1f), 7f, 13f, 12f, 18f, 0.15f, 0.5f);
            mist.Drift = new Vector3(0.4f, 0f, 0.1f); mist.Box = new Vector3(46f, 3f, 46f); mist.Height = -3f;
            s["mist"] = mist;

            Spec drizzle = Make(night ? 140f : 95f, C(0.75f, 0.88f, 1f, 0.32f), C(0.65f, 0.8f, 0.95f, 0.22f), 0.035f, 0.055f, 0.9f, 1.2f, 0f, 0f);
            drizzle.Drift = new Vector3(0.6f, -13f, 0f); drizzle.Box = new Vector3(34f, 2f, 34f); drizzle.Height = 8f; drizzle.Stretch = 9f;
            s["streak"] = drizzle;

            Spec wisps = Make(night ? 18f : 3f, C(1.0f, 1.9f, 1.5f, 0.9f), C(0.6f, 1.7f, 1.7f, 0.8f), 0.09f, 0.17f, 5f, 9f, 0.1f, 0.4f);
            wisps.Noise = 1.3f; wisps.Box = new Vector3(34f, 6f, 34f); wisps.Height = -2f;
            s["glow"] = wisps;

            if (!night)
            {
                Spec dew = Make(10f, C(0.9f, 1f, 1f, 0.4f), C(0.85f, 0.95f, 1f, 0.3f), 0.03f, 0.06f, 6f, 10f, 0.05f, 0.2f);
                dew.Noise = 0.6f;
                s["soft"] = dew;
            }
        }

        private void EarthSpecs(Dictionary<string, Spec> s, bool night)
        {
            Spec spores = Make(night ? 26f : 14f, C(1.3f, 1.7f, 0.5f, 0.7f), C(0.9f, 1.4f, 0.4f, 0.6f), 0.05f, 0.11f, 7f, 12f, 0.05f, 0.3f, -0.01f);
            spores.Noise = 1f; spores.Box = new Vector3(34f, 8f, 34f); spores.Height = -2f;
            s["glow"] = spores;

            Spec leaves = Make(7f, C(0.55f, 0.4f, 0.2f), C(0.4f, 0.5f, 0.2f), 0.2f, 0.32f, 8f, 12f, 0.1f, 0.4f, 0.09f);
            leaves.Spin = 2.5f; leaves.Noise = 0.7f; leaves.Height = 6f;
            s["leaf"] = leaves;

            if (!night)
            {
                Spec dapples = Make(1.6f, C(1f, 0.85f, 0.4f, 0.08f), C(0.9f, 0.8f, 0.35f, 0.06f), 3f, 6f, 8f, 12f, 0.05f, 0.2f);
                dapples.Box = new Vector3(40f, 4f, 40f); dapples.Height = 0f;
                s["haze"] = dapples;
                Spec dust = Make(10f, C(1f, 0.92f, 0.6f, 0.4f), C(0.95f, 0.9f, 0.55f, 0.3f), 0.03f, 0.06f, 6f, 10f, 0.05f, 0.2f);
                dust.Noise = 0.7f;
                s["soft"] = dust;
            }
        }

        private void FireSpecs(Dictionary<string, Spec> s, bool night)
        {
            float boost = night ? 1.4f : 1f;
            Spec embers = Make(50f * boost, C(2.8f, 1f, 0.25f, 0.9f), C(1.8f, 0.4f, 0.1f, 0.8f), 0.06f, 0.15f, 3f, 6f, 0.4f, 1.4f, -0.07f);
            embers.Noise = 0.7f; embers.Shrink = true; embers.Box = new Vector3(36f, 12f, 36f); embers.Height = -2f;
            s["glow"] = embers;

            Spec ash = Make(26f, C(0.5f, 0.47f, 0.45f, 0.75f), C(0.22f, 0.2f, 0.19f, 0.6f), 0.07f, 0.17f, 7f, 11f, 0.1f, 0.4f, 0.04f);
            ash.Spin = 3f; ash.Noise = 0.5f; ash.Height = 4f;
            s["flake"] = ash;

            Spec smoke = Make(2.5f, C(0.14f, 0.12f, 0.12f, 0.26f), C(0.24f, 0.19f, 0.17f, 0.2f), 5f, 9f, 10f, 16f, 0.2f, 0.6f, -0.03f);
            smoke.Drift = new Vector3(0.6f, 0f, 0f); smoke.Box = new Vector3(50f, 4f, 50f); smoke.Height = -2f;
            s["smoke"] = smoke;

            Spec glow = Make(1.3f * boost, C(1f, 0.32f, 0.1f, 0.07f), C(1f, 0.45f, 0.12f, 0.05f), 8f, 14f, 8f, 12f, 0.05f, 0.2f);
            glow.Box = new Vector3(40f, 6f, 40f); glow.Height = -1f;
            s["haze"] = glow;
        }

        private void WindSpecs(Dictionary<string, Spec> s, bool night)
        {
            Spec leaves = Make(20f, C(0.95f, 0.8f, 0.35f), C(0.5f, 0.75f, 0.3f), 0.22f, 0.34f, 4f, 7f, 0.2f, 0.8f);
            leaves.Drift = new Vector3(7.5f, 0f, 0f); leaves.DriftJitter = new Vector3(1.5f, 1f, 1.5f); leaves.Spin = 6f; leaves.Noise = 0.5f;
            leaves.Box = new Vector3(40f, 8f, 40f); leaves.Height = -1f;
            s["leaf"] = leaves;

            Spec streaks = Make(34f, C(0.95f, 0.88f, 0.6f, 0.22f), C(0.9f, 0.9f, 0.75f, 0.16f), 0.04f, 0.055f, 1.2f, 2f, 0f, 0f);
            streaks.Drift = new Vector3(13f, 0f, 0f); streaks.DriftJitter = new Vector3(2f, 0.4f, 0.6f); streaks.Stretch = 14f;
            streaks.Box = new Vector3(40f, 8f, 40f); streaks.Height = -1f;
            s["streak"] = streaks;

            Spec seeds = Make(14f, C(1f, 1f, 0.95f, 0.8f), C(1f, 0.98f, 0.85f, 0.6f), 0.05f, 0.09f, 6f, 9f, 0.1f, 0.3f);
            seeds.Drift = new Vector3(2.5f, 0.4f, 0f); seeds.DriftJitter = new Vector3(1f, 0.4f, 0.8f); seeds.Noise = 1.4f;
            s["soft"] = seeds;

            if (night)
            {
                Spec flies = Make(6f, C(1.4f, 1.8f, 1.2f, 0.8f), C(1.2f, 1.6f, 1.6f, 0.7f), 0.08f, 0.13f, 5f, 9f, 0.2f, 0.6f);
                flies.Noise = 1.5f; flies.Box = new Vector3(34f, 6f, 34f); flies.Height = -2f;
                s["glow"] = flies;
            }
        }

        // ---------- Procedural particle textures ----------

        private static Texture2D MakeSoft()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
        }

        // A pointed leaf with a midrib, so falling and blowing leaves read as leaves and not as dots.
        private static Texture2D MakeLeaf()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float along = Mathf.Abs(v);
                    float halfWidth = 0.5f * Mathf.Sin((1f - along) * Mathf.PI * 0.5f) * (1f - along * 0.35f);
                    float inside = Mathf.Clamp01((halfWidth - Mathf.Abs(u)) * 14f);
                    float rib = Mathf.Abs(u) < 0.04f ? 0.72f : 1f;
                    byte shade = (byte)(255f * rib);
                    pixels[y * size + x] = new Color32(shade, shade, shade, (byte)(inside * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
        }

        // An irregular little flake for ash.
        private static Texture2D MakeFlake()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            var random = new System.Random(77);
            float[] radii = new float[12];
            for (int i = 0; i < radii.Length; i++) radii[i] = 0.55f + (float)random.NextDouble() * 0.4f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float angle = Mathf.Atan2(dy, dx) + Mathf.PI;
                    float edge = radii[Mathf.Min(radii.Length - 1, (int)(angle / (Mathf.PI * 2f) * radii.Length))];
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((edge - r) * 10f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
        }
    }
}
