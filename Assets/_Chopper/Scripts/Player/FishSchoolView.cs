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
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color Shadow = new Color(0.05f, 0.09f, 0.13f, 1f);

        private struct Shown
        {
            public Transform Body;
            public Vector3 Target;
            public SchoolFishState State;
            public float FledAt;
        }

        private Shown[] _fish = new Shown[0];

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

        public void Move(Vector3[] snapshot)
        {
            if (snapshot == null) return;
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
                Vector3 next = Vector3.Lerp(before, _fish[i].Target, 1f - Mathf.Exp(-GlideSharpness * dt));
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
                    Quaternion look = Quaternion.LookRotation(heading.normalized) * Quaternion.Euler(0f, wiggle, 0f);
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
            if (material == null) return null;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SchoolFish";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(length * 0.32f, 0.008f, length);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, Shadow);
            renderer.SetPropertyBlock(block);
            return go.transform;
        }
    }
}
