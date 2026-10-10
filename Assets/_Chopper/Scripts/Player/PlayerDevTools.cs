using Chopper.Core;
using Chopper.Enemies;
using Chopper.World;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Chopper.Player
{
    public enum DevCommand
    {
        NextPhase = 0,
        ToNight = 1,
        ToDay = 2,
        GodMode = 3,
        GiveWood = 4,
        GiveAxe = 5,
        HealFull = 6,
        RepairHut = 7,
        SpawnEnemies = 8,
        SpawnBoss = 9,
        RandomBoon = 10,
        Fly = 11,
        GiveHerbs = 12,
        GiveFabric = 13,
        GiveIron = 14,
        GiveShards = 15,
        GiveTorches = 16,
        GiveBandages = 17
    }

    // Developer and creative tools for the host: skip the clock, god mode, free wood, spawn enemies and so on.
    // The panel (F1) and the console (F5) send one of these commands; the server runs it only when it comes from the
    // player who is hosting the game, so joined players cannot use it on someone else's world.
    public class PlayerDevTools : NetworkBehaviour
    {
        private const int MaxPhaseSkips = 6;
        private const float SpawnRingMin = 9f;
        private const float SpawnRingMax = 16f;

        private PlayerCombat _combat;
        private PlayerWood _wood;
        private PlayerHealth _health;
        private PlayerAxe _axe;
        private PlayerInventory _inventory;
        private PlayerCrypt _crypt;

        // Free flight for exploring: no gravity or collisions, and the server speed check stands aside.
        private readonly SyncVar<bool> _flying = new SyncVar<bool>();

        public bool Flying => _flying.Value;

        private void Awake()
        {
            _combat = GetComponent<PlayerCombat>();
            _wood = GetComponent<PlayerWood>();
            _health = GetComponent<PlayerHealth>();
            _axe = GetComponent<PlayerAxe>();
            _inventory = GetComponent<PlayerInventory>();
            _crypt = GetComponent<PlayerCrypt>();
        }

        // True on the machine that hosts the game: the server and this player's own client are the same process.
        public bool IsHostPlayer => IsServerInitialized && IsOwner;

        public void Request(DevCommand command, int value = 0)
        {
            if (!IsOwner) return;
            DevServerRpc((int)command, value);
        }

        [ServerRpc]
        private void DevServerRpc(int command, int value)
        {
            if (!Owner.IsLocalClient) return;
            Execute((DevCommand)command, value);
        }

        // Moves this player anywhere (the console's tp command).
        public void RequestTeleport(Vector3 position, float yaw)
        {
            if (!IsOwner) return;
            TeleportServerRpc(position, yaw);
        }

        [ServerRpc]
        private void TeleportServerRpc(Vector3 position, float yaw)
        {
            if (!Owner.IsLocalClient || _crypt == null) return;
            _crypt.ServerTeleport(position, yaw);
        }

        // Server only.
        public void Execute(DevCommand command, int value)
        {
            DayNightCycle cycle = DayNightCycle.Instance;
            EnemyDirector director = EnemyDirector.Instance;
            switch (command)
            {
                case DevCommand.NextPhase:
                    if (cycle != null) cycle.ServerSkipPhase();
                    break;
                case DevCommand.ToNight:
                    for (int i = 0; i < MaxPhaseSkips && cycle != null && !cycle.IsNight; i++) cycle.ServerSkipPhase();
                    break;
                case DevCommand.ToDay:
                    for (int i = 0; i < MaxPhaseSkips && cycle != null && cycle.Phase != DayPhase.Day; i++) cycle.ServerSkipPhase();
                    break;
                case DevCommand.GodMode:
                    if (_combat != null) _combat.ServerSetGod(value != 0);
                    break;
                case DevCommand.GiveWood:
                    if (_wood != null) _wood.ServerAddWood(Mathf.Max(1, value));
                    break;
                case DevCommand.GiveAxe:
                    if (_axe != null) _axe.ServerRepair();
                    break;
                case DevCommand.HealFull:
                    if (_health != null) _health.ServerHealFull();
                    break;
                case DevCommand.RepairHut:
                    RepairEverything();
                    break;
                case DevCommand.SpawnEnemies:
                    if (director != null) SpawnAround(director, Mathf.Max(1, value));
                    break;
                case DevCommand.SpawnBoss:
                    if (director != null) WakeWarden(director);
                    break;
                case DevCommand.RandomBoon:
                    GrantBoon();
                    break;
                case DevCommand.Fly:
                    // Host only: a joined player's movement is always speed checked, and flight would fail it.
                    if (value != 0 && !Owner.IsLocalClient)
                    {
                        Debug.LogWarning($"[DevTools] Refused flight for client {Owner.ClientId}: only the host may fly.");
                        break;
                    }
                    _flying.Value = value != 0;
                    break;
                case DevCommand.GiveHerbs:
                    if (_inventory != null) _inventory.ServerAddHerbs(Mathf.Max(1, value));
                    break;
                case DevCommand.GiveFabric:
                    if (_inventory != null) _inventory.ServerAddFabric(Mathf.Max(1, value));
                    break;
                case DevCommand.GiveIron:
                    if (_inventory != null) _inventory.ServerAddRelics(Mathf.Max(1, value), 0);
                    break;
                case DevCommand.GiveShards:
                    if (_inventory != null) _inventory.ServerAddRelics(0, Mathf.Max(1, value));
                    break;
                case DevCommand.GiveTorches:
                    if (_inventory != null) _inventory.ServerGrant(Mathf.Max(1, value), 0);
                    break;
                case DevCommand.GiveBandages:
                    if (_inventory != null) _inventory.ServerGrant(0, Mathf.Max(1, value));
                    break;
            }
        }

        private void RepairEverything()
        {
            Hut hut = Hut.Instance;
            if (hut == null) return;
            hut.ServerSetRepaired(true);
            hut.ServerHealToFull();
        }

        private void SpawnAround(EnemyDirector director, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float distance = Random.Range(SpawnRingMin, SpawnRingMax);
                Vector3 p = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                Terrain terrain = Terrain.activeTerrain;
                if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y;

                Element element = (Element)Random.Range(0, 5);
                TreeSize size = (TreeSize)Random.Range(0, 3);
                Treant treant = director.ServerSpawn(element, size, false, p);
                treant.ServerActivate();
            }
        }

        // A grove warden rises a little way off, of the forest the player stands in (Fire in the hub).
        private void WakeWarden(EnemyDirector director)
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            Vector3 at = transform.position + (forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward) * SpawnRingMax;
            GameConfig config = Chopper.Trees.TreeManager.Instance != null ? Chopper.Trees.TreeManager.Instance.Config : null;
            Element zone = ZoneMap.ElementAt(at, config != null ? config.HubRadius : 40f);
            director.ServerWakeWarden(at, zone == Element.Neutral ? Element.Fire : zone);
        }

        private static void GrantBoon()
        {
            var rng = new System.Random();
            BoonKind kind = BoonRules.Pick(rng);
            foreach (PlayerHealth player in PlayerHealth.All)
            {
                PlayerBoons boons = player.GetComponent<PlayerBoons>();
                if (boons != null) boons.ServerGrant(kind);
            }
        }
    }
}
