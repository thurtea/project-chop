using System.Collections.Generic;
using UnityEngine;

namespace Chopper.World
{
    // A campfire by the hut or the pond: always burning, it lights the ground around it and dries anyone standing
    // close (WetRules.FireDryRate). Purely local, like the night torches: every peer places the same fires.
    public class Campfire : MonoBehaviour
    {
        public const float WarmRadius = 4f;
        private const float FlickerSpeed = 6f;

        private static readonly List<Campfire> _all = new List<Campfire>();

        [SerializeField] private Light _light;
        [SerializeField] private float _intensity = 5f;

        private float _seed;

        public static IReadOnlyList<Campfire> All => _all;

        // True when position is close enough to a fire to dry off.
        public static bool IsWarmAt(Vector3 position)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                Vector3 offset = _all[i].transform.position - position;
                if (Mathf.Abs(offset.y) > 2.5f) continue;
                offset.y = 0f;
                if (offset.sqrMagnitude <= WarmRadius * WarmRadius) return true;
            }
            return false;
        }

        private void OnEnable()
        {
            _all.Add(this);
            _seed = Random.value * 100f;
        }

        private void OnDisable()
        {
            _all.Remove(this);
        }

        private void Update()
        {
            if (_light == null) return;
            float flicker = Mathf.PerlinNoise(Time.time * FlickerSpeed, _seed);
            _light.intensity = _intensity * Mathf.Lerp(0.75f, 1.1f, flicker);
        }
    }
}
