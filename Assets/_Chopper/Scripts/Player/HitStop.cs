using UnityEngine;

namespace Chopper.Player
{
    public class HitStop : MonoBehaviour
    {
        private const float FrozenSpeed = 0.05f;

        private Animator _animator;
        private float _startAt;
        private float _endAt;
        private bool _frozen;

        public static void Apply(Animator animator, float delay, float seconds)
        {
            if (animator == null || seconds <= 0f) return;
            HitStop stop = animator.GetComponent<HitStop>();
            if (stop == null) stop = animator.gameObject.AddComponent<HitStop>();
            stop.Schedule(animator, delay, seconds);
        }

        private void Schedule(Animator animator, float delay, float seconds)
        {
            _animator = animator;
            float start = Time.time + Mathf.Max(0f, delay);
            if (_frozen)
            {
                _endAt = Mathf.Max(_endAt, start + seconds);
                return;
            }
            _startAt = start;
            _endAt = start + seconds;
            enabled = true;
        }

        private void Update()
        {
            if (_animator == null)
            {
                enabled = false;
                return;
            }
            float now = Time.time;
            if (!_frozen && now >= _startAt && now < _endAt)
            {
                _animator.speed = FrozenSpeed;
                _frozen = true;
            }
            if (now >= _endAt) enabled = false;
        }

        private void OnDisable()
        {
            if (!_frozen) return;
            _frozen = false;
            if (_animator != null) _animator.speed = 1f;
        }
    }
}
