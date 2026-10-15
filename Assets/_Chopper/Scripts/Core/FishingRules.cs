using System;
using UnityEngine;

namespace Chopper.Core
{
    // Where a fish lives. The order is stable: saves store counts by FishKind index.
    public enum FishWater
    {
        Pond = 0,
        Marsh = 1,
        Fire = 2,
        Earth = 3,
        Wind = 4,
        Sea = 5
    }

    // When a fish bites. Always is every hour and every weather. The others bite only in that window, on top of the
    // water's usual fish. Rain and night can overlap, so both kinds bite in a rainy night.
    public enum FishWindow
    {
        Always = 0,
        Rain = 1,
        Night = 2,
        Twilight = 3
    }

    // Index into GameConfig's fish table (the order there must match). The first four stay the hub pond's usual
    // catch so an old packed save still means perch, pike, golden carp and Old Mossback.
    public enum FishKind
    {
        Perch = 0,
        Pike = 1,
        GoldenCarp = 2,
        OldMossback = 3,
        MoonEel = 4,
        SilverShiner = 5,
        RainTrout = 6,
        ReedPerch = 7,
        BogPike = 8,
        BlackEel = 9,
        MistCarp = 10,
        StormEel = 11,
        CinderChar = 12,
        EmberTrout = 13,
        AshEel = 14,
        DuskSpark = 15,
        SteamSalmon = 16,
        MudCat = 17,
        StoneLoach = 18,
        CaveBlind = 19,
        DewGrubber = 20,
        FloodCarp = 21,
        GaleMinnow = 22,
        SkyMackerel = 23,
        NightDart = 24,
        DawnDart = 25,
        SquallTuna = 26,
        Herring = 27,
        Cod = 28,
        Lanternfish = 29,
        DawnBass = 30,
        BreakerCod = 31
    }

    public enum FightResult
    {
        Ongoing,
        Landed,
        // The line went too tight and broke.
        Snapped,
        // The fish ran out the whole line and got away.
        Escaped
    }

    // A hooked fish cycles through these, always in this order, so the player can learn it: calm (reel), pulling
    // (careful; its last moment warns that a thrash is coming), thrashing (let go), tired (reel hard).
    public enum FishPhase { Calm, Pulling, Thrashing, Tired }

    // How tight the line is, in words as well as colors: safe, risky, about to break.
    public enum TensionZone { Safe, Risky, Critical }

    // One kind of fish, set in GameConfig. Durations are seconds, distances meters, resistance and pullback scale the
    // shared reel model (FishingRules), so every fish plays the same game with its own weight.
    [Serializable]
    public class FishSpecies
    {
        public string Name = "Fish";
        public FishWater Home = FishWater.Pond;
        public FishWindow Window = FishWindow.Always;
        [Tooltip("0 common, 1 uncommon, 2 rare, 3 legendary.")]
        public int Rarity;
        [Tooltip("Fishing level needed before this fish can bite.")]
        public int MinLevel;
        public float BiteDelayMin = 5f;
        public float BiteDelayMax = 10f;
        [Tooltip("Seconds the bobber stays down for the strike.")]
        public float HookWindow = 1f;
        [Tooltip("How fast reeling tightens the line against this fish.")]
        public float Resistance = 1f;
        [Tooltip("How much line the fish takes back while the reel is let go.")]
        public float Pullback = 1f;
        public float CalmSeconds = 2.5f;
        public float PullSeconds = 1.5f;
        public float ThrashSeconds = 0.8f;
        public float TiredSeconds = 1.6f;
        [Tooltip("Line out beyond the cast when it is hooked: bigger fish start further off.")]
        public float ExtraLine = 4f;
        public int Heal = 20;
        public int Xp = 3;
        [Tooltip("How often it bites compared with the others.")]
        public float Weight = 10f;

        public static FishSpecies[] Defaults()
        {
            FishSpecies perch = new FishSpecies { Name = "Perch", Home = FishWater.Pond, Window = FishWindow.Always, Rarity = 0, MinLevel = 0, BiteDelayMin = 4f, BiteDelayMax = 9f, HookWindow = 1.1f, Resistance = 0.8f, Pullback = 0.6f,
                CalmSeconds = 3f, PullSeconds = 1.2f, ThrashSeconds = 0.6f, TiredSeconds = 2f, ExtraLine = 3f, Heal = 20, Xp = 3, Weight = 60f };
            FishSpecies pike = new FishSpecies { Name = "Pike", Home = FishWater.Pond, Window = FishWindow.Always, Rarity = 1, MinLevel = 0, BiteDelayMin = 6f, BiteDelayMax = 12f, HookWindow = 0.95f, Resistance = 1f, Pullback = 1f,
                CalmSeconds = 2.4f, PullSeconds = 1.6f, ThrashSeconds = 0.9f, TiredSeconds = 1.6f, ExtraLine = 6f, Heal = 35, Xp = 6, Weight = 28f };
            FishSpecies golden = new FishSpecies { Name = "Golden carp", Home = FishWater.Pond, Window = FishWindow.Always, Rarity = 2, MinLevel = 1, BiteDelayMin = 9f, BiteDelayMax = 16f, HookWindow = 0.8f, Resistance = 1.05f, Pullback = 1f,
                CalmSeconds = 2.2f, PullSeconds = 1.8f, ThrashSeconds = 1f, TiredSeconds = 1.6f, ExtraLine = 8f, Heal = 60, Xp = 12, Weight = 10f };
            FishSpecies moss = new FishSpecies { Name = "Old Mossback", Home = FishWater.Pond, Window = FishWindow.Always, Rarity = 3, MinLevel = 3, BiteDelayMin = 14f, BiteDelayMax = 24f, HookWindow = 0.7f, Resistance = 1.15f, Pullback = 1.2f,
                CalmSeconds = 2f, PullSeconds = 2.2f, ThrashSeconds = 1.3f, TiredSeconds = 1.8f, ExtraLine = 11f, Heal = 100, Xp = 30, Weight = 2f };
            return new[]
            {
                perch, pike, golden, moss,
                Like(golden, "Moon eel", FishWater.Pond, FishWindow.Night, 55, 14, 40f, 2),
                Like(pike, "Silver shiner", FishWater.Pond, FishWindow.Twilight, 30, 7, 48f, 0),
                Like(pike, "Rain trout", FishWater.Pond, FishWindow.Rain, 32, 7, 70f, 0),

                Like(perch, "Reed perch", FishWater.Marsh, FishWindow.Always, 18, 3, 58f, 0),
                Like(pike, "Bog pike", FishWater.Marsh, FishWindow.Always, 34, 6, 26f, 0),
                Like(golden, "Black eel", FishWater.Marsh, FishWindow.Night, 58, 14, 36f, 2),
                Like(pike, "Mist carp", FishWater.Marsh, FishWindow.Twilight, 30, 7, 44f, 0),
                Like(pike, "Storm eel", FishWater.Marsh, FishWindow.Rain, 36, 8, 66f, 1),

                Like(perch, "Cinder char", FishWater.Fire, FishWindow.Always, 20, 3, 55f, 0),
                Like(pike, "Ember trout", FishWater.Fire, FishWindow.Always, 36, 6, 24f, 0),
                Like(golden, "Ash eel", FishWater.Fire, FishWindow.Night, 62, 16, 34f, 2),
                Like(pike, "Dusk spark", FishWater.Fire, FishWindow.Twilight, 32, 8, 42f, 0),
                Like(pike, "Steam salmon", FishWater.Fire, FishWindow.Rain, 40, 9, 64f, 1),

                Like(perch, "Mud cat", FishWater.Earth, FishWindow.Always, 22, 3, 56f, 0),
                Like(pike, "Stone loach", FishWater.Earth, FishWindow.Always, 34, 6, 26f, 0),
                Like(golden, "Cave blind", FishWater.Earth, FishWindow.Night, 64, 16, 34f, 2),
                Like(pike, "Dew grubber", FishWater.Earth, FishWindow.Twilight, 28, 7, 42f, 0),
                Like(pike, "Flood carp", FishWater.Earth, FishWindow.Rain, 38, 8, 64f, 1),

                Like(perch, "Gale minnow", FishWater.Wind, FishWindow.Always, 18, 3, 58f, 0),
                Like(pike, "Sky mackerel", FishWater.Wind, FishWindow.Always, 34, 6, 24f, 0),
                Like(golden, "Night dart", FishWater.Wind, FishWindow.Night, 56, 14, 36f, 2),
                Like(pike, "Dawn dart", FishWater.Wind, FishWindow.Twilight, 30, 7, 46f, 0),
                Like(pike, "Squall tuna", FishWater.Wind, FishWindow.Rain, 40, 9, 66f, 1),

                Like(perch, "Herring", FishWater.Sea, FishWindow.Always, 16, 3, 60f, 0),
                Like(pike, "Cod", FishWater.Sea, FishWindow.Always, 36, 6, 26f, 0),
                Like(golden, "Lanternfish", FishWater.Sea, FishWindow.Night, 60, 15, 34f, 2),
                Like(pike, "Dawn bass", FishWater.Sea, FishWindow.Twilight, 32, 8, 44f, 0),
                Like(pike, "Breaker cod", FishWater.Sea, FishWindow.Rain, 42, 10, 68f, 1),
            };
        }

        // A fish that fights like another, but lives somewhere else and bites on its own terms.
        private static FishSpecies Like(FishSpecies template, string name, FishWater home, FishWindow window, int heal, int xp, float weight, int minLevel)
        {
            return new FishSpecies
            {
                Name = name,
                Home = home,
                Window = window,
                Rarity = template.Rarity,
                MinLevel = minLevel,
                BiteDelayMin = template.BiteDelayMin,
                BiteDelayMax = template.BiteDelayMax,
                HookWindow = template.HookWindow,
                Resistance = template.Resistance,
                Pullback = template.Pullback,
                CalmSeconds = template.CalmSeconds,
                PullSeconds = template.PullSeconds,
                ThrashSeconds = template.ThrashSeconds,
                TiredSeconds = template.TiredSeconds,
                ExtraLine = template.ExtraLine,
                Heal = heal,
                Xp = xp,
                Weight = weight
            };
        }
    }

    // One fish on the line: line out between rod and fish, how tight it is (0 to 1), and time since the strike.
    public struct Fight
    {
        public FishKind Kind;
        public float Distance;
        public float StartDistance;
        public float Tension;
        public float Elapsed;
        // How hard this one fish fights for its kind (LureRules.Strength); 0 counts as 1.
        public float Strength;
        public float Kg;
    }

    // Pure fishing rules, built on New World's tension fishing and the calm readable bobber of older fishing games:
    // cast, wait for the bite, strike in time, then reel. Reeling pulls the fish in and tightens the line; letting go
    // slackens it while the fish takes some line back. The fish's phases (FishPhase) make reeling safer or riskier.
    // Better rods reel faster with less strain; better lures, hotspots and fishing level bring bites sooner and rarer
    // fish; a long cast takes a little of the strain off. Each kind has its own count (0 to MaxStack). Old saves packed
    // the first four kinds into one int, 8 bits each (PackLegacy, UnpackLegacy).
    public static class FishingRules
    {
        public const int Kinds = 32;
        public const int MaxRodLevel = 3;
        public const int MaxLureLevel = 3;
        // Past this much line beyond where it was hooked, the fish is gone.
        public const float EscapeSlack = 14f;
        public const float StartTension = 0.15f;
        public const float RiskyTension = 0.55f;
        public const float CriticalTension = 0.8f;
        // The last part of a pull that warns of the thrash after it.
        public const float ThrashWarningSeconds = 0.6f;
        public const int MaxStack = 255;
        private const int Bits = 8;
        private const int Mask = (1 << Bits) - 1;
        private const float BaseTensionGain = 0.28f;
        private const float SlackenPerSecond = 0.6f;

        // Reel speed, tension gain and line taken back in each phase (Calm, Pulling, Thrashing, Tired).
        private static readonly float[] PhaseReel = { 1f, 0.45f, 0.2f, 1.35f };
        private static readonly float[] PhaseGain = { 1f, 1.7f, 3.4f, 0.55f };
        private static readonly float[] PhaseTake = { 0.15f, 1f, 0.6f, 0f };
        private static readonly string[] RarityNames = { "Common", "Uncommon", "Rare", "Legendary" };

        public static string RarityName(int rarity) => RarityNames[Mathf.Clamp(rarity, 0, RarityNames.Length - 1)];

        public static string RodName(int level)
        {
            switch (level)
            {
                case 1: return "Willow rod";
                case 2: return "Ash rod";
                case 3: return "Crypt iron rod";
                default: return "No rod";
            }
        }

        public static string WaterName(FishWater water)
        {
            switch (water)
            {
                case FishWater.Pond: return "Hub pond";
                case FishWater.Marsh: return "Marsh";
                case FishWater.Fire: return "Ash lake";
                case FishWater.Earth: return "Deep woods lake";
                case FishWater.Wind: return "Highland lake";
                default: return "Sea";
            }
        }

        public static string WindowName(FishWindow window)
        {
            switch (window)
            {
                case FishWindow.Rain: return "Rain";
                case FishWindow.Night: return "Night";
                case FishWindow.Twilight: return "Dawn and dusk";
                default: return "Any time";
            }
        }

        public static FishWater FromElement(Element element)
        {
            switch (element)
            {
                case Element.Water: return FishWater.Marsh;
                case Element.Fire: return FishWater.Fire;
                case Element.Earth: return FishWater.Earth;
                case Element.Wind: return FishWater.Wind;
                default: return FishWater.Pond;
            }
        }

        // True when this fish's window matches the weather and the time of day. Usual fish bite either way.
        public static bool WindowOpen(FishWindow window, WeatherKind weather, DayPhase phase)
        {
            switch (window)
            {
                case FishWindow.Rain: return weather == WeatherKind.Rain;
                case FishWindow.Night: return phase == DayPhase.Night;
                case FishWindow.Twilight: return phase == DayPhase.Dawn || phase == DayPhase.Dusk;
                default: return true;
            }
        }

        public static string LureName(int level)
        {
            switch (level)
            {
                case 1: return "Feather lure";
                case 2: return "Bone lure";
                case 3: return "Rune lure";
                default: return "Plain hook";
            }
        }

        public static FishSpecies Species(FishSpecies[] table, FishKind kind)
        {
            int i = (int)kind;
            return table != null && i >= 0 && i < table.Length ? table[i] : new FishSpecies();
        }

        // ---------- Counts ----------

        public static int Count(int packed, FishKind kind)
        {
            int i = (int)kind;
            if (i < 0 || i > 3) return 0;
            return (packed >> (i * Bits)) & Mask;
        }

        public static int Add(int packed, FishKind kind, int amount)
        {
            int i = (int)kind;
            if (i < 0 || i > 3) return packed;
            int value = Mathf.Clamp(Count(packed, kind) + amount, 0, Mask);
            int shift = i * Bits;
            return (packed & ~(Mask << shift)) | (value << shift);
        }

        // The first four kinds, the way saves used to store them. Kinds past those do not fit.
        public static int Total(int packed)
        {
            int total = 0;
            for (int k = 0; k < 4; k++) total += Count(packed, (FishKind)k);
            return total;
        }

        public static int[] UnpackLegacy(int packed)
        {
            var counts = new int[Kinds];
            for (int k = 0; k < 4; k++) counts[k] = Count(packed, (FishKind)k);
            return counts;
        }

        public static int PackLegacy(int[] counts)
        {
            int packed = 0;
            if (counts == null) return 0;
            for (int k = 0; k < 4 && k < counts.Length; k++) packed = Add(packed, (FishKind)k, counts[k]);
            return packed;
        }

        public static int CountOf(int[] counts, FishKind kind)
        {
            int i = (int)kind;
            return counts != null && i >= 0 && i < counts.Length ? counts[i] : 0;
        }

        public static void AddTo(int[] counts, FishKind kind, int amount)
        {
            int i = (int)kind;
            if (counts == null || i < 0 || i >= counts.Length) return;
            counts[i] = Mathf.Clamp(counts[i] + amount, 0, MaxStack);
        }

        public static int TotalOf(int[] counts)
        {
            int total = 0;
            if (counts == null) return 0;
            int n = counts.Length < Kinds ? counts.Length : Kinds;
            for (int i = 0; i < n; i++) if (counts[i] > 0) total += counts[i];
            return total;
        }

        // The fish eaten next: the one that heals the least, so the good ones are saved for later.
        public static bool NextToEat(int[] counts, FishSpecies[] table, out FishKind kind)
        {
            int best = -1;
            int bestHeal = int.MaxValue;
            int n = counts != null ? counts.Length : 0;
            if (n > Kinds) n = Kinds;
            for (int k = 0; k < n; k++)
            {
                if (counts[k] <= 0) continue;
                int heal = Species(table, (FishKind)k).Heal;
                if (heal < bestHeal)
                {
                    bestHeal = heal;
                    best = k;
                }
            }
            if (best < 0)
            {
                kind = FishKind.Perch;
                return false;
            }
            kind = (FishKind)best;
            return true;
        }

        // ---------- Level ----------

        // Fishing level from experience: the number of thresholds reached after the first (which is 0).
        public static int Level(int xp, int[] thresholds)
        {
            int level = 0;
            if (thresholds == null) return 0;
            for (int i = 1; i < thresholds.Length; i++) if (xp >= thresholds[i]) level = i;
            return level;
        }

        // ---------- Casting and biting ----------

        public static float CastDistance(float power, float min, float max) => Mathf.Lerp(min, max, Mathf.Clamp01(power));

        // 0 for the shortest cast, 1 for the longest: how much of the long cast bonus applies.
        public static float CastFactor(float distance, float min, float max) => max > min ? Mathf.Clamp01((distance - min) / (max - min)) : 0f;

        // Seconds until this fish bites, from a roll of 0 to 1. Lures, hotspots and fishing level bring it sooner.
        public static float BiteDelay(FishSpecies species, double roll, int lure, bool hotspot, int level, float hotspotMultiplier)
        {
            float delay = Mathf.Lerp(species.BiteDelayMin, species.BiteDelayMax, (float)roll);
            delay *= 1f - 0.12f * Mathf.Clamp(lure, 0, MaxLureLevel);
            delay *= 1f - 0.04f * Mathf.Clamp(level, 0, 5);
            if (hotspot) delay *= hotspotMultiplier;
            return Mathf.Max(1.5f, delay);
        }

        // Seconds the bobber stays down for the strike. A better lure holds the fish a little longer.
        public static float HookWindow(FishSpecies species, int lure) => species.HookWindow + 0.12f * Mathf.Clamp(lure, 0, MaxLureLevel);

        // Which fish bites, from a roll of 0 to 1. Only fish of this water, and only if their window is open. Fish above
        // the player's level never bite; lures, a hotspot and a long cast make the rarer ones more likely.
        public static FishKind Roll(FishSpecies[] table, double roll, int lure, int level, bool hotspot, float hotspotRareMultiplier, float power, FishWater water, WeatherKind weather, DayPhase phase)
        {
            float total = 0f;
            for (int i = 0; i < table.Length && i < Kinds; i++) total += Weight(table[i], lure, level, hotspot, hotspotRareMultiplier, power, water, weather, phase);
            if (total <= 0f)
            {
                for (int i = 0; i < table.Length && i < Kinds; i++)
                    if (table[i].Home == water && table[i].Window == FishWindow.Always) return (FishKind)i;
                return FishKind.Perch;
            }
            float pick = (float)roll * total;
            for (int i = 0; i < table.Length && i < Kinds; i++)
            {
                float w = Weight(table[i], lure, level, hotspot, hotspotRareMultiplier, power, water, weather, phase);
                if (w <= 0f) continue;
                if (pick < w) return (FishKind)i;
                pick -= w;
            }
            return FishKind.Perch;
        }

        private static float Weight(FishSpecies s, int lure, int level, bool hotspot, float hotspotRareMultiplier, float power, FishWater water, WeatherKind weather, DayPhase phase)
        {
            if (s.Home != water || !WindowOpen(s.Window, weather, phase) || level < s.MinLevel) return 0f;
            float w = s.Weight * (1f + s.Rarity * 0.35f * Mathf.Clamp(lure, 0, MaxLureLevel)) * (1f + 0.1f * Mathf.Clamp01(power) * s.Rarity);
            if (hotspot && s.Rarity > 0) w *= hotspotRareMultiplier;
            return w;
        }

        // ---------- The fight ----------

        public static FishPhase PhaseAt(FishSpecies s, float elapsed, out bool thrashComing)
        {
            float cycle = Mathf.Max(0.1f, s.CalmSeconds + s.PullSeconds + s.ThrashSeconds + s.TiredSeconds);
            float t = Mathf.Repeat(elapsed, cycle);
            thrashComing = false;
            if (t < s.CalmSeconds) return FishPhase.Calm;
            t -= s.CalmSeconds;
            if (t < s.PullSeconds)
            {
                thrashComing = t > s.PullSeconds - ThrashWarningSeconds;
                return FishPhase.Pulling;
            }
            t -= s.PullSeconds;
            return t < s.ThrashSeconds ? FishPhase.Thrashing : FishPhase.Tired;
        }

        public static TensionZone Zone(float tension) => tension >= CriticalTension ? TensionZone.Critical : tension >= RiskyTension ? TensionZone.Risky : TensionZone.Safe;

        public static float ReelSpeed(int rod) => 1f + 0.25f * Mathf.Clamp(rod, 1, MaxRodLevel);

        // Line out when the fish is hooked: some of the cast plus the fish's own run.
        public static Fight Begin(FishKind kind, FishSpecies s, float castDistance, float kg = 0f)
        {
            float distance = castDistance * 0.6f + s.ExtraLine;
            float strength = kg > 0f ? LureRules.Strength(s, kg) : 1f;
            return new Fight { Kind = kind, Distance = distance, StartDistance = distance, Tension = StartTension, Strength = strength, Kg = kg };
        }

        // The shortest fight the server believes: the line reeled in non stop at the best speed there is.
        public static float MinFightSeconds(FishSpecies s, float castDistance)
        {
            float distance = castDistance * 0.6f + s.ExtraLine;
            return distance / (ReelSpeed(MaxRodLevel) * PhaseReel[(int)FishPhase.Tired]) * 0.8f;
        }

        // One step of the fight. reeling: the reel is held. castFactor: 0 to 1 (CastFactor), how long the cast was.
        public static FightResult Step(ref Fight fight, FishSpecies s, bool reeling, int rod, float castFactor, float longCastModifier, float dt)
        {
            return Step(ref fight, s, reeling, rod, castFactor, longCastModifier, dt, 1f);
        }

        // The fishing skill eases the fight: a higher skillEase takes more strain off the line.
        public static FightResult Step(ref Fight fight, FishSpecies s, bool reeling, int rod, float castFactor, float longCastModifier, float dt, float skillEase)
        {
            rod = Mathf.Clamp(rod, 1, MaxRodLevel);
            int phase = (int)PhaseAt(s, fight.Elapsed, out _);
            // A stronger rod and a longer cast both take some strain off the line.
            float strain = (1f - 0.15f * (rod - 1)) * (1f - longCastModifier * Mathf.Clamp01(castFactor)) / Mathf.Max(0.01f, skillEase);
            float strength = fight.Strength > 0f ? fight.Strength : 1f;
            if (reeling)
            {
                fight.Distance -= ReelSpeed(rod) * PhaseReel[phase] * dt;
                fight.Tension += BaseTensionGain * s.Resistance * strength * PhaseGain[phase] * strain * dt;
            }
            else
            {
                fight.Tension -= SlackenPerSecond * dt;
                fight.Distance += s.Pullback * strength * PhaseTake[phase] * dt;
            }
            fight.Tension = Mathf.Clamp(fight.Tension, 0f, 1.2f);
            fight.Elapsed += dt;
            if (fight.Tension >= 1f) return FightResult.Snapped;
            if (fight.Distance >= fight.StartDistance + EscapeSlack) return FightResult.Escaped;
            if (fight.Distance <= 0f)
            {
                fight.Distance = 0f;
                return FightResult.Landed;
            }
            return FightResult.Ongoing;
        }
    }
}
