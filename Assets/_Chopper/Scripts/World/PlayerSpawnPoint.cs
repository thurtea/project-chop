using System.Collections.Generic;
using UnityEngine;

namespace Chopper.World
{
    // Marks a hub position where players spawn. Registers itself so the spawner never has to search the scene.
    public class PlayerSpawnPoint : MonoBehaviour
    {
        [Tooltip("Outside the house, where new players start. Points inside (beside the bed) are used for respawns once the hut is repaired.")]
        [SerializeField] private bool _outside;

        private static readonly List<PlayerSpawnPoint> _active = new List<PlayerSpawnPoint>();

        public static IReadOnlyList<PlayerSpawnPoint> Active => _active;

        public bool Outside => _outside;

        // For points made at runtime (WorldBuilder); set before the object is enabled.
        public void SetOutside(bool outside) => _outside = outside;

        // The index-th point of the wanted kind (wrapping around). Falls back to any point when there is none of that kind.
        public static PlayerSpawnPoint Pick(bool outside, int index)
        {
            int count = 0;
            foreach (PlayerSpawnPoint point in _active) if (point._outside == outside) count++;
            if (count == 0) return _active.Count > 0 ? _active[Mathf.Abs(index) % _active.Count] : null;

            int wanted = Mathf.Abs(index) % count;
            foreach (PlayerSpawnPoint point in _active)
            {
                if (point._outside != outside) continue;
                if (wanted-- == 0) return point;
            }
            return null;
        }

        private void OnEnable()
        {
            _active.Add(this);
        }

        private void OnDisable()
        {
            _active.Remove(this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawRay(transform.position, transform.forward);
        }
    }
}
