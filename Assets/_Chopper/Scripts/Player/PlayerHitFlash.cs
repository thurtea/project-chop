using System.Collections.Generic;
using UnityEngine;

namespace Chopper.Player
{
    // A short, slight red tint on the character when they take a hit, seen by everyone (instead of a red screen flash).
    // The outfit sets its colors through property blocks (CharacterOutfitter), so each material's own tint is read
    // first and put back exactly when the flash ends.
    public class PlayerHitFlash : MonoBehaviour
    {
        private const float Seconds = 0.28f;
        private const float Strength = 0.45f;
        private static readonly Color Red = new Color(1f, 0.18f, 0.12f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private struct Slot
        {
            public SkinnedMeshRenderer Renderer;
            public int Index;
            public Color Original;
        }

        private readonly List<Slot> _slots = new List<Slot>();
        private MaterialPropertyBlock _block;
        private float _until = -1f;

        public void Flash()
        {
            if (_block == null) _block = new MaterialPropertyBlock();
            // A new hit during a flash keeps the colors read the first time.
            if (_until < 0f)
            {
                _slots.Clear();
                foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == null || !materials[i].HasProperty(BaseColorId)) continue;
                        renderer.GetPropertyBlock(_block, i);
                        Color original = _block.HasColor(BaseColorId) ? _block.GetColor(BaseColorId) : materials[i].GetColor(BaseColorId);
                        _slots.Add(new Slot { Renderer = renderer, Index = i, Original = original });
                    }
                }
            }
            _until = Time.time + Seconds;
        }

        private void LateUpdate()
        {
            if (_until < 0f) return;
            float left = Mathf.Clamp01((_until - Time.time) / Seconds);
            bool done = left <= 0f;
            foreach (Slot slot in _slots)
            {
                if (slot.Renderer == null) continue;
                slot.Renderer.GetPropertyBlock(_block, slot.Index);
                Color tinted = Color.Lerp(slot.Original, slot.Original * Red, Strength * left);
                tinted.a = slot.Original.a;
                _block.SetColor(BaseColorId, done ? slot.Original : tinted);
                slot.Renderer.SetPropertyBlock(_block, slot.Index);
            }
            if (done)
            {
                _until = -1f;
                _slots.Clear();
            }
        }
    }
}
