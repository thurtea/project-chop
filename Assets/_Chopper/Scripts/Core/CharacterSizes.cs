namespace Chopper.Core
{
    public static class CharacterSizes
    {
        public const float Player = 1.8f;
        public const float PlayerLegX = 0.20f;
        public const float PlayerLegForward = 0.03f;
        public const float ElementalSmall = 2.05f;
        public const float ElementalMedium = 2.7f;
        public const float ElementalLarge = 3.6f;
        public const float ElementalSmallBefore = 1.7f;
        public const float ElementalMediumBefore = 2.3f;
        public const float ElementalLargeBefore = 3.2f;
        public const float EnemyAgentRadius = 0.5f;
        public const float Warden = 5.5f;
        public const float DraugrSmall = 1.75f;
        public const float DraugrMedium = 1.85f;
        public const float DraugrLarge = 2.0f;
        public const float CryptBoss = 3.2f;

        public static float Elemental(TreeSize size) =>
            size == TreeSize.Small ? ElementalSmall : size == TreeSize.Medium ? ElementalMedium : ElementalLarge;

        public static float ElementalGrowth(TreeSize size) =>
            Elemental(size) / (size == TreeSize.Small ? ElementalSmallBefore : size == TreeSize.Medium ? ElementalMediumBefore : ElementalLargeBefore);

        public static float Draugr(TreeSize size) =>
            size == TreeSize.Small ? DraugrSmall : size == TreeSize.Medium ? DraugrMedium : DraugrLarge;
    }
}
