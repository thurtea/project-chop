using System.Collections.Generic;
using UnityEngine;

namespace Chopper.World
{
    // The bed in the house. Players come back beside it after dying (the spawn points), and from dusk on they can
    // lie down in it (PlayerRest); once every player is in bed the night is skipped to the morning. A plain scene
    // object: the server checks positions only.
    public class Bed : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float _reach = 2.4f;

        private static readonly List<Bed> _all = new List<Bed>();

        private void OnEnable() => _all.Add(this);

        private void OnDisable() => _all.Remove(this);

        public bool IsInReach(Vector3 position)
        {
            Vector3 offset = transform.position - position;
            if (Mathf.Abs(offset.y) > 3f) return false;
            offset.y = 0f;
            return offset.magnitude <= _reach;
        }

        public static Bed InReach(Vector3 position)
        {
            foreach (Bed bed in _all)
            {
                if (bed != null && bed.isActiveAndEnabled && bed.IsInReach(position)) return bed;
            }
            return null;
        }
    }
}
