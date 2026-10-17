namespace Chopper.Core
{
    public enum ZoneGoal
    {
        Fish = 0,
        Gem = 1,
        Warden = 2
    }

    public static class ZoneGoalRules
    {
        public const int GoalsPerZone = 3;
        public static readonly Element[] Zones = { Element.Water, Element.Fire, Element.Earth, Element.Wind };

        public static FishKind RareFish(Element zone)
        {
            switch (zone)
            {
                case Element.Water: return FishKind.StormEel;
                case Element.Fire: return FishKind.SteamSalmon;
                case Element.Earth: return FishKind.FloodCarp;
                default: return FishKind.SquallTuna;
            }
        }

        public static MineFindKind Gem(Element zone)
        {
            switch (zone)
            {
                case Element.Water: return MineFindKind.Aquamarine;
                case Element.Fire: return MineFindKind.FireOpal;
                case Element.Earth: return MineFindKind.Amber;
                default: return MineFindKind.StormCrystal;
            }
        }

        public static bool IsZone(Element zone) => zone != Element.Neutral;

        private static int Bit(Element zone, ZoneGoal goal) => 1 << (((int)zone - 1) * GoalsPerZone + (int)goal);

        public static bool Has(int mask, Element zone, ZoneGoal goal) => IsZone(zone) && (mask & Bit(zone, goal)) != 0;

        public static int Mark(int mask, Element zone, ZoneGoal goal) => IsZone(zone) ? mask | Bit(zone, goal) : mask;

        public static int Count(int mask, Element zone)
        {
            int count = 0;
            for (int g = 0; g < GoalsPerZone; g++) if (Has(mask, zone, (ZoneGoal)g)) count++;
            return count;
        }

        public static bool HomeUnlocked(int mask, Element zone) => IsZone(zone) && Count(mask, zone) == GoalsPerZone;

        public static bool ZoneOfFish(FishKind kind, out Element zone)
        {
            foreach (Element z in Zones)
            {
                if (RareFish(z) != kind) continue;
                zone = z;
                return true;
            }
            zone = Element.Neutral;
            return false;
        }

        public static bool ZoneOfGem(MineFindKind kind, out Element zone)
        {
            foreach (Element z in Zones)
            {
                if (Gem(z) != kind) continue;
                zone = z;
                return true;
            }
            zone = Element.Neutral;
            return false;
        }

        public static string ZoneName(Element zone) => zone + " forest";
    }
}
