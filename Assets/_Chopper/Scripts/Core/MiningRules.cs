using UnityEngine;

namespace Chopper.Core
{
    // Which mine a find comes from: one mine per forest, and Any for finds every mine holds. FishingOnly finds never
    // come out of the rock: they come up on a fishing line.
    public enum MineZone
    {
        Any = 0,
        Water = 1,
        Fire = 2,
        Earth = 3,
        Wind = 4,
        FishingOnly = 5
    }

    // How a swing met the glint: at its peak, near it, or in the dark.
    public enum StrikeQuality { Clean, Good, Rough }

    // The order is stable: saves keep counts and records by index.
    public enum MineFindKind
    {
        IronOre = 0,
        Quartz = 1,
        GoldNugget = 2,
        Geode = 3,
        Glowworm = 4,
        RiverPearl = 5,
        NautilusFossil = 6,
        Aquamarine = 7,
        Obsidian = 8,
        SulfurCrystal = 9,
        FireOpal = 10,
        Amber = 11,
        Ammonite = 12,
        FernFossil = 13,
        SkyQuartz = 14,
        FeatherFossil = 15,
        StormCrystal = 16,
        OldPickHead = 17
    }

    public struct MineFind
    {
        public string Name;
        public string Icon;
        public MineZone Home;
        // 0 common, 1 uncommon, 2 rare, 3 legendary (the fishing names).
        public int Rarity;
        public float Weight;
        // Grams: the size of a find is rolled between these, larger with cleaner strikes.
        public float MinGrams;
        public float MaxGrams;
        public string About;
    }

    // Mining is a pastime like fishing, with little weight on the rest of the game: veins in the mines hold finds
    // (stones, gems, fossils), each with a size and a record. A vein's crack glows and fades in a rhythm; a swing at
    // the peak of the glow is clean and frees most, one near it is good, one in the dark is rough and can crack the
    // find. Glowworms from the mines are bait that brings rarer fish; fishing now and then brings up a geode or an
    // old pick head, which makes the glow easier to catch. Pure C# apart from Unity math types, unit tested.
    public static class MiningRules
    {
        public static readonly MineFind[] Finds =
        {
            new MineFind { Name = "Iron ore", Icon = "find-ore", Home = MineZone.Any, Rarity = 0, Weight = 40f, MinGrams = 120f, MaxGrams = 900f, About = "Rust red rock, heavy in the hand." },
            new MineFind { Name = "Quartz", Icon = "find-quartz", Home = MineZone.Any, Rarity = 0, Weight = 40f, MinGrams = 20f, MaxGrams = 400f, About = "Clear points that catch the lantern light." },
            new MineFind { Name = "Gold nugget", Icon = "find-gold", Home = MineZone.Any, Rarity = 2, Weight = 5f, MinGrams = 2f, MaxGrams = 60f, About = "A bright lump, soft enough to dent with a nail." },
            new MineFind { Name = "Geode", Icon = "find-geode", Home = MineZone.Any, Rarity = 2, Weight = 5f, MinGrams = 300f, MaxGrams = 2400f, About = "A plain round stone with crystals inside. Sometimes it comes up on a fishing line." },
            new MineFind { Name = "Glowworm", Icon = "find-glowworm", Home = MineZone.Any, Rarity = 1, Weight = 16f, MinGrams = 1f, MaxGrams = 6f, About = "Bait. While you carry one, a cast takes it and rarer fish come to the lure." },
            new MineFind { Name = "River pearl", Icon = "find-pearl", Home = MineZone.Water, Rarity = 1, Weight = 22f, MinGrams = 1f, MaxGrams = 12f, About = "Washed into the rock by an old river." },
            new MineFind { Name = "Nautilus fossil", Icon = "find-nautilus", Home = MineZone.Water, Rarity = 2, Weight = 7f, MinGrams = 200f, MaxGrams = 3000f, About = "A coiled shell turned to stone." },
            new MineFind { Name = "Aquamarine", Icon = "find-aquamarine", Home = MineZone.Water, Rarity = 3, Weight = 1.5f, MinGrams = 2f, MaxGrams = 40f, About = "Sea blue, clear as the deep lake." },
            new MineFind { Name = "Obsidian", Icon = "find-obsidian", Home = MineZone.Fire, Rarity = 0, Weight = 30f, MinGrams = 80f, MaxGrams = 1200f, About = "Black glass from old fires under the ash." },
            new MineFind { Name = "Sulfur crystal", Icon = "find-sulfur", Home = MineZone.Fire, Rarity = 1, Weight = 18f, MinGrams = 10f, MaxGrams = 300f, About = "Yellow and brittle, and it smells of matches." },
            new MineFind { Name = "Fire opal", Icon = "find-fireopal", Home = MineZone.Fire, Rarity = 3, Weight = 1.5f, MinGrams = 1f, MaxGrams = 30f, About = "Orange light moving inside the stone." },
            new MineFind { Name = "Amber", Icon = "find-amber", Home = MineZone.Earth, Rarity = 1, Weight = 20f, MinGrams = 5f, MaxGrams = 250f, About = "Old resin. Hold it to the light: something is inside." },
            new MineFind { Name = "Ammonite", Icon = "find-ammonite", Home = MineZone.Earth, Rarity = 2, Weight = 7f, MinGrams = 150f, MaxGrams = 2600f, About = "A ridged spiral older than the forests." },
            new MineFind { Name = "Fern fossil", Icon = "find-fern", Home = MineZone.Earth, Rarity = 1, Weight = 18f, MinGrams = 100f, MaxGrams = 1500f, About = "A leaf pressed into the slate." },
            new MineFind { Name = "Sky quartz", Icon = "find-skyquartz", Home = MineZone.Wind, Rarity = 0, Weight = 30f, MinGrams = 15f, MaxGrams = 350f, About = "Pale blue, cool to the touch." },
            new MineFind { Name = "Feather fossil", Icon = "find-feather", Home = MineZone.Wind, Rarity = 2, Weight = 7f, MinGrams = 60f, MaxGrams = 900f, About = "The print of a wing in fine stone." },
            new MineFind { Name = "Storm crystal", Icon = "find-storm", Home = MineZone.Wind, Rarity = 3, Weight = 1.5f, MinGrams = 2f, MaxGrams = 45f, About = "It hums when the wind picks up." },
            new MineFind { Name = "Old pick head", Icon = "find-pickhead", Home = MineZone.FishingOnly, Rarity = 3, Weight = 0f, MinGrams = 600f, MaxGrams = 1400f, About = "A miner's pick head, hooked out of the water. Carried, it makes the glow easier to catch." }
        };

        public static int Kinds => Finds.Length;

        // ---------- The glint ----------

        // One glow of a vein's crack, in seconds. Each vein runs its own offset of the same rhythm.
        public const float GlintPeriod = 1.7f;
        // Brightness at or above these is a clean or a good strike. The old pick head lowers the clean mark.
        public const float CleanAt = 0.82f;
        public const float CleanAtWithPickHead = 0.64f;
        public const float GoodAt = 0.35f;
        // Seconds between strikes the server accepts.
        public const float StrikeCooldown = 0.75f;
        // A combo is three strikes, each within this long of the last (one glint, 1.7 s, plus a little slack).
        public const int ComboLength = 3;
        public const float ComboWindow = 2.4f;

        // How bright a vein's crack is at a moment: a sharp peak once a period, dark most of the time.
        public static float Glint(float time, int vein)
        {
            float phase = Mathf.Repeat(time / GlintPeriod + VeinOffset(vein), 1f);
            float wave = 0.5f + 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
            return wave * wave * wave * wave;
        }

        public static float VeinOffset(int vein) => Mathf.Repeat(vein * 0.6180339f, 1f);

        public static StrikeQuality Judge(float glint, bool pickHead)
        {
            if (glint >= (pickHead ? CleanAtWithPickHead : CleanAt)) return StrikeQuality.Clean;
            return glint >= GoodAt ? StrikeQuality.Good : StrikeQuality.Rough;
        }

        // ---------- Freeing a find ----------

        // A find is free once the strikes add up to this.
        public const float FreeAt = 3f;
        // A rough strike cracks the find (it is lost, and the vein is spent) this often.
        public const float RoughCrackChance = 0.2f;

        public static float Progress(StrikeQuality quality)
        {
            switch (quality)
            {
                case StrikeQuality.Clean: return 1f;
                case StrikeQuality.Good: return 0.6f;
                default: return 0.3f;
            }
        }

        // The next link in a combo. A late strike, or the first, starts again at 1.
        public static int NextChain(int chain, float sinceLast, float window)
        {
            if (chain <= 0 || sinceLast > window) return 1;
            return chain + 1;
        }

        // Links before the third barely count. The third strike in time pays for the set.
        public static float ComboProgress(StrikeQuality quality, int chain)
        {
            float step = Progress(quality);
            return chain > 0 && chain % ComboLength == 0 ? step * 3f : step * 0.35f;
        }

        public static string ComboWord(StrikeQuality quality, int chain)
        {
            if (chain > 0 && chain % ComboLength == 0) return "Combo";
            return Word(quality);
        }

        public static bool Cracks(StrikeQuality quality, double roll) => quality == StrikeQuality.Rough && roll < RoughCrackChance;

        // The word that pops up for a strike (fishing style: one word, fading).
        public static string Word(StrikeQuality quality)
        {
            switch (quality)
            {
                case StrikeQuality.Clean: return "Clean";
                case StrikeQuality.Good: return "Good";
                default: return "Rough";
            }
        }

        // Which find a vein in this mine holds, from a roll of 0 to 1. A vein freed only with clean strikes favors the
        // rarer finds (cleanShare 0 to 1).
        public static MineFindKind Roll(MineZone mine, double roll, float cleanShare)
        {
            float total = 0f;
            for (int i = 0; i < Finds.Length; i++) total += Weight(Finds[i], mine, cleanShare);
            float pick = (float)roll * total;
            for (int i = 0; i < Finds.Length; i++)
            {
                float w = Weight(Finds[i], mine, cleanShare);
                if (w <= 0f) continue;
                if (pick < w) return (MineFindKind)i;
                pick -= w;
            }
            return MineFindKind.Quartz;
        }

        private static float Weight(MineFind find, MineZone mine, float cleanShare)
        {
            if (find.Home == MineZone.FishingOnly) return 0f;
            if (find.Home != MineZone.Any && find.Home != mine) return 0f;
            return find.Weight * (1f + find.Rarity * 0.5f * Mathf.Clamp01(cleanShare));
        }

        // The size of a freed find in grams: clean work brings out bigger, whole pieces.
        public static float Grams(MineFindKind kind, double roll, float cleanShare)
        {
            MineFind find = Finds[(int)kind];
            float t = (float)roll * (0.6f + 0.4f * Mathf.Clamp01(cleanShare));
            return Mathf.Round(Mathf.Lerp(find.MinGrams, find.MaxGrams, t * t) * 10f) / 10f;
        }

        public static bool IsRecord(float grams, float best) => grams > best + 0.001f;

        public static string Size(float grams) => grams >= 1000f ? $"{grams / 1000f:F2} kg" : $"{grams:F0} g";

        // ---------- Fishing ----------

        // A landed fish now and then brings something else up with it.
        public const float GeodeOnCatchChance = 0.04f;
        public const float PickHeadOnCatchChance = 0.015f;
        // A cast that takes a glowworm makes the rare fish this much more likely to come.
        public const float GlowwormRareMultiplier = 3f;

        public static MineZone ZoneOf(Element forest)
        {
            switch (forest)
            {
                case Element.Water: return MineZone.Water;
                case Element.Fire: return MineZone.Fire;
                case Element.Earth: return MineZone.Earth;
                case Element.Wind: return MineZone.Wind;
                default: return MineZone.Any;
            }
        }

        public static string MineName(MineZone zone)
        {
            switch (zone)
            {
                case MineZone.Water: return "Drip Mine";
                case MineZone.Fire: return "Cinder Mine";
                case MineZone.Earth: return "Deep Root Mine";
                case MineZone.Wind: return "High Gallery Mine";
                default: return "Mine";
            }
        }
    }
}
