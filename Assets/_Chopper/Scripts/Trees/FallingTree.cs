using System.Collections.Generic;
using UnityEngine;

namespace Chopper.Trees
{
    // Local only (plan D14: felled trees are not networked). The trunk hinges at the ground, where the stump stays,
    // so the dirt pad and roots are left in the earth instead of swinging up with the crown. It topples slowly at
    // first and then faster, lies for a moment, then sinks and is destroyed.
    public class FallingTree : MonoBehaviour
    {
        public const float FallDuration = 1.5f;
        private const float RestDuration = 3f;
        private const float SinkDuration = 1.5f;
        private const float SinkDepth = 4f;
        private const float FallDegrees = 88f;

        private Quaternion _startRotation;
        private Vector3 _hinge;
        private Vector3 _localHinge;
        private Vector3 _axis;
        private float _startTime;

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

        public static void Spawn(GameObject model, Vector3 position, Quaternion rotation, float scale, Vector3 fallDirection, Chopper.Core.Element zone, Vector3 ground)
        {
            GameObject go = Instantiate(model, position, rotation);
            go.transform.localScale = Vector3.one * scale;
            TreeTint.Apply(go, zone);
            var falling = go.AddComponent<FallingTree>();
            Vector3 flat = new Vector3(fallDirection.x, 0f, fallDirection.z);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
            falling._axis = Vector3.Cross(Vector3.up, flat.normalized);
            falling._startRotation = rotation;
            falling._hinge = ground;
            falling._localHinge = Quaternion.Inverse(rotation) * ((ground - position) / Mathf.Max(0.01f, scale));
            falling._startTime = Time.time;
            StripPad(go, falling._localHinge.y);
            falling.Apply(0f);
        }

        // The dirt pad sits below the ground line. Drop those faces so only the trunk tips over the stump.
        // Height is measured in the tree root's space: the mesh itself is still on its side from the Blender export.
        private static void StripPad(GameObject go, float cutY)
        {
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>())
            {
                Mesh source = filter.sharedMesh;
                if (source == null || !source.isReadable) continue;
                Vector3[] verts = source.vertices;
                int[] tris = source.triangles;
                var kept = new List<int>(tris.Length);
                float Height(int index)
                {
                    return go.transform.InverseTransformPoint(filter.transform.TransformPoint(verts[index])).y;
                }
                for (int i = 0; i < tris.Length; i += 3)
                {
                    float y = Mathf.Max(Height(tris[i]), Mathf.Max(Height(tris[i + 1]), Height(tris[i + 2])));
                    if (y >= cutY - 0.02f)
                    {
                        kept.Add(tris[i]);
                        kept.Add(tris[i + 1]);
                        kept.Add(tris[i + 2]);
                    }
                }
                if (kept.Count == tris.Length) continue;
                Mesh cut = Instantiate(source);
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

            Apply(FallDegrees);
            float sinkElapsed = elapsed - FallDuration - RestDuration;
            if (sinkElapsed > SinkDuration)
            {
                Destroy(gameObject);
                return;
            }
            if (sinkElapsed > 0f) _hinge += Vector3.down * (SinkDepth * Time.deltaTime / SinkDuration);
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
