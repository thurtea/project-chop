using Chopper.Core;
using UnityEngine;

namespace Chopper.Player
{
    // One way to read and change any carried item by its ItemKind, whichever component keeps it: wood (PlayerWood),
    // fish (PlayerFishing) and the rest (PlayerInventory). The chest, recipes and repairs use it. Changes are server only.
    public static class PlayerItems
    {
        public static int Get(GameObject player, int kind)
        {
            if (player == null || !ItemRules.Valid(kind)) return 0;
            if (kind == (int)ItemKind.Wood)
            {
                PlayerWood wood = player.GetComponent<PlayerWood>();
                return wood != null ? wood.Carried : 0;
            }
            if (ItemRules.IsFish(kind))
            {
                PlayerFishing fishing = player.GetComponent<PlayerFishing>();
                return fishing != null ? fishing.FishCount(ItemRules.FishOf(kind)) : 0;
            }
            if (ItemRules.IsFind(kind))
            {
                PlayerMining mining = player.GetComponent<PlayerMining>();
                return mining != null ? mining.FindCount(ItemRules.FindOf(kind)) : 0;
            }
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null) return 0;
            switch ((ItemKind)kind)
            {
                case ItemKind.Herb: return inventory.Herbs;
                case ItemKind.Fiber: return inventory.Fabric;
                case ItemKind.Bandage: return inventory.Salves;
                case ItemKind.Torch: return inventory.Torches;
                case ItemKind.Iron: return inventory.Iron;
                case ItemKind.Shard: return inventory.Shards;
                default: return inventory.Essence(ItemRules.ElementOf(kind));
            }
        }

        // Mining finds carried, of each kind.
        public const int MiningFindCap = 99;

        // The most of a kind a player can carry.
        public static int Cap(GameConfig config, int kind)
        {
            if (kind == (int)ItemKind.Wood) return config.WoodCarryCap;
            if (ItemRules.IsFish(kind)) return FishingRules.MaxStack;
            if (ItemRules.IsFind(kind)) return MiningFindCap;
            switch ((ItemKind)kind)
            {
                case ItemKind.Herb: return config.HerbCarryCap;
                case ItemKind.Fiber: return config.FabricCarryCap;
                case ItemKind.Bandage:
                case ItemKind.Torch: return config.CraftStackCap;
                case ItemKind.Iron:
                case ItemKind.Shard: return ItemRules.ChestCap;
                default: return 255;
            }
        }

        // Server only. Adds delta (negative removes) and returns how much changed.
        public static int ServerChange(GameObject player, int kind, int delta)
        {
            if (player == null || !ItemRules.Valid(kind) || delta == 0) return 0;
            if (kind == (int)ItemKind.Wood)
            {
                PlayerWood wood = player.GetComponent<PlayerWood>();
                return wood != null ? wood.ServerChange(delta) : 0;
            }
            if (ItemRules.IsFish(kind))
            {
                PlayerFishing fishing = player.GetComponent<PlayerFishing>();
                return fishing != null ? fishing.ServerChangeFish(ItemRules.FishOf(kind), delta) : 0;
            }
            if (ItemRules.IsFind(kind))
            {
                PlayerMining mining = player.GetComponent<PlayerMining>();
                return mining != null ? mining.ServerChangeFind(ItemRules.FindOf(kind), delta) : 0;
            }
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            return inventory != null ? inventory.ServerChange((ItemKind)kind, delta) : 0;
        }
    }
}
