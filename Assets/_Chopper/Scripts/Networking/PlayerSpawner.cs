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

            SpawnWorld(scene);

            NetworkObject player = _networkManager.GetPooledInstantiated(_playerPrefab, position, rotation, true);
            _networkManager.ServerManager.Spawn(player, conn, scene);
            Debug.Log($"[PlayerSpawner] Spawned player for client {conn.ClientId}.");
        }
    }
}
