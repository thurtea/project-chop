using UnityEngine;

namespace Chopper.World.Building
{
    // On every placed piece: which piece it is (its id in BuildManager), so the hammer can find what the crosshair is
    // on. Damage darkens it a little, the same on every peer.
    public class PieceView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _block;
        private float _shownShare = 1f;

        public int Id { get; private set; }
        public int Kind { get; private set; }

        public void Bind(int id, int kind)
        {
            Id = id;
            Kind = kind;
        }

        // Health share 0 to 1: below full the timber darkens toward a charred brown.
        public void ShowHealth(float share)
        {
            share = Mathf.Clamp01(share);
            if (Mathf.Abs(share - _shownShare) < 0.02f) return;
            _shownShare = share;
            if (_block == null) _block = new MaterialPropertyBlock();
            float shade = Mathf.Lerp(0.45f, 1f, share);
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                Material material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty(BaseColorId)) continue;
                if (share >= 0.999f)
                {
                    renderer.SetPropertyBlock(null);
                    continue;
                }
                Color color = material.GetColor(BaseColorId);
                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, new Color(color.r * shade, color.g * shade, color.b * shade, color.a));
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}
