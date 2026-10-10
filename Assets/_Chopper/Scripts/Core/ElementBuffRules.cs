using UnityEngine;

namespace Chopper.Core
{
    // Pure axe element charge math (plan D20, revised). Killing an elemental enemy loads the axe with that
    // element for a number of hits. Charges are packed 8 bits per elemental element into one int so a single
    // SyncVar carries them. A charged element boosts damage against the enemies it beats.
    public static class ElementBuffRules
    {
        private const int BitsPerElement = 8;
        private const int MaxCharges = 255;
        private const int FirstElemental = (int)Element.Water;
        private const int LastElemental = (int)Element.Wind;

        public static bool IsElemental(Element element) => element != Element.Neutral;

        public static int GetCharges(int packed, Element element)
        {
            if (!IsElemental(element)) return 0;
            return (packed >> ((int)element - FirstElemental) * BitsPerElement) & MaxCharges;
        }

        public static bool HasAny(int packed) => packed != 0;

        // Sets the charge of one element to at least amount, never above cap. Neutral does nothing.
        public static int LoadCharges(int packed, Element element, int amount, int cap)
        {
            if (!IsElemental(element)) return packed;

            int shift = ((int)element - FirstElemental) * BitsPerElement;
            int loaded = Mathf.Clamp(Mathf.Max(GetCharges(packed, element), amount), 0, Mathf.Min(cap, MaxCharges));
            return (packed & ~(MaxCharges << shift)) | (loaded << shift);
        }

        // Spends one charge for a hit on an enemy. Normally every charged element loses one; with
        // onlyEffective just the elements that beat the enemy do.
        public static int ConsumeHit(int packed, Element enemy, bool onlyEffective)
        {
            int result = packed;
            for (int i = FirstElemental; i <= LastElemental; i++)
            {
                var element = (Element)i;
                int charges = GetCharges(result, element);
                if (charges <= 0) continue;
                if (onlyEffective && !ElementRules.Beats(element, enemy)) continue;

                int shift = (i - FirstElemental) * BitsPerElement;
                result = (result & ~(MaxCharges << shift)) | ((charges - 1) << shift);
            }
            return result;
        }

        // Damage multiplier for a hit on an enemy of the given element: the bonus applies when any charged
        // element, or any element stone set in the axe, beats it. Bonuses never stack.
        public static float DamageMultiplier(int packed, Element enemy, float bonus, int stones = 0)
        {
            for (int i = FirstElemental; i <= LastElemental; i++)
            {
                bool carried = GetCharges(packed, (Element)i) > 0 || HasStone(stones, (Element)i);
                if (carried && ElementRules.Beats((Element)i, enemy)) return 1f + bonus;
            }
            return 1f;
        }

        // Element stones: permanent enchantments of the axe, one bit per element. They never run out.
        public static bool HasStone(int stones, Element element) => IsElemental(element) && (stones & (1 << (int)element)) != 0;

        public static int AddStone(int stones, Element element) => IsElemental(element) ? stones | (1 << (int)element) : stones;

        // The first element whose stone is set, or Neutral.
        public static Element FirstStone(int stones)
        {
            for (int i = FirstElemental; i <= LastElemental; i++) if (HasStone(stones, (Element)i)) return (Element)i;
            return Element.Neutral;
        }

        // The element with the most charges left, or Neutral when the axe is empty. Used for the axe color.
        public static Element Strongest(int packed)
        {
            Element best = Element.Neutral;
            int bestCharges = 0;
            for (int i = FirstElemental; i <= LastElemental; i++)
            {
                int charges = GetCharges(packed, (Element)i);
                if (charges > bestCharges)
                {
                    bestCharges = charges;
                    best = (Element)i;
                }
            }
            return best;
        }
    }
}
