using System.Collections.Generic;
using UnityEngine;

namespace Chopper.World
{
    // Makes the grass and flowers under this object lean away from anything registered in GrassPushers, then
    // spring back once it has passed. Plants are bucketed into a coarse grid so each frame only the few plants
    // near a pusher are touched. Plants are ordinary meshes (not batched static), so no shader work is needed.
    public class GrassField : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _cellSize = 6f;
        [Tooltip("Added to each pusher radius: plants this much further out still start to lean.")]
        [SerializeField, Min(0.1f)] private float _pushRadiusPadding = 0.6f;
        [SerializeField, Range(0f, 80f)] private float _maxBendDegrees = 42f;
        [Tooltip("How quickly a plant bends away when pushed, per second.")]
        [SerializeField, Min(0.1f)] private float _bendSpeed = 12f;
        [Tooltip("How quickly a plant springs back once free, per second.")]
        [SerializeField, Min(0.1f)] private float _recoverSpeed = 2.5f;

        private Transform[] _plants;
        private Quaternion[] _baseRotation;
        private float[] _bend;
        private float[] _target;
        private Vector3[] _pushDirection;
        private bool[] _isActive;
        private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
        private readonly List<int> _active = new List<int>();

        private static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

        private void Start()
        {
            int count = transform.childCount;
            _plants = new Transform[count];
            _baseRotation = new Quaternion[count];
            _bend = new float[count];
            _target = new float[count];
            _pushDirection = new Vector3[count];
            _isActive = new bool[count];

            for (int i = 0; i < count; i++)
            {
                Transform plant = transform.GetChild(i);
                _plants[i] = plant;
                _baseRotation[i] = plant.rotation;
                int cx = Mathf.FloorToInt(plant.position.x / _cellSize);
                int cz = Mathf.FloorToInt(plant.position.z / _cellSize);
                long key = Key(cx, cz);
                if (!_cells.TryGetValue(key, out List<int> list))
                {
                    list = new List<int>();
                    _cells[key] = list;
                }
                list.Add(i);
            }
        }

        private void Update()
        {
            if (_plants == null) return;

            for (int a = 0; a < _active.Count; a++) _target[_active[a]] = 0f;

            IReadOnlyList<GrassPushers.Pusher> pushers = GrassPushers.All;
            for (int p = 0; p < pushers.Count; p++)
            {
                Transform pusher = pushers[p].Transform;
                if (pusher == null) continue;
                Push(pusher.position, pushers[p].Radius + _pushRadiusPadding);
            }

            float dt = Time.deltaTime;
            for (int a = _active.Count - 1; a >= 0; a--)
            {
                int i = _active[a];
                float speed = _target[i] > _bend[i] ? _bendSpeed : _recoverSpeed;
                _bend[i] = Mathf.MoveTowards(_bend[i], _target[i], speed * dt);

                if (_bend[i] <= 0.001f && _target[i] <= 0f)
                {
                    _bend[i] = 0f;
                    _plants[i].rotation = _baseRotation[i];
                    _isActive[i] = false;
                    _active.RemoveAt(a);
                    continue;
                }

                Vector3 axis = Vector3.Cross(Vector3.up, _pushDirection[i]);
                _plants[i].rotation = Quaternion.AngleAxis(_bend[i] * _maxBendDegrees, axis) * _baseRotation[i];
            }
        }

        private void Push(Vector3 center, float radius)
        {
            int minX = Mathf.FloorToInt((center.x - radius) / _cellSize);
            int maxX = Mathf.FloorToInt((center.x + radius) / _cellSize);
            int minZ = Mathf.FloorToInt((center.z - radius) / _cellSize);
            int maxZ = Mathf.FloorToInt((center.z + radius) / _cellSize);
            float radiusSqr = radius * radius;

            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cz = minZ; cz <= maxZ; cz++)
                {
                    if (!_cells.TryGetValue(Key(cx, cz), out List<int> list)) continue;
                    for (int k = 0; k < list.Count; k++)
                    {
                        int i = list[k];
                        if (_plants[i] == null || !_plants[i].gameObject.activeInHierarchy) continue;
                        Vector3 offset = _plants[i].position - center;
                        offset.y = 0f;
                        float sqr = offset.sqrMagnitude;
                        if (sqr >= radiusSqr) continue;

                        float distance = Mathf.Sqrt(sqr);
                        // Smooth falloff: full lean at the pusher's feet, none at the edge of the radius.
                        float strength = 1f - distance / radius;
                        strength = strength * strength * (3f - 2f * strength);
                        if (strength <= _target[i]) continue;

                        _target[i] = strength;
                        _pushDirection[i] = distance > 0.01f ? offset / distance : Vector3.forward;
                        if (!_isActive[i])
                        {
                            _isActive[i] = true;
                            _active.Add(i);
                        }
                    }
                }
            }
        }
    }
}
