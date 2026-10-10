using System.Collections.Generic;
using UnityEngine;

namespace Chopper.World
{
    // Where the water is and how deep it is at a spot. Every pond and marsh pool registers its circle and surface
    // height; depth is the surface height minus the terrain height there, so walking out of a pool makes the water
    // shallow on its own. The player wades in shallows and swims once the water is deep enough.
    public class WaterSurface : MonoBehaviour
    {
        private static readonly List<WaterSurface> _all = new List<WaterSurface>();
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");

        [SerializeField] private float _radius = 10f;
        [Tooltip("Inside this radius there is no water (the sea is a ring around the island).")]
        [SerializeField] private float _innerRadius;
        [SerializeField] private Vector2 _scroll = new Vector2(0.012f, 0.007f);

        private Material _material;
        private Vector2 _offset;

        public static IReadOnlyList<WaterSurface> All => _all;
        public float Radius => _radius;
        public float InnerRadius => _innerRadius;
        public float SurfaceY => transform.position.y;

        public void Configure(float radius, float innerRadius = 0f)
        {
            _radius = radius;
            _innerRadius = innerRadius;
        }

        private void OnEnable()
        {
            _all.Add(this);
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null) _material = meshRenderer.material;
        }

        private void OnDisable()
        {
            _all.Remove(this);
        }

        // Slowly drifting ripples.
        private void Update()
        {
            if (_material == null || !_material.HasProperty(BumpMapId)) return;
            _offset += _scroll * Time.deltaTime;
            _material.SetTextureOffset(BumpMapId, _offset);
        }
    }
}
