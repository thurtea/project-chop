using Chopper.Core;
using Chopper.World.Generation;
using UnityEngine;

namespace Chopper.World
{
    // Stepped flagstones on the hub paths and the front steps of the hall. Positions come from the world
    // seed, the same on every peer, so nothing is networked.
    public class HubPathStones : MonoBehaviour
    {
        private const float PathSpacing = 5.2f;
        private const float PathStart = 18f;

        [SerializeField] private GameObject _model;

        private void Awake()
        {
            WorldSession.OnReady += Build;
        }

        private void OnDestroy()
        {
            WorldSession.OnReady -= Build;
        }

        private void Build()
        {
            if (_model == null || WorldSession.Gen == null) return;
            if (transform.Find("PathStones") != null) return;

            var root = new GameObject("PathStones").transform;
            root.SetParent(transform, false);
            WorldGen gen = WorldSession.Gen;
            int count = 0;
            PlaceStairs(root, ref count);
            float end = gen.HubRadius - 8f;
            foreach (Element forest in WorldGen.Forests)
            {
                Vector2 axis = ZoneMap.Axis(forest);
                Vector2 side = new Vector2(-axis.y, axis.x);
                for (float along = PathStart; along < end; along += PathSpacing)
                {
                    Vector2 point = gen.PathPoint(forest, along);
                    Vector2 ahead = gen.PathPoint(forest, along + 2f);
                    float wobble = ((count * 17) % 5 - 2) * 0.22f;
                    Vector2 at = point + side * wobble;
                    if (gen.InWater(at, 0.9f)) continue;
                    if (Vector2.Distance(at, gen.PondCenter) < WorldGen.PondOuterRadius + 1f) continue;
                    if (gen.MountainWeight(at.x, at.y) > 0.35f) continue;
                    Place(root, at.x, at.y, ahead - point, 0.9f + (count % 4) * 0.06f, ref count);
                }
            }
            Debug.Log($"[HubPaths] placed {count}");
        }

        private void PlaceStairs(Transform root, ref int count)
        {
            for (int i = 0; i < 7; i++)
            {
                float z = WorldGen.HouseHalfZ + 0.8f + i * 1.05f;
                float x = WorldGen.TerraceStairsX + (i % 2) * 0.2f;
                Place(root, x, z, new Vector2(0f, 1f), 0.58f, ref count);
            }
        }

        private void Place(Transform root, float x, float z, Vector2 tangent, float scale, ref int count)
        {
            if (tangent.sqrMagnitude < 1e-4f) tangent = Vector2.up;
            tangent.Normalize();
            // The mesh is long on local X. LookRotation's forward is local Z, so a left turn of the
            // tangent puts the long axis along the path.
            Vector3 forward = new Vector3(-tangent.y, 0f, tangent.x);
            float y = WorldGround.Exists ? WorldGround.HeightAt(x, z) : 0f;
            GameObject stone = Instantiate(_model, new Vector3(x, y, z), Quaternion.LookRotation(forward, Vector3.up), root);
            stone.name = "PathStone_" + count;
            stone.transform.localScale = Vector3.one * scale;
            foreach (Collider collider in stone.GetComponentsInChildren<Collider>()) Destroy(collider);
            count++;
        }
    }
}
