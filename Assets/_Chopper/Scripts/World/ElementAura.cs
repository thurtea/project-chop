using Chopper.Core;
using UnityEngine;

namespace Chopper.World
{
    public class ElementAura : MonoBehaviour
    {
        private ParticleSystem _ps;
        private Light _light;
        private Element _shown = (Element)(-1);
        private System.Func<Element> _element;
        private System.Func<Color> _color;
        private float _lightBase;
        private float _seed;

        private void Awake() => _seed = Random.value * 100f;

        public static ElementAura Attach(GameObject host, System.Func<Element> element, System.Func<Color> color)
        {
            var aura = host.AddComponent<ElementAura>();
            aura._element = element;
            aura._color = color;
            return aura;
        }

        public bool Showing => _ps != null && _ps.gameObject.activeSelf;

        private Transform Anchor()
        {
            Animator animator = GetComponentInChildren<Animator>();
            Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            Transform search = hand != null ? hand : transform;
            Transform best = null;
            foreach (Transform t in search.GetComponentsInChildren<Transform>(true))
            {
                if (t == search) continue;
                string n = t.name.ToLowerInvariant();
                if (n.Contains("weapon") || n.Contains("spear") || n.Contains("sword") || n.Contains("mace") || n.Contains("blade") || n.Contains("axe")) { best = t; break; }
            }
            if (best != null) return best;
            if (hand != null) return hand;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n == "hand_r" || n.Contains("righthand") || n.Contains("hand.r")) return t;
            }
            return null;
        }

        private float _nextAnchorCheck;

        private void Reanchor()
        {
            if (_ps == null || Time.time < _nextAnchorCheck) return;
            _nextAnchorCheck = Time.time + 0.5f;
            Transform anchor = Anchor();
            if (anchor == null || _ps.transform.parent == anchor) return;
            _ps.transform.SetParent(anchor, false);
            _ps.transform.localPosition = Vector3.zero;
        }
        private void LateUpdate()
        {
            if (_element == null) return;
            Element e = _element();
            if (e != _shown) Build(e);
            Reanchor();
            if (_light != null && _light.enabled)
                _light.intensity = _lightBase * (0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 6f, _seed));
        }

        private void Build(Element e)
        {
            _shown = e;
            AuraStyle s = ElementAuraRules.Style(e);
            if (_ps == null)
            {
                Transform anchor = Anchor();
                var go = new GameObject("ElementAura");
                go.transform.SetParent(anchor != null ? anchor : transform, false);
                if (anchor == null) go.transform.localPosition = Vector3.up * 1.3f;
                _ps = go.AddComponent<ParticleSystem>();
                _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = CombatFx.BaseMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _light = go.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.range = 4.5f;
                _light.shadows = LightShadows.None;
            }
            _ps.gameObject.SetActive(s.On);
            if (!s.On) return;
            Color c = _color != null ? _color() : Color.white;
            var main = _ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.Life * 0.6f, s.Life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(s.Speed * 0.5f, s.Speed);
            main.startSize = new ParticleSystem.MinMaxCurve(s.Size * 1.1f, s.Size * 1.9f);
            main.gravityModifier = s.Gravity;
            main.maxParticles = 160;
            Color hot = s.Glow ? c * 1.8f : c;
            hot.a = 1f;
            main.startColor = new ParticleSystem.MinMaxGradient(hot, Color.Lerp(hot, Color.white, e == Element.Fire ? 0.5f : 0.25f));
            var emission = _ps.emission;
            emission.rateOverTime = s.Rate * 2.2f;
            var shape = _ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.28f;
            var vel = _ps.velocityOverLifetime;
            vel.enabled = s.Swirl > 0f;
            if (vel.enabled)
            {
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.orbitalY = new ParticleSystem.MinMaxCurve(s.Swirl);
                vel.x = vel.y = vel.z = new ParticleSystem.MinMaxCurve(0f);
            }
            var size = _ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, e == Element.Water ? 0.7f : 0.1f));
            var col = _ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(e == Element.Fire ? new Color(1f, 0.4f, 0.15f) : Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            _lightBase = s.LightIntensity;
            _light.enabled = s.LightIntensity > 0f;
            _light.color = c;
            _ps.Play();
        }
    }
}
