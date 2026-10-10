using UnityEngine;

namespace Chopper.Core
{
    // Everything a player can carry, as one list of kinds: the base materials and crafted items, one essence per
    // element, one entry per fish kind, then one per mining find (ItemRules.FirstFind). The shared chest keeps one count per kind in this order, and recipes name
    // their inputs and outputs with it.
    public enum ItemKind
    {
        Wood = 0,
        Herb = 1,
        Fiber = 2,
        Bandage = 3,
        Torch = 4,
        Iron = 5,
        Shard = 6,
        EssenceWater = 7,
        EssenceFire = 8,
        EssenceEarth = 9,
        EssenceWind = 10,
        // FirstFish + k is fish kind k (FishingRules.Kinds of them).
        FirstFish = 11
    }

    public static class ItemRules
    {
        public const int ChestCap = 999;

        // Mining finds come after the fish: FirstFind + k is find kind k (MiningRules.Kinds of them).
        public static int FirstFind => (int)ItemKind.FirstFish + FishingRules.Kinds;

        public static int Count => FirstFind + MiningRules.Kinds;

        public static bool IsFind(int kind) => kind >= FirstFind && kind < Count;

        public static int Find(MineFindKind kind) => FirstFind + (int)kind;

        public static MineFindKind FindOf(int kind) => (MineFindKind)(kind - FirstFind);

        public static bool IsFish(int kind) => kind >= (int)ItemKind.FirstFish && kind < FirstFind;

        public static int Fish(FishKind kind) => (int)ItemKind.FirstFish + (int)kind;

        public static FishKind FishOf(int kind) => (FishKind)(kind - (int)ItemKind.FirstFish);

        public static bool IsEssence(int kind) => kind >= (int)ItemKind.EssenceWater && kind <= (int)ItemKind.EssenceWind;

        public static Element ElementOf(int kind) => IsEssence(kind) ? (Element)(kind - (int)ItemKind.EssenceWater + 1) : Element.Neutral;

        public static int Essence(Element element) => element == Element.Neutral ? -1 : (int)ItemKind.EssenceWater + (int)element - 1;

        public static bool Valid(int kind) => kind >= 0 && kind < Count;

        // How many move from a stack of `from` into a stack holding `toHave` that can hold `toCap`, when `wanted` are asked
        // for. Never negative, never more than the source has or the target has room for.
        public static int Movable(int from, int toHave, int toCap, int wanted)
        {
            if (wanted <= 0 || from <= 0) return 0;
            int room = Mathf.Max(0, toCap - toHave);
            return Mathf.Min(wanted, Mathf.Min(from, room));
        }

        // Moves up to `wanted` from one count to the other (capped by the target's room). Returns how many moved.
        public static int Transfer(ref int from, ref int to, int toCap, int wanted)
        {
            int moved = Movable(from, to, toCap, wanted);
            from -= moved;
            to += moved;
            return moved;
        }
    }
}
