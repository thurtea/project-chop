using System.Collections.Generic;
using UnityEngine;

namespace Chopper.World
{
    // A campfire by the hut or the pond: always burning, it lights the ground around it and dries anyone standing
    // close (WetRules.FireDryRate). The flame itself is a smaller volume: standing in it burns (BurnRules). The
    // repaired hut's fireplace flame counts too. Purely local, like the night torches: every peer places the same fires.
    public class Campfire : MonoBehaviour
    {
        public const float WarmRadius = 4f;
        // The fire itself, not the warmth around it. Feet inside this of the pit or the hearth flame are in the fire.
        public const float FlameRadius = 0.85f;
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

        // True when feet are in a campfire or in the hut fireplace's flame. Beside a fire is warm, not burning.
        public static bool IsInFlame(Vector3 feet)
        {
            float radiusSqr = FlameRadius * FlameRadius;
            for (int i = 0; i < _all.Count; i++)
            {
                if (InVolume(feet, _all[i].transform.position, radiusSqr)) return true;
            }
            return InFireplace(feet, radiusSqr);
        }

        private static Transform _hearth;
        private static bool _hearthSearched;

        private static bool InFireplace(Vector3 feet, float radiusSqr)
        {
            if (Hut.Instance == null)
            {
                _hearth = null;
                _hearthSearched = false;
                return false;
            }
            if (_hearth == null)
            {
                if (_hearthSearched) return false;
                _hearth = FindHearth();
                _hearthSearched = true;
                if (_hearth == null) return false;
            }
            if (!_hearth.gameObject.activeInHierarchy) return false;
            return InVolume(feet, _hearth.position, radiusSqr);
        }

        // The lit fireplace only: its flame sits under IntactOnly, which a ruin switches off.
        private static Transform FindHearth()
        {
            Hut hut = Hut.Instance;
            if (hut == null) return null;
            Transform fireplace = null;
            foreach (Transform t in hut.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Fireplace")
                {
                    fireplace = t;
                    break;
                }
            }
            if (fireplace == null) return null;
            foreach (Transform t in fireplace.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Flame" || t.name == "Flames") return t;
            }
            return null;
        }

        private static bool InVolume(Vector3 feet, Vector3 origin, float radiusSqr)
        {
            Vector3 offset = feet - origin;
            if (offset.y < -1.2f || offset.y > 1.7f) return false;
            offset.y = 0f;
            return offset.sqrMagnitude <= radiusSqr;
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
