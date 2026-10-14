using Chopper.Core;
using Chopper.Player;
using Chopper.World.Generation;
using UnityEngine;

namespace Chopper.World
{
    // One vein in a mine wall: rock with chunks of ore in the mine's colour. While this player works it the ore flashes
    // the strike beats (RhythmRules) and the strikes throw sparks, chips or dust. There is no point light. Once this
    // player has worked the vein today the ore goes dull until dawn.
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
            PlayerMining.RhythmCue += OnCue;
            Refresh();
        }

        private void OnDestroy()
        {
            MineWorld.Changed -= Refresh;
            PlayerMining.RhythmCue -= OnCue;
        }

        private void OnCue(int vein, RhythmHit hit, bool bar)
        {
            if (vein != Vein || _ore == null) return;
            Vector3 at = _ore.bounds.center + transform.forward * 0.15f;
            if (bar)
            {
                CombatFx.Sparks(at, Glow, 30);
                CombatFx.Chips(at, Glow);
            }
            else if (hit == RhythmHit.OnBeat) CombatFx.Sparks(at, Glow, 12);
            else if (hit == RhythmHit.OffBeat) CombatFx.Chips(at, new Color(0.45f, 0.43f, 0.40f));
            else CombatFx.Dust(at, new Color(0.55f, 0.52f, 0.48f), 8);
        }

        private void Refresh()
        {
            _spent = MineWorld.SpentHere(Vein);
            if (_spent) Paint(0.35f);
        }

        private void Update()
        {
            if (_spent || _ore == null) return;
            PlayerMining mining = PlayerMining.Local;
            bool worked = mining != null && mining.EngagedVein == Vein;
            Paint(worked ? Mathf.Lerp(0.5f, 1.35f, RhythmRules.Pulse(Time.time - mining.RhythmStart)) : 0.6f);
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
