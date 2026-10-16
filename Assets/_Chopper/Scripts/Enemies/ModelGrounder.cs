using UnityEngine;

namespace Chopper.Enemies
{
    public class ModelGrounder : MonoBehaviour
    {
        public Transform Model;
        private Renderer[] _renderers;
        private float _offset;
        private float _ground;
        private bool _hasGround;
        private float _next;
        public float Offset => _offset;

        private void LateUpdate()
        {
            if (Model == null) return;
            if (_renderers == null || _renderers.Length == 0) _renderers = Model.GetComponentsInChildren<Renderer>();
            if (_renderers.Length == 0) return;
            Model.position -= Vector3.up * _offset;
            _offset = 0f;
            bool first = true;
            Bounds b = default;
            foreach (Renderer r in _renderers)
            {
                if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            if (first) return;
            if (Time.time >= _next)
            {
                _next = Time.time + 0.2f;
                _hasGround = false;
                _ground = float.NegativeInfinity;
                Vector3 c = transform.position;
                float ex = Mathf.Min(0.6f, b.extents.x * 0.35f), ez = Mathf.Min(0.6f, b.extents.z * 0.35f);
                Vector3[] at = { c, c + new Vector3(ex, 0f, 0f), c - new Vector3(ex, 0f, 0f), c + new Vector3(0f, 0f, ez), c - new Vector3(0f, 0f, ez) };
                Transform self = transform.root;
                foreach (Vector3 p in at)
                {
                    foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 1.2f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (h.collider is CharacterController || h.collider.transform.IsChildOf(self)) continue;
                        if (h.point.y > _ground) { _ground = h.point.y; _hasGround = true; }
                    }
                }
            }
            if (!_hasGround) return;
            _offset = Mathf.Clamp(_ground - b.min.y, -0.6f, 0.6f);
            Model.position += Vector3.up * _offset;
        }
    }
}
