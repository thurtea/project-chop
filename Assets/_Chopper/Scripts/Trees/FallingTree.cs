using UnityEngine;

namespace Chopper.Trees
{
    // Local only (plan D14: felled trees are not networked). Topples a copy of the tree around its base,
    // slowly at first and then faster so it has weight, lets it lie for a moment, then sinks it and destroys it.
    // Scripted for now; a Rigidbody log with a stump is planned in T14b.
    public class FallingTree : MonoBehaviour
    {
        public const float FallDuration = 1.5f;
        private const float RestDuration = 3f;
        private const float SinkDuration = 1.5f;
        private const float SinkDepth = 4f;
        private const float FallDegrees = 88f;

        private Quaternion _startRotation;
        private Vector3 _startPosition;
        private Vector3 _axis;
        private float _startTime;

        // Which way along the ground the top of a tree ends up when it is felled with this aim (flat, unit length).
        public static Vector3 GroundDirection(Vector3 fallDirection)
        {
            Vector3 flat = new Vector3(fallDirection.x, 0f, fallDirection.z);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
            Vector3 axis = Vector3.Cross(Vector3.up, flat.normalized);
            Vector3 top = Quaternion.AngleAxis(FallDegrees, axis) * Vector3.up;
            top.y = 0f;
            return top.normalized;
        }

        public static void Spawn(GameObject model, Vector3 position, Quaternion rotation, float scale, Vector3 fallDirection, Chopper.Core.Element zone)
        {
            GameObject go = Instantiate(model, position, rotation);
            go.transform.localScale = Vector3.one * scale;
            TreeTint.Apply(go, zone);
            var falling = go.AddComponent<FallingTree>();
            Vector3 flat = new Vector3(fallDirection.x, 0f, fallDirection.z);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
            falling._axis = Vector3.Cross(Vector3.up, flat.normalized);
            falling._startRotation = rotation;
            falling._startPosition = position;
            falling._startTime = Time.time;
        }

        private void Update()
        {
            float elapsed = Time.time - _startTime;
            if (elapsed < FallDuration)
            {
                float t = elapsed / FallDuration;
                transform.rotation = Quaternion.AngleAxis(FallDegrees * t * t, _axis) * _startRotation;
                return;
            }

            transform.rotation = Quaternion.AngleAxis(FallDegrees, _axis) * _startRotation;
            float sinkElapsed = elapsed - FallDuration - RestDuration;
            if (sinkElapsed > SinkDuration)
            {
                Destroy(gameObject);
                return;
            }
            if (sinkElapsed > 0f) transform.position = _startPosition + Vector3.down * (SinkDepth * sinkElapsed / SinkDuration);
        }
    }
}
