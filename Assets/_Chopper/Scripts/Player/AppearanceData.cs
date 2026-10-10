using UnityEngine;

namespace Chopper.Player
{
    // A look for the shared Quaternius skeleton, small enough to sync as one int (5 bits per choice). Players are always
    // Default (Chopper); the other values are only used by the draugr and editor tools.
    public struct AppearanceData
    {
        private const int Bits = 5;
        private const int Mask = (1 << Bits) - 1;

        public int Outfit;
        public int Hair;
        public int Beard;
        public int HairColor;
        public int Cloth;
        public int Skin;

        // Chopper as in the concept art: the plaid shirt and jeans, short dark hair parted in the middle, a full beard.
        public static AppearanceData Default => new AppearanceData { Outfit = 0, Hair = 2, Beard = 1, HairColor = 1, Cloth = 0, Skin = 1 };

        public int Pack()
        {
            return (Outfit & Mask) | ((Hair & Mask) << Bits) | ((Beard & Mask) << (2 * Bits)) |
                   ((HairColor & Mask) << (3 * Bits)) | ((Cloth & Mask) << (4 * Bits)) | ((Skin & Mask) << (5 * Bits));
        }

        public static AppearanceData Unpack(int packed)
        {
            return new AppearanceData
            {
                Outfit = packed & Mask,
                Hair = (packed >> Bits) & Mask,
                Beard = (packed >> (2 * Bits)) & Mask,
                HairColor = (packed >> (3 * Bits)) & Mask,
                Cloth = (packed >> (4 * Bits)) & Mask,
                Skin = (packed >> (5 * Bits)) & Mask
            };
        }

        // Keeps every choice inside what the catalog actually offers.
        public AppearanceData Clamped(CharacterCatalog catalog)
        {
            if (catalog == null) return this;
            return new AppearanceData
            {
                Outfit = CharacterCatalog.Clamp(Outfit, catalog.Outfits != null ? catalog.Outfits.Length : 0),
                Hair = CharacterCatalog.Clamp(Hair, catalog.Hairs != null ? catalog.Hairs.Length : 0),
                Beard = CharacterCatalog.Clamp(Beard, catalog.Beards != null ? catalog.Beards.Length : 0),
                HairColor = CharacterCatalog.Clamp(HairColor, catalog.HairColors != null ? catalog.HairColors.Length : 0),
                Cloth = CharacterCatalog.Clamp(Cloth, catalog.ClothTints != null ? catalog.ClothTints.Length : 0),
                Skin = CharacterCatalog.Clamp(Skin, catalog.SkinTones != null ? catalog.SkinTones.Length : 0)
            };
        }

        // Everyone plays as Chopper: there is no creator, and old saved choices or test overrides are ignored.
        public static AppearanceData Load() => Default;
    }
}
