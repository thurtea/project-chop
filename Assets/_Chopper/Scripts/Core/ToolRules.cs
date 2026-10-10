using UnityEngine;

namespace Chopper.Core
{
    // Tools wear out (Valheim style): the axe loses a point per hit, the rod per catch and more for a snapped line.
    // At zero the tool is broken (the axe falls back to fists, the rod cannot cast) until it is repaired at a workbench,
    // which puts it back to full.
    public enum ToolKind
    {
        Axe = 0,
        Rod = 1
    }

    public static class ToolRules
    {
        public static int Wear(int durability, int amount) => Mathf.Max(0, durability - Mathf.Max(0, amount));

        public static bool Broken(int durability) => durability <= 0;

        public static bool Worn(int durability, int max) => durability < max;

        // How full the hotbar bar is, 0 to 1.
        public static float Fraction(int durability, int max) => max > 0 ? Mathf.Clamp01(durability / (float)max) : 0f;

        // The bar's color: green while healthy, yellow under half, red when nearly broken.
        public static Color BarColor(float fraction)
        {
            if (fraction <= 0.2f) return new Color(0.85f, 0.25f, 0.2f);
            if (fraction <= 0.5f) return new Color(0.92f, 0.75f, 0.25f);
            return new Color(0.45f, 0.8f, 0.35f);
        }

        // Saves keep how much was used, so older saves (no field, 0) load with full tools.
        public static int FromWear(int wear, int max) => Mathf.Clamp(max - Mathf.Max(0, wear), 0, max);

        public static int ToWear(int durability, int max) => Mathf.Clamp(max - durability, 0, max);
    }
}
