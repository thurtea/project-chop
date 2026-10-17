using System.Collections.Generic;
using UnityEngine;

namespace Chopper.Player
{
    // Keeps feet on the real ground under them, for the player and every rigged enemy. The CharacterController's round
    // bottom rests on a step edge or a slope at one point, so the animated feet sink into the higher side and float over
    // the lower one. After the Animator (and GuardPose) have posed the body, each foot is ray cast to the ground below it;
    // the hips drop to the lowest planted foot's ground and each leg is bent with a two bone solve so its foot never
    // goes below the surface. Added at run time to every Animator that has leg bones (see Spawner below).
    [DefaultExecutionOrder(200)]
    public class FootGrounder : MonoBehaviour
    {
        public static readonly List<FootGrounder> All = new List<FootGrounder>();
        private const float MaxDrop = 0.4f;
        private const float MaxRaise = 0.3f;
        private const float PlantedHeight = 0.1f;
        private const float HipSpeed = 2.5f;

        private Transform _hips, _thighL, _calfL, _footL, _thighR, _calfR, _footR, _toesL, _toesR;
        private CharacterController _controller;
        private PlayerMotor _motor;
        private float _ankle = -1f;
        private float _hip;
        private int _calib;
        private bool _meshFit;
        public bool MeshFit => _meshFit;
        public float FittedRadius { get; private set; }
        public float Ankle => _ankle;
        public bool Ready { get; private set; }
        // Last measured gap between each sole and the ground below it, after grounding (negative = sunk in).
        public float GapL { get; private set; }
        public float GapR { get; private set; }
        public Transform FootL => _footL;
        public Transform FootR => _footR;

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() => All.Remove(this);

        public bool Find()
        {
            Animator a = GetComponent<Animator>();
            if (a != null && a.isHuman)
            {
                _hips = a.GetBoneTransform(HumanBodyBones.Hips);
                _thighL = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg); _calfL = a.GetBoneTransform(HumanBodyBones.LeftLowerLeg); _footL = a.GetBoneTransform(HumanBodyBones.LeftFoot);
                _thighR = a.GetBoneTransform(HumanBodyBones.RightUpperLeg); _calfR = a.GetBoneTransform(HumanBodyBones.RightLowerLeg); _footR = a.GetBoneTransform(HumanBodyBones.RightFoot);
                _toesL = a.GetBoneTransform(HumanBodyBones.LeftToes); _toesR = a.GetBoneTransform(HumanBodyBones.RightToes);
            }
            if (_footL == null)
            {
                _hips = Deep("pelvis") ?? Deep("Hips");
                _thighL = Deep("thigh_l") ?? Deep("LeftUpperLeg"); _calfL = Deep("calf_l") ?? Deep("LeftLowerLeg"); _footL = Deep("foot_l") ?? Deep("LeftFoot");
                _thighR = Deep("thigh_r") ?? Deep("RightUpperLeg"); _calfR = Deep("calf_r") ?? Deep("RightLowerLeg"); _footR = Deep("foot_r") ?? Deep("RightFoot");
                _toesL = Deep("ball_l") ?? Deep("LeftToes"); _toesR = Deep("ball_r") ?? Deep("RightToes");
            }
            _controller = GetComponentInParent<CharacterController>();
            Ready = _hips != null && _thighL != null && _calfL != null && _footL != null && _thighR != null && _calfR != null && _footR != null;
            return Ready;
        }

        private Transform Deep(string n)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
            return null;
        }

        private float Sole(Transform foot, Transform toes)
        {
            float y = foot.position.y - _ankle;
            return y;
        }

        // The ground under a point: the nearest solid surface below it that is not part of this character.
        public float Ground(Vector3 at, out bool hit)
        {
            hit = false;
            float best = float.NegativeInfinity;
            Transform self = transform.root;
            foreach (RaycastHit h in Physics.RaycastAll(at + Vector3.up * 0.7f, Vector3.down, 1.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider is CharacterController || h.collider.transform.IsChildOf(self)) continue;
                if (h.point.y > best) { best = h.point.y; hit = true; }
            }
            return best;
        }

        private void LateUpdate()
        {
            if (!Ready && !Find()) { enabled = false; return; }
            if (_motor == null) _motor = GetComponentInParent<PlayerMotor>();
            if (_motor != null && _motor.IsClimbing) return;
            float baseY = transform.position.y;
            if (_calib < 40)
            {
                float a = Mathf.Clamp(Mathf.Min(_footL.position.y, _footR.position.y) - baseY, 0.02f, 0.25f);
                _ankle = _ankle < 0f ? a : Mathf.Min(_ankle, a);
                _calib++;
                if (_calib == 12 && FitToMesh()) _calib = 40;
                return;
            }
            float gL = Ground(_footL.position, out bool hitL);
            float gR = Ground(_footR.position, out bool hitR);
            bool grounded = _controller != null ? (_controller.enabled ? _controller.isGrounded : (hitL || hitR)) : (hitL || hitR);
            float want = 0f;
            bool any = false;
            if (grounded)
            {
                float hL = _footL.position.y - baseY - _ankle, hR = _footR.position.y - baseY - _ankle;
                if (hitL && hL < PlantedHeight) { want = gL - (_footL.position.y - _ankle); any = true; }
                if (hitR && hR < PlantedHeight) { float d = gR - (_footR.position.y - _ankle); want = any ? Mathf.Min(want, d) : d; any = true; }
                if (!any) want = 0f;
            }
            want = Mathf.Clamp(want, -MaxDrop, MaxRaise);
            _hip = Mathf.MoveTowards(_hip, want, HipSpeed * Time.deltaTime + Mathf.Abs(want - _hip) * 0.25f);
            _hips.position += Vector3.up * _hip;
            Vector3 pole = transform.forward;
            if (hitL) Lift(_thighL, _calfL, _footL, gL, pole);
            if (hitR) Lift(_thighR, _calfR, _footR, gR, pole);
            GapL = hitL ? (_footL.position.y - _ankle) - gL : 0f;
            GapR = hitR ? (_footR.position.y - _ankle) - gR : 0f;
        }

        private bool FitToMesh()
        {
            SkinnedMeshRenderer[] skins = GetComponentsInChildren<SkinnedMeshRenderer>();
            if (skins.Length == 0) return false;
            Transform body = _controller != null ? _controller.transform : transform;
            float baseY = body.position.y;
            Vector3 fl = _footL.position, fr = _footR.position;
            float lowL = float.PositiveInfinity, lowR = float.PositiveInfinity, top = float.NegativeInfinity;
            var mesh = new Mesh();
            var verts = new List<Vector3>();
            var all = new List<Vector3>();
            foreach (SkinnedMeshRenderer s in skins)
            {
                if (!s.enabled || s.sharedMesh == null) continue;
                s.BakeMesh(mesh, true);
                mesh.GetVertices(verts);
                Matrix4x4 m = Matrix4x4.TRS(s.transform.position, s.transform.rotation, Vector3.one);
                foreach (Vector3 v in verts)
                {
                    Vector3 w = m.MultiplyPoint3x4(v);
                    all.Add(w);
                    top = Mathf.Max(top, w.y);
                    float dl = new Vector2(w.x - fl.x, w.z - fl.z).magnitude, dr = new Vector2(w.x - fr.x, w.z - fr.z).magnitude;
                    if (dl < 0.22f && dl <= dr && w.y < fl.y + 0.05f) lowL = Mathf.Min(lowL, w.y);
                    if (dr < 0.22f && dr < dl && w.y < fr.y + 0.05f) lowR = Mathf.Min(lowR, w.y);
                }
            }
            Destroy(mesh);
            if (float.IsInfinity(lowL) || float.IsInfinity(lowR)) return false;
            _ankle = Mathf.Clamp(((fl.y - lowL) + (fr.y - lowR)) * 0.5f, 0.02f, 0.25f);
            _meshFit = true;
            if (_controller != null)
            {
                float h = top - baseY;
                var radial = new List<float>();
                Vector3 c = body.position;
                foreach (Vector3 w in all)
                {
                    float y = w.y - baseY;
                    if (y < h * 0.35f || y > h * 0.65f) continue;
                    radial.Add(new Vector2(w.x - c.x, w.z - c.z).magnitude);
                }
                if (radial.Count > 20)
                {
                    radial.Sort();
                    float r = Mathf.Clamp(radial[(int)(radial.Count * 0.75f)] + 0.04f, 0.28f, 0.5f);
                    _controller.radius = r;
                    FittedRadius = r;
                }
            }
            Debug.Log($"[FootGrounder] {transform.root.name} mesh fit ankle {_ankle:F3} radius {FittedRadius:F2} top {top - baseY:F2}");
            return true;
        }

        private void Lift(Transform thigh, Transform calf, Transform foot, float ground, Vector3 pole)
        {
            float sole = foot.position.y - _ankle;
            if (sole >= ground) return;
            Quaternion keep = foot.rotation;
            Vector3 target = foot.position + Vector3.up * (ground - sole);
            Solve(thigh, calf, foot, target, pole);
            foot.rotation = keep;
        }

        private static void Solve(Transform root, Transform middle, Transform end, Vector3 target, Vector3 pole)
        {
            float a = Vector3.Distance(root.position, middle.position);
            float b = Vector3.Distance(middle.position, end.position);
            Vector3 to = target - root.position;
            float d = Mathf.Clamp(to.magnitude, 0.05f, a + b - 0.001f);
            Vector3 dir = to.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(pole, dir);
            bend = bend.sqrMagnitude > 1e-6f ? bend.normalized : Vector3.Cross(dir, Vector3.right).normalized;
            float cos = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
            float ang = Mathf.Acos(cos);
            Vector3 joint = root.position + (dir * Mathf.Cos(ang) + bend * Mathf.Sin(ang)) * a;
            Aim(root, middle, (joint - root.position).normalized);
            Aim(middle, end, (target - middle.position).normalized);
        }

        private static void Aim(Transform bone, Transform child, Vector3 want)
        {
            Vector3 cur = child.position - bone.position;
            if (cur.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.FromToRotation(cur.normalized, want) * bone.rotation;
        }

        // Puts a FootGrounder on every Animator with legs, as characters and enemies spawn.
        private class Spawner : MonoBehaviour
        {
            private float _next;
            private void Update()
            {
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + 0.5f;
                foreach (Animator a in FindObjectsByType<Animator>(FindObjectsSortMode.None))
                {
                    if (a.GetComponent<FootGrounder>() != null) continue;
                    var g = a.gameObject.AddComponent<FootGrounder>();
                    if (!g.Find()) g.enabled = false;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("FootGrounderSpawner") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<Spawner>();
        }
    }
}
