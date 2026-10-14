using Chopper.Core;
using UnityEngine;

namespace Chopper.Player
{
    // The fish under a cast, drawn as flat dark shapes just under the surface for everyone near (the Ocarina of Time
    // fishing pond look); the water shader shows them through the ripples, fading with depth. The server sends where they are a few times a second; the view glides them there, turns them
    // the way they swim, sizes them by weight, and lets the ones that fled fade away.
    public class FishSchoolView
    {
        private const float Lift = -0.12f;
        private const float GlideSharpness = 6f;
        private const float FleeFadeSeconds = 1.2f;
        private const float JumpSeconds = 0.9f;
        private const float JumpHeight = 0.78f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color Shadow = new Color(0.05f, 0.09f, 0.13f, 1f);

        private struct Shown
        {
            public Transform Body;
            public Vector3 Target;
            public SchoolFishState State;
            public float FledAt;
            public float Jump;
        }

        private Shown[] _fish = new Shown[0];
        private float _poseUntil;
        private float _surface;

        public int Count => _fish.Length;

        public bool AnyFollowing
        {
            get
            {
                foreach (Shown f in _fish) if (f.Body != null && f.State == SchoolFishState.Following) return true;
                return false;
            }
        }

        // True when a following fish is within distance of the lure (the float dips).
        public bool NearLure(Vector3 lure, float distance)
        {
            foreach (Shown f in _fish)
            {
                if (f.Body == null || f.State != SchoolFishState.Following) continue;
                Vector3 offset = f.Body.position - lure;
                offset.y = 0f;
                if (offset.magnitude <= distance) return true;
            }
            return false;
        }

        public void Begin(int[] kinds, float[] kgs, Vector3[] snapshot)
        {
            Clear();
            int n = Mathf.Min(kinds != null ? kinds.Length : 0, snapshot != null ? snapshot.Length : 0);
            _fish = new Shown[n];
            Material material = GearMaterial();
            for (int i = 0; i < n; i++)
            {
                float kg = kgs != null && i < kgs.Length ? kgs[i] : 1f;
                // Length grows with the cube root of the weight: a small perch is a hand long, Old Mossback a meter.
                float length = 0.3f + 0.35f * Mathf.Pow(Mathf.Max(0.05f, kg), 1f / 3f);
                Transform body = MakeBody(material, length);
                Vector3 at = Point(snapshot[i]);
                if (body != null) body.position = at;
                _fish[i] = new Shown { Body = body, Target = at, State = (SchoolFishState)Mathf.RoundToInt(snapshot[i].z) };
            }
        }

        public void Pose(Vector3 lure)
        {
            _poseUntil = Time.time + 4f;
            _surface = lure.y;
            for (int i = 0; i < _fish.Length; i++)
            {
                float ang = i * 1.9f;
                var at = new Vector3(lure.x + Mathf.Cos(ang) * 0.48f, _surface + Lift, lure.z + Mathf.Sin(ang) * 0.32f);
                Shown fish = _fish[i];
                fish.Target = at;
                fish.Jump = JumpSeconds;
                fish.State = SchoolFishState.Following;
                if (fish.Body != null)
                {
                    fish.Body.gameObject.SetActive(true);
                    fish.Body.position = at;
                }
                _fish[i] = fish;
            }
        }

        public void HoldJump()
        {
            for (int i = 0; i < _fish.Length; i++)
            {
                Shown fish = _fish[i];
                if (fish.Body == null) continue;
                fish.Jump = JumpSeconds;
                _fish[i] = fish;
            }
        }

        public void Sink(float below)
        {
            _poseUntil = Time.time + 4f;
            float y = _surface - below;
            for (int i = 0; i < _fish.Length; i++)
            {
                Shown fish = _fish[i];
                fish.Jump = 0f;
                Vector3 at = fish.Target;
                at.y = y;
                fish.Target = at;
                if (fish.Body != null) fish.Body.position = at;
                _fish[i] = fish;
            }
        }

        public void Move(Vector3[] snapshot)
        {
            if (snapshot == null || Time.time < _poseUntil) return;
            for (int i = 0; i < _fish.Length && i < snapshot.Length; i++)
            {
                var state = (SchoolFishState)Mathf.RoundToInt(snapshot[i].z);
                if (state == SchoolFishState.Fled && _fish[i].State != SchoolFishState.Fled) _fish[i].FledAt = Time.time;
                _fish[i].State = state;
                _fish[i].Target = Point(snapshot[i]);
            }
        }

        public void Clear()
        {
            foreach (Shown f in _fish) if (f.Body != null) Object.Destroy(f.Body.gameObject);
            _fish = new Shown[0];
        }

        public void Destroy() => Clear();

        public void Update(float dt, Vector3 lure)
        {
            for (int i = 0; i < _fish.Length; i++)
            {
                Transform body = _fish[i].Body;
                if (body == null) continue;
                if (_fish[i].State == SchoolFishState.Fled && Time.time - _fish[i].FledAt > FleeFadeSeconds)
                {
                    body.gameObject.SetActive(false);
                    continue;
                }
                Vector3 before = body.position;
                bool posing = Time.time < _poseUntil;
                Vector3 next = posing ? _fish[i].Target : Vector3.Lerp(before, _fish[i].Target, 1f - Mathf.Exp(-GlideSharpness * dt));
                if (!posing && _fish[i].State == SchoolFishState.Following && _fish[i].Jump <= 0f && Mathf.Repeat(Time.time * 0.23f + i * 0.17f, 1f) < 0.08f)
                    _fish[i].Jump = JumpSeconds;
                float jump = _fish[i].Jump;
                if (jump > 0f)
                {
                    _fish[i].Jump = jump - dt;
                    float u = 1f - Mathf.Clamp01(jump / JumpSeconds);
                    next.y = _surface + Mathf.Sin(u * Mathf.PI) * JumpHeight;
                }
                Vector3 heading = next - before;
                heading.y = 0f;
                // A fish that is barely moving keeps its nose toward the lure when it follows, else where it last swam.
                if (heading.sqrMagnitude < 0.00001f && _fish[i].State == SchoolFishState.Following && lure != Vector3.zero)
                {
                    heading = lure - next;
                    heading.y = 0f;
                }
                body.position = next;
                if (heading.sqrMagnitude > 0.00001f)
                {
                    float wiggle = Mathf.Sin(Time.time * 9f + i * 1.7f) * (heading.magnitude / Mathf.Max(dt, 0.0001f) > 0.3f ? 12f : 4f);
                    float nose = _fish[i].Jump > 0f ? Mathf.Sin((1f - _fish[i].Jump / JumpSeconds) * Mathf.PI) * -34f : 0f;
                    Quaternion look = Quaternion.LookRotation(heading.normalized) * Quaternion.Euler(nose, wiggle, 0f);
                    body.rotation = Quaternion.Slerp(body.rotation, look, 1f - Mathf.Exp(-8f * dt));
                }
            }
        }

        private static Vector3 Point(Vector3 snapshot)
        {
            return new Vector3(snapshot.x, Chopper.World.Generation.WorldGen.WaterHeight + Lift, snapshot.y);
        }

        private static Material GearMaterial()
        {
            CharacterCatalog catalog = Resources.Load<CharacterCatalog>("CharacterCatalog");
            return catalog != null ? catalog.GearMaterial : null;
        }

        private static Transform MakeBody(Material material, float length)
        {
            length = Mathf.Max(0.55f, length * 1.45f);
            var root = new GameObject("SchoolFish");
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material paint = shader != null ? new Material(shader) : material;
            AddPart(root.transform, paint, new Vector3(length * 0.28f, length * 0.2f, length * 0.62f), new Vector3(0f, length * 0.02f, length * 0.06f), Quaternion.identity, new Color(0.1f, 0.28f, 0.22f, 1f));
            AddPart(root.transform, paint, new Vector3(length * 0.24f, length * 0.08f, length * 0.48f), new Vector3(0f, -length * 0.06f, length * 0.04f), Quaternion.identity, new Color(0.78f, 0.82f, 0.72f, 1f));
            AddPart(root.transform, paint, new Vector3(length * 0.05f, length * 0.2f, length * 0.22f), new Vector3(0f, length * 0.02f, -length * 0.42f), Quaternion.Euler(0f, 18f, 0f), new Color(0.16f, 0.42f, 0.3f, 1f));
            AddPart(root.transform, paint, new Vector3(length * 0.05f, length * 0.16f, length * 0.18f), new Vector3(0f, length * 0.02f, -length * 0.4f), Quaternion.Euler(0f, -22f, 0f), new Color(0.85f, 0.42f, 0.16f, 1f));
            AddPart(root.transform, paint, new Vector3(length * 0.22f, length * 0.03f, length * 0.12f), new Vector3(length * 0.16f, 0f, length * 0.02f), Quaternion.Euler(0f, 0f, -18f), new Color(0.2f, 0.48f, 0.36f, 1f));
            AddPart(root.transform, paint, new Vector3(length * 0.22f, length * 0.03f, length * 0.12f), new Vector3(-length * 0.16f, 0f, length * 0.02f), Quaternion.Euler(0f, 0f, 18f), new Color(0.2f, 0.48f, 0.36f, 1f));
            AddPart(root.transform, paint, new Vector3(length * 0.03f, length * 0.12f, length * 0.16f), new Vector3(0f, length * 0.12f, 0f), Quaternion.identity, new Color(0.12f, 0.32f, 0.24f, 1f));
            AddPart(root.transform, paint, new Vector3(length * 0.06f, length * 0.06f, length * 0.06f), new Vector3(length * 0.08f, length * 0.06f, length * 0.28f), Quaternion.identity, new Color(0.95f, 0.86f, 0.25f, 1f));
            return root.transform;
        }

        private static void AddPart(Transform parent, Material material, Vector3 scale, Vector3 local, Quaternion rotation, Color color)
        {
            if (material == null) return;
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localScale = scale;
            part.transform.localPosition = local;
            part.transform.localRotation = rotation;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            if (material.HasProperty(BaseColorId)) block.SetColor(BaseColorId, color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }
    }
}
