using System;
using System.Collections;
using System.Collections.Generic;
using Chopper.Core;
using Chopper.Enemies;
using Chopper.Player;
using Chopper.Trees;
using Chopper.World.Building;
using Chopper.World.Generation;
using FishNet.Object;
using UnityEngine;

namespace Chopper.World.Saving
{
    // Server side saving and loading of the whole world. When the world is up it applies a pending save (if the host
    // chose Load or Continue), then gives each player their own record when their profile id arrives. It saves at
    // every dawn, when asked (pause menu), and when the host quits. Offline players' records are kept as they were.
    public class WorldSaver : NetworkBehaviour
    {
        private readonly Dictionary<string, PlayerRecord> _records = new Dictionary<string, PlayerRecord>();
        private readonly HashSet<PlayerIdentity> _applied = new HashSet<PlayerIdentity>();

        public static WorldSaver Instance { get; private set; }

        // Server: true once the saved world (or a fresh one) is in place and players can get their records.
        public bool WorldApplied { get; private set; }
        // Server: the save that was loaded at start, if any (tests read it).
        public WorldSave Loaded { get; private set; }

        public static event Action<bool> Saved;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            PlayerIdentity.ServerIdentified += OnIdentified;
            PlayerIdentity.ServerLeaving += OnLeaving;
            DayNightCycle.ServerPhaseChanged += OnPhaseChanged;
            StartCoroutine(ApplyWhenReady());
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            PlayerIdentity.ServerIdentified -= OnIdentified;
            PlayerIdentity.ServerLeaving -= OnLeaving;
            DayNightCycle.ServerPhaseChanged -= OnPhaseChanged;
        }

        private void OnApplicationQuit()
        {
            if (IsServerInitialized) ServerSave();
        }

        // Everything a save touches must exist and have run its own server start (the hut resets itself there).
        private static bool WorldUp()
        {
            return WorldSession.Ready && Hut.Instance != null && Hut.Instance.IsServerInitialized
                   && DayNightCycle.Instance != null && DayNightCycle.Instance.IsServerInitialized
                   && TreeManager.Instance != null && TreeManager.Instance.IsServerInitialized && TreeManager.Instance.Database != null
                   && EnemyDirector.Instance != null && EnemyDirector.Instance.IsServerInitialized
                   && CryptManager.Instance != null && CryptManager.Instance.IsServerInitialized
                   && ArenaManager.Instance != null && ArenaManager.Instance.IsServerInitialized
                   && (BuildManager.Instance == null || BuildManager.Instance.IsServerInitialized);
        }

        private IEnumerator ApplyWhenReady()
        {
            while (!WorldUp()) yield return null;
            yield return null;

            WorldSave pending = SaveSystem.TakePending();
            if (pending != null)
            {
                ServerApply(pending);
                Debug.Log($"[Save] Loaded \"{pending.Name}\" (seed {pending.Seed}, day {pending.Day}).");
            }
            WorldApplied = true;
            foreach (PlayerIdentity player in PlayerIdentity.ServerAll) ApplyPlayer(player);
            // A new world is written at once, so Continue finds it even if the host quits before the first dawn.
            if (pending == null && SaveSystem.Saving) ServerSave();
        }

        private void OnIdentified(PlayerIdentity player)
        {
            if (WorldApplied) ApplyPlayer(player);
        }

        private void OnLeaving(PlayerIdentity player)
        {
            if (WorldApplied && !string.IsNullOrEmpty(player.Id)) _records[player.Id] = CapturePlayer(player);
        }

        // Dawn: the night is over and cleaned up a frame later (EnemyDirector), then the world is saved.
        private void OnPhaseChanged(DayPhase phase)
        {
            if (phase == DayPhase.Dawn && IsServerInitialized) StartCoroutine(SaveNextFrame());
        }

        private IEnumerator SaveNextFrame()
        {
            yield return null;
            ServerSave();
        }

        // Server only. Writes the world to its file. False when this game is not saved or the write failed.
        public bool ServerSave()
        {
            if (!SaveSystem.Saving || !WorldApplied) return false;
            bool ok = SaveFiles.Write(ServerCapture(), SaveSystem.Path);
            if (ok) Debug.Log($"[Save] Saved \"{SaveSystem.WorldName}\" to {SaveSystem.Path}");
            Saved?.Invoke(ok);
            return ok;
        }

        // ---------- Capture ----------

        public WorldSave ServerCapture()
        {
            var save = new WorldSave
            {
                Name = SaveSystem.WorldName,
                Seed = WorldSession.Seed,
                SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            };

            DayNightCycle cycle = DayNightCycle.Instance;
            save.Day = cycle.Day;
            save.Phase = (int)cycle.Phase;
            save.Elapsed = cycle.PhaseElapsed;

            Hut hut = Hut.Instance;
            save.Hut = new HutRecord { Repaired = hut.IsRepaired, RepairWood = hut.RepairWoodHave, Level = hut.Level, Health = hut.Health, WoodStore = hut.WoodStore };
            SharedChest storage = SharedChest.Instance;
            if (storage != null) save.Chest = storage.ServerCapture();
            if (BuildManager.Instance != null)
            {
                save.Pieces.AddRange(BuildManager.Instance.ServerCapture());
                save.Domes = BuildManager.Instance.ServerDomePositions();
            }

            EnemyDirector director = EnemyDirector.Instance;
            save.TreesFelled = director.TreesFelledSinceBoss;
            save.BossesDefeated = director.BossesDefeated;
            save.BossPlanted = director.BossPlanted;
            foreach ((Vector3 position, Element element) in director.ServerPendingBosses())
            {
                save.PendingBosses.Add(new BossRecord { Position = position, Element = (int)element });
            }
            save.GroveFelled.AddRange(director.GroveFelled);
            Treant warden = director.Warden;
            if (warden != null)
            {
                save.WardenAwake = true;
                save.WardenHome = warden.Home;
                save.WardenElement = (int)warden.Element;
            }
            save.FelledTrees = TreeManager.Instance.ServerFelledIds();
            save.FirstLargeTreeFelled = TreeManager.Instance.FirstLargeTreeFelled;

            foreach (Treant t in Treant.All)
            {
                if (t == null) continue;
                // Arena fights are not saved: a world reloads with its unbeaten arenas waiting to start again.
                if (t.Arena >= 0) continue;
                // The warden is saved on its own and rises again at full health; its guards are called again in the fight.
                if (t.HasLeash) continue;
                if (t.IsGuardian)
                {
                    save.Guardians.Add(new GuardianRecord { Position = t.transform.position, Element = (int)t.Element, Size = (int)t.Size });
                    continue;
                }
                // An enemy awake at night goes back in the ground where it stands, and wakes again with the night.
                save.Saplings.Add(new SaplingRecord { Position = t.transform.position, Element = (int)t.Element, Size = (int)t.Size, Boss = t.IsBoss, BornDays = t.BornDays });
            }
            foreach (WoodPile pile in WoodPile.All)
            {
                if (pile != null && pile.Amount > 0) save.Piles.Add(new PileRecord { Position = pile.transform.position, Amount = pile.Amount });
            }
            foreach (GroundItem item in GroundItem.All)
            {
                if (item == null || !GroundItem.IsShoreGear(item.Kind)) continue;
                save.Gear.Add(new GearRecord { Position = item.transform.position, Kind = (int)item.Kind, Amount = item.Amount });
            }

            CryptManager crypts = CryptManager.Instance;
            save.AwakeCrypts.AddRange(crypts.AwakeCrypts);
            foreach ((int chest, string player) in crypts.LootedPairs()) save.Looted.Add(new LootRecord { Chest = chest, Player = player });
            save.ClearedArenas.AddRange(ArenaManager.Instance.ClearedArenas());

            foreach (PlayerIdentity player in PlayerIdentity.ServerAll)
            {
                if (!string.IsNullOrEmpty(player.Id)) _records[player.Id] = CapturePlayer(player);
            }
            save.Players.AddRange(_records.Values);
            return save;
        }

        private static PlayerRecord CapturePlayer(PlayerIdentity player)
        {
            var record = new PlayerRecord { Id = player.Id, Position = player.transform.position };
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null) record.Health = health.IsDown ? 0 : health.Health;
            PlayerAxe axe = player.GetComponent<PlayerAxe>();
            if (axe != null)
            {
                record.AxeWear = axe.Wear;
                record.AxeDropped = !axe.HasAxe;
                record.LogSkill = axe.LogSkill;
            }
            PlayerWood wood = player.GetComponent<PlayerWood>();
            if (wood != null) record.Wood = wood.Carried;
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                record.Torches = inventory.Torches;
                record.Salves = inventory.Salves;
                record.Herbs = inventory.Herbs;
                record.Fabric = inventory.Fabric;
                record.Iron = inventory.Iron;
                record.Shards = inventory.Shards;
                record.Essence = inventory.EssencePacked;
            }
            PlayerUpgrades upgrades = player.GetComponent<PlayerUpgrades>();
            if (upgrades != null)
            {
                record.AxeLevel = upgrades.AxeLevel;
                record.ArmorLevel = upgrades.ArmorLevel;
                record.SturdyLevel = upgrades.SturdyLevel;
                record.RuneLevel = upgrades.RuneLevel;
                record.Wards = upgrades.WardsPacked;
                record.Stones = upgrades.Stones;
                record.ArmourKits = upgrades.ArmourKits;
                record.DomeStones = upgrades.DomeStones;
                record.Armoured = upgrades.Armoured;
                record.Undressed = !upgrades.Dressed;
                record.HasShield = upgrades.HasShield;
            }
            PlayerBoons boons = player.GetComponent<PlayerBoons>();
            if (boons != null) record.Boons = boons.Packed;
            PlayerFishing fishing = player.GetComponent<PlayerFishing>();
            if (fishing != null)
            {
                record.Rod = fishing.Rod;
                record.Lure = fishing.Lure;
                record.FishCounts = fishing.CopyCounts();
                record.FishRecords = fishing.CopyRecords();
                record.Fish = fishing.FishPacked;
                record.FishXp = fishing.Xp;
            record.FishLanded = fishing.FishLanded;
            Chopper.Player.PlayerBoat saveBoat = fishing.GetComponent<Chopper.Player.PlayerBoat>();
            if (saveBoat != null)
            {
                record.SharkRod = saveBoat.HasSharkRod;
                record.Boats = saveBoat.Boats + (saveBoat.BoatOut ? 1 : 0);
            }
                record.RodWear = fishing.RodWear;
                record.FishSkill = fishing.FishSkill;
            }
            PlayerMining mining = player.GetComponent<PlayerMining>();
            if (mining != null)
            {
                record.FindCounts = mining.CopyFinds();
                record.FindRecords = mining.CopyRecords();
                record.MineSkill = mining.MineSkill;
            }
            // A player who was down is put back at the hut instead of where they fell.
            if (health != null && health.IsDown) record.Position = Vector3.zero;
            return record;
        }

        // ---------- Apply ----------

        public void ServerApply(WorldSave save)
        {
            Loaded = save;
            DayNightCycle cycle = DayNightCycle.Instance;
            cycle.ServerLoad((DayPhase)Mathf.Clamp(save.Phase, 0, 3), save.Day, save.Elapsed);

            HutRecord hut = save.Hut ?? new HutRecord();
            Hut.Instance.ServerLoad(hut.Repaired, hut.RepairWood, hut.Level, hut.Health, hut.WoodStore);
            if (SharedChest.Instance != null) SharedChest.Instance.ServerLoad(save.Chest);
            if (BuildManager.Instance != null)
            {
                BuildManager.Instance.ServerLoad(save.Pieces);
                BuildManager.Instance.ServerLoadDomes(save.Domes);
            }

            EnemyDirector director = EnemyDirector.Instance;
            var pending = new List<(Vector3, Element)>();
            foreach (BossRecord boss in save.PendingBosses) pending.Add((boss.Position, (Element)boss.Element));
            director.ServerLoadProgress(save.TreesFelled, save.BossesDefeated, save.BossPlanted, pending);
            director.ServerLoadGroves(save.GroveFelled);
            TreeManager.Instance.ServerLoadFelled(save.FelledTrees, save.FirstLargeTreeFelled);

            // Whatever enemies the fresh world started with make way for the saved ones.
            foreach (Treant t in new List<Treant>(Treant.All)) t.ServerDespawn();
            foreach (SaplingRecord sapling in save.Saplings)
            {
                float age = cycle.WorldDays - sapling.BornDays;
                director.ServerSpawn((Element)sapling.Element, (TreeSize)Mathf.Clamp(sapling.Size, 0, 2), sapling.Boss, sapling.Position, age);
            }
            foreach (PileRecord pile in save.Piles) director.ServerDropWood(pile.Position, pile.Amount);
            // The fresh world's start kits make way for the saved gear (kits included), so nothing doubles up.
            foreach (GroundItem item in new List<GroundItem>(GroundItem.All))
                if (item != null && GroundItem.IsShoreGear(item.Kind)) item.ServerRemove();
            foreach (GearRecord gear in save.Gear)
            {
                var kind = (GroundItemKind)gear.Kind;
                if (GroundItem.IsShoreGear(kind)) director.ServerDropItem(kind, gear.Amount, gear.Position, gear.Position);
            }

            var looted = new List<(int, string)>();
            foreach (LootRecord loot in save.Looted) looted.Add((loot.Chest, loot.Player));
            CryptManager.Instance.ServerLoad(save.AwakeCrypts, looted);
            ArenaManager.Instance.ServerLoad(save.ClearedArenas);
            foreach (GuardianRecord guardian in save.Guardians)
            {
                Treant t = director.ServerSpawn((Element)guardian.Element, (TreeSize)Mathf.Clamp(guardian.Size, 0, 2), false, guardian.Position);
                t.ServerMakeGuardian(guardian.Position);
                t.ServerActivate();
            }
            if (save.WardenAwake) director.ServerWakeWarden(save.WardenHome, (Element)Mathf.Clamp(save.WardenElement, 0, 4));

            _records.Clear();
            foreach (PlayerRecord record in save.Players)
            {
                if (!string.IsNullOrEmpty(record.Id)) _records[record.Id] = record;
            }
            _applied.Clear();
        }

        // Server only, tests: gives this player their record from the last applied save again.
        public void ServerApplyPlayer(PlayerIdentity player)
        {
            _applied.Remove(player);
            ApplyPlayer(player);
        }

        // Gives a player their saved things back (once per session), and shows them the chests they emptied before.
        private void ApplyPlayer(PlayerIdentity player)
        {
            if (player == null || string.IsNullOrEmpty(player.Id) || !_applied.Add(player)) return;
            PlayerCrypt crypt = player.GetComponent<PlayerCrypt>();
            if (crypt != null && CryptManager.Instance != null) CryptManager.Instance.ServerSendLooted(crypt);
            if (!_records.TryGetValue(player.Id, out PlayerRecord record)) return;

            PlayerWood wood = player.GetComponent<PlayerWood>();
            if (wood != null)
            {
                wood.ServerTakeAll();
                wood.ServerAddWood(record.Wood);
            }
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                inventory.ServerLoad(record.Torches, record.Salves, record.Herbs, record.Fabric, record.Iron, record.Shards);
                inventory.ServerLoadEssence(record.Essence);
            }
            PlayerUpgrades upgrades = player.GetComponent<PlayerUpgrades>();
            if (upgrades != null)
            {
                upgrades.ServerLoad(record.AxeLevel, record.ArmorLevel, record.SturdyLevel, record.RuneLevel);
                upgrades.ServerLoadWards(record.Wards);
                upgrades.ServerLoadStones(record.Stones);
                upgrades.ServerLoadOutfit(record.ArmourKits, record.Armoured);
                upgrades.ServerLoadDomeStones(record.DomeStones);
                if (record.Undressed) upgrades.ServerSetDressed(false);
                upgrades.ServerSetShield(record.HasShield);
            }
            PlayerBoons boons = player.GetComponent<PlayerBoons>();
            if (boons != null) boons.ServerLoad(record.Boons);
            PlayerFishing fishing = player.GetComponent<PlayerFishing>();
            if (fishing != null)
            {
                fishing.ServerLoad(record.Rod, record.Lure, record.Fish, record.FishCounts, record.FishXp);
                fishing.ServerLoadRecords(record.FishRecords);
                fishing.ServerLoadRodWear(record.RodWear);
                fishing.ServerLoadSkill(record.FishSkill);
            fishing.ServerLoadLanded(record.FishLanded);
            Chopper.Player.PlayerBoat loadBoat = fishing.GetComponent<Chopper.Player.PlayerBoat>();
            if (loadBoat != null) loadBoat.ServerLoad(record.Boats, record.SharkRod);
            }
            PlayerMining mining = player.GetComponent<PlayerMining>();
            if (mining != null)
            {
                mining.ServerLoad(record.FindCounts, record.FindRecords);
                mining.ServerLoadSkill(record.MineSkill);
            }
            PlayerAxe axe = player.GetComponent<PlayerAxe>();
            if (axe != null)
            {
                axe.ServerLoadWear(record.AxeWear);
                axe.ServerLoadLogSkill(record.LogSkill);
                if (record.AxeDropped) axe.ServerSetHasAxe(false);
            }
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null) health.ServerLoadHealth(record.Health);
            if (crypt != null && record.Position != Vector3.zero) crypt.ServerTeleport(OnTheGround(record.Position) + Vector3.up * 0.2f, player.transform.eulerAngles.y);
        }

        // A spot saved before the land changed (the mountains between the zones) can now be inside a hill: lift it
        // onto the ground. Crypt floors are far below the land and are left alone.
        private static Vector3 OnTheGround(Vector3 position)
        {
            bool hasGround = Chopper.World.WorldGround.Exists;
            if (!hasGround || position.y < -100f) return position;
            float ground = Chopper.World.WorldGround.HeightAt(position);
            if (position.y < ground) position.y = ground;
            return position;
        }
    }
}
