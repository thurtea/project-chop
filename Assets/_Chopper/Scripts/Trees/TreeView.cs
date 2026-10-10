using UnityEngine;

namespace Chopper.Trees
{
    // Visual of one standing tree. Only knows how to wobble when hit.
    public class TreeView : MonoBehaviour
    {
        private const float ShakeDuration = 0.35f;
        private const float ShakeDegrees = 3.5f;
        private const float ShakeFrequency = 40f;

        private Quaternion _baseRotation;
        private Vector3 _axis = Vector3.right;
        private float _shakeStart = float.NegativeInfinity;

        public int TreeId { get; set; }

        private void Awake()
        {
            _baseRotation = transform.rotation;
            enabled = false;
        }

        // hitDirection is the flat direction the axe swung, so the tree rocks away from the player.
        public void Shake(Vector3 hitDirection)
        {
            Vector3 flat = new Vector3(hitDirection.x, 0f, hitDirection.z);
            if (flat.sqrMagnitude > 0.0001f) _axis = Vector3.Cross(Vector3.up, flat.normalized);
            _shakeStart = Time.time;
            enabled = true;
        }

        private void Update()
        {
            float t = (Time.time - _shakeStart) / ShakeDuration;
            if (t >= 1f)
            {
                transform.rotation = _baseRotation;
                enabled = false;
                return;
            }

            float angle = Mathf.Sin(t * ShakeFrequency * ShakeDuration) * (1f - t) * ShakeDegrees;
            transform.rotation = Quaternion.AngleAxis(angle, _axis) * _baseRotation;
        }
    }
}
