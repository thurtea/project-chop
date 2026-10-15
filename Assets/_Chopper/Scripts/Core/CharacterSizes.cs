namespace Chopper.Core
{
    public static class CharacterSizes
    {
        public const float Player = 1.8f;
        public const float PlayerLegX = 0.20f;
        public const float ElementalSmall = 1.7f;
        public const float ElementalMedium = 2.3f;
        public const float ElementalLarge = 3.2f;
        public const float Warden = 5.5f;
        public const float DraugrSmall = 1.75f;
        public const float DraugrMedium = 1.85f;
        public const float DraugrLarge = 2.0f;
        public const float CryptBoss = 3.2f;

        public static float Elemental(TreeSize size) =>
            size == TreeSize.Small ? ElementalSmall : size == TreeSize.Medium ? ElementalMedium : ElementalLarge;

        public static float Draugr(TreeSize size) =>
            size == TreeSize.Small ? DraugrSmall : size == TreeSize.Medium ? DraugrMedium : DraugrLarge;
    }
}
