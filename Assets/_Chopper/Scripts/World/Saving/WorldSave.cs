using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chopper.World.Saving
{
    // Everything a world needs to carry on where it stopped, as plain data for JsonUtility. The land itself is not
    // stored: the seed rebuilds it. Players are stored by their profile id, so each one gets their own things back
    // when they join again.
    [Serializable]
    public class WorldSave
    {
        // 2: the Unity Terrain is gone and the ground is the block columns (W5-08 step 13). Older files are not loaded.
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;
        public string Name = "World";
        public int Seed;
        // Local time of the save, "yyyy-MM-dd HH:mm", for the load list.
        public string SavedAt = string.Empty;

        public int Day = 1;
        public int Phase;
        public float Elapsed;

        public HutRecord Hut = new HutRecord();
        // The shared storage chest: one count per ItemKind (older saves have none: an empty chest).
        public int[] Chest = new int[0];
        // Building pieces players put up (BuildManager).
        public List<PieceRecord> Pieces = new List<PieceRecord>();

        public int TreesFelled;
        public int BossesDefeated;
        public bool BossPlanted;
        public List<SaplingRecord> Saplings = new List<SaplingRecord>();
        public List<BossRecord> PendingBosses = new List<BossRecord>();
        // Felled forest trees still counting toward a grove warden, and the warden if one was awake.
        public List<Vector3> GroveFelled = new List<Vector3>();
        public bool WardenAwake;
        public Vector3 WardenHome;
        public int WardenElement;
        public int[] FelledTrees = new int[0];
        public bool FirstLargeTreeFelled;
        public List<PileRecord> Piles = new List<PileRecord>();
        // W9-02: axes and outfits lying on the ground (start kits on the bank, gear left where a player fell).
        public List<GearRecord> Gear = new List<GearRecord>();
        // Dome centres in the forests (W9-04). Missing in a save that has none.
        public List<Vector3> Domes = new List<Vector3>();

        public List<int> AwakeCrypts = new List<int>();
        public List<GuardianRecord> Guardians = new List<GuardianRecord>();
        public List<LootRecord> Looted = new List<LootRecord>();
        public List<int> ClearedArenas = new List<int>();

        public List<PlayerRecord> Players = new List<PlayerRecord>();

        public PlayerRecord FindPlayer(string id) => Players.Find(p => p.Id == id);
    }

    [Serializable]
    public class HutRecord
    {
        public bool Repaired;
        public int RepairWood;
        public int Level;
        public int Health;
        public int WoodStore;
    }

    // One building piece: which kind (its index in the piece catalog), where, which way it faces and its health.
    [Serializable]
    public class PieceRecord
    {
        public int Kind;
        public Vector3 Position;
        public float Yaw;
        public int Health;
    }

    // A sapling in the ground (or an enemy that was awake: it goes back to being a sapling and wakes again at night).
    [Serializable]
    public class SaplingRecord
    {
        public Vector3 Position;
        public int Element;
        public int Size;
        public bool Boss;
        public float BornDays;
    }

    [Serializable]
    public class BossRecord
    {
        public Vector3 Position;
        public int Element;
    }

    [Serializable]
    public class GearRecord
    {
        public Vector3 Position;
        public int Kind;     // GroundItemKind.Axe or GroundItemKind.Outfit
        public int Amount;   // the axe's durability; 1 for an outfit
    }

    [Serializable]
    public class PileRecord
    {
        public Vector3 Position;
        public int Amount;
    }

    [Serializable]
    public class GuardianRecord
    {
        public Vector3 Position;
        public int Element;
        public int Size;
    }

    // One player who has looted one chest.
    [Serializable]
    public class LootRecord
    {
        public int Chest;
        public string Player = string.Empty;
    }

    [Serializable]
    public class PlayerRecord
    {
        public string Id = string.Empty;
        public Vector3 Position;
        public int Health;
        // How worn the axe and the rod are (0 is new; older saves have neither, so their tools load whole).
        public int AxeWear;
        public int RodWear;
        public int Wood;
        public int Torches;
        public int Salves;
        public int Herbs;
        public int Fabric;
        public int Iron;
        public int Shards;
        public int AxeLevel;
        public int ArmorLevel;
        public int SturdyLevel;
        public int RuneLevel;
        public int Boons;
        // Packed per element (EssenceRules): arena essence carried, and elemental ward levels.
        public int Essence;
        public int Wards;
        // Element stones set in the axe, one bit per element (ElementBuffRules).
        public int Stones;
        // The armoured outfit: kits carried and whether it is worn (missing in older saves: unarmoured, no kits).
        public int ArmourKits;
        public int DomeStones;
        public bool Armoured;
        // W9-02: the everyday outfit and the axe left where the player fell (false in older saves: dressed, axe in hand).
        public bool Undressed;
        public bool AxeDropped;
        // Fishing: rod and lure levels, the fish carried (one count per kind), and the old packed int for the first four.
        public int Rod;
        public int Lure;
        public int Fish;
        public int[] FishCounts = new int[0];
        // The heaviest catch of each kind in kilograms (missing in older saves).
        public float[] FishRecords = new float[0];
        public int FishXp;
        // Mining: the finds carried (one count per MineFindKind) and the biggest of each in grams (missing in older saves).
        public int[] FindCounts = new int[0];
        public float[] FindRecords = new float[0];
    }

    // What the load list shows for one save file, read without keeping the whole save.
    public struct SaveSummary
    {
        public string Path;
        public string Name;
        public int Seed;
        public int Day;
        public string SavedAt;
        public DateTime Written;
    }
}
