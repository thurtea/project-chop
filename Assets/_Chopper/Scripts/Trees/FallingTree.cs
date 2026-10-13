using System.Collections.Generic;
using UnityEngine;

namespace Chopper.Trees
{
    // Local only: the server keeps the log's state (TreeRemains) and every peer shows it. Only the trunk above the
    // stump and the crown fall: the mesh is cut at the stump top and hinges there. It topples slowly, then faster, and
    // lies on the ground as a log until it is chopped up (W11-09), then breaks apart in a burst of chips.
    public class FallingTree : MonoBehaviour
    {
        public const float FallDuration = 1.5f;
        private const float JoltSeconds = 0.25f;
        private const float JoltHeight = 0.1f;
        private const float BreakDuration = 0.4f;
        private const float FallDegrees = 88f;

        private Quaternion _startRotation;
        private Vector3 _hinge;
        private Vector3 _localHinge;
        private Vector3 _axis;
        private float _startTime;
        private float _scale;
        private System.Action<Vector3> _onBreak;
        private bool _broken;
        private float _breakTime;
        private Vector3 _breakCenter;
        private float _joltTime = -1f;
        private float _groundY;
        private float _restLift = -1f;

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

        // landed: already lying on the ground (loading, late join).
        public static FallingTree Spawn(GameObject model, Vector3 position, Quaternion rotation, float scale, Vector3 fallDirection, Chopper.Core.Element zone,
            Vector3 cut, System.Action<Vector3> onBreak, bool landed = false, float groundY = float.NaN)
        {
            GameObject go = Instantiate(model, position, rotation);
            go.transform.localScale = Vector3.one * scale;
            TreeTint.Apply(go, zone);
            foreach (Collider collider in go.GetComponentsInChildren<Collider>()) Destroy(collider);
            var falling = go.AddComponent<FallingTree>();
            Vector3 flat = new Vector3(fallDirection.x, 0f, fallDirection.z);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
            falling._axis = Vector3.Cross(Vector3.up, flat.normalized);
            falling._startRotation = rotation;
            falling._hinge = cut;
            falling._localHinge = Quaternion.Inverse(rotation) * ((cut - position) / Mathf.Max(0.01f, scale));
            falling._startTime = landed ? Time.time - FallDuration : Time.time;
            falling._scale = scale;
            falling._onBreak = onBreak;
            falling._groundY = float.IsNaN(groundY) ? cut.y : groundY;
            CutBelow(go, falling._localHinge.y);
            falling.Apply(landed ? FallDegrees : 0f);
            return falling;
        }

        public bool Landed => Time.time - _startTime >= FallDuration;

        // A chop on the log: it jumps a little.
        public void Jolt() => _joltTime = Time.time;

        // The log is used up: it breaks apart in chips.
        public void Break()
        {
            if (_broken) return;
            _broken = true;
            _breakTime = Time.time;
            Apply(FallDegrees);
            _breakCenter = transform.position;
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
                _breakCenter = bounds.center;
            }
            _onBreak?.Invoke(_breakCenter);
        }

        // Everything below the cut (stump, roots, dirt pad) stays behind: faces wholly below are dropped and faces
        // crossing the cut are clamped to it. Heights are in the tree root's space (the mesh is on its side from the export).
        private static void CutBelow(GameObject go, float cutY)
        {
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>())
            {
                Mesh source = filter.sharedMesh;
                if (source == null || !source.isReadable) continue;
                Vector3[] verts = source.vertices;
                int[] tris = source.triangles;
                Matrix4x4 toRoot = go.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Matrix4x4 toMesh = toRoot.inverse;
                var heights = new float[verts.Length];
                for (int v = 0; v < verts.Length; v++) heights[v] = toRoot.MultiplyPoint3x4(verts[v]).y;
                var kept = new List<int>(tris.Length);
                bool clamped = false;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    float top = Mathf.Max(heights[tris[i]], Mathf.Max(heights[tris[i + 1]], heights[tris[i + 2]]));
                    if (top <= cutY) continue;
                    kept.Add(tris[i]);
                    kept.Add(tris[i + 1]);
                    kept.Add(tris[i + 2]);
                }
                for (int v = 0; v < verts.Length; v++)
                {
                    if (heights[v] >= cutY) continue;
                    Vector3 p = toRoot.MultiplyPoint3x4(verts[v]);
                    p.y = cutY;
                    verts[v] = toMesh.MultiplyPoint3x4(p);
                    clamped = true;
                }
                if (kept.Count == tris.Length && !clamped) continue;
                Mesh cut = Instantiate(source);
                cut.vertices = verts;
                cut.triangles = kept.ToArray();
                cut.RecalculateBounds();
                filter.sharedMesh = cut;
            }
        }

        private void Update()
        {
            if (!_broken)
            {
                float elapsed = Time.time - _startTime;
                if (elapsed < FallDuration)
                {
                    Apply(FallDegrees * (elapsed / FallDuration) * (elapsed / FallDuration));
                    return;
                }
                Apply(FallDegrees);
                float jolt = Time.time - _joltTime;
                if (_joltTime >= 0f && jolt < JoltSeconds) transform.position += Vector3.up * (JoltHeight * Mathf.Sin(jolt / JoltSeconds * Mathf.PI));
                return;
            }

            float breakElapsed = Time.time - _breakTime;
            if (breakElapsed > BreakDuration)
            {
                Destroy(gameObject);
                return;
            }
            float size = Mathf.Max(0.01f, _scale * (1f - breakElapsed / BreakDuration));
            transform.position = _breakCenter + (transform.position - _breakCenter) * (size / transform.localScale.x);
            transform.localScale = Vector3.one * size;
        }

        // The hinge (the stump cut) stays put while the trunk rotates about it. Once it has landed, the mesh is lifted
        // so the log lies on the ground instead of clipping through it.
        private void Apply(float degrees)
        {
            Quaternion rot = Quaternion.AngleAxis(degrees, _axis) * _startRotation;
            transform.rotation = rot;
            transform.position = _hinge - rot * (_localHinge * transform.localScale.x);
            if (degrees < FallDegrees - 0.01f) return;
            if (_restLift < 0f) _restLift = LiftToGround();
            if (_restLift > 0f) transform.position += Vector3.up * _restLift;
        }

        private float LiftToGround()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            float minY = float.PositiveInfinity;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (renderers[i].bounds.min.y < minY) minY = renderers[i].bounds.min.y;
            }
            if (minY == float.PositiveInfinity) return 0f;
            return Mathf.Max(0f, _groundY - minY);
        }
    }
}
