using UnityEngine;

namespace Chopper.World
{
    // Small one-shot particle bursts shared by combat, rolling, splashes and footsteps: bark chips, sparks, dust and
    // water droplets. Two particle systems (normal and glowing) are created on first use and reused. The material
    // comes from ZoneAtmosphere, which owns the particle shader reference.
    public static class CombatFx
    {
        private static ParticleSystem _soft;
        private static ParticleSystem _glow;

        public static Material BaseMaterial { get; set; }
        public static Texture SoftTexture { get; set; }

        private static ParticleSystem Build(string name, bool additive)
        {
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 800;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (BaseMaterial != null)
            {
                var material = new Material(BaseMaterial);
                if (SoftTexture != null)
                {
                    material.SetTexture("_BaseMap", SoftTexture);
                    material.mainTexture = SoftTexture;
                }
                if (additive)
                {
                    material.SetFloat("_Blend", 2f);
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                }
                renderer.sharedMaterial = material;
            }
            return ps;
        }

        // A burst of count particles at position. Directions spread in a cone around up; speed and size in meters.
        public static void Burst(Vector3 position, Color color, int count, float speed, float size, float life, float gravity, bool glow = false, Vector3? bias = null)
        {
            if (count <= 0 || BaseMaterial == null) return;
            ParticleSystem ps;
            if (glow) ps = _glow != null ? _glow : (_glow = Build("FxGlow", true));
            else ps = _soft != null ? _soft : (_soft = Build("FxSoft", false));

            var main = ps.main;
            main.gravityModifier = gravity;
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 0.9f + 0.1f;
                if (bias.HasValue) direction = (direction + bias.Value).normalized;
                var emit = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = direction * speed * Random.Range(0.45f, 1f),
                    startColor = color,
                    startSize = size * Random.Range(0.6f, 1.2f),
                    startLifetime = life * Random.Range(0.7f, 1.2f)
                };
                ps.Emit(emit, 1);
            }
        }

        public static void Chips(Vector3 position, Color color) => Burst(position, color, 9, 4.2f, 0.11f, 0.7f, 1.6f);

        public static void Sparks(Vector3 position, Color color, int count = 10) => Burst(position, color, count, 5.5f, 0.09f, 0.45f, 0.6f, true);

        public static void Dust(Vector3 position, Color color, int count = 6) => Burst(position, color, count, 1.6f, 0.32f, 0.9f, -0.05f);

        public static void Splash(Vector3 position, float strength)
        {
            int count = Mathf.RoundToInt(Mathf.Lerp(5f, 26f, Mathf.Clamp01(strength)));
            Burst(position, new Color(0.82f, 0.93f, 1f, 0.85f), count, Mathf.Lerp(2.2f, 5f, strength), 0.12f, 0.75f, 1.4f);
            Burst(position, new Color(1f, 1f, 1f, 0.5f), Mathf.Max(2, count / 3), 0.9f, 0.5f, 0.9f, 0f);
        }
    }
}
