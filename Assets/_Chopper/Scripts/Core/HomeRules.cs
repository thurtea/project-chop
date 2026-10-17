namespace Chopper.Core
{
    public enum HomeRaiseResult
    {
        Ok = 0,
        InHub = 1,
        Locked = 2,
        AlreadyHome = 3,
        Underground = 4,
        CannotAfford = 5
    }

    public static class HomeRules
    {
        public const float RespawnOffset = 2.5f;

        public static HomeRaiseResult CanRaise(int zoneGoals, Element zone, bool zoneHasHome, bool underground, int wood, int iron, int woodCost, int ironCost)
        {
            if (!ZoneGoalRules.IsZone(zone)) return HomeRaiseResult.InHub;
            if (underground) return HomeRaiseResult.Underground;
            if (!ZoneGoalRules.HomeUnlocked(zoneGoals, zone)) return HomeRaiseResult.Locked;
            if (zoneHasHome) return HomeRaiseResult.AlreadyHome;
            if (wood < woodCost || iron < ironCost) return HomeRaiseResult.CannotAfford;
            return HomeRaiseResult.Ok;
        }

        public static string Explain(HomeRaiseResult result, Element zone, int zoneGoals, int woodCost, int ironCost)
        {
            switch (result)
            {
                case HomeRaiseResult.Ok: return $"A home rises in the {ZoneGoalRules.ZoneName(zone)}.";
                case HomeRaiseResult.InHub: return "Homes are raised in the four forests, not in the hub.";
                case HomeRaiseResult.Underground: return "Raise the home on open ground, not underground.";
                case HomeRaiseResult.Locked: return $"The {ZoneGoalRules.ZoneName(zone)} is not ready for a home ({ZoneGoalRules.Count(zoneGoals, zone)}/{ZoneGoalRules.GoalsPerZone} goals, see Zones).";
                case HomeRaiseResult.AlreadyHome: return $"The {ZoneGoalRules.ZoneName(zone)} already has a home.";
                default: return $"A home heart needs {woodCost} wood and {ironCost} crypt iron.";
            }
        }

        public static float AfterDamage(float health, float damage) => health - (damage > 0f ? damage : 0f);

        public static bool Destroyed(float health) => health <= 0f;
    }
}
