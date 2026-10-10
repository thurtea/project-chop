using System;
using UnityEngine;

namespace Chopper.Core
{
    // Maps each Element to its shared material and colors. Every elemental visual reads from here.
    [CreateAssetMenu(fileName = "SO_ElementPalette", menuName = "Chopper/Element Palette")]
    public class ElementPalette : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [SerializeField] private Element _element;
            [SerializeField] private Material _material;
            [SerializeField] private Color _baseColor;
            [SerializeField, ColorUsage(false, true)] private Color _emissionColor;

            public Entry(Element element, Color baseColor, Color emissionColor)
            {
                _element = element;
                _material = null;
                _baseColor = baseColor;
                _emissionColor = emissionColor;
            }

            public Element Element => _element;
            public Material Material => _material;
            public Color BaseColor => _baseColor;
            public Color EmissionColor => _emissionColor;
            public bool HasEmission => _emissionColor.maxColorComponent > 0f;
        }

        // Defaults from the plan, section 7. Earth uses "none" from its "none or faint" option.
        [SerializeField] private Entry[] _entries =
        {
            new Entry(Element.Neutral, new Color32(0x4F, 0x8A, 0x3C, 0xFF), Color.black),
            new Entry(Element.Water, new Color32(0x2F, 0x7F, 0xD8, 0xFF), new Color(0.18f, 0.5f, 0.85f)),
            new Entry(Element.Fire, new Color32(0xE0, 0x52, 0x1F, 0xFF), new Color(2.4f, 0.9f, 0.2f)),
            new Entry(Element.Earth, new Color32(0x7A, 0x62, 0x32, 0xFF), Color.black),
            new Entry(Element.Wind, new Color32(0xBF, 0xEF, 0xEA, 0xFF), new Color(0.6f, 0.6f, 0.6f))
        };

        public bool TryGetEntry(Element element, out Entry entry)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Element == element)
                {
                    entry = _entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public Material GetMaterial(Element element)
        {
            return TryGetEntry(element, out Entry entry) ? entry.Material : null;
        }

        public Color GetBaseColor(Element element)
        {
            return TryGetEntry(element, out Entry entry) ? entry.BaseColor : Color.magenta;
        }

        public Color GetEmissionColor(Element element)
        {
            return TryGetEntry(element, out Entry entry) ? entry.EmissionColor : Color.black;
        }
    }
}
