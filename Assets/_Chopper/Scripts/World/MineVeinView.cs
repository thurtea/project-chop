using Chopper.Core;
using Chopper.World.Generation;
using UnityEngine;

namespace Chopper.World
{
    // One vein in a mine wall: rock with chunks of ore in the mine's colour. The ore brightens and dulls with the
    // vein's rhythm (MiningRules.Glint) so a strike can still be timed. There is no point light. Once this player
    // has worked the vein today the ore goes dull until dawn.
    public class MineVeinView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public int Vein;
        public Color Glow = new Color(1f, 0.75f, 0.35f);

        private Renderer _ore;
        private MaterialPropertyBlock _block;
        private bool _spent;

        public void Init(int vein, Color glow, Light unused)
        {
            Vein = vein;
            Glow = glow;
            Transform ore = transform.Find("Ore");
            _ore = ore != null ? ore.GetComponent<Renderer>() : null;
            _block = new MaterialPropertyBlock();
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
            if (_spent) Paint(0.35f);
        }

        private void Update()
        {
            if (_spent || _ore == null) return;
            float glint = MiningRules.Glint(MiningClock.Now, Vein);
            Paint(Mathf.Lerp(0.55f, 1f, glint));
        }

        private void Paint(float brightness)
        {
            if (_ore == null) return;
            _ore.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, new Color(brightness, brightness, brightness, 1f));
            _ore.SetPropertyBlock(_block);
        }

        public static float Range => 0f;
    }
}
