using System;
using System.Collections.Generic;
using Chopper.Core;
using UnityEngine;

namespace Chopper.Enemies
{
    // Weighted element odds for one forest. Weights do not need to sum to 1; they are normalized.
    [CreateAssetMenu(fileName = "SO_SpawnTable", menuName = "Chopper/Spawn Table")]
    public class SpawnTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [SerializeField] private Element _element;
            [SerializeField, Min(0f)] private float _weight;

            public Entry(Element element, float weight)
            {
                _element = element;
                _weight = weight;
            }

            public Element Element => _element;
            public float Weight => _weight;
        }

        [SerializeField] private Element _zoneElement;
        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        public Element ZoneElement => _zoneElement;
        public IReadOnlyList<Entry> Entries => _entries;

        public float TotalWeight
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < _entries.Length; i++)
                {
                    total += Mathf.Max(0f, _entries[i].Weight);
                }
                return total;
            }
        }

        public void Configure(Element zoneElement, IReadOnlyList<Entry> entries)
        {
            _zoneElement = zoneElement;
            _entries = new Entry[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                _entries[i] = entries[i];
            }
        }

        public float GetProbability(Element element)
        {
            float total = TotalWeight;
            if (total <= 0f) return 0f;

            float weight = 0f;
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Element == element) weight += Mathf.Max(0f, _entries[i].Weight);
            }
            return weight / total;
        }

        // Deterministic for a given System.Random seed. The server owns the Random instance.
        public Element Roll(System.Random rng)
        {
            return Roll((float)rng.NextDouble());
        }

        // normalized is in [0, 1). Returns Neutral when the table has no positive weights.
        public Element Roll(float normalized)
        {
            float total = TotalWeight;
            if (total <= 0f) return Element.Neutral;

            float target = Mathf.Clamp01(normalized) * total;
            float cumulative = 0f;
            Element last = Element.Neutral;
            for (int i = 0; i < _entries.Length; i++)
            {
                float weight = Mathf.Max(0f, _entries[i].Weight);
                if (weight <= 0f) continue;

                cumulative += weight;
                last = _entries[i].Element;
                if (target < cumulative) return last;
            }
            return last;
        }
    }
}
