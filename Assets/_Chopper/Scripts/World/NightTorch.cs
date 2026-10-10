using UnityEngine;

namespace Chopper.World
{
    // A torch post around the hut: it lights itself when evening comes and goes out at dawn, with a gentle flicker.
    // Purely visual, so every client runs it from its own clock.
    public class NightTorch : MonoBehaviour
    {
        private const float LightAbove = 0.35f;
        private const float FlickerSpeed = 7f;

        [SerializeField] private Light _light;
        [SerializeField] private GameObject _flame;
        [SerializeField] private float _intensity = 9f;

        private float _seed;

        private void Awake()
        {
            _seed = Random.value * 100f;
            SetLit(false);
        }

        private void Update()
        {
            DayNightCycle cycle = DayNightCycle.Instance;
            bool lit = cycle != null && cycle.Darkness > LightAbove;
            SetLit(lit);
            if (!lit || _light == null) return;
            float flicker = Mathf.PerlinNoise(Time.time * FlickerSpeed, _seed);
            _light.intensity = _intensity * Mathf.Lerp(0.8f, 1.1f, flicker);
        }

        private void SetLit(bool lit)
        {
            if (_light != null && _light.enabled != lit) _light.enabled = lit;
            if (_flame != null && _flame.activeSelf != lit) _flame.SetActive(lit);
        }
    }
}
