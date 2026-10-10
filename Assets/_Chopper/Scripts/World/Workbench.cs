using System.Collections.Generic;
using UnityEngine;

namespace Chopper.World
{
    // A workbench: walk up and interact to open the workbench menu (crafting, upgrades, the forge and tool repairs).
    // Everything but the bandage is made at one. The hut has one by its east wall; players can build more (the
    // Workbench building piece carries this component too), and building other pieces needs one nearby. It is a plain
    // scene object: the server checks positions only.
    public class Workbench : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float _reach = 3f;

        private static readonly List<Workbench> _all = new List<Workbench>();

        public static IReadOnlyList<Workbench> All => _all;

        private void OnEnable() => _all.Add(this);

        private void OnDisable() => _all.Remove(this);

        public bool IsInReach(Vector3 position)
        {
            Vector3 offset = transform.position - position;
            if (Mathf.Abs(offset.y) > 3f) return false;
            offset.y = 0f;
            return offset.magnitude <= _reach;
        }

        // The workbench a player at this position can use, or null.
        public static Workbench InReach(Vector3 position)
        {
            foreach (Workbench bench in _all)
            {
                if (bench != null && bench.isActiveAndEnabled && bench.IsInReach(position)) return bench;
            }
            return null;
        }

        public static bool AnyInReach(Vector3 position) => InReach(position) != null;

        // True when a workbench stands within radius (flat distance), for the building rule.
        public static bool AnyWithin(Vector3 position, float radius)
        {
            foreach (Workbench bench in _all)
            {
                if (bench == null || !bench.isActiveAndEnabled) continue;
                Vector3 offset = bench.transform.position - position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= radius * radius) return true;
            }
            return false;
        }
    }
}
