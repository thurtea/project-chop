using UnityEngine;

namespace Chopper.Player
{
    [DefaultExecutionOrder(150)]
    public class ClimbPose : MonoBehaviour
    {
        private Animator _animator;
        private PlayerMotor _motor;
        private float _blend;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _motor = GetComponentInParent<PlayerMotor>();
        }

        public static float HandLift(float phase, bool left) => Mathf.Sin(phase + (left ? 0f : Mathf.PI)) * 0.22f;

        private void LateUpdate()
        {
            if (_animator == null || !_animator.isHuman || _motor == null) return;
            _blend = Mathf.MoveTowards(_blend, _motor.IsClimbing ? 1f : 0f, Time.deltaTime * 3.5f);
            if (_blend <= 0.001f) return;
            Transform root = _motor.transform;
            Vector3 fwd = root.forward, right = root.right;
            Transform chest = _animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? _animator.GetBoneTransform(HumanBodyBones.Chest);
            Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            if (chest == null || hips == null) return;
            float y = root.position.y;
            Vector3 at = root.position + fwd * 0.38f;
            float lh = Chopper.Core.ClimbCycle.Grip(y, 1.95f, 0f, out float ll);
            float rh = Chopper.Core.ClimbCycle.Grip(y, 1.95f, 0.5f, out float rl);
            float lf = Chopper.Core.ClimbCycle.Grip(y, 0.45f, 0.5f, out float lfl);
            float rf = Chopper.Core.ClimbCycle.Grip(y, 0.45f, 0f, out float rfl);
            Limb(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                new Vector3(at.x, lh, at.z) - right * 0.22f - fwd * ll, fwd + Vector3.down * 0.3f - right * 0.5f);
            Limb(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                new Vector3(at.x, rh, at.z) + right * 0.22f - fwd * rl, fwd + Vector3.down * 0.3f + right * 0.5f);
            Vector3 foot = root.position + fwd * 0.3f;
            Limb(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                new Vector3(foot.x, lf, foot.z) - right * 0.14f - fwd * lfl, fwd);
            Limb(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
                new Vector3(foot.x, rf, foot.z) + right * 0.14f - fwd * rfl, fwd);
        }

        private void Limb(HumanBodyBones a, HumanBodyBones b, HumanBodyBones c, Vector3 target, Vector3 pole)
        {
            Transform root = _animator.GetBoneTransform(a), mid = _animator.GetBoneTransform(b), end = _animator.GetBoneTransform(c);
            if (root == null || mid == null || end == null) return;
            Quaternion r0 = root.rotation, m0 = mid.rotation;
            Vector3 goal = Vector3.Lerp(end.position, target, Chopper.Core.ClimbCycle.Ease(_blend));
            float la = Vector3.Distance(root.position, mid.position), lb = Vector3.Distance(mid.position, end.position);
            Vector3 to = goal - root.position;
            float d = Mathf.Clamp(to.magnitude, 0.05f, la + lb - 0.001f);
            Vector3 dir = to.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(pole, dir);
            bend = bend.sqrMagnitude > 1e-6f ? bend.normalized : Vector3.Cross(dir, Vector3.right).normalized;
            float ang = Mathf.Acos(Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f));
            Vector3 joint = root.position + (dir * Mathf.Cos(ang) + bend * Mathf.Sin(ang)) * la;
            Aim(root, mid, (joint - root.position).normalized);
            Aim(mid, end, (goal - mid.position).normalized);
        }

        private static void Aim(Transform bone, Transform child, Vector3 want)
        {
            Vector3 cur = child.position - bone.position;
            if (cur.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.FromToRotation(cur.normalized, want) * bone.rotation;
        }

        private class Spawner : MonoBehaviour
        {
            private float _next;
            private void Update()
            {
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + 0.5f;
                foreach (PlayerMotor m in FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None))
                {
                    Animator a = m.GetComponentInChildren<Animator>();
                    if (a != null && a.GetComponent<ClimbPose>() == null) a.gameObject.AddComponent<ClimbPose>();
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("ClimbPoseSpawner") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<Spawner>();
        }
    }
}
