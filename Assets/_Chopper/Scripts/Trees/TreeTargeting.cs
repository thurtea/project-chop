using UnityEngine;

namespace Chopper.Trees
{
    // Pure axe targeting on the ground plane (plan D14: the server resolves hits against tree data, not physics).
    // A tree is hit when its trunk circle touches the swing capsule: a strip of half width swingRadius that
    // starts at the player and runs swingRange along aim. Of all hit trees the nearest along aim wins, so the
    // result does not depend on the approach angle or on the order trees are tested.
    public static class TreeTargeting
    {
        // Returns the distance along aim to the trunk if it is hit, or a negative value if not.
        public static float HitDistance(Vector3 origin, Vector3 aim, float swingRange, float swingRadius,
            Vector3 treePosition, float trunkRadius)
        {
            Vector3 flatAim = new Vector3(aim.x, 0f, aim.z);
            if (flatAim.sqrMagnitude < 0.0001f) return -1f;
            flatAim.Normalize();

            Vector2 toTree = new Vector2(treePosition.x - origin.x, treePosition.z - origin.z);
            Vector2 dir = new Vector2(flatAim.x, flatAim.z);
            float along = Vector2.Dot(toTree, dir);
            float lateral = Mathf.Abs(toTree.x * dir.y - toTree.y * dir.x);

            if (along < 0f || along > swingRange + trunkRadius) return -1f;
            if (lateral > swingRadius + trunkRadius) return -1f;
            return along;
        }

        // A log lying on the ground from a to b: the nearest of points along it, every half meter.
        public static float SegmentHitDistance(Vector3 origin, Vector3 aim, float swingRange, float swingRadius,
            Vector3 a, Vector3 b, float logRadius)
        {
            float length = Vector3.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.5f));
            float best = -1f;
            for (int i = 0; i <= steps; i++)
            {
                float d = HitDistance(origin, aim, swingRange, swingRadius, Vector3.Lerp(a, b, i / (float)steps), logRadius);
                if (d >= 0f && (best < 0f || d < best)) best = d;
            }
            return best;
        }
    }
}
