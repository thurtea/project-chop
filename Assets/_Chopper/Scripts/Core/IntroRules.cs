using UnityEngine;

namespace Chopper.Core
{
    // Bare fists: a punch is a fraction of an axe hit. A tree counts a hit only when enough punches have added up.
    public static class FistRules
    {
        // Adds one punch to the running progress. Returns true when the progress reaches a whole hit (and spends it).
        public static bool Accumulate(ref float progress, float fistMultiplier)
        {
            progress += Mathf.Max(0.01f, fistMultiplier);
            if (progress < 0.999f) return false;
            progress -= 1f;
            return true;
        }

        public static int PunchesPerHit(float fistMultiplier)
        {
            return Mathf.Max(1, Mathf.CeilToInt(1f / Mathf.Max(0.01f, fistMultiplier) - 0.001f));
        }
    }

    // What a sapling becomes when it wakes up at night. Each night rolls an elemental share, so some nights bring
    // mostly elemental trees (hard, but every kill charges the axe) and some mostly plain ones.
    public static class NightRules
    {
        // Only the forests fight back: a tree felled in the hub plants no saplings, so players who stay home (fishing,
        // building, talking) have a quiet night.
        public static bool PlantsSaplings(Element zone) => zone != Element.Neutral;

        public static float RollShare(System.Random rng, float min, float max)
        {
            float low = Mathf.Clamp01(Mathf.Min(min, max));
            float high = Mathf.Clamp01(Mathf.Max(min, max));
            return Mathf.Lerp(low, high, (float)rng.NextDouble());
        }

        // With probability share the enemy is elemental: it keeps its own element, or gets a random one if it had none.
        public static Element RollElement(System.Random rng, float share, Element origin)
        {
            if (rng.NextDouble() >= share) return Element.Neutral;
            if (origin != Element.Neutral) return origin;
            return (Element)(1 + rng.Next(4));
        }

        // Tonight's mix decides which sapling wakes next: elemental ones come first with probability share, plain
        // ones otherwise. Falls back to whatever is left when one kind has run out.
        public static bool WakeElementalNext(System.Random rng, float share, bool anyElemental, bool anyPlain)
        {
            if (!anyElemental) return false;
            if (!anyPlain) return true;
            return rng.NextDouble() < Mathf.Clamp01(share);
        }

        // The element that beats the given one (the one whose enchanted axe makes that element's trees easy).
        public static Element CounterOf(Element element)
        {
            for (int i = 1; i <= 4; i++)
            {
                if (ElementRules.Beats((Element)i, element)) return (Element)i;
            }
            return Element.Neutral;
        }

        // Tonight's theme: one element dominates the swarm. Chosen among the elements that have saplings waiting,
        // more likely the more of them there are. Neutral when no elemental sapling exists. available is indexed by
        // Element (Neutral 0 to Wind 4).
        public static Element RollDominant(System.Random rng, int[] available)
        {
            int total = 0;
            for (int i = 1; i <= 4; i++) total += Mathf.Max(0, available[i]);
            if (total <= 0) return Element.Neutral;
            int pick = rng.Next(total);
            for (int i = 1; i <= 4; i++)
            {
                pick -= Mathf.Max(0, available[i]);
                if (pick < 0) return (Element)i;
            }
            return Element.Neutral;
        }

        private const float DominantWeight = 0.55f;
        private const float CounterWeight = 0.2f;
        private const float OtherWeight = 0.125f;

        // Which element wakes next. share is the elemental fraction of the night. Among the elemental ones the dominant
        // element is favoured, its counter element (the one that beats it) comes second so the player can always find an
        // enemy to charge the axe on, and the rest are rare. Only elements with sleeping saplings can be picked.
        public static Element PickWake(System.Random rng, float share, Element dominant, int[] available)
        {
            Element counter = CounterOf(dominant);
            float[] weight = new float[5];
            float sum = 0f;
            share = Mathf.Clamp01(share);
            if (available[0] > 0) weight[0] = 1f - share;
            for (int i = 1; i <= 4; i++)
            {
                if (available[i] <= 0) continue;
                float w = (Element)i == dominant ? DominantWeight : ((Element)i == counter ? CounterWeight : OtherWeight);
                weight[i] = share * w;
            }
            for (int i = 0; i < 5; i++) sum += weight[i];
            if (sum <= 0f)
            {
                // The wanted kinds have run out: wake whatever is left.
                for (int i = 0; i < 5; i++) if (available[i] > 0) return (Element)i;
                return Element.Neutral;
            }
            float roll = (float)rng.NextDouble() * sum;
            for (int i = 0; i < 5; i++)
            {
                roll -= weight[i];
                if (roll < 0f) return (Element)i;
            }
            return Element.Neutral;
        }

        public static string DescribeTheme(Element dominant, float share)
        {
            if (dominant == Element.Neutral || share <= 0.2f) return Describe(share);
            Element counter = CounterOf(dominant);
            return $"A {dominant} night. {dominant} trees swarm. Kill a {counter} tree to charge your axe against them.";
        }

        public static string Describe(float share)
        {
            if (share >= 0.6f) return "The forests are restless tonight. Many of the trees are elemental.";
            if (share <= 0.35f) return "A quiet night. Most of the trees are plain.";
            return "Night has fallen. The saplings are awake!";
        }
    }

    // When a boss tree starts growing: after enough days and enough trees felled, then again after each boss, a bit later.
    public static class BossRules
    {
        public static int RequiredTrees(int baseTrees, float growthPerBoss, int bossesDefeated)
        {
            return Mathf.Max(1, Mathf.RoundToInt(baseTrees * (1f + Mathf.Max(0f, growthPerBoss) * Mathf.Max(0, bossesDefeated))));
        }
    }

    // Permanent rewards from a boss. Everyone standing gets the same one. The kinds are the ones the player listed:
    // the axe, the outfit (armor), the torch, the bandage and stamina.
    public enum BoonKind
    {
        Axe = 0,
        Armor = 1,
        Torch = 2,
        Salve = 3,
        Stamina = 4
    }

    public static class BoonRules
    {
        public const int KindCount = 5;
        public const int MaxLevel = 5;
        private const int BitsPerKind = 4;
        private const int Mask = (1 << BitsPerKind) - 1;

        public static int Level(int packed, BoonKind kind) => (packed >> ((int)kind * BitsPerKind)) & Mask;

        public static int WithLevel(int packed, BoonKind kind, int level)
        {
            int shift = (int)kind * BitsPerKind;
            return (packed & ~(Mask << shift)) | (Mathf.Clamp(level, 0, MaxLevel) << shift);
        }

        // Adds a level (up to the cap).
        public static int Grant(int packed, BoonKind kind) => WithLevel(packed, kind, Level(packed, kind) + 1);

        public static BoonKind Pick(System.Random rng) => (BoonKind)rng.Next(KindCount);

        public static string Name(BoonKind kind)
        {
            switch (kind)
            {
                case BoonKind.Axe: return "Sharper axe";
                case BoonKind.Armor: return "Sturdier outfit";
                case BoonKind.Torch: return "Longer burning torch";
                case BoonKind.Salve: return "Stronger bandages";
                default: return "More stamina";
            }
        }
    }
}
