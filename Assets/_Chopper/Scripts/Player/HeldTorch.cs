using UnityEngine;

namespace Chopper.Player
{
    // Carries the lit torch the way Valheim does: the left arm holds it up in front of the chest (the animator's
    // CarryLeft layer, the library's torch idle) with the fist closed round the handle (GripLeft), and the torch runs
    // through the fist from the little finger to the index, as in the clip. While the guard is up or the rod is out
    // the arm is left to them. The light flickers gently. Runs after the animator and GuardPose.
    [DefaultExecutionOrder(100)]
    public class HeldTorch : MonoBehaviour
    {
        private const float FlickerSpeed = 5f;
        private const float BlendSeconds = 0.18f;
        // How much of the handle shows below the fist.
        private const float ButtBelowFist = 0.07f;

        [SerializeField] private Transform _hand;
        [SerializeField] private Transform _body;
        [SerializeField] private Light _light;

        private HandGrip _grip;
        private Animator _animator;
        private int _carryLayer = -1;
        private int _gripLayer = -1;
        private PlayerCombat _combat;
        private PlayerFishing _fishing;
        private float _weight;
        private float _intensity;
        private float _seed;

        private void Awake()
        {
            if (_light != null) { _light.range = 12f; _intensity = _light.intensity * 1.9f; }
            _seed = Random.value * 100f;
            if (_body != null)
            {
                _animator = _body.GetComponentInChildren<Animator>();
                if (_animator != null)
                {
                    _carryLayer = _animator.GetLayerIndex("CarryLeft");
                    _gripLayer = _animator.GetLayerIndex("GripLeft");
                }
                _combat = _body.GetComponent<PlayerCombat>();
                _fishing = _body.GetComponent<PlayerFishing>();
            }
            _grip = new HandGrip(_hand, "l");
        }

        private void OnEnable()
        {
            _weight = 0f;
        }

        // Put out: the arm and fingers go back to the animation underneath.
        private void OnDisable()
        {
            SetLayers(0f);
        }

        private void SetLayers(float weight)
        {
            if (_animator == null) return;
            if (_carryLayer >= 0) _animator.SetLayerWeight(_carryLayer, weight);
            if (_gripLayer >= 0) _animator.SetLayerWeight(_gripLayer, weight);
        }

        private void LateUpdate()
        {
            if (_hand == null || _body == null) return;
            bool armFree = !(_combat != null && (_combat.IsBlocking || _combat.GuardRaised)) && !(_fishing != null && _fishing.Busy);
            _weight = Mathf.MoveTowards(_weight, armFree ? 1f : 0f, Time.deltaTime / BlendSeconds);
            SetLayers(Mathf.SmoothStep(0f, 1f, _weight));

            // The handle goes through the hollow of the fist along the fist's own line, a short butt below it.
            Vector3 axis = _grip.Across;
            transform.SetPositionAndRotation(_grip.Center - axis * ButtBelowFist, Quaternion.FromToRotation(Vector3.up, axis));
            if (_light != null) _light.intensity = _intensity * Mathf.Lerp(0.8f, 1.08f, Mathf.PerlinNoise(Time.time * FlickerSpeed, _seed));
        }
    }
}
