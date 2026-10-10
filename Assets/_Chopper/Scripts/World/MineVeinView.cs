using Chopper.Core;
using Chopper.World.Generation;
using UnityEngine;

namespace Chopper.World
{
    // One vein in a mine wall: a lump of rock with a crack that glows and fades in the vein's own rhythm
    // (MiningRules.Glint), in the mine's color, with a small light that pulses with it. Once this player has worked
    // the vein today it goes dark and dull until dawn. Its renderers use WorldAssets.MineVein, a saved material with
    // emission on (black), so the build keeps the emissive shader variant and no other rock glows.
    public class MineVeinView : MonoBehaviour
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const float LightRange = 2.6f;
        private const float LightPeak = 1.8f;
        private const float GlowPeak = 2.4f;

        public int Vein;
        public Color Glow = new Color(1f, 0.75f, 0.35f);

        private Renderer[] _renderers;
        private Light _light;
        private MaterialPropertyBlock _block;
        private Color _base = Color.white;
        private bool _spent;

        public void Init(int vein, Color glow, Light light)
        {
            Vein = vein;
            Glow = glow;
            _light = light;
            _renderers = GetComponentsInChildren<Renderer>();
            _block = new MaterialPropertyBlock();
            if (_renderers.Length > 0 && _renderers[0].sharedMaterial != null && _renderers[0].sharedMaterial.HasProperty(BaseColorId))
                _base = _renderers[0].sharedMaterial.GetColor(BaseColorId);
            MineWorld.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            MineWorld.Changed -= Refresh;
        }

        private void Refresh()
        {
            _spent = MineWorld.SpentHere(Vein);
            if (_light != null) _light.enabled = !_spent;
            if (_spent) Paint(0f);
        }

        private void Update()
        {
            if (_spent || _renderers == null) return;
            float glint = MiningRules.Glint(MiningClock.Now, Vein);
            Paint(glint);
            if (_light != null) _light.intensity = LightPeak * glint;
        }

        private void Paint(float glint)
        {
            Color tint = _spent ? _base * 0.45f : Color.Lerp(_base, Glow, 0.25f);
            Color emission = Glow * (GlowPeak * glint);
            foreach (Renderer r in _renderers)
            {
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, tint);
                _block.SetColor(EmissionId, emission);
                r.SetPropertyBlock(_block);
            }
        }

        public static float Range => LightRange;
    }
}
