using UnityEngine;

namespace Chopper.Player
{
    // The axe guard, posed in code on top of whatever the Animator plays (the animation library has no block clip, and
    // the strapped on shield never sat in the hand, so it was removed on 2026-10-02). The right hand brings the axe up in
    // front of the chest with the handle slanting up and across to the left, the left hand reaches over and grips the
    // handle near the head, the torso leans in a little and the knees flex with the feet kept where the animation put
    // them. Blows are taken on the haft.
    //
    // It aims bones (the direction from a bone to its child) at directions given in the character's own frame (x right,
    // y up, z forward), so it does not depend on the rig's axis conventions, solves the left arm and the legs with a two
    // bone IK, and is blended by a smooth weight so entering and leaving the guard never snaps. It runs in LateUpdate,
    // after the Animator.
    public class GuardPose : MonoBehaviour
    {
        // Directions in the character's frame. The axe (right) upper arm points forward and down from the shoulder, the
        // forearm comes back across the body, so the hand sits in front of the chest a little left of center.
        public static readonly Vector3 AxeUpperArm = new Vector3(0.20f, -0.66f, 0.72f);
        public static readonly Vector3 AxeForearm = new Vector3(-0.60f, 0.18f, 0.78f);
        // Where the axe handle points from the hand: up and to the left, tipped a little forward, across the body.
        public static readonly Vector3 AxeHandle = new Vector3(-0.62f, 0.62f, 0.48f);
        // How far up the handle (meters from the right hand) the left hand grips it.
        public const float LeftGripAlongHandle = 0.30f;
        // The whole torso is aimed at this forward lean from vertical, whatever lean the animation has underneath.
        private const float LeanDegrees = 6f;
        // How far the hips sink, in meters. The legs are solved so the feet stay planted, which bends the knees.
        public const float CrouchDrop = 0.09f;
        private const float EnterSeconds = 0.16f;
        private const float ExitSeconds = 0.14f;

        private Transform _pelvis, _spine1, _spine2, _spine3, _neck, _head;
        private Transform _upperL, _lowerL, _handL, _upperR, _lowerR, _handR;
        private Transform _thighL, _calfL, _footL, _thighR, _calfR, _footR;
        private Transform _axeGrip;
        private Renderer[] _axeRenderers;
        private float _weight;
        private bool _found;
        // The pose is applied on top of what the Animator wrote this frame. Some bones (the spine on this rig) are not
        // rewritten by the Animator every frame, so applying the guard to last frame's result stacked it and the upper
        // body spun round. Each frame the guard therefore first undoes its own last change on any bone the Animator left
        // alone (still exactly as the guard set it), then applies the pose again.
        private Transform[] _posed;
        private Quaternion[] _before;
        private Quaternion[] _after;
        private Vector3 _pelvisBefore;
        private Vector3 _pelvisAfter;
        private bool _applied;

        // Set by PlayerAnimator every frame: true while the guard is up.
        public bool Raised { get; set; }

        public float Weight => _weight;

        private void Awake()
        {
            Find();
        }

        private void Find()
        {
            _pelvis = FindDeep(transform, "pelvis");
            _spine1 = FindDeep(transform, "spine_01");
            _spine2 = FindDeep(transform, "spine_02");
            _spine3 = FindDeep(transform, "spine_03");
            _neck = FindDeep(transform, "neck_01");
            _head = FindDeep(transform, "Head");
            _upperL = FindDeep(transform, "upperarm_l");
            _lowerL = FindDeep(transform, "lowerarm_l");
            _handL = FindDeep(transform, "hand_l");
            _upperR = FindDeep(transform, "upperarm_r");
            _lowerR = FindDeep(transform, "lowerarm_r");
            _handR = FindDeep(transform, "hand_r");
            _thighL = FindDeep(transform, "thigh_l");
            _calfL = FindDeep(transform, "calf_l");
            _footL = FindDeep(transform, "foot_l");
            _thighR = FindDeep(transform, "thigh_r");
            _calfR = FindDeep(transform, "calf_r");
            _footR = FindDeep(transform, "foot_r");
            // Chopper's own rig names its bones Hips, Spine, Chest, RightHand and so on, so none of the names above
            // match and the guard never posed. Fall back to the humanoid Avatar's bones.
            if (_spine1 == null || _upperR == null || _handR == null) FindHumanoid();
            _found = _spine1 != null && _spine2 != null && _spine3 != null && _neck != null && _upperL != null && _lowerL != null
                     && _handL != null && _upperR != null && _lowerR != null && _handR != null;
            _posed = new[] { _pelvis, _spine1, _spine2, _spine3, _neck, _head, _upperL, _lowerL, _handL, _upperR, _lowerR, _handR,
                             _thighL, _calfL, _footL, _thighR, _calfR, _footR };
            _before = new Quaternion[_posed.Length];
            _after = new Quaternion[_posed.Length];
        }

        private void FindHumanoid()
        {
            Animator animator = GetComponentInParent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;
            Transform Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone);
            _pelvis = Bone(HumanBodyBones.Hips);
            _spine1 = Bone(HumanBodyBones.Spine);
            _spine2 = Bone(HumanBodyBones.Chest);
            _spine3 = Bone(HumanBodyBones.UpperChest);
            _neck = Bone(HumanBodyBones.Neck);
            _head = Bone(HumanBodyBones.Head);
            _upperL = Bone(HumanBodyBones.LeftUpperArm);
            _lowerL = Bone(HumanBodyBones.LeftLowerArm);
            _handL = Bone(HumanBodyBones.LeftHand);
            _upperR = Bone(HumanBodyBones.RightUpperArm);
            _lowerR = Bone(HumanBodyBones.RightLowerArm);
            _handR = Bone(HumanBodyBones.RightHand);
            _thighL = Bone(HumanBodyBones.LeftUpperLeg);
            _calfL = Bone(HumanBodyBones.LeftLowerLeg);
            _footL = Bone(HumanBodyBones.LeftFoot);
            _thighR = Bone(HumanBodyBones.RightUpperLeg);
            _calfR = Bone(HumanBodyBones.RightLowerLeg);
            _footR = Bone(HumanBodyBones.RightFoot);
        }

        private void LateUpdate()
        {
            float target = Raised ? 1f : 0f;
            float seconds = Raised ? EnterSeconds : ExitSeconds;
            _weight = Mathf.MoveTowards(_weight, target, Time.deltaTime / seconds);
            UndoUntouched();
            if (_weight <= 0.0005f) return;
            Remember(_before, ref _pelvisBefore);
            Evaluate(Mathf.SmoothStep(0f, 1f, _weight));
            Remember(_after, ref _pelvisAfter);
            _applied = true;
        }

        private void Remember(Quaternion[] into, ref Vector3 pelvis)
        {
            if (!_found) return;
            for (int i = 0; i < _posed.Length; i++) if (_posed[i] != null) into[i] = _posed[i].localRotation;
            if (_pelvis != null) pelvis = _pelvis.localPosition;
        }

        // Puts back the animation pose on every bone that still holds exactly what the guard left there last frame.
        private void UndoUntouched()
        {
            if (!_applied || !_found) return;
            _applied = false;
            for (int i = 0; i < _posed.Length; i++)
            {
                Transform bone = _posed[i];
                if (bone != null && bone.localRotation == _after[i]) bone.localRotation = _before[i];
            }
            if (_pelvis != null && _pelvis.localPosition == _pelvisAfter) _pelvis.localPosition = _pelvisBefore;
        }

        // Where the axe handle points from the right hand right now (toward the middle of the axe), in world space.
        public Vector3 AxeHandleDirection()
        {
            if (!FindAxe()) return _handR != null ? _handR.up : Vector3.up;
            Bounds bounds = _axeRenderers[0].bounds;
            for (int i = 1; i < _axeRenderers.Length; i++) bounds.Encapsulate(_axeRenderers[i].bounds);
            Vector3 along = bounds.center - _axeGrip.position;
            return along.sqrMagnitude > 1e-6f ? along.normalized : _handR.up;
        }

        public Vector3 AxeGripPosition => FindAxe() ? _axeGrip.position : (_handR != null ? _handR.position : transform.position);

        private bool FindAxe()
        {
            if (_axeGrip != null && _axeRenderers != null && _axeRenderers.Length > 0) return true;
            if (_handR == null) return false;
            _axeGrip = _handR.Find("AxeGrip");
            if (_axeGrip == null) return false;
            _axeRenderers = _axeGrip.GetComponentsInChildren<Renderer>(true);
            return _axeRenderers.Length > 0;
        }

        // Poses the bones at the given blend (0 leaves the animation alone, 1 is the full guard). Public so an editor
        // tool and the tests can pose it without playing.
        public void Evaluate(float blend)
        {
            if (!_found) Find();
            if (!_found || blend <= 0f) return;

            // The frame of the character: the model sits under the player root, which is turned toward where it faces.
            Transform frame = transform.parent != null ? transform.parent : transform;
            Vector3 right = frame.right, up = frame.up, forward = frame.forward;

            // Where the animation put the feet, so they can be held there while the hips sink.
            bool legs = _pelvis != null && _thighL != null && _calfL != null && _footL != null && _thighR != null && _calfR != null && _footR != null;
            Vector3 footLPosition = legs ? _footL.position : Vector3.zero;
            Vector3 footRPosition = legs ? _footR.position : Vector3.zero;
            Quaternion footLRotation = legs ? _footL.rotation : Quaternion.identity;
            Quaternion footRRotation = legs ? _footR.rotation : Quaternion.identity;
            if (legs) _pelvis.position -= up * (CrouchDrop * blend);

            // Torso first, because the arms hang from it: straighten the spine toward one fixed lean, so the result does
            // not depend on the animation underneath. The head keeps looking ahead.
            float lean = LeanDegrees * Mathf.Deg2Rad;
            Vector3 torso = (up * Mathf.Cos(lean) + forward * Mathf.Sin(lean)).normalized;
            Aim(_spine1, _spine2, torso, blend);
            Aim(_spine2, _spine3, torso, blend);
            Aim(_spine3, _neck, torso, blend);
            if (_head != null) _head.rotation = Quaternion.AngleAxis(-LeanDegrees * blend * 0.7f, right) * _head.rotation;

            // The axe arm brings the hand in front of the chest, then the wrist turns the handle up and across.
            Aim(_upperR, _lowerR, ToWorld(AxeUpperArm, right, up, forward), blend);
            Aim(_lowerR, _handR, ToWorld(AxeForearm, right, up, forward), blend);
            if (FindAxe())
            {
                Vector3 wanted = ToWorld(AxeHandle, right, up, forward);
                Quaternion turn = Quaternion.FromToRotation(AxeHandleDirection(), wanted);
                _handR.rotation = Quaternion.Slerp(Quaternion.identity, turn, blend) * _handR.rotation;

                // The left hand reaches over and grips the handle; the elbow points down and out to the left.
                Vector3 grip = _axeGrip.position + AxeHandleDirection() * LeftGripAlongHandle;
                Vector3 target = Vector3.Lerp(_handL.position, grip, blend);
                SolveLimb(_upperL, _lowerL, _handL, target, (-up * 0.8f - right * 0.6f - forward * 0.2f).normalized);
            }

            // Legs last: the hips are lower now, so bend each leg to put its foot back where it was.
            if (legs)
            {
                SolveLimb(_thighL, _calfL, _footL, footLPosition, forward);
                SolveLimb(_thighR, _calfR, _footR, footRPosition, forward);
                _footL.rotation = footLRotation;
                _footR.rotation = footRRotation;
            }
        }

        private static Vector3 ToWorld(Vector3 local, Vector3 right, Vector3 up, Vector3 forward)
        {
            return (right * local.x + up * local.y + forward * local.z).normalized;
        }

        // Two bone solve: puts the end bone at the target by bending the middle joint toward the pole direction.
        private static void SolveLimb(Transform root, Transform middle, Transform end, Vector3 target, Vector3 pole)
        {
            float upperLength = Vector3.Distance(root.position, middle.position);
            float lowerLength = Vector3.Distance(middle.position, end.position);
            Vector3 toTarget = target - root.position;
            float distance = Mathf.Clamp(toTarget.magnitude, 0.05f, upperLength + lowerLength - 0.001f);
            Vector3 direction = toTarget.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(pole, direction);
            bend = bend.sqrMagnitude > 1e-6f ? bend.normalized : Vector3.Cross(direction, Vector3.right).normalized;

            float cosine = Mathf.Clamp((upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * upperLength * distance), -1f, 1f);
            float angle = Mathf.Acos(cosine);
            Vector3 joint = root.position + (direction * Mathf.Cos(angle) + bend * Mathf.Sin(angle)) * upperLength;
            Aim(root, middle, (joint - root.position).normalized, 1f);
            Aim(middle, end, (target - middle.position).normalized, 1f);
        }

        // Swings a bone so the line from it to its child points along desiredWorld, by the given fraction.
        private static void Aim(Transform bone, Transform child, Vector3 desiredWorld, float blend)
        {
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-8f) return;
            Quaternion delta = Quaternion.FromToRotation(current.normalized, desiredWorld);
            bone.rotation = Quaternion.Slerp(bone.rotation, delta * bone.rotation, blend);
        }

        private static Transform FindDeep(Transform root, string boneName)
        {
            if (root.name == boneName) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, boneName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
