using UnityEngine;

namespace Chopper.World
{
    public class ScatterCuller : MonoBehaviour
    {
        public float Radius = 0.5f;

        private void OnEnable() => ScatterMask.Changed += OnMaskChanged;

        private void OnDisable() => ScatterMask.Changed -= OnMaskChanged;

        public void OnMaskChanged(Bounds zone)
        {
            zone.Expand(new Vector3((ScatterMask.Margin + Radius) * 2f, ScatterMask.BelowReach * 2f, (ScatterMask.Margin + Radius) * 2f));
            foreach (Transform child in transform)
            {
                Vector3 p = child.position;
                if (p.x < zone.min.x || p.x > zone.max.x || p.z < zone.min.z || p.z > zone.max.z) continue;
                bool clear = !ScatterMask.PieceNear(p + Vector3.up * 0.1f, Radius);
                if (child.gameObject.activeSelf != clear) child.gameObject.SetActive(clear);
            }
        }
    }
}