namespace Chopper.Core
{
    // Where something can be made. Valheim's rule: everything is crafted at a station, except the few things a player
    // can make with bare hands. In Chopper that is only the bandage.
    public enum CraftStation
    {
        Anywhere = 0,
        Workbench = 1
    }

    public struct Ingredient
    {
        public ItemKind Kind;
        public int Amount;

        public Ingredient(ItemKind kind, int amount)
        {
            Kind = kind;
            Amount = amount;
        }
    }

    // One recipe: what goes in, what comes out, and where it is made.
    public struct Recipe
    {
        public int Id;
        public string Name;
        public ItemKind Output;
        public CraftStation Station;
        public Ingredient[] Inputs;
    }

    // Every recipe, with the costs from GameConfig. New recipes are a new entry here plus an id; the crafting code, the
    // workbench menu and the Tab screen read this table.
    public static class RecipeBook
    {
        public const int Torch = 0;
        public const int Bandage = 1;
        public const int Boat = 2;
        public const int SharkRod = 3;
        public const int Count = 4;

        public const int DefaultSharkRodFish = 10;

        public static int FishToUnlock(int id, GameConfig config) =>
            id == SharkRod ? (config != null ? config.SharkRodFishToUnlock : DefaultSharkRodFish) : 0;

        public static bool Unlocked(int id, int fishLanded, GameConfig config) => fishLanded >= FishToUnlock(id, config);

        public static string LockHint(int id, int fishLanded, GameConfig config) =>
            Unlocked(id, fishLanded, config) ? string.Empty : "Land " + FishToUnlock(id, config) + " fish";

        public static Recipe Get(int id, GameConfig config)
        {
            switch (id)
            {
                case Torch:
                    return new Recipe
                    {
                        Id = Torch, Name = "Torch", Output = ItemKind.Torch, Station = CraftStation.Workbench,
                        Inputs = new[] { new Ingredient(ItemKind.Wood, config != null ? config.TorchWoodCost : 3) }
                    };
                case SharkRod:
                    return new Recipe
                    {
                        Id = SharkRod, Name = "Shark rod", Output = (ItemKind)ItemRules.SharkRod, Station = CraftStation.Workbench,
                        Inputs = new[]
                        {
                            new Ingredient(ItemKind.Wood, config != null ? config.SharkRodWoodCost : 12),
                            new Ingredient(ItemKind.Fiber, config != null ? config.SharkRodFiberCost : 8),
                            new Ingredient(ItemKind.Iron, config != null ? config.SharkRodIronCost : 4)
                        }
                    };
                case Boat:
                    return new Recipe
                    {
                        Id = Boat, Name = "Little boat", Output = (ItemKind)ItemRules.Boat, Station = CraftStation.Workbench,
                        Inputs = new[]
                        {
                            new Ingredient(ItemKind.Wood, config != null ? config.BoatWoodCost : 20),
                            new Ingredient(ItemKind.Fiber, config != null ? config.BoatFiberCost : 6)
                        }
                    };
                default:
                    return new Recipe
                    {
                        Id = Bandage, Name = "Bandage", Output = ItemKind.Bandage, Station = CraftStation.Anywhere,
                        Inputs = new[]
                        {
                            new Ingredient(ItemKind.Herb, config != null ? config.BandageHerbCost : 2),
                            new Ingredient(ItemKind.Fiber, config != null ? config.BandageFabricCost : 1)
                        }
                    };
            }
        }

        public static bool Valid(int id) => id >= 0 && id < Count;

        public static CraftStation StationFor(int id) => id == Bandage ? CraftStation.Anywhere : CraftStation.Workbench;

        // The crafting rule: a recipe that needs the workbench can only be made standing at one.
        public static bool StationAllows(CraftStation station, bool atWorkbench) => station == CraftStation.Anywhere || atWorkbench;

        public static bool StationAllows(int id, bool atWorkbench) => Valid(id) && StationAllows(StationFor(id), atWorkbench);

        // Upgrades, the forge and tool repairs are workbench work too.
        public static bool UpgradeAllowed(bool atWorkbench) => atWorkbench;

        // True when every ingredient is there (have(kind) gives the carried count) and the output stack has room.
        public static bool CanAfford(Recipe recipe, System.Func<ItemKind, int> have, int outputHave, int outputCap)
        {
            if (outputHave >= outputCap) return false;
            foreach (Ingredient input in recipe.Inputs)
            {
                if (input.Amount < 0 || have(input.Kind) < input.Amount) return false;
            }
            return true;
        }
    }
}
