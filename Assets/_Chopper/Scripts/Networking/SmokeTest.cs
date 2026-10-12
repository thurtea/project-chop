#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using Chopper.Core;
using Chopper.Enemies;
using Chopper.Player;
using Chopper.Trees;
using Chopper.World;
using Chopper.World.Generation;
using UnityEngine;

namespace Chopper.Networking
{
    // Development only. Started with the -chopperSmoke command line flag together with -chopperHost.
    // Drives the server side of the whole loop through the real networking path and logs PASS or FAIL:
    // chop, wood and repair, saplings, night, NavMesh walking, kills and buffs, dawn, boss, upgrades, game over.
    public static class SmokeTest
    {
        private const string SmokeArg = "-chopperSmoke";
        private const float WaitTimeout = 30f;
        private const float ChopAimOffset = 1.5f;
        private const float SettleSeconds = 0.5f;
        private const float EnemyWalkTimeout = 45f;

        private static bool _ok = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), SmokeArg) < 0) return;
            var host = new GameObject("SmokeTest");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<SmokeRunner>().StartCoroutine(Run());
        }

        private class SmokeRunner : MonoBehaviour { }

        private static void Check(bool condition, string message)
        {
            Debug.Log($"[Smoke] {(condition ? "ok  " : "FAIL")} {message}");
            if (!condition) _ok = false;
        }

        private static IEnumerator Run()
        {
            float deadline = Time.unscaledTime + WaitTimeout;
            while (Time.unscaledTime < deadline && !WorldReady()) yield return null;
            if (!WorldReady())
            {
                Finish(false, "world objects never appeared");
                yield break;
            }

            // A saved game's axe is handed back quietly in the first seconds after spawning (PlayerAxe), so wait
            // until a real pickup would open the How to play screen.
            yield return new WaitForSeconds(4.5f);
            // The building checks run in the hub (W9-03 keeps players from building there; the tours check that rule).
            Chopper.World.Building.BuildManager.DebugAllowHubBuilding = true;
            yield return IntroFlow();
            yield return CheckAnimator();
            yield return CheckSurvival();
            yield return CombatMoves();
            yield return HerbsWork();
            yield return BranchesWork();
            yield return FallingTreesAndDying();
            yield return CryptsWork();
            yield return ArenaWorks();
            yield return FishingWorks();
            yield return SavingWorks();
            yield return ChopAndRepair();
            yield return SaplingsAndNight();
            yield return EnemiesGoForThePlayerFirst();
            yield return DawnAndBoss();
            yield return GroveWardenWorks();
            yield return UpgradesAndGameOver();

            Finish(_ok, _ok ? "full loop works" : "see FAIL lines above");
        }

        private static bool WorldReady()
        {
            return PlayerWood.Local != null && TreeManager.Instance != null && Hut.Instance != null &&
                   DayNightCycle.Instance != null && EnemyDirector.Instance != null;
        }

        // ---------- 0a. The start of the game: broken hut, fists, repair, axe ----------

        private static void ResetHut(Hut hut)
        {
            hut.ServerReset();
            hut.ServerSetRepaired(true);
        }

        private static IEnumerator IntroFlow()
        {
            PlayerWood wood = PlayerWood.Local;
            PlayerAxe axe = wood.GetComponent<PlayerAxe>();
            Hut hut = Hut.Instance;
            TreeDatabase db = TreeManager.Instance.Database;
            GameConfig config = TreeManager.Instance.Config;
            EnemyDirector.Instance.ServerLockNightShare(1f);

            Check(!hut.IsRepaired, "the hut starts broken");
            PlayerFishing startRod = wood.GetComponent<PlayerFishing>();
            // W9-01 (user, 2026-10-12): players arrive in the pond with no axe and no outfit; both lie on the bank.
            PlayerUpgrades startUpgrades = wood.GetComponent<PlayerUpgrades>();
            Check(!axe.HasAxe && !startUpgrades.Dressed, "players arrive with no axe and no outfit");
            int kitPieces = 0;
            foreach (GroundItem item in GroundItem.All)
                if (item != null && (item.Kind == GroundItemKind.Axe || item.Kind == GroundItemKind.Outfit)) kitPieces++;
            Check(kitPieces >= 2, $"the start kit lies on the pond bank ({kitPieces} pieces)");
            // The rest of the run uses them, as if picked up on the shore.
            axe.ServerResetTool();
            startUpgrades.ServerSetDressed(true);
            Check(axe.HasAxe && axe.Durability == config.AxeDurability, "the axe from the shore is new");
            Check(startRod != null && startRod.Rod >= 1 && startRod.CanFish, "players start with a fishing rod");
            Check(Workbench.All.Count >= 1 && Sign.Guide != null, "the house has a workbench and the yard a notice board");
            Check(SharedChest.Instance != null && Bed.InReach(Workbench.All[0].transform.position) == null, "the hut has the shared chest; the bed is away from the bench");
            Check(UnityEngine.Object.FindAnyObjectByType<StorageChest>() != null && UnityEngine.Object.FindAnyObjectByType<Bed>() != null, "the house has a storage chest and a bed");
            Vector3 start = wood.transform.position;
            Check(new Vector2(start.x, start.z).magnitude > 12f, $"the player starts outside the house ({start.x:F1}, {start.z:F1})");
            // Players come back beside the bed, inside the house.
            int insideSpots = 0;
            foreach (PlayerSpawnPoint point in PlayerSpawnPoint.Active)
            {
                if (point.Outside) continue;
                Vector3 local = point.transform.position - hut.transform.position;
                if (Mathf.Abs(local.x) < Chopper.World.Generation.WorldGen.HutHalfWidth && Mathf.Abs(local.z) < Chopper.World.Generation.WorldGen.HutHalfDepth) insideSpots++;
            }
            Check(insideSpots >= 4, $"the respawn spots are inside the house ({insideSpots})");
            // The workbench stands inside the house, by the east wall.
            Vector3 bench = Workbench.All[0].transform.position - hut.transform.position;
            Check(bench.x > 2f && bench.x < Chopper.World.Generation.WorldGen.HutHalfWidth && Mathf.Abs(bench.z) < Chopper.World.Generation.WorldGen.HutHalfDepth,
                $"the workbench is inside the house by the east wall ({bench.x:F1}, {bench.z:F1})");
            Check(UnityEngine.Object.FindAnyObjectByType<Chopper.World.Building.BuildManager>() != null, "the building manager is in the world");

            // Bare fists (the axe on the back): several punches add up to one hit on a tree.
            axe.RequestToggleDrawn();
            yield return new WaitForSeconds(0.3f);
            Check(axe.HasAxe && !axe.IsDrawn, "the Axe key puts it on the back");
            int id = FindTree(db, TreeSize.Small);
            TreeEntry tree = db.Get(id);
            Vector3 aim = Vector3.forward;
            TeleportPlayer(wood, tree.Position - aim * ChopAimOffset);
            yield return new WaitForSeconds(0.3f);
            int punches = FistRules.PunchesPerHit(config.FistDamageMultiplier) * config.GetHitsToFell(TreeSize.Small);
            for (int i = 1; i < punches; i++)
            {
                axe.ServerResolveHit(aim);
                yield return null;
                Check(wood.Carried == 0, $"punch {i} of {punches} does not fell the small tree yet");
            }
            axe.ServerResolveHit(aim);
            yield return null;
            Check(wood.Carried == config.GetWoodForSize(TreeSize.Small), $"{punches} punches fell a small tree ({wood.Carried} wood)");
            Check(axe.Durability == config.AxeDurability, "punching does not wear the axe");

            // Wood deposited at a broken hut repairs it first; the rest goes into the store.
            wood.ServerAddWood(2);
            int partWood = config.GetWoodForSize(TreeSize.Small) + 2;
            hut.ServerDeposit(wood);
            yield return null;
            Check(hut.RepairWoodHave == partWood && !hut.IsRepaired && hut.WoodStore == 0, $"{partWood} of {hut.RepairWoodNeeded} wood in, hut still broken ({hut.RepairWoodHave})");
            wood.ServerAddWood(config.HutRepairWood);
            hut.ServerDeposit(wood);
            yield return null;
            Check(hut.IsRepaired, "enough wood repairs the hut");
            Check(hut.WoodStore == partWood, $"the leftover wood goes to the store ({hut.WoodStore})");
            hut.ServerTrySpend(hut.WoodStore);

            axe.RequestToggleDrawn();
            yield return new WaitForSeconds(0.3f);
            Check(axe.IsDrawn, "and draws it again");
            yield return new WaitForSeconds(0.5f);
            Check(!Chopper.UI.UiState.HowToPlayOpen && !Chopper.UI.UiState.InputBlocked, "no guide screen opens by itself");

            TreeManager.Instance.ServerRegrowAll();
            TeleportPlayer(wood, new Vector3(0f, 0f, 7.5f));
            yield return new WaitForSeconds(0.5f);
        }

        // ---------- 0. The character animates (not stuck in its T-pose) ----------

        private const float TPoseHandHeight = 1.44f;

        private static IEnumerator CheckAnimator()
        {
            Animator animator = PlayerWood.Local.GetComponentInChildren<Animator>();
            Check(animator != null, "player has an Animator");
            if (animator == null) yield break;

            Check(animator.runtimeAnimatorController != null, "Animator has a controller");
            Check(animator.avatar != null && animator.avatar.isValid, "Animator has a valid avatar");
            yield return new WaitForSeconds(1.5f);

            Transform hand = null;
            foreach (Transform t in animator.GetComponentsInChildren<Transform>()) if (t.name == "hand_r") hand = t;
            Check(hand != null, "model has a right hand bone");
            if (hand != null)
            {
                // Relative to the player, who may be standing on the mound.
                float handHeight = hand.position.y - PlayerWood.Local.transform.position.y;
                Check(handHeight < TPoseHandHeight - 0.2f, $"right hand is lowered by the idle animation (y {handHeight:F2}, T-pose is {TPoseHandHeight:F2})");
            }
            Check(animator.GetCurrentAnimatorClipInfo(0).Length > 0, "base layer is playing a clip");

            // The axe sits in the right hand: its grip hangs off the hand bone, inside the fist (a hand's width from the
            // wrist bone, not at it), and the axe itself is within reach of it.
            Transform grip = null;
            foreach (Transform t in PlayerWood.Local.GetComponentsInChildren<Transform>(true)) if (t.name == "AxeGrip") grip = t;
            Check(grip != null && hand != null && grip.IsChildOf(hand), $"the axe grip hangs off the right hand ({(grip != null && grip.parent != null ? grip.parent.name : "none")})");
            if (grip != null && hand != null)
            {
                Renderer axeRenderer = grip.GetComponentInChildren<Renderer>(true);
                float gripGap = Vector3.Distance(grip.position, hand.position);
                float handleGap = axeRenderer != null ? Vector3.Distance(axeRenderer.bounds.ClosestPoint(hand.position), hand.position) : 99f;
                Check(gripGap < 0.14f && handleGap < 0.12f, $"the axe is held in the hand (grip {gripGap:F2} m, handle {handleGap:F2} m from the hand)");
            }

            // A real swing (the same call the mouse button makes) must move the right hand: the chop plays on the
            // upper body layer, and a wrong mask leaves it doing nothing.
            PlayerAxe axe = PlayerWood.Local.GetComponent<PlayerAxe>();
            Transform root = PlayerWood.Local.transform;
            if (hand != null && axe != null)
            {
                Vector3 idleHand = root.InverseTransformPoint(hand.position);
                bool swung = axe.TrySwing();
                // The furthest the hand gets from rest during the swing (the clips differ in timing).
                float moved = 0f;
                for (float waited = 0f; waited < 0.45f; waited += Time.deltaTime)
                {
                    yield return null;
                    moved = Mathf.Max(moved, Vector3.Distance(idleHand, root.InverseTransformPoint(hand.position)));
                }
                Check(swung, "a swing starts when the attack is used");
                Check(moved > 0.25f, $"the chop animation moves the right hand ({moved:F2} m)");
                yield return new WaitForSeconds(1f);
            }
        }

        // ---------- 0b. Stamina, crafting, torch, salve, UI ----------

        private static IEnumerator CheckSurvival()
        {
            PlayerWood wood = PlayerWood.Local;
            PlayerInventory inventory = wood.GetComponent<PlayerInventory>();
            PlayerHealth health = wood.GetComponent<PlayerHealth>();
            GameConfig config = TreeManager.Instance.Config;

            Check(PlayerStamina.Local != null, "local player has stamina");
            if (PlayerStamina.Local != null)
            {
                float before = PlayerStamina.Local.Current;
                Check(PlayerStamina.Local.TryUse(config.SwingStamina), "a swing can be paid for");
                Check(PlayerStamina.Local.Current < before, "swinging spends stamina");
                PlayerStamina.Local.Refill();
            }

            Check(UnityEngine.Object.FindAnyObjectByType<Chopper.UI.GameUi>() != null, "game UI exists");
            Check(Chopper.UI.UiState.InputBlocked == false, "no menu is open at start");

            // The axe recordings ship in Resources (several variations each) and are the ones used for swings and hits.
            AudioClip[] impacts = Chopper.Audio.SoundBank.All("axe-impact");
            AudioClip[] swings = Chopper.Audio.SoundBank.All("axe-slash");
            Check(impacts.Length >= 3, $"axe impact recordings are in the build ({impacts.Length})");
            Check(swings.Length >= 3, $"axe swing recordings are in the build ({swings.Length})");
            Check(System.Array.IndexOf(impacts, Chopper.Audio.ProceduralSfx.Chop) >= 0, "chop sound uses a real axe impact");
            Check(System.Array.IndexOf(swings, Chopper.Audio.ProceduralSfx.Swing) >= 0, "swing sound uses a real axe swing");
            Check(Chopper.Audio.SoundBank.All("dawn-birds").Length == 0 && Chopper.Audio.SoundBank.All("ambience-day").Length == 0, "no bird or ambience recordings ship");

            // Crafting: everything at a workbench, except the bandage, which is made anywhere.
            TeleportPlayer(wood, new Vector3(0f, 0f, 30f));
            yield return new WaitForSeconds(0.3f);
            wood.ServerAddWood(20);
            yield return null;
            Check(!Workbench.AnyInReach(wood.transform.position), "the player is away from every workbench");
            Check(!inventory.ServerCraft(PlayerInventory.RecipeTorch, false), "a torch cannot be made away from a workbench");
            Check(inventory.Torches == config.StartTorches && inventory.Salves == config.StartSalves, $"players start with {inventory.Torches} torch and {inventory.Salves} salve");
            Check(!inventory.ServerCraft(PlayerInventory.RecipeSalve, false), "a bandage cannot be made without herbs and fiber");
            inventory.ServerAddHerbs(config.BandageHerbCost);
            Check(!inventory.ServerCraft(PlayerInventory.RecipeSalve, false), "a bandage cannot be made from herbs alone");
            inventory.ServerAddFabric(config.BandageFabricCost);
            Check(inventory.ServerCraft(PlayerInventory.RecipeSalve, false) && inventory.Herbs == 0 && inventory.Fabric == 0, "a bandage is made from herbs and fiber, away from any bench");
            Vector3 benchSpot = Workbench.All[0].transform.position + new Vector3(-1.4f, 0f, 0f);
            TeleportPlayer(wood, benchSpot);
            yield return new WaitForSeconds(0.3f);
            Check(Workbench.AnyInReach(wood.transform.position), "the player stands at the workbench");
            Check(inventory.ServerCraft(PlayerInventory.RecipeTorch, false), "a torch is made at the workbench from carried wood");
            yield return null;
            Check(inventory.Torches == config.StartTorches + 1 && inventory.Salves == config.StartSalves + 1, $"crafted one more of each ({inventory.Torches}, {inventory.Salves})");
            Check(wood.Carried == 20 - config.TorchWoodCost, $"the torch spent wood and the bandage did not (carrying {wood.Carried})");

            // Wrapping a bandage plays the Heal animation: the hands come together in front of the chest.
            Animator body = wood.GetComponentInChildren<Animator>();
            Transform leftHand = null, rightHand = null;
            if (body != null)
            {
                foreach (Transform t in body.GetComponentsInChildren<Transform>())
                {
                    if (t.name == "hand_l") leftHand = t;
                    if (t.name == "hand_r") rightHand = t;
                }
            }
            PlayerHealth bandaged = wood.GetComponent<PlayerHealth>();
            if (body != null && leftHand != null && rightHand != null && bandaged != null)
            {
                float apart = Vector3.Distance(leftHand.position, rightHand.position);
                bandaged.ServerDamage(30);
                Check(inventory.ServerUseSalve(), "a bandage can be used when hurt");
                bool healing = false;
                float closest = apart;
                float until = Time.time + 1.2f;
                while (Time.time < until)
                {
                    for (int l = 0; l < body.layerCount; l++)
                        if (body.GetCurrentAnimatorStateInfo(l).IsName("Heal") || body.GetNextAnimatorStateInfo(l).IsName("Heal")) healing = true;
                    closest = Mathf.Min(closest, Vector3.Distance(leftHand.position, rightHand.position));
                    yield return null;
                }
                Check(healing, "using a bandage plays the Heal animation");
                Check(closest < apart - 0.15f, $"the hands come together to wrap it ({apart:F2} m apart at rest, {closest:F2} m at closest)");
                bandaged.ServerHealFull();
                yield return new WaitForSeconds(1f);
            }

            inventory.ServerToggleTorch();
            yield return new WaitForSeconds(0.3f);
            Check(inventory.TorchLit && inventory.Torches == config.StartTorches && inventory.TorchSecondsLeft > 0, "torch lights and uses up one torch");
            inventory.ServerToggleTorch();
            yield return null;
            Check(!inventory.TorchLit, "torch can be put out");

            health.ServerDamage(60);
            yield return null;
            int hurt = health.Health;
            Check(inventory.ServerUseSalve(), "salve can be used when hurt");
            yield return null;
            Check(health.Health > hurt, $"salve healed ({hurt} to {health.Health})");
            health.ServerHealFull();
            inventory.ServerReset();
            wood.ServerTakeAll();
            yield return null;
        }

        // ---------- 1. Chop, wood, repair ----------

        private static IEnumerator ChopAndRepair()
        {
            PlayerWood wood = PlayerWood.Local;
            Hut hut = Hut.Instance;
            TreeDatabase db = TreeManager.Instance.Database;
            GameConfig config = TreeManager.Instance.Config;
            int startHealth = hut.Health;
            Check(startHealth == config.HutStartHealth, $"hut starts at {startHealth} (expected {config.HutStartHealth})");

            int expectedWood = 0;
            foreach (TreeSize size in new[] { TreeSize.Small, TreeSize.Medium, TreeSize.Large })
            {
                int id = FindTree(db, size);
                TreeEntry tree = db.Get(id);
                int hitsNeeded = config.GetHitsToFell(size);
                Vector3 aim = Quaternion.Euler(0f, 120f * (int)size + 30f, 0f) * Vector3.forward;
                Vector3 origin = tree.Position - aim * ChopAimOffset;

                for (int hit = 1; hit <= hitsNeeded; hit++)
                {
                    TreeManager.Instance.ServerChop(wood, origin, aim);
                    yield return null;
                    if (hit < hitsNeeded) Check(wood.Carried == expectedWood, $"{size} tree {id} hit {hit}/{hitsNeeded} leaves wood at {expectedWood}");
                }
                expectedWood += config.GetWoodForSize(size);
                Check(wood.Carried == expectedWood, $"{size} tree felled in {hitsNeeded} hits, wood {wood.Carried} (expected {expectedWood})");
                TreeManager.Instance.ServerChop(wood, origin, aim);
            }

            yield return new WaitForSeconds(SettleSeconds);
            Check(wood.Carried == expectedWood, "extra swing at felled tree gives nothing");
            Check(TreeManager.Instance.StumpCount == 3, $"client shows {TreeManager.Instance.StumpCount} stumps (expected 3)");

            hut.ServerDeposit(wood);
            yield return new WaitForSeconds(SettleSeconds);
            Check(wood.Carried == 0 && hut.WoodStore == expectedWood, $"deposit moved {expectedWood} wood into the store ({hut.WoodStore})");
            Check(hut.Health == startHealth, "depositing alone does not repair");

            // Repair is its own action (R key): it pays from the store and restores the hut.
            hut.ServerDamage(150);
            int damaged = hut.Health;
            int restored = hut.ServerRepair(wood, out int spent);
            yield return new WaitForSeconds(SettleSeconds);
            int expectedRestore = Mathf.Min(150, expectedWood * config.HutHealthPerWood);
            Check(restored == expectedRestore && hut.Health == damaged + expectedRestore,
                $"repair restored {restored} health (expected {expectedRestore}), hut at {hut.Health}");
            Check(spent == Mathf.CeilToInt(expectedRestore / (float)config.HutHealthPerWood) && hut.WoodStore == expectedWood - spent,
                $"repair spent {spent} wood from the store ({hut.WoodStore} left)");
            ResetHut(hut);

            ClearTreants();
            yield return null;
        }

        // ---------- 2. Saplings, night, walking, kills and buffs ----------

        private static IEnumerator SaplingsAndNight()
        {
            EnemyDirector dir = EnemyDirector.Instance;
            DayNightCycle cycle = DayNightCycle.Instance;
            PlayerWood wood = PlayerWood.Local;
            PlayerBuffs buffs = wood.GetComponent<PlayerBuffs>();
            Hut hut = Hut.Instance;
            GameConfig config = TreeManager.Instance.Config;

            Check(cycle.Phase == DayPhase.Day, "starts in the day");

            // Drops by day: hub trees plant nothing (staying home keeps the night quiet); forest trees do.
            for (int i = 0; i < 60; i++) dir.ServerOnTreeHit(new Vector3(0f, 0f, -30f), Element.Neutral, TreeSize.Large);
            yield return null;
            Check(Treant.All.Count == 0, $"60 large hub trees planted no saplings ({Treant.All.Count})");
            ClearTreants();
            yield return null;
            dir.ServerOnTreeHit(new Vector3(0f, 0f, -80f), Element.Fire, TreeSize.Small);
            yield return null;
            Check(Treant.All.Count >= 1 && Treant.All.Count <= 3, $"one small forest tree planted {Treant.All.Count} saplings (1 to 2 expected at one player)");
            ClearTreants();
            yield return null;
            Treant sapling = dir.ServerSpawn(Element.Neutral, TreeSize.Small, false, new Vector3(0f, 0f, -40f));
            yield return null;
            sapling.ServerHit(999f, buffs, wood);
            yield return null;
            Check(sapling != null && sapling.IsSpawned, "a sapling cannot be destroyed with the axe");
            Treant found = Treant.ServerFindTarget(new Vector3(0f, 0f, -41f), Vector3.forward, config.SwingRange, config.SwingRadius, out _);
            Check(found == null, "the axe passes through saplings");
            ClearTreants();
            yield return null;

            for (int i = 0; i < 80; i++) dir.ServerOnTreeHit(new Vector3(0f, 0f, -80f), Element.Fire, TreeSize.Medium);
            yield return null;
            int fire = 0;
            foreach (Treant t in Treant.All) if (t.Element == Element.Fire) fire++;
            Check(fire > 0, $"Fire forest drops produced {fire} Fire saplings out of {Treant.All.Count}");
            ClearTreants();
            yield return null;

            // Night: three Fire saplings and a Wind one near the hut wake up and walk on the NavMesh.
            var fires = new Treant[3];
            for (int i = 0; i < 3; i++) fires[i] = dir.ServerSpawn(Element.Fire, TreeSize.Small, false, new Vector3(-6f + i * 6f, 0f, -14f));
            Treant wind = dir.ServerSpawn(Element.Wind, TreeSize.Small, false, new Vector3(0f, 0f, -18f));
            yield return null;
            int hutBefore = hut.Health;

            cycle.ServerSkipPhase();
            cycle.ServerSkipPhase();
            Check(cycle.Phase == DayPhase.Night, "two skips reach night");

            float until = Time.time + 6f;
            while (Time.time < until && EnemyDirector.CountActive() < 4) yield return null;
            Check(EnemyDirector.CountActive() >= 4, $"{EnemyDirector.CountActive()} enemies awake at night (expected 4)");
            Check(fires[0] != null && fires[0].Agent.isOnNavMesh, "awake enemy stands on the NavMesh");

            until = Time.time + EnemyWalkTimeout;
            while (Time.time < until && hut.Health >= hutBefore) yield return null;
            Check(hut.Health < hutBefore, $"with no palisade, enemies reach the house and hit its walls ({hutBefore} to {hut.Health})");

            // One Fire kill loads the axe with Fire for 15 hits; it boosts hits on Wind, not on Water.
            fires[0].ServerHit(1f, buffs, wood);
            yield return new WaitForSeconds(SettleSeconds);
            Check(buffs.Charges(Element.Fire) == config.ChargesPerKill, $"one Fire kill loads {buffs.Charges(Element.Fire)} charges (expected {config.ChargesPerKill})");
            Check(Mathf.Abs(buffs.DamageMultiplier(Element.Wind) - 2.25f) < 0.001f, "Fire charge gives +50% enchant and +50% against Wind");
            Check(Mathf.Abs(buffs.DamageMultiplier(Element.Water) - 1.5f) < 0.001f, "Fire charge still enchants the axe against Water (+50%)");

            // Each hit spends a charge; after 15 the axe is empty and the bonus is gone.
            for (int i = 0; i < config.ChargesPerKill; i++) buffs.ServerConsumeHit(Element.Wind);
            yield return null;
            Check(buffs.Charges(Element.Fire) == 0, "15 hits use up the Fire charge");
            Check(Mathf.Abs(buffs.DamageMultiplier(Element.Wind) - 1f) < 0.001f, "empty axe has no bonus");

            // A Wind kill loads Wind, and the leftover kills still work.
            wind.ServerHit(1f, buffs, wood);
            fires[1].ServerHit(1f, buffs, wood);
            yield return new WaitForSeconds(SettleSeconds);
            Check(buffs.Charges(Element.Wind) == config.ChargesPerKill && buffs.Charges(Element.Fire) == config.ChargesPerKill, "Wind and Fire can be loaded together");
            Check(Mathf.Abs(buffs.DamageMultiplier(Element.Earth) - 2.25f) < 0.001f, "Wind charge gives enchant and counter bonus against Earth");
            Check(Mathf.Abs(buffs.DamageMultiplier(Element.Wind) - 2.25f) < 0.001f, "Fire charge still counters Wind");
        }

        // ---------- 2b. Enemies go for the player before buildings or the hut ----------

        private static IEnumerator EnemiesGoForThePlayerFirst()
        {
            EnemyDirector dir = EnemyDirector.Instance;
            PlayerWood wood = PlayerWood.Local;
            PlayerHealth health = wood.GetComponent<PlayerHealth>();
            Hut hut = Hut.Instance;
            GameConfig config = TreeManager.Instance.Config;

            health.ServerHealFull();
            ClearTreants();
            ResetHut(hut);
            yield return null;

            // The player stands out in the meadow; an awake enemy starts well inside its aggro range.
            Vector3 playerSpot = new Vector3(0f, 0f, -45f);
            TeleportPlayer(wood, playerSpot);
            yield return new WaitForSeconds(0.5f);
            int hutBefore = hut.Health;
            Treant enemy = dir.ServerSpawn(Element.Neutral, TreeSize.Small, false, new Vector3(0f, 0f, -75f));
            yield return null;
            Check(enemy.ServerActivate(), "an enemy wakes up on the NavMesh");

            float until = Time.time + 25f;
            while (Time.time < until && health.Health >= config.PlayerMaxHealth) yield return null;
            Check(health.Health < config.PlayerMaxHealth, $"the enemy attacked the player first (health {health.Health})");
            Check(hut.Health == hutBefore, "no hut damage before the player was hit");

            ClearTreants();
            TeleportPlayer(wood, new Vector3(0f, 0f, 7.5f));
            health.ServerHealFull();
            yield return new WaitForSeconds(0.5f);
        }

        // Moves the local player (client authoritative), telling the server movement check first.
        private static void TeleportPlayer(PlayerWood wood, Vector3 position)
        {
            bool hasGround = Chopper.World.WorldGround.Exists;
            position.y = Chopper.World.WorldGround.HeightAt(position) + 0.3f;
            wood.GetComponent<PlayerMovementValidator>().ServerNotifyTeleport(position);
            var controller = wood.GetComponent<CharacterController>();
            controller.enabled = false;
            wood.transform.position = position;
            controller.enabled = true;
            var networkTransform = wood.GetComponent<FishNet.Component.Transforming.NetworkTransform>();
            if (networkTransform != null) networkTransform.Teleport();
        }

        // ---------- 3. Dawn, boss ----------

        private static IEnumerator DawnAndBoss()
        {
            EnemyDirector dir = EnemyDirector.Instance;
            DayNightCycle cycle = DayNightCycle.Instance;
            PlayerWood wood = PlayerWood.Local;
            PlayerBuffs buffs = wood.GetComponent<PlayerBuffs>();
            Hut hut = Hut.Instance;
            GameConfig config = TreeManager.Instance.Config;

            // Leftover enemies wither at dawn, buffs reset, felled trees regrow.
            dir.ServerSpawn(Element.Earth, TreeSize.Medium, false, new Vector3(10f, 0f, -14f)).ServerActivate();
            yield return null;
            cycle.ServerSkipPhase();
            Check(cycle.Phase == DayPhase.Dawn, "night ends in dawn");
            yield return new WaitForSeconds(SettleSeconds);
            Check(Treant.All.Count == 0, $"no treants left after dawn ({Treant.All.Count})");
            Check(!buffs.IsActive(Element.Fire) && !buffs.IsActive(Element.Wind), "axe charges reset at dawn");
            Check(TreeManager.Instance.StumpCount == 0, $"trees regrown at dawn ({TreeManager.Instance.StumpCount} stumps left)");
            cycle.ServerSkipPhase();
            Check(cycle.Phase == DayPhase.Day && cycle.Day == 2, "next day begins");
            // Day 2: a whole grove felled is remembered, but no warden rises before the first boss day.
            for (int i = 0; i < config.GroveTreesFelled + 5; i++) dir.ServerOnTreeFelled(new Vector3(20f + i % 4, 0f, -14f + i % 3), Element.Fire);
            Check(dir.Warden == null, "no grove warden rises before the first boss day");
            dir.ServerLoadGroves(null);

            // A young boss sapling shrugs off the axe; a grown one can be chopped down.
            Treant young = dir.ServerSpawn(Element.Fire, TreeSize.Large, true, new Vector3(20f, 0f, -14f));
            yield return null;
            for (int i = 0; i < 40; i++) young.ServerHit(1f, buffs, wood);
            Check(young != null && !young.IsGrown, "young boss sapling survives 40 hits");
            young.ServerDespawn();
            yield return null;

            Treant grown = dir.ServerSpawn(Element.Fire, TreeSize.Large, true, new Vector3(20f, 0f, -14f), bornOffsetDays: config.BossGrowDays + 1f);
            yield return null;
            Check(grown.IsGrown, "boss sapling older than the grow time is grown");
            for (int i = 0; i < config.BossChopHits - 1; i++) grown.ServerHit(1f, buffs, wood);
            Check(grown != null && grown.IsSpawned, $"grown boss tree still stands after {config.BossChopHits - 1} hits");
            grown.ServerHit(1f, buffs, wood);
            yield return new WaitForSeconds(SettleSeconds);
            Check(Treant.All.Count == 0, "felling the boss tree removes it");

            // Nightfall: the queued boss rises, awake, and dies to the axe for a reward.
            cycle.ServerSkipPhase();
            cycle.ServerSkipPhase();
            float until = Time.time + 3f;
            Treant boss = null;
            while (Time.time < until && boss == null)
            {
                foreach (Treant t in Treant.All) if (t.IsBoss && t.IsActive) boss = t;
                yield return null;
            }
            Check(boss != null, "boss rises at nightfall");
            if (boss != null)
            {
                int storeBefore = hut.WoodStore;
                boss.ServerHit(config.BossHealth + 1f, buffs, wood);
                yield return new WaitForSeconds(SettleSeconds);
                Check(hut.WoodStore == storeBefore + config.BossRewardWood, $"boss reward added to the store ({hut.WoodStore})");
                PlayerBoons boons = wood.GetComponent<PlayerBoons>();
                Check(boons != null && boons.Packed != 0, "the boss drops a permanent boon for the player");
                Check(dir.BossesDefeated == 1, "one boss counted as defeated");
            }
            ClearTreants();
            yield return null;
        }

        // ---------- 3b. The grove warden (zone boss) ----------

        private static IEnumerator GroveWardenWorks()
        {
            EnemyDirector dir = EnemyDirector.Instance;
            DayNightCycle cycle = DayNightCycle.Instance;
            PlayerWood wood = PlayerWood.Local;
            PlayerBuffs buffs = wood.GetComponent<PlayerBuffs>();
            PlayerCombat combat = wood.GetComponent<PlayerCombat>();
            Hut hut = Hut.Instance;
            GameConfig config = TreeManager.Instance.Config;
            ClearTreants();
            for (int i = 0; i < 8 && cycle.Phase != DayPhase.Day; i++) cycle.ServerSkipPhase();
            yield return null;
            Check(cycle.Phase == DayPhase.Day && cycle.Day >= config.BossFirstDay, $"a boss day has come (day {cycle.Day})");
            if (combat != null) combat.ServerSetGod(true);
            dir.ServerLoadGroves(null);

            // Trees felled one short of a grove: nothing. The next one in the same grove wakes its warden.
            int required = BossRules.RequiredTrees(config.GroveTreesFelled, config.BossTreesFelledGrowth, dir.BossesDefeated);
            Vector3 grove = new Vector3(20f, 0f, -14f);
            for (int i = 0; i < required - 1; i++) dir.ServerOnTreeFelled(grove + new Vector3(i % 4 - 1.5f, 0f, i % 3 - 1f), Element.Fire);
            dir.ServerOnTreeFelled(new Vector3(60f, 0f, 60f), Element.Earth);
            Check(dir.Warden == null, $"no warden one tree short of a grove ({required - 1} of {required})");
            dir.ServerOnTreeFelled(grove, Element.Fire);
            Treant warden = dir.Warden;
            Check(warden != null && warden.IsWarden && warden.IsActive && warden.WardenPhase == 1, "the grove's warden rises among the stumps");
            if (warden == null)
            {
                if (combat != null) combat.ServerSetGod(false);
                yield break;
            }
            Vector3 flat = warden.Home - grove;
            flat.y = 0f;
            Check(flat.magnitude < 3f, $"the warden rises in the middle of the grove ({flat.magnitude:F1} m off)");
            Check(dir.GroveFelled.Count == 1, $"the grove's trees are spent, the other forest's tree still counts ({dir.GroveFelled.Count})");
            yield return new WaitForSeconds(0.5f);

            // Phase 2: guards come and their bark shield takes most of each blow.
            int players = Mathf.Max(1, PlayerHealth.All.Count);
            float max = ZoneBossRules.Health(config.WardenHealth, config.WardenHealthPerExtraPlayer, players);
            warden.ServerHit(max * 0.4f, buffs, wood);
            yield return null;
            Check(warden.WardenPhase == 2, $"the warden enters phase 2 below two thirds (phase {warden.WardenPhase})");
            Check(dir.WardenGuardsAlive > 0 && warden.IsShielded, $"phase 2 calls guards and shields the warden ({dir.WardenGuardsAlive} guards)");
            float before = warden.HealthFraction;
            warden.ServerHit(4f, buffs, wood);
            float lost = (before - warden.HealthFraction) * max;
            Check(Mathf.Abs(lost - 4f * ZoneBossRules.ShieldedDamage) < 0.01f, $"the shield takes most of a blow ({lost:F2} of 4 got through)");
            foreach (Treant t in new System.Collections.Generic.List<Treant>(Treant.All))
            {
                if (t.HasLeash && !t.IsWarden) t.ServerHit(100f, buffs, wood);
            }
            yield return new WaitForSeconds(SettleSeconds);
            Check(dir.WardenGuardsAlive == 0 && !warden.IsShielded, "cutting down the guards breaks the shield");

            // The warden holds its grove through the dawn.
            for (int i = 0; i < 3; i++) cycle.ServerSkipPhase();
            yield return new WaitForSeconds(SettleSeconds);
            Check(cycle.Phase == DayPhase.Dawn && dir.Warden == warden, "the warden does not wither at dawn");
            cycle.ServerSkipPhase();

            // Phase 3: enraged, more guards. Leaving the grove resets it.
            warden.ServerHit((warden.HealthFraction - 0.25f) * max, buffs, wood);
            yield return null;
            Check(warden.WardenPhase == 3, $"the warden is enraged below a third (phase {warden.WardenPhase})");
            Check(dir.WardenGuardsAlive > 0, "the enraged warden calls guards again");
            dir.ServerResetWarden(warden);
            yield return new WaitForSeconds(SettleSeconds);
            Check(warden.WardenPhase == 1 && warden.HealthFraction >= 0.999f && dir.WardenGuardsAlive == 0, "a reset warden heals and sheds its guards");

            // The kill: a boss beaten, wood in the hut, a boon for everyone, and nothing left of the fight.
            int beaten = dir.BossesDefeated;
            int store = hut.WoodStore;
            warden.ServerHit(max * 2f, buffs, wood);
            yield return new WaitForSeconds(SettleSeconds);
            Check(dir.Warden == null && dir.BossesDefeated == beaten + 1, "felling the warden counts as a boss beaten");
            Check(hut.WoodStore == store + config.WardenRewardWood, $"the warden's wood reaches the hut ({hut.WoodStore - store})");
            bool leftovers = false;
            foreach (Treant t in Treant.All) if (t.HasLeash) leftovers = true;
            Check(!leftovers, "the warden's guards go with it");

            if (combat != null) combat.ServerSetGod(false);
            ClearTreants();
            yield return null;
        }

        // ---------- 4. Upgrades, game over ----------

        private static IEnumerator UpgradesAndGameOver()
        {
            PlayerWood wood = PlayerWood.Local;
            PlayerUpgrades upgrades = wood.GetComponent<PlayerUpgrades>();
            PlayerHealth health = wood.GetComponent<PlayerHealth>();
            Hut hut = Hut.Instance;
            GameConfig config = TreeManager.Instance.Config;

            hut.ServerAddStoreWood(1000);
            Check(upgrades.ServerBuy(PlayerUpgrades.KindAxe), "buy axe level 1");
            Check(upgrades.ServerBuy(PlayerUpgrades.KindArmor), "buy armor level 1");
            Check(hut.ServerTryUpgrade(), "buy hut level 1");
            yield return null;
            Check(upgrades.AxeLevel == 1 && upgrades.ArmorLevel == 1 && hut.Level == 1, "levels went up to 1");
            // The boss test earlier grants a random permanent boon, which may add to either value.
            PlayerBoons boonsNow = wood.GetComponent<PlayerBoons>();
            float axeBoon = boonsNow != null ? boonsNow.AxeDamageBonus : 0f;
            float armorBoon = boonsNow != null ? boonsNow.ArmorBonus : 0f;
            Check(Mathf.Abs(upgrades.AxeDamageMultiplier - config.AxeDamageMultipliers[1] * (1f + axeBoon)) < 0.001f, "axe level 1 damage multiplier applies");
            Check(upgrades.ArmorReduction >= config.ArmorDamageReduction[1] - 0.001f
                  && upgrades.ArmorReduction <= config.ArmorDamageReduction[1] + armorBoon + 0.001f, "armor level 1 reduction applies");
            Check(hut.MaxHealth == config.HutMaxHealth + config.HutHealthPerLevel, $"hut max health {hut.MaxHealth} after level 1");

            for (int i = 0; i < 5; i++) { upgrades.ServerBuy(PlayerUpgrades.KindAxe); upgrades.ServerBuy(PlayerUpgrades.KindArmor); hut.ServerTryUpgrade(); }
            yield return null;
            Check(upgrades.AxeLevel == 3 && upgrades.ArmorLevel == 3 && hut.Level == 3, "levels stop at the maximum of 3");

            int before = health.Health;
            health.ServerDamage(50);
            yield return null;
            int lost = before - health.Health;
            // The boss boon earlier in the run is random and can be armor, so the reduction includes it.
            int expectedLoss = Mathf.Max(1, Mathf.CeilToInt(50 * (1f - upgrades.ArmorReduction)));
            Check(lost == expectedLoss, $"armor level 3 cuts 50 damage to {lost} (expected {expectedLoss})");
            health.ServerHealFull();

            hut.ServerDamage(1000000);
            yield return new WaitForSeconds(SettleSeconds);
            Check(hut.Health == config.HutStartHealth && hut.Level == 0, $"game over resets the hut to {hut.Health}, level {hut.Level}");
            Check(DayNightCycle.Instance.Day == 1 && DayNightCycle.Instance.Phase == DayPhase.Day, "game over restarts at day one");
            Check(upgrades.AxeLevel == 0 && upgrades.ArmorLevel == 0, "game over resets upgrades");
            Check(!hut.IsRepaired, "game over leaves the hut broken again");

            yield return WallsAndLives(health, config);
            yield return DevToolsWork();
        }

        // ---------- 0d. Healing herbs ----------

        // A dropped item can be taken a moment after it lands (GroundItem).
        private const float PickupWait = 0.9f;

        private static IEnumerator HerbsWork()
        {
            PlayerWood wood = PlayerWood.Local;
            PlayerInventory inventory = wood.GetComponent<PlayerInventory>();
            HerbManager herbs = HerbManager.Instance;
            Check(herbs != null && herbs.Count > 50, $"healing herbs grow in the world ({(herbs != null ? herbs.Count : 0)})");
            if (herbs == null || herbs.Count == 0) yield break;

            inventory.ServerReset();
            Vector3 spot = herbs.PositionOf(0);
            TeleportPlayer(wood, spot + new Vector3(0.5f, 0f, 0f));
            yield return new WaitForSeconds(0.4f);
            int itemsBefore = GroundItem.All.Count;
            wood.ServerPickHerb();
            yield return null;
            Check(GroundItem.All.Count == itemsBefore + 1 && inventory.Herbs == 0, "a press picks the herb and it drops on the ground");
            yield return new WaitForSeconds(PickupWait);
            Check(inventory.Herbs == 1 && GroundItem.All.Count == itemsBefore, "walking over the dropped herb takes it");
            wood.ServerPickHerb();
            yield return new WaitForSeconds(PickupWait);
            Check(inventory.Herbs == 1, "a picked herb is gone");
            herbs.ServerRegrowAll();
            wood.ServerPickHerb();
            yield return new WaitForSeconds(PickupWait);
            Check(inventory.Herbs == 2, "herbs grow back");

            int bush = herbs.FirstFabricBush;
            Check(bush >= 0 && herbs.Count - bush > 50, $"ferns grow in the world ({(bush >= 0 ? herbs.Count - bush : 0)})");
            if (bush >= 0)
            {
                TeleportPlayer(wood, herbs.PositionOf(bush) + new Vector3(0.5f, 0f, 0f));
                yield return new WaitForSeconds(0.4f);
                wood.ServerPickHerb();
                yield return new WaitForSeconds(PickupWait);
                Check(inventory.Fabric == 1 && inventory.Herbs == 2, $"picking a fern gives fiber ({inventory.Fabric} fiber, {inventory.Herbs} herbs)");
            }
            inventory.ServerReset();
            TeleportPlayer(wood, new Vector3(0f, 0f, 7.5f));
            yield return new WaitForSeconds(0.3f);
        }

        // ---------- 0e. Fallen branches ----------

        private static IEnumerator BranchesWork()
        {
            PlayerWood wood = PlayerWood.Local;
            BranchManager branches = BranchManager.Instance;
            Check(branches != null && branches.BranchCount > 100, $"fallen branches lie in the world ({(branches != null ? branches.BranchCount : 0)})");
            if (branches == null || branches.BranchCount == 0) yield break;

            int nearYard = 0;
            for (int i = 0; i < branches.BranchCount; i++)
            {
                Vector3 p = branches.PositionOf(i);
                if (p.z > 10f && new Vector2(p.x, p.z).magnitude < 45f) nearYard++;
            }
            Check(nearYard >= 5, $"a few branches lie just outside the yard ({nearYard})");

            wood.ServerTakeAll();
            TeleportPlayer(wood, branches.PositionOf(0) + new Vector3(0.4f, 0f, 0f));
            yield return new WaitForSeconds(0.4f);
            // With full arms the branch still comes loose, but its wood waits on the ground until there is room.
            wood.ServerAddWood(wood.Cap);
            int itemsBefore = GroundItem.All.Count;
            wood.ServerPickBranch();
            yield return new WaitForSeconds(PickupWait);
            Check(branches.IsPicked(0), "a picked branch is gone");
            Check(GroundItem.All.Count == itemsBefore + 1, "with full arms the branch's wood stays on the ground");
            wood.ServerTakeAll();
            yield return new WaitForSeconds(PickupWait);
            Check(wood.Carried == BranchManager.WoodPerBranch && GroundItem.All.Count == itemsBefore, "with room again, walking over it takes the wood");
            branches.ServerRegrowAll();
            Check(!branches.IsPicked(0), "branches come back at dawn");
            wood.ServerTakeAll();
            TeleportPlayer(wood, new Vector3(0f, 0f, 7.5f));
            yield return new WaitForSeconds(0.3f);
        }

        // ---------- 0f. Falling trees and dying ----------

        private static IEnumerator FallingTreesAndDying()
        {
            PlayerWood wood = PlayerWood.Local;
            PlayerHealth health = wood.GetComponent<PlayerHealth>();
            TreeManager trees = TreeManager.Instance;
            TreeDatabase db = trees.Database;
            health.ServerHealFull();

            // Stand where a large tree felled northward would land, then where a medium one would.
            TreeEntry large = db.Get(FindTree(db, TreeSize.Large));
            Vector3 along = FallingTree.GroundDirection(Vector3.forward);
            TeleportPlayer(wood, large.Position + along * 6f);
            yield return new WaitForSeconds(0.4f);
            Check(trees.ServerPlayersUnderFall(large, Vector3.forward).Contains(health), "a player where a large tree lands is under it");
            TeleportPlayer(wood, large.Position - along * 6f);
            yield return new WaitForSeconds(0.3f);
            Check(!trees.ServerPlayersUnderFall(large, Vector3.forward).Contains(health), "a player behind the tree is safe");
            TreeEntry medium = db.Get(FindTree(db, TreeSize.Medium));
            Check(medium.Size != TreeSize.Large, "only large trees crush (medium trees never call the crush)");

            // Without Sturdy gear one large tree puts the player down; the wood they carried is left in a pile.
            wood.ServerTakeAll();
            wood.ServerAddWood(7);
            Vector3 fellAt = wood.transform.position;
            health.ServerCrush();
            yield return new WaitForSeconds(0.4f);
            Check(health.IsDown, "a falling tree kills a player without Sturdy gear");
            Check(health.LastCause == PlayerHealth.DeathCause.FallingTree, "the death screen knows it was a tree");
            Check(wood.Carried == 0 && health.DroppedWood, "the carried wood is dropped on death");
            WoodPile pile = WoodPile.Nearest(fellAt);
            Check(pile != null && pile.Amount == 7, $"the wood lies in a pile where the player fell ({(pile != null ? pile.Amount : 0)})");

            health.ServerRespawnNow();
            yield return new WaitForSeconds(0.5f);
            TeleportPlayer(wood, fellAt + new Vector3(0.8f, 0f, 0f));
            yield return new WaitForSeconds(0.4f);
            wood.ServerTakePile();
            yield return new WaitForSeconds(0.3f);
            Check(wood.Carried == 7, "walking back and pressing interact takes the wood back");
            Check(WoodPile.Nearest(fellAt) == null, "the empty pile is gone");
            // W9-01: the axe and the outfit were left there too, and walking back picks them up again.
            Check(wood.GetComponent<PlayerAxe>().HasAxe && wood.GetComponent<PlayerUpgrades>().Dressed,
                "walking back picks the dropped axe and outfit up again");
            wood.ServerTakeAll();

            // Sturdy gear from crypt iron lets the player live through one.
            PlayerUpgrades upgrades = wood.GetComponent<PlayerUpgrades>();
            PlayerInventory inventory = wood.GetComponent<PlayerInventory>();
            health.ServerHealFull();
            Check(!upgrades.ServerBuy(PlayerUpgrades.KindSturdy), "Sturdy gear needs crypt iron");
            inventory.ServerAddRelics(4, 0);
            Check(upgrades.ServerBuy(PlayerUpgrades.KindSturdy) && upgrades.SturdyLevel == 1 && inventory.Iron == 0, "crypt iron buys Sturdy gear");
            health.ServerCrush();
            Check(!health.IsDown && health.Health == health.MaxHealth - CrushRules.Damage(health.MaxHealth, 1),
                $"with Sturdy gear a falling tree hurts but does not kill ({health.Health})");
            upgrades.ServerReset();
            health.ServerHealFull();
            TeleportPlayer(wood, new Vector3(0f, 0f, 7.5f));
            yield return new WaitForSeconds(0.3f);
        }

        // ---------- 0g. Crypts ----------

        private static void TeleportExact(PlayerWood wood, Vector3 position)
        {
            wood.GetComponent<PlayerMovementValidator>().ServerNotifyTeleport(position);
            var controller = wood.GetComponent<CharacterController>();
            controller.enabled = false;
            wood.transform.position = position;
            controller.enabled = true;
            var networkTransform = wood.GetComponent<FishNet.Component.Transforming.NetworkTransform>();
            if (networkTransform != null) networkTransform.Teleport();
        }

        private static IEnumerator CryptsWork()
        {
            PlayerWood wood = PlayerWood.Local;
            PlayerHealth health = wood.GetComponent<PlayerHealth>();
            PlayerInventory inventory = wood.GetComponent<PlayerInventory>();
            PlayerCrypt traveller = wood.GetComponent<PlayerCrypt>();
            CryptManager crypts = CryptManager.Instance;
            Check(crypts != null && traveller != null, "the crypt manager and the player's crypt actions exist");
            Check(CryptWorld.Plans.Count == WorldGen.CryptCount, $"the world has {WorldGen.CryptCount} crypts ({CryptWorld.Plans.Count})");
            if (crypts == null || traveller == null || CryptWorld.Plans.Count == 0) yield break;

            CryptPlan plan = CryptWorld.Plans[0];
            TeleportExact(wood, plan.DoorSpot + Vector3.up * 0.2f);
            yield return new WaitForSeconds(0.4f);
            Check(CryptWorld.DoorInReach(wood.transform.position) == 0, "standing at a crypt door puts it in reach");
            Check(crypts.ServerTryEnter(traveller), "the door can be used");
            yield return new WaitForSeconds(0.6f);
            Check(CryptWorld.IsUnderground(wood.transform.position), $"the door takes the player down into the crypt ({wood.transform.position.y:F0})");
            Check(CryptManager.CountGuardians(0) == plan.Guardians.Count,
                $"every guardian woke up on the crypt's NavMesh ({CryptManager.CountGuardians(0)} of {plan.Guardians.Count})");
            Check(EnemyDirector.CountActive() == 0, "guardians do not count toward the night's enemy cap");

            // Every chest of this crypt gives its loot once.
            int iron = inventory.Iron;
            int shards = inventory.Shards;
            int expectedIron = iron;
            int expectedShards = shards;
            bool allOpened = true;
            bool onlyOnce = true;
            for (int id = 0; id < CryptWorld.Chests.Count; id++)
            {
                CryptWorld.ChestRef chest = CryptWorld.Chests[id];
                if (chest.Crypt != 0) continue;
                Vector3 front = chest.Chest.Position + Quaternion.Euler(0f, chest.Chest.Yaw, 0f) * Vector3.forward * 1.3f + Vector3.up * 0.2f;
                TeleportExact(wood, front);
                yield return new WaitForSeconds(0.3f);
                health.ServerHealFull();
                if (!crypts.ServerTryOpenChest(traveller)) allOpened = false;
                if (crypts.ServerTryOpenChest(traveller)) onlyOnce = false;
                expectedIron += chest.Chest.Iron;
                expectedShards += chest.Chest.Shards;
            }
            Check(allOpened, "every chest in the crypt opens");
            Check(onlyOnce, "a chest is looted only once per player");
            Check(inventory.Iron == expectedIron && inventory.Shards == expectedShards && expectedIron > iron,
                $"the chests hold crypt iron and rune shards ({inventory.Iron} iron, {inventory.Shards} shards)");

            TeleportExact(wood, plan.ExitSpot + Vector3.up * 0.2f);
            yield return new WaitForSeconds(0.3f);
            Check(crypts.ServerTryExit(traveller), "the stairs lead back up");
            yield return new WaitForSeconds(0.6f);
            Check(!CryptWorld.IsUnderground(wood.transform.position) && Vector3.Distance(wood.transform.position, plan.ReturnSpot) < 2f,
                "climbing out puts the player in front of the entrance");

            // Leave the rest of the run as it was: no guardians, no crypt finds, full health.
            foreach (Treant t in new System.Collections.Generic.List<Treant>(Treant.All)) if (t.IsGuardian) t.ServerDespawn();
            inventory.ServerAddRelics(-1000, -1000);
            health.ServerHealFull();
            TeleportPlayer(wood, new Vector3(0f, 0f, 7.5f));
            yield return new WaitForSeconds(0.3f);
        }

        // ---------- 0i. Crypt arena ----------

        private static System.Collections.Generic.List<Treant> ArenaEnemies(int crypt)
        {
            var list = new System.Collections.Generic.List<Treant>();
            foreach (Treant t in Treant.All) if (t != null && t.Arena == crypt && t.IsActive) list.Add(t);
            return list;
        }

        private static IEnumerator ArenaWorks()
        {
            PlayerWood wood = PlayerWood.Local;
            PlayerHealth health = wood.GetComponent<PlayerHealth>();
            PlayerInventory inventory = wood.GetComponent<PlayerInventory>();
            PlayerUpgrades upgrades = wood.GetComponent<PlayerUpgrades>();
            PlayerCrypt traveller = wood.GetComponent<PlayerCrypt>();
            ArenaManager arenas = ArenaManager.Instance;
            GameConfig gameConfig = TreeManager.Instance.Config;
            Check(arenas != null, "the arena manager exists");
            if (arenas == null || CryptWorld.Plans.Count == 0) yield break;
            CryptPlan plan = CryptWorld.Plans[0];
            Element element = plan.Site.Zone;

            TeleportExact(wood, plan.ArenaDoorSpot + Vector3.up * 0.2f);
            yield return new WaitForSeconds(0.4f);
            Check(CryptWorld.ArenaDoorInReach(wood.transform.position) == 0, "the vault has an archway down to the arena");
            Check(arenas.ServerTryEnter(traveller), "the archway can be used");
            yield return new WaitForSeconds(0.6f);
            Check(CryptWorld.ArenaAt(wood.transform.position) == 0 && arenas.IsFighting(0), "the archway leads into the arena and the fight starts");

            // Three waves, each cleared, then the boss.
            int waves = 0;
            float deadline = Time.time + 60f;
            while (Time.time < deadline && !arenas.BossUp(0))
            {
                health.ServerHealFull();
                var enemies = ArenaEnemies(0);
                if (enemies.Count > 0)
                {
                    if (arenas.WaveOf(0) > waves)
                    {
                        waves = arenas.WaveOf(0);
                        Check(enemies.Count == ArenaManager.WaveSize(plan.Site.Tier, waves, 1), $"wave {waves} brings {enemies.Count} enemies on the arena floor");
                    }
                    foreach (Treant t in enemies) t.ServerHit(1000f, null, wood);
                }
                yield return new WaitForSeconds(0.3f);
            }
            Check(waves == ArenaManager.Waves, $"all {ArenaManager.Waves} waves came ({waves})");
            yield return new WaitForSeconds(0.5f);
            var boss = ArenaEnemies(0);
            Check(arenas.BossUp(0) && boss.Count == 1 && boss[0].IsBoss && boss[0].Element == element, "the crypt's boss rises after the last wave");

            int shards = inventory.Shards;
            foreach (Treant t in boss) t.ServerHit(10000f, null, wood);
            yield return new WaitForSeconds(0.8f);
            Check(arenas.IsCleared(0), "felling the boss clears the arena");
            Check(inventory.Essence(element) == ArenaManager.EssenceReward && inventory.Shards == shards + ArenaManager.ShardReward,
                $"the boss leaves {element} essence and rune shards ({inventory.Essence(element)} essence)");

            // A ward made from the essence takes a share off that element's hits.
            Check(upgrades.ServerBuyWard(element) && upgrades.WardLevel(element) == 1, $"essence forges a {element} ward");
            Treant attacker = EnemyDirector.Instance.ServerSpawn(element, TreeSize.Small, false, wood.transform.position + Vector3.forward * 3f);
            health.ServerHealFull();
            int before = health.Health;
            health.ServerDamage(40, attacker);
            int lost = before - health.Health;
            Check(lost == Mathf.CeilToInt(40 * (1f - upgrades.WardReduction(element))), $"the {element} ward cuts a {element} hit from 40 to {lost}");
            attacker.ServerDespawn();

            // An element stone from essence and shards enchants the axe for good: no charges, still the counter bonus.
            Check(!upgrades.ServerBuyStone(element) || upgrades.HasStone(element), "a stone cannot be forged without enough essence");
            inventory.ServerAddEssence(element, gameConfig.StoneEssenceCost);
            inventory.ServerAddRelics(0, gameConfig.StoneShardCost);
            int essenceBefore = inventory.Essence(element);
            int shardsBefore = inventory.Shards;
            Check(upgrades.ServerBuyStone(element) && upgrades.HasStone(element), $"essence and shards forge a {element} stone");
            Check(inventory.Essence(element) == essenceBefore - gameConfig.StoneEssenceCost && inventory.Shards == shardsBefore - gameConfig.StoneShardCost,
                "the stone costs its essence and shards");
            Check(!upgrades.ServerBuyStone(element), "each stone is forged once");
            PlayerBuffs stoneBuffs = wood.GetComponent<PlayerBuffs>();
            Element countered = Element.Neutral;
            foreach (Element e in EssenceRules.Elements) if (ElementRules.Beats(element, e)) countered = e;
            Check(stoneBuffs.PackedCharges == 0 && stoneBuffs.DamageMultiplier(countered) > stoneBuffs.DamageMultiplier(element),
                $"the {element} stone hits {countered} harder with no charges on the axe");
            upgrades.ServerLoadStones(0);

            TeleportExact(wood, plan.ArenaExitSpot + Vector3.up * 0.2f);
            yield return new WaitForSeconds(0.3f);
            Check(arenas.ServerTryExit(traveller), "the arena archway leads back up");
            yield return new WaitForSeconds(0.6f);
            Check(CryptWorld.ArenaAt(wood.transform.position) < 0 && Vector3.Distance(wood.transform.position, plan.VaultReturnSpot) < 2f, "climbing out puts the player back in the vault");

            upgrades.ServerLoadWards(0);
            inventory.ServerLoadEssence(0);
            inventory.ServerAddRelics(-1000, -1000);
            foreach (Treant t in new System.Collections.Generic.List<Treant>(Treant.All)) if (t.IsGuardian) t.ServerDespawn();
            health.ServerHealFull();
            TeleportPlayer(wood, new Vector3(0f, 0f, 7.5f));
            yield return new WaitForSeconds(0.3f);
        }

        // ---------- 0j. Fishing ----------

        private static IEnumerator FishingWorks()
        {
            PlayerWood wood = PlayerWood.Local;
            PlayerFishing fishing = wood.GetComponent<PlayerFishing>();
            PlayerHealth health = wood.GetComponent<PlayerHealth>();
            Check(fishing != null, "the player can fish");
            WorldGen gen = WorldSession.Gen;
            if (fishing == null || gen == null) yield break;

            Check(fishing.Rod == 1 && fishing.CanFish, "every player has a rod from the start");
            Check(!fishing.TryToggle(), "but it only comes out at the water");
            TeleportPlayer(wood, new Vector3(0f, 0f, 7.5f));
            yield return new WaitForSeconds(0.3f);
            Check(!fishing.TryToggle(), "fishing needs the pond's bank");
            Vector2 bank = gen.PondCenter + Vector2.right * (WorldGen.PondOuterRadius + 1.5f);
            TeleportPlayer(wood, new Vector3(bank.x, 0f, bank.y));
            yield return new WaitForSeconds(0.5f);
            Check(PlayerFishing.AtPond(wood.transform.position), "the hub pond's bank is a fishing spot");
            Check(fishing.TryToggle() && fishing.Busy, "interact on the bank takes the rod out");
            yield return new WaitForSeconds(0.4f);

            var target = new Vector3(gen.PondCenter.x, 0f, gen.PondCenter.y);
            int cast = fishing.ServerCast(target, 1f);
            Check(cast > 0, "a cast lands in the pond");
            Check(!fishing.ServerHook(cast, false), "striking before the bite catches nothing");
            FishKind kind = fishing.ServerPendingKind;
            int xp = fishing.Xp;
            int rodBefore = fishing.RodDurability;
            Check(fishing.ServerHook(cast, true) && fishing.ServerCatch(cast, true), "a hooked fish can be landed");
            Check(fishing.RodDurability == rodBefore - 1, "each catch wears the rod a little");
            Check(fishing.FishCount(kind) == 1, $"the {fishing.SpeciesOf(kind).Name} goes in the bag");
            Check(fishing.Xp == xp + fishing.SpeciesOf(kind).Xp, "the catch gives its fishing experience");
            Check(!fishing.ServerCatch(cast, true) && fishing.FishCount(kind) == 1 && fishing.Xp == xp + fishing.SpeciesOf(kind).Xp,
                "the same fish is not landed (or paid) twice");

            // The Ocarina side: the cast lands near a school, a twitch draws the lure home, a fish takes it, and the
            // weight of the catch is kept as a record.
            int school = fishing.ServerCast(target, 0.6f);
            Check(school > 0 && fishing.ServerSchoolSize >= LureRules.MinSchool, $"a cast lands near a school of fish ({fishing.ServerSchoolSize})");
            Check(!fishing.ServerHook(school, false), "no strike counts before a fish takes the lure");
            yield return new WaitForSeconds(0.7f);
            Vector3 lureBefore = fishing.ServerLure;
            Check(fishing.ServerTwitch(school), "the lure can be twitched");
            Vector3 lureMoved = fishing.ServerLure - lureBefore;
            lureMoved.y = 0f;
            Check(lureMoved.magnitude > 0.5f, $"a twitch moves the lure ({lureMoved.magnitude:F2} m)");
            Check(fishing.ServerBiteNow(school) && fishing.ServerHook(school, false), "a fish takes the lure and the strike hooks it");
            FishKind bitten = fishing.ServerPendingKind;
            float kg = fishing.ServerPendingKg;
            FishSpecies bittenSpecies = fishing.SpeciesOf(bitten);
            Check(kg >= LureRules.MinKg(bittenSpecies) - 0.01f && kg <= LureRules.MaxKg(bittenSpecies) + 0.01f, $"the {bittenSpecies.Name} weighs {kg:F2} kg");
            Check(fishing.ServerCatch(school, true) && fishing.Record(bitten) >= kg - 0.001f, "the catch's weight is kept as a record");

            // A hotspot: the cast lands in it, the catch pays more experience and uses up one of its fish.
            FishingHotspots hotspots = FishingHotspots.Instance;
            Check(hotspots != null, "the pond has fishing hotspots");
            if (hotspots != null && hotspots.State(0) == FishingHotspots.SpotState.Active)
            {
                fishing.ServerForceKind((int)FishKind.Perch);
                int spotCast = fishing.ServerCast(hotspots.Position(0), 0.5f);
                Check(fishing.ServerPendingSpot == 0, "a cast into a hotspot is known to land there");
                int before = fishing.Xp;
                Check(fishing.ServerHook(spotCast, true) && fishing.ServerCatch(spotCast, true), "a hotspot fish can be landed");
                Check(fishing.Xp > before + fishing.SpeciesOf(FishKind.Perch).Xp - 1 && fishing.Xp - before >= fishing.SpeciesOf(FishKind.Perch).Xp,
                    "a hotspot catch gives at least its experience");
                hotspots.ServerToggle(0);
                Check(hotspots.State(0) != FishingHotspots.SpotState.Active && hotspots.SpotAt(hotspots.Position(0)) < 0, "an emptied hotspot gives nothing more");
                hotspots.ServerToggle(0);
                fishing.ServerForceKind(-1);
            }

            health.ServerHealFull();
            health.ServerDamage(40);
            int hurt = health.Health;
            // The smallest fish carried is eaten first: a meal for stamina, not a heal.
            FishingRules.NextToEat(fishing.CopyCounts(), fishing.SpeciesTable, out FishKind eaten);
            Check(fishing.ServerEat() && health.Health == hurt && fishing.FedBonus > 0,
                $"eating the {fishing.SpeciesOf(eaten).Name} gives +{fishing.FedBonus} stamina and no health");

            WaterBody lake = default;
            bool haveLake = false;
            foreach (WaterBody body in gen.WaterBodies)
            {
                if (!body.Deep) continue;
                lake = body;
                haveLake = true;
                break;
            }
            Check(haveLake, "a forest has a deep lake");
            if (haveLake)
            {
                if (fishing.Busy) fishing.TryToggle();
                fishing.ServerStop();
                float edge = WorldGen.ShoreEdge(lake);
                Vector2 outward = lake.Center.sqrMagnitude > 1f ? lake.Center.normalized : Vector2.right;
                Vector2 lakeBank = lake.Center + outward * (edge + 2f);
                TeleportPlayer(wood, new Vector3(lakeBank.x, 0f, lakeBank.y));
                yield return new WaitForSeconds(0.5f);
                Check(PlayerFishing.AtWater(wood.transform.position), "a forest lake bank is a fishing spot");
                Check(!PlayerFishing.AtPond(wood.transform.position), "the lake is not the hub pond");
                FishWater expected = FishingRules.FromElement(ZoneMap.ElementAt(lake.Center.x, lake.Center.y, gen.HubRadius));
                Check(fishing.TryToggle() && fishing.Busy, "the rod comes out at a lake");
                // The server learns the rod is out a tick later, as at the pond.
                yield return new WaitForSeconds(0.4f);
                int lakeCast = fishing.ServerCast(new Vector3(lake.Center.x, 0f, lake.Center.y), 1f);
                Check(lakeCast > 0, "a cast lands in the lake");
                Check(fishing.SpeciesOf(fishing.ServerPendingKind).Home == expected, "the lake's fish belong to that forest");
                fishing.ServerStop();
            }

            Check(fishing.ServerGrantGear() && fishing.Lure == 1, "the next arena gift is a lure");
            Check(fishing.ServerGrantGear() && fishing.Rod == 2, "then a better rod");

            fishing.ServerLoad(0, 0, 0, 0);
            fishing.ServerStop();
            health.ServerHealFull();
            TeleportPlayer(wood, new Vector3(0f, 0f, 7.5f));
            yield return new WaitForSeconds(0.4f);
            Check(!fishing.Busy, "walking away puts the rod away");
        }

        // ---------- 0h. Saving and loading ----------

        private static IEnumerator SavingWorks()
        {
            PlayerWood wood = PlayerWood.Local;
            PlayerInventory inventory = wood.GetComponent<PlayerInventory>();
            PlayerUpgrades upgrades = wood.GetComponent<PlayerUpgrades>();
            PlayerIdentity identity = wood.GetComponent<PlayerIdentity>();
            Chopper.World.Saving.WorldSaver saver = Chopper.World.Saving.WorldSaver.Instance;
            Check(saver != null && saver.WorldApplied, "the world saver is ready");
            Check(identity != null && !string.IsNullOrEmpty(identity.Id), $"the player has a profile id ({(identity != null ? identity.Id : "none")})");
            if (saver == null || identity == null) yield break;

            // A scratch file, never the player's own worlds.
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "smoke" + Chopper.World.Saving.SaveFiles.Extension);
            Chopper.World.Saving.SaveFiles.Delete(path);
            Chopper.World.Saving.SaveSystem.UseScratchFile(path, "Smoke world");

            Hut hut = Hut.Instance;
            TreeManager trees = TreeManager.Instance;
            EnemyDirector director = EnemyDirector.Instance;
            int storeBefore = hut.WoodStore;
            hut.ServerAddStoreWood(37);
            int felledTree = FindTree(trees.Database, TreeSize.Medium);
            trees.ServerLoadFelled(new[] { felledTree });
            inventory.ServerLoad(2, 1, 4, 2, 5, 3);
            upgrades.ServerLoad(1, 0, 2, 1);
            wood.ServerTakeAll();
            wood.ServerAddWood(6);
            Vector3 pileAt = new Vector3(20f, 0f, 30f);
            director.ServerDropWood(pileAt, 4);
            director.ServerSpawn(Element.Fire, TreeSize.Medium, false, new Vector3(-25f, 0f, 35f));
            yield return new WaitForSeconds(0.3f);

            Check(saver.ServerSave() && System.IO.File.Exists(path), "the world is written to its save file");
            Chopper.World.Saving.WorldSave read = Chopper.World.Saving.SaveFiles.Read(path);
            Check(read != null && read.Seed == Chopper.World.Generation.WorldSession.Seed && read.Name == "Smoke world", "the save reads back with its seed and name");
            if (read == null) yield break;
            Chopper.World.Saving.PlayerRecord record = read.FindPlayer(identity.Id);
            Check(record != null && record.Iron == 5 && record.Shards == 3 && record.Fabric == 2 && record.SturdyLevel == 2 && record.Wood == 6,
                "the player's wood, fabric, crypt finds and Sturdy level are in the save");
            Check(read.Hut.WoodStore == storeBefore + 37 && System.Array.IndexOf(read.FelledTrees, felledTree) >= 0, "the hut store and felled trees are in the save");
            Check(read.Saplings.Count >= 1 && read.Piles.Exists(p => p.Amount == 4), $"saplings and wood piles are in the save ({read.Saplings.Count} saplings)");

            // Wipe it all, then load the save back.
            hut.ServerTrySpend(hut.WoodStore);
            inventory.ServerLoad(0, 0, 0, 0, 0, 0);
            upgrades.ServerLoad(0, 0, 0, 0);
            wood.ServerTakeAll();
            trees.ServerRegrowAll();
            foreach (Treant t in new System.Collections.Generic.List<Treant>(Treant.All)) t.ServerDespawn();
            foreach (WoodPile pile in new System.Collections.Generic.List<WoodPile>(WoodPile.All)) pile.ServerTake(wood);
            wood.ServerTakeAll();
            yield return new WaitForSeconds(0.3f);

            saver.ServerApply(read);
            saver.ServerApplyPlayer(identity);
            yield return new WaitForSeconds(0.6f);
            Check(hut.WoodStore == storeBefore + 37, $"loading brings the hut store back ({hut.WoodStore})");
            Check(trees.ServerFelledIds().Length == read.FelledTrees.Length && trees.IsFelledOnClient(felledTree), "loading fells the saved trees again");
            Check(inventory.Iron == 5 && inventory.Shards == 3 && inventory.Fabric == 2 && upgrades.SturdyLevel == 2 && wood.Carried == 6,
                $"loading gives the player their things back ({inventory.Iron} iron, Sturdy {upgrades.SturdyLevel}, {wood.Carried} wood)");
            Check(EnemyDirector.CountSaplings() == read.Saplings.Count, $"loading plants the saved saplings ({EnemyDirector.CountSaplings()})");
            Check(WoodPile.Nearest(new Vector3(pileAt.x, Chopper.World.WorldGround.HeightAt(pileAt), pileAt.z)) != null,
                "loading puts the wood pile back");

            // Leave the rest of the run as it was.
            Chopper.World.Saving.SaveFiles.Delete(path);
            Chopper.World.Saving.SaveSystem.Clear();
            hut.ServerTrySpend(hut.WoodStore);
            hut.ServerAddStoreWood(storeBefore);
            inventory.ServerLoad(1, 1, 0, 0, 0, 0);
            upgrades.ServerLoad(0, 0, 0, 0);
            trees.ServerRegrowAll();
            foreach (Treant t in new System.Collections.Generic.List<Treant>(Treant.All)) t.ServerDespawn();
            foreach (WoodPile pile in new System.Collections.Generic.List<WoodPile>(WoodPile.All)) pile.ServerTake(wood);
            wood.ServerTakeAll();
            TeleportPlayer(wood, new Vector3(0f, 0f, 7.5f));
            yield return new WaitForSeconds(0.3f);
        }

        // ---------- 0c. Blocking, dodging, hit reactions ----------

        private static IEnumerator CombatMoves()
        {
            PlayerWood wood = PlayerWood.Local;
            PlayerHealth health = wood.GetComponent<PlayerHealth>();
            PlayerCombat combat = wood.GetComponent<PlayerCombat>();
            GameConfig config = TreeManager.Instance.Config;
            Check(combat != null, "the player can block and roll");
            if (combat == null) yield break;

            health.ServerHealFull();
            health.ServerDamage(20);
            yield return null;
            Check(health.Health == config.PlayerMaxHealth - 20, "an unguarded hit does full damage");

            health.ServerHealFull();
            combat.DebugSetBlocking(true);
            yield return new WaitForSeconds(config.ParryWindow + 0.3f);
            health.ServerDamage(20);
            yield return null;
            int expected = config.PlayerMaxHealth - CombatRules.BlockedDamage(20, config.BlockReduction);
            Check(combat.IsBlocking && health.Health == expected, $"a block cuts 20 damage to {config.PlayerMaxHealth - health.Health}");

            // A guard raised just now parries the next hit completely.
            combat.DebugSetBlocking(false);
            yield return new WaitForSeconds(0.3f);
            health.ServerHealFull();
            combat.DebugSetBlocking(true);
            yield return new WaitForSeconds(0.1f);
            health.ServerDamage(20);
            yield return null;
            Check(health.Health == config.PlayerMaxHealth, "a fresh block parries the hit");
            combat.DebugSetBlocking(false);
            yield return new WaitForSeconds(0.3f);

            // A roll makes the player untouchable for a moment.
            health.ServerHealFull();
            PlayerStamina.Local.Refill();
            combat.DebugRoll();
            yield return new WaitForSeconds(0.12f);
            health.ServerDamage(20);
            yield return null;
            Check(combat.IsRolling && health.Health == config.PlayerMaxHealth, "a roll dodges the hit");
            yield return new WaitForSeconds(config.RollSeconds + config.RollCooldown);

            // God mode blocks everything.
            combat.ServerSetGod(true);
            yield return null;
            health.ServerDamage(500);
            yield return null;
            Check(health.Health == config.PlayerMaxHealth && !health.IsDown, "god mode takes no damage");
            combat.ServerSetGod(false);
            yield return null;

            // An enemy that is hit without dying is stunned and shoved.
            Treant foe = EnemyDirector.Instance.ServerSpawn(Element.Neutral, TreeSize.Medium, false, new Vector3(0f, 0f, -60f));
            yield return null;
            foe.ServerActivate();
            Vector3 before = foe.transform.position;
            foe.ServerHit(0.3f, null, wood, before + new Vector3(0f, 0f, 3f));
            yield return null;
            Check(foe.IsStaggered, "a hit that does not kill stuns the enemy");
            yield return new WaitForSeconds(0.4f);
            Check((foe.transform.position - before).magnitude > 0.3f, "the hit shoves the enemy back");
            ClearTreants();
            health.ServerHealFull();
            yield return null;
        }

        // ---------- 6. Developer tools ----------

        private static IEnumerator DevToolsWork()
        {
            PlayerDevTools tools = PlayerWood.Local.GetComponent<PlayerDevTools>();
            DayNightCycle cycle = DayNightCycle.Instance;
            Check(tools != null && tools.IsHostPlayer, "the host has the developer tools");
            if (tools == null) yield break;

            tools.Execute(DevCommand.ToNight, 0);
            yield return null;
            Check(cycle.IsNight, "the dev command skips to night");
            tools.Execute(DevCommand.SpawnEnemies, 3);
            yield return null;
            Check(EnemyDirector.CountActive() >= 3, "the dev command spawns awake enemies");
            tools.Execute(DevCommand.ToDay, 0);
            yield return new WaitForSeconds(0.3f);
            Check(cycle.Phase == DayPhase.Day, "the dev command skips back to day");
            ClearTreants();

            PlayerInventory inventory = tools.GetComponent<PlayerInventory>();
            int fabric = inventory.Fabric;
            tools.Execute(DevCommand.GiveFabric, 3);
            yield return null;
            Check(inventory.Fabric == fabric + 3, "the dev command gives fiber");

            // Flying: a jump far faster than running is not snapped back, and gravity is off.
            tools.Execute(DevCommand.Fly, 1);
            yield return null;
            yield return null;
            Vector3 high = tools.transform.position + new Vector3(30f, 25f, 0f);
            tools.transform.position = high;
            yield return new WaitForSeconds(0.8f);
            Check(tools.Flying && Vector3.Distance(tools.transform.position, high) < 0.5f,
                $"a flying player hovers and is not snapped back ({Vector3.Distance(tools.transform.position, high):F2} m off)");
            tools.Execute(DevCommand.Fly, 0);
            yield return new WaitForSeconds(3f);
            bool hasGround = Chopper.World.WorldGround.Exists;
            float above = tools.transform.position.y - Chopper.World.WorldGround.HeightAt(tools.transform.position);
            // It may land on a rock or a stump, so only check that it came down from the 25 m it was flying at.
            Check(!tools.Flying && above < 3f, $"after flying the player falls back to the ground ({above:F1} m above it)");
            TeleportPlayer(PlayerWood.Local, new Vector3(0f, 0f, 7.5f));
            yield return new WaitForSeconds(0.3f);
        }

        // ---------- 5. The house (chest, bed, tools, building) and lives ----------

        private static IEnumerator WallsAndLives(PlayerHealth health, GameConfig config)
        {
            Hut hut = Hut.Instance;
            PlayerWood wood = PlayerWood.Local;
            // No palisade: the house section takes its place.
            yield return HouseWorks(wood, config);
            // Lives are infinite: going down only costs the respawn wait, however many times it happens.
            health.ServerHealFull();
            Check(!config.FiniteLives, "lives are infinite by default");
            for (int fall = 1; fall <= 4; fall++)
            {
                health.ServerDamage(10000);
                yield return null;
                Check(health.IsDown && !health.IsOut, $"fall {fall}: down but not out of lives");
                health.ServerRespawnNow();
                yield return null;
                Check(!health.IsDown && health.Health == config.PlayerMaxHealth, $"fall {fall}: respawned with full health");
            }
            Check(DayNightCycle.Instance.Day == 1 && hut.Health > 0, "falling never ends the run by itself");
        }

        // The shared chest, the bed, tools that wear out and are mended at the bench, and building pieces.
        private static IEnumerator HouseWorks(PlayerWood wood, GameConfig config)
        {
            Hut hut = Hut.Instance;
            ResetHut(hut);
            SharedChest chest = SharedChest.Instance;
            PlayerInventory inventory = wood.GetComponent<PlayerInventory>();
            PlayerAxe axe = wood.GetComponent<PlayerAxe>();
            PlayerFishing fishing = wood.GetComponent<PlayerFishing>();
            PlayerUpgrades upgrades = wood.GetComponent<PlayerUpgrades>();
            PlayerRest rest = wood.GetComponent<PlayerRest>();
            DayNightCycle cycle = DayNightCycle.Instance;

            // Chest: items go in and come out again, only as much as there is and as fits.
            wood.ServerTakeAll();
            wood.ServerAddWood(12);
            inventory.ServerAddHerbs(3);
            Check(chest.ServerMove(wood.gameObject, (int)ItemKind.Wood, 999, true) == 12 && wood.Carried == 0 && chest.Count((int)ItemKind.Wood) == 12,
                "the chest takes all the carried wood");
            Check(chest.ServerMove(wood.gameObject, (int)ItemKind.Herb, 2, true) == 2 && inventory.Herbs == 1, "and two of three herbs");
            Check(chest.ServerMove(wood.gameObject, (int)ItemKind.Wood, 5, false) == 5 && wood.Carried == 5 && chest.Count((int)ItemKind.Wood) == 7, "five wood come back out");
            Check(chest.ServerMove(wood.gameObject, (int)ItemKind.Iron, 3, false) == 0, "nothing comes out of an empty stack");
            chest.ServerReset();
            wood.ServerTakeAll();
            inventory.ServerLoad(1, 1, 0, 0, 0, 0);

            // Tools wear: every axe hit, and a broken axe falls back to fists until it is mended at a bench.
            axe.ServerLoadWear(config.AxeDurability - 1);
            Check(axe.HasAxe && axe.Durability == 1, "a nearly worn out axe is still in the hand");
            int tree = FindTree(TreeManager.Instance.Database, TreeSize.Large);
            Vector3 aim = Vector3.forward;
            TeleportPlayer(wood, TreeManager.Instance.Database.Get(tree).Position - aim * ChopAimOffset);
            yield return new WaitForSeconds(0.3f);
            axe.ServerResolveHit(aim);
            yield return null;
            Check(axe.IsBroken && !axe.HasAxe && !axe.IsDrawn, "the last hit breaks the axe: fists from now on");
            TreeManager.Instance.ServerRegrowAll();
            fishing.ServerLoadRodWear(config.RodDurability);
            Check(fishing.RodBroken && !fishing.CanFish, "a worn out rod cannot cast");

            Vector3 benchSpot = Workbench.All[0].transform.position + new Vector3(-1.4f, 0f, 0f);
            TeleportPlayer(wood, benchSpot);
            yield return new WaitForSeconds(0.3f);
            wood.ServerTakeAll();
            Check(!upgrades.ServerRepair(ToolKind.Axe), "no repair without the wood for it");
            wood.ServerAddWood(config.AxeRepairWood + config.RodRepairWood);
            inventory.ServerAddFabric(config.RodRepairFabric);
            Check(upgrades.ServerRepair(ToolKind.Axe) && axe.HasAxe && axe.Durability == config.AxeDurability, "the axe is mended at the workbench");
            Check(upgrades.ServerRepair(ToolKind.Rod) && fishing.CanFish && fishing.RodDurability == config.RodDurability, "and the rod, with wood and fiber");
            Check(wood.Carried == 0 && inventory.Fabric == 0, "the repairs took their wood and fiber");
            Check(!upgrades.ServerRepair(ToolKind.Axe), "a whole axe needs no repair");

            // Eating a fish raises the stamina maximum for a while instead of healing.
            fishing.ServerChangeFish(FishKind.Perch, 1);
            Check(fishing.ServerEat(FishKind.Perch) && fishing.FedBonus > 0 && fishing.FedSecondsLeft > 0, $"a fish is a meal: +{fishing.FedBonus} stamina");
            yield return new WaitForSeconds(0.3f);
            PlayerStamina stamina = wood.GetComponent<PlayerStamina>();
            Check(stamina != null && stamina.Max >= config.StaminaMax + fishing.FedBonus - 0.01f, $"the meal shows in the stamina maximum ({(stamina != null ? stamina.Max : 0f):F0})");

            // The bed: no sleeping by day; at night everyone in bed skips to the morning.
            Bed bed = UnityEngine.Object.FindAnyObjectByType<Bed>();
            TeleportPlayer(wood, bed.transform.position + new Vector3(1.2f, 0f, 0f));
            yield return new WaitForSeconds(0.3f);
            cycle.ServerDebugSetClock(DayPhase.Day, 0.5f);
            Check(!rest.ServerLieDown() && !rest.InBed, "nobody sleeps by day");
            int day = cycle.Day;
            cycle.ServerDebugSetClock(DayPhase.Night, 0.2f);
            yield return null;
            Check(rest.ServerLieDown(), "at night the bed takes the player");
            yield return null;
            Check(cycle.Phase == DayPhase.Dawn && !rest.InBed, $"everyone asleep: the night is skipped to dawn ({cycle.Phase}), and the player is up");
            cycle.ServerDebugSetClock(DayPhase.Day, 0.05f);
            ClearTreants();
            yield return null;
            Check(cycle.Day >= day, "the clock went forward");

            // Building: a workbench is needed nearby; pieces cost wood, overlap nothing, and come down for their full cost.
            var build = Chopper.World.Building.BuildManager.Instance;
            Check(build != null && build.Catalog != null && build.Catalog.Count >= 8, $"the building catalog has its pieces ({(build != null && build.Catalog != null ? build.Catalog.Count : 0)})");
            if (build == null || build.Catalog == null) yield break;
            int floor = 0, wall = 1, bench = 5;
            Vector3 far = new Vector3(40f, 0f, 60f);
            far.y = GroundY(far);
            TeleportPlayer(wood, far + new Vector3(0f, 0f, -3f));
            yield return new WaitForSeconds(0.3f);
            wood.ServerTakeAll();
            wood.ServerAddWood(40);
            Vector3 spot = BuildRules.Snap(far, PieceSnap.Cell, 0f);
            spot.y = GroundY(spot);
            Check(build.ServerPlace(wood.gameObject, floor, spot, 0f) == Chopper.World.Building.PlaceResult.NoWorkbench, "nothing but a workbench is built far from one");
            Vector3 benchAt = spot + new Vector3(3f, 0f, 0f);
            benchAt.y = GroundY(benchAt);
            Chopper.World.Building.PlaceResult benchResult = build.ServerPlace(wood.gameObject, bench, BuildRules.Snap(benchAt, PieceSnap.Free, 0f), 0f);
            yield return null;
            Check(benchResult == Chopper.World.Building.PlaceResult.Ok, $"a workbench can be built anywhere ({benchResult})");
            Chopper.World.Building.PlaceResult floorResult = build.ServerPlace(wood.gameObject, floor, spot, 0f);
            yield return null;
            Check(floorResult == Chopper.World.Building.PlaceResult.Ok, $"then a floor next to it ({floorResult})");
            Check(build.ServerPlace(wood.gameObject, floor, spot, 0f) == Chopper.World.Building.PlaceResult.Overlaps, "a second floor in the same cell overlaps");
            int woodBefore = wood.Carried;
            Vector3 wallAt = BuildRules.Snap(spot + new Vector3(0f, 0.2f, 0.9f), PieceSnap.Edge, 0f);
            Chopper.World.Building.PlaceResult wallResult = build.ServerPlace(wood.gameObject, wall, wallAt, 0f);
            yield return null;
            Check(wallResult == Chopper.World.Building.PlaceResult.Ok && wood.Carried == woodBefore - build.Catalog.Get(wall).WoodCost,
                $"a wall stands on the floor's edge and costs its wood ({wallResult})");
            yield return new WaitForSeconds(0.3f);
            Check(UnityEngine.Object.FindObjectsByType<Chopper.World.Building.PieceView>().Length == build.Count, $"every piece is in the scene ({build.Count})");
            // Minecraft style: a wall may hang in the air. Taken straight down again so the checks below see the same list.
            Check(build.ServerPlace(wood.gameObject, wall, wallAt + Vector3.up * 6f, 0f) == Chopper.World.Building.PlaceResult.Ok, "a wall in the air is allowed");
            build.ServerRemove(wood.gameObject, build.Pieces[build.Count - 1].Id);
            yield return null;

            // An enemy breaks what is in its way; a broken piece is gone.
            int wallId = build.Pieces[build.Count - 1].Id;
            build.ServerDamage(wallId, 50);
            Check(build.Pieces[build.Count - 1].Health == build.Catalog.Get(wall).Health - 50, "a blow takes health off a piece");
            Check(build.ServerNearest(wallAt + new Vector3(0f, 0f, 1.2f), 1.6f, out int nearest) && nearest == wallId, "an enemy beside the wall finds it");

            // Saved with the world and put back on load.
            var records = build.ServerCapture();
            Check(records.Count == build.Count, "the pieces go into the save");
            int before = build.Count;
            build.ServerLoad(new System.Collections.Generic.List<Chopper.World.Saving.PieceRecord>());
            yield return new WaitForSeconds(0.2f);
            build.ServerLoad(records);
            yield return new WaitForSeconds(0.3f);
            Check(build.Count == before && build.Pieces[build.Count - 1].Health == build.Catalog.Get(wall).Health - 50, "loading puts the pieces back, damage and all");

            wallId = build.Pieces[build.Count - 1].Id;
            woodBefore = wood.Carried;
            int wallRefund = BuildRules.Refund(build.Catalog.Get(wall).WoodCost, build.Pieces[build.Count - 1].Health, build.Catalog.Get(wall).Health);
            Check(build.ServerRemove(wood.gameObject, wallId) && wood.Carried == woodBefore + wallRefund,
                $"taking a damaged piece down gives back wood by how whole it is ({wallRefund} of {build.Catalog.Get(wall).WoodCost})");
            build.ServerDamage(build.Pieces[build.Count - 1].Id, 99999);
            Check(build.Count == before - 2, "a piece broken by blows is gone");

            // No collapse: a wall stacked on a wall stands, and taking the lower wall away leaves the upper one hanging.
            build.ServerLoad(null);
            yield return null;
            wood.ServerAddWood(80);
            Check(build.ServerPlace(wood.gameObject, bench, BuildRules.Snap(benchAt, PieceSnap.Free, 0f), 0f) == Chopper.World.Building.PlaceResult.Ok, "a workbench again");
            Check(build.ServerPlace(wood.gameObject, floor, spot, 0f) == Chopper.World.Building.PlaceResult.Ok, "a fresh floor");
            Check(build.ServerPlace(wood.gameObject, wall, wallAt, 0f) == Chopper.World.Building.PlaceResult.Ok, "a wall on it");
            int lowerId = build.Count > 0 ? build.Pieces[build.Count - 1].Id : 0;
            Vector3 upper = wallAt + Vector3.up * build.Catalog.Get(wall).Size.y;
            Chopper.World.Building.PlaceResult upperResult = build.Count > 0 ? build.ServerPlace(wood.gameObject, wall, upper, 0f) : Chopper.World.Building.PlaceResult.BadKind;
            Check(upperResult == Chopper.World.Building.PlaceResult.Ok, $"a second wall on top of the first ({upperResult})");
            int woodBeforeRemove = wood.Carried;
            build.ServerRemove(wood.gameObject, lowerId);
            yield return null;
            Check(build.Count == 3, $"taking the lower wall away leaves the wall on it hanging ({build.Count} left)");
            Check(wood.Carried == woodBeforeRemove + build.Catalog.Get(wall).WoodCost, $"only the lower wall gives its wood back ({wood.Carried - woodBeforeRemove})");
            build.ServerLoad(null);
            wood.ServerTakeAll();
            TeleportPlayer(wood, new Vector3(0f, 0f, 7.5f));
            yield return new WaitForSeconds(0.3f);
        }

        private static float GroundY(Vector3 position)
        {
            bool hasGround = Chopper.World.WorldGround.Exists;
            return hasGround ? Chopper.World.WorldGround.HeightAt(position) : position.y;
        }

        private static void ClearTreants()
        {
            foreach (Treant t in new System.Collections.Generic.List<Treant>(Treant.All)) t.ServerDespawn();
        }

        private static int FindTree(TreeDatabase db, TreeSize size)
        {
            for (int i = 0; i < db.Count; i++)
            {
                if (db.Get(i).Size == size && !TreeManager.Instance.IsFelledOnClient(i)) return i;
            }
            return 0;
        }

        private static void Finish(bool ok, string message)
        {
            Debug.Log($"[Smoke] {(ok ? "PASS" : "FAIL")}: {message}");
            Application.Quit(ok ? 0 : 1);
        }
    }
}
#endif
