using UnityEngine;

namespace Chopper.Enemies
{
    // Client only. What is left of a felled tree enemy: it topples away from the killing blow, then sinks into the
    // ground and is gone, instead of vanishing on the spot.
    public class FallingCorpse : MonoBehaviour
    {
        private const float FallSeconds = 0.55f;
        private const float RestSeconds = 0.5f;
        private const float SinkSeconds = 0.7f;
        private const float FallAngle = 80f;

        private Vector3 _axis;
        private Vector3 _start;
        private Quaternion _startRotation;
        private float _height;
        private float _born;

        // Starts the fall of a visual already detached from its enemy. away is the push of the last hit.
        public static void Begin(Transform visual, Vector3 away, float height)
        {
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -visual.forward;
            FallingCorpse corpse = visual.gameObject.AddComponent<FallingCorpse>();
            corpse._axis = Vector3.Cross(Vector3.up, away.normalized);
            corpse._start = visual.position;
            corpse._startRotation = visual.rotation;
            corpse._height = Mathf.Max(0.5f, height);
            corpse._born = Time.time;
        }

        private void Update()
        {
            float age = Time.time - _born;
            float fall = Mathf.Clamp01(age / FallSeconds);
            // Speeds up as it goes, like a falling trunk, then stops on the ground.
            float angle = FallAngle * fall * fall;
            transform.rotation = Quaternion.AngleAxis(angle, _axis) * _startRotation;

            float sink = Mathf.Clamp01((age - FallSeconds - RestSeconds) / SinkSeconds);
            transform.position = _start + Vector3.down * (sink * _height * 0.35f);
            transform.localScale = Vector3.one * (1f - 0.6f * sink);
            if (sink >= 1f) Destroy(gameObject);
        }
    }
}
