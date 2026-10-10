using Chopper.Core;
using UnityEngine;

namespace Chopper.World
{
    // Pure zone layout (plan section 2, slice version). The hub is a disc around the hut and is the safe zone
    // during the day. Beyond it the map splits into four quadrants split along the diagonals:
    // North (+Z) Water, East (+X) Earth, South (-Z) Fire, West (-X) Wind. Neutral means the hub.
    public static class ZoneMap
    {
        public static Element ElementAt(float x, float z, float hubRadius)
        {
            if (x * x + z * z <= hubRadius * hubRadius) return Element.Neutral;
            if (Mathf.Abs(z) >= Mathf.Abs(x)) return z > 0f ? Element.Water : Element.Fire;
            return x > 0f ? Element.Earth : Element.Wind;
        }

        public static Element ElementAt(Vector3 position, float hubRadius)
        {
            return ElementAt(position.x, position.z, hubRadius);
        }

        // Unit direction from the hut toward the middle of a forest, used for markers and painting.
        public static Vector2 Axis(Element zone)
        {
            switch (zone)
            {
                case Element.Water: return Vector2.up;
                case Element.Earth: return Vector2.right;
                case Element.Fire: return Vector2.down;
                case Element.Wind: return Vector2.left;
                default: return Vector2.zero;
            }
        }
    }
}
