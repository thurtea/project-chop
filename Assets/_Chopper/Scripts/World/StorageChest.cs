using System.Collections.Generic;
using UnityEngine;

namespace Chopper.World
{
    // The storage chest in the house: walk up and interact to open the shared chest (SharedChest holds what is in
    // it). A plain scene object; the server checks positions only.
    public class StorageChest : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float _reach = 2.4f;

        private static readonly List<StorageChest> _all = new List<StorageChest>();

        private void OnEnable() => _all.Add(this);

        private void OnDisable() => _all.Remove(this);

        public bool IsInReach(Vector3 position)
        {
            Vector3 offset = transform.position - position;
            if (Mathf.Abs(offset.y) > 3f) return false;
            offset.y = 0f;
            return offset.magnitude <= _reach;
        }

        public static bool AnyInReach(Vector3 position)
        {
            foreach (StorageChest chest in _all)
            {
                if (chest != null && chest.isActiveAndEnabled && chest.IsInReach(position)) return true;
            }
            return false;
        }
    }
}
