using System.Collections.Generic;
using Chopper.World;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chopper.Networking
{
    // Server side. Spawns one owned player for each connection once it has loaded the global scenes.
    // Spawn points live in World, so they are found at spawn time instead of being referenced from Boot.
    // FishNet despawns the player automatically when its owner disconnects.
    [RequireComponent(typeof(NetworkManager))]
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkObject _playerPrefab;

        [Tooltip("Networked world objects (trees, hut) spawned once by the server when the first client is ready.")]
        [SerializeField] private NetworkObject[] _worldPrefabs;

        private NetworkManager _networkManager;
        private bool _worldSpawned;
        // The first player wakes up somewhere random in the world; friends who join wake up near them.
        private bool _hasAnchor;
        private Vector3 _anchorPosition;
        private const float GroupSpreadMin = 2f;
        private const float GroupSpreadMax = 5f;
        private const float SpawnPointWaitSeconds = 30f;

        private void Start()
        {
            _networkManager = GetComponent<NetworkManager>();
            if (_networkManager == null || !_networkManager.Initialized)
            {
                enabled = false;
                return;
            }

            _networkManager.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
            _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
        }

        private void OnServerConnectionState(FishNet.Transporting.ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Stopped)
            {
                _worldSpawned = false;
                _hasAnchor = false;
            }
        }

        private void OnDestroy()
        {
            if (_networkManager == null || !_networkManager.Initialized) return;
            _networkManager.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
            _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        }

        private void SpawnWorld(Scene scene)
        {
            if (_worldSpawned || _worldPrefabs == null) return;
            _worldSpawned = true;

            foreach (NetworkObject prefab in _worldPrefabs)
            {
                if (prefab == null) continue;
                NetworkObject instance = _networkManager.GetPooledInstantiated(prefab, prefab.transform.position, prefab.transform.rotation, true);
                _networkManager.ServerManager.Spawn(instance, null, scene);
            }
        }

        private void OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            if (!asServer) return;
            StartCoroutine(SpawnWhenReady(conn));
        }

        // The scene's spawn points register when their objects are enabled, which can be a frame or two after the
        // scene counts as loaded, so wait for them instead of falling back to the origin.
        private System.Collections.IEnumerator SpawnWhenReady(NetworkConnection conn)
        {
            float deadline = Time.unscaledTime + SpawnPointWaitSeconds;
            // The wake up spots are made by WorldBuilder from the world seed; wait for the world to be built.
            while ((!Chopper.World.Generation.WorldSession.Ready || PlayerSpawnPoint.Active.Count == 0) && Time.unscaledTime < deadline) yield return null;
            if (conn == null || !conn.IsValid || !conn.IsActive) yield break;
            SpawnPlayer(conn);
        }

        private void SpawnPlayer(NetworkConnection conn)
        {
            if (_playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawner] Player prefab is not assigned.");
                return;
            }

            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            Scene scene = default;

            // Players wake up far from the hut and have to find their way back to it.
            PlayerSpawnPoint point = PlayerSpawnPoint.Pick(true, Random.Range(0, 100000));
            if (point != null)
            {
                scene = point.gameObject.scene;
                rotation = point.transform.rotation;
                if (!_hasAnchor)
                {
                    _hasAnchor = true;
                    _anchorPosition = point.transform.position;
                    position = _anchorPosition;
                }
                else
                {
                    float angle = Random.value * Mathf.PI * 2f;
                    float distance = Random.Range(GroupSpreadMin, GroupSpreadMax);
                    position = _anchorPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                    bool hasGround = Chopper.World.WorldGround.Exists;
                    if (hasGround) position.y = Chopper.World.WorldGround.HeightAt(position) + 0.3f;
                }
            }
            else
            {
                Debug.LogWarning("[PlayerSpawner] No PlayerSpawnPoint in the loaded scenes; spawning at the origin.");
            }

            // W9-01 (user, 2026-10-12): outside the practice yard every player arrives by falling into the hub pond,
            // facing the hut, with no outfit and no axe; their kit lies on the bank toward the hut.
            bool dropIn = !TutorialSession.Active && Chopper.World.Generation.WorldSession.Gen != null;
            if (dropIn)
            {
                position = WorldStart.DropPoint(_arrivals);
                Vector3 toHut = -new Vector3(position.x, 0f, position.z);
                rotation = toHut.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toHut.normalized, Vector3.up) : Quaternion.identity;
                _arrivals++;
            }

            SpawnWorld(scene);

            NetworkObject player = _networkManager.GetPooledInstantiated(_playerPrefab, position, rotation, true);
            _networkManager.ServerManager.Spawn(player, conn, scene);
            Debug.Log($"[PlayerSpawner] Spawned player for client {conn.ClientId}{(dropIn ? $", dropping into the pond from {position:F1}" : "")}.");
            if (dropIn) DropStartKit(player);
        }

        private int _arrivals;

        // One outfit and one axe per arriving player, on the pond bank toward the hut. Whoever has neither picks them up
        // by walking over them (GroundItem); a player who loads dressed from a save simply leaves them for others.
        private static void DropStartKit(NetworkObject player)
        {
            Chopper.Enemies.EnemyDirector director = Chopper.Enemies.EnemyDirector.Instance;
            if (director == null) return;
            Vector3 outfitAt = WorldStart.KitSpot(0);
            Vector3 axeAt = WorldStart.KitSpot(1);
            Vector3 shieldAt = WorldStart.KitSpot(2);
            // One kit per player still without an outfit: an unused kit left on the bank (a saved player came back
            // dressed, or rejoined) is enough, so kits do not pile up over many joins.
            int spare = 0;
            foreach (GroundItem item in GroundItem.All)
            {
                if (item == null || item.Kind != GroundItemKind.Outfit) continue;
                Vector3 off = item.transform.position - outfitAt;
                off.y = 0f;
                if (off.magnitude < 4f) spare++;
            }
            int bare = 0;
            foreach (Chopper.Player.PlayerUpgrades someone in UnityEngine.Object.FindObjectsByType<Chopper.Player.PlayerUpgrades>(FindObjectsSortMode.None))
                if (!someone.Dressed) bare++;
            if (spare >= bare) return;
            Chopper.Player.PlayerAxe axe = player.GetComponent<Chopper.Player.PlayerAxe>();
            int durability = axe != null ? axe.MaxDurability : 1;
            director.ServerDropItem(GroundItemKind.Outfit, 1, outfitAt, outfitAt);
            director.ServerDropItem(GroundItemKind.Axe, durability, axeAt, axeAt);
            director.ServerDropItem(GroundItemKind.Shield, 1, shieldAt, shieldAt);
        }
    }
}
