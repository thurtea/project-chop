using System.Collections.Generic;
using UnityEngine;

namespace Chopper.Trees
{
    // Local only (plan D14: felled trees are not networked). Only the trunk above the stump and the crown fall: the
    // mesh is cut at the stump top and hinges there. It topples slowly, then faster, lies on the ground, then breaks
    // apart in a burst of chips.
    public class FallingTree : MonoBehaviour
    {
        public const float FallDuration = 1.5f;
        private const float RestDuration = 6f;
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
        private Vector3 _breakCenter;

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

        public static void Spawn(GameObject model, Vector3 position, Quaternion rotation, float scale, Vector3 fallDirection, Chopper.Core.Element zone,
            Vector3 cut, System.Action<Vector3> onBreak)
        {
            GameObject go = Instantiate(model, position, rotation);
            go.transform.localScale = Vector3.one * scale;
            TreeTint.Apply(go, zone);
            var falling = go.AddComponent<FallingTree>();
            Vector3 flat = new Vector3(fallDirection.x, 0f, fallDirection.z);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
            falling._axis = Vector3.Cross(Vector3.up, flat.normalized);
            falling._startRotation = rotation;
            falling._hinge = cut;
            falling._localHinge = Quaternion.Inverse(rotation) * ((cut - position) / Mathf.Max(0.01f, scale));
            falling._startTime = Time.time;
            falling._scale = scale;
            falling._onBreak = onBreak;
            CutBelow(go, falling._localHinge.y);
            falling.Apply(0f);
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
            float elapsed = Time.time - _startTime;
            if (elapsed < FallDuration)
            {
                Apply(FallDegrees * (elapsed / FallDuration) * (elapsed / FallDuration));
                return;
            }

            float breakElapsed = elapsed - FallDuration - RestDuration;
            if (breakElapsed < 0f)
            {
                Apply(FallDegrees);
                return;
            }
            if (!_broken)
            {
                _broken = true;
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
            if (breakElapsed > BreakDuration)
            {
                Destroy(gameObject);
                return;
            }
            float size = Mathf.Max(0.01f, _scale * (1f - breakElapsed / BreakDuration));
            transform.position = _breakCenter + (transform.position - _breakCenter) * (size / transform.localScale.x);
            transform.localScale = Vector3.one * size;
        }

        // The hinge (the stump cut, on the ground) stays put. The trunk rotates about it.
        private void Apply(float degrees)
        {
            Quaternion rot = Quaternion.AngleAxis(degrees, _axis) * _startRotation;
            transform.rotation = rot;
            transform.position = _hinge - rot * (_localHinge * transform.localScale.x);
        }
    }
}
