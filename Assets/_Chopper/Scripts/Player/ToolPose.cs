using Chopper.Core;
using UnityEngine;

namespace Chopper.Player
{
    [DefaultExecutionOrder(-200)]
    public class ToolPose : MonoBehaviour
    {
        private Animator _body;
        private SwingTool _kind;
        private int _variant;
        private float _start = -10f;
        private float _seconds = 0.6f;
        private PlayerAxe _axe;
        private PlayerMining _mining;

        public SwingTool Kind => _kind;
        public int Variant => _variant;
        public float Phase => _seconds > 0f ? (Time.time - _start) / _seconds : 1f;
        public bool Playing => Phase >= 0f && Phase < 1f;
        public float ContactTime => _start + _seconds * ToolMotion.Contact(_kind);

        public void Play(SwingTool kind, int variant, float seconds)
        {
            _kind = kind;
            _variant = variant;
            _seconds = Mathf.Max(0.2f, seconds);
            _start = Time.time;
        }

        private void Awake()
        {
            _axe = GetComponent<PlayerAxe>();
            _mining = GetComponent<PlayerMining>();
        }

        private void LateUpdate()
        {
            float phase = Phase;
            float weight = ToolMotion.Weight(phase);
            if (weight <= 0f) return;
            if (_body == null) _body = GetComponentInChildren<Animator>();
            if (_body == null || !_body.isHuman) return;
            ToolPoseKey key = ToolMotion.Sample(_kind, _variant, phase);
            Quaternion frame = transform.rotation;
            Transform spine = _body.GetBoneTransform(HumanBodyBones.Chest);
            if (spine == null) spine = _body.GetBoneTransform(HumanBodyBones.Spine);
            if (spine != null)
            {
                Quaternion lean = Quaternion.AngleAxis(key.Twist * weight, Vector3.up) * Quaternion.AngleAxis(key.Bend * weight, transform.right);
                spine.rotation = lean * spine.rotation;
            }
            Aim(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, frame * key.RightUpper, weight);
            Aim(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, frame * key.RightLower, weight);
            if (!ToolMotion.TwoHanded(_kind)) return;
            Aim(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, frame * key.LeftUpper, weight);
            Aim(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, frame * key.LeftLower, weight);
        }

        private void Aim(HumanBodyBones bone, HumanBodyBones child, Vector3 aim, float weight)
        {
            Transform t = _body.GetBoneTransform(bone);
            Transform end = _body.GetBoneTransform(child);
            if (t == null || end == null) return;
            Vector3 now = end.position - t.position;
            if (now.sqrMagnitude < 0.0001f) return;
            Quaternion turn = Quaternion.FromToRotation(now.normalized, aim.normalized);
            t.rotation = Quaternion.Slerp(Quaternion.identity, turn, weight) * t.rotation;
        }
    }
}
