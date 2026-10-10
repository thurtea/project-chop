using System.Collections.Generic;
using UnityEngine;

namespace Chopper.World
{
    // Things that push grass aside as they move through it (players and awake enemies). Client side only.
    // Objects register themselves when they appear and unregister when they go away.
    public static class GrassPushers
    {
        public struct Pusher
        {
            public Transform Transform;
            public float Radius;
        }

        private static readonly List<Pusher> _all = new List<Pusher>();

        public static IReadOnlyList<Pusher> All => _all;

        public static void Add(Transform transform, float radius)
        {
            if (transform == null) return;
            _all.Add(new Pusher { Transform = transform, Radius = radius });
        }

        public static void Remove(Transform transform)
        {
            _all.RemoveAll(p => p.Transform == transform);
        }
    }
}
