using Chopper.Core;
using Chopper.Player;
using UnityEngine;

namespace Chopper.UI
{
    // How an ItemKind looks in a slot grid: its name, icon and tint (the chest and the piece costs use it).
    public static class ItemLook
    {
        public static string Name(int kind)
        {
            if (ItemRules.IsFish(kind))
            {
                PlayerFishing fishing = PlayerFishing.Local;
                return fishing != null ? fishing.SpeciesOf(ItemRules.FishOf(kind)).Name : "Fish";
            }
            if (ItemRules.IsFind(kind)) return MiningRules.Finds[(int)ItemRules.FindOf(kind)].Name;
            if (ItemRules.IsEssence(kind)) return ItemRules.ElementOf(kind) + " essence";
            if (ItemRules.IsBoat(kind)) return "Little boat";
            if (ItemRules.IsSharkRod(kind)) return "Shark rod";
            switch ((ItemKind)kind)
            {
                case ItemKind.Wood: return "Wood";
                case ItemKind.Herb: return "Healing herb";
                case ItemKind.Fiber: return "Fiber";
                case ItemKind.Bandage: return "Bandage";
                case ItemKind.Torch: return "Torch";
                case ItemKind.Iron: return "Crypt iron";
                case ItemKind.Shard: return "Rune shard";
                default: return "Item";
            }
        }

        public static Sprite Icon(int kind)
        {
            if (ItemRules.IsFish(kind)) return HotbarIcons.Fish;
            if (ItemRules.IsFind(kind)) return HotbarIcons.Get(MiningRules.Finds[(int)ItemRules.FindOf(kind)].Icon);
            if (ItemRules.IsEssence(kind)) return PickupFeedView.IconFor(PickupFeed.Kind.Essence);
            switch ((ItemKind)kind)
            {
                case ItemKind.Wood: return PickupFeedView.IconFor(PickupFeed.Kind.Wood);
                case ItemKind.Herb: return PickupFeedView.IconFor(PickupFeed.Kind.Herb);
                case ItemKind.Fiber: return PickupFeedView.IconFor(PickupFeed.Kind.Fabric);
                case ItemKind.Bandage: return PickupFeedView.IconFor(PickupFeed.Kind.Bandage);
                case ItemKind.Torch: return PickupFeedView.IconFor(PickupFeed.Kind.Torch);
                case ItemKind.Iron: return PickupFeedView.IconFor(PickupFeed.Kind.Iron);
                default: return PickupFeedView.IconFor(PickupFeed.Kind.Shard);
            }
        }

        // The same rarity tints the fish use.
        public static Color RarityTint(int rarity)
        {
            switch (rarity)
            {
                case 1: return new Color(0.75f, 0.95f, 0.8f);
                case 2: return new Color(1f, 0.85f, 0.4f);
                case 3: return new Color(0.75f, 0.85f, 0.6f);
                default: return Color.white;
            }
        }

        public static Color Tint(int kind)
        {
            if (ItemRules.IsEssence(kind)) return Color.Lerp(Color.white, Chopper.World.ZonePalette.Label(ItemRules.ElementOf(kind)), 0.7f);
            if (ItemRules.IsFind(kind)) return RarityTint(MiningRules.Finds[(int)ItemRules.FindOf(kind)].Rarity);
            if (ItemRules.IsFish(kind))
            {
                PlayerFishing fishing = PlayerFishing.Local;
                int rarity = fishing != null ? fishing.SpeciesOf(ItemRules.FishOf(kind)).Rarity : 0;
                switch (rarity)
                {
                    case 1: return new Color(0.75f, 0.95f, 0.8f);
                    case 2: return new Color(1f, 0.85f, 0.4f);
                    case 3: return new Color(0.75f, 0.85f, 0.6f);
                }
            }
            return Color.white;
        }
    }
}
