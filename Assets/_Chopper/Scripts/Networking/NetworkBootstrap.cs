using System;
using FishNet.Managing.Transporting;
using FishNet.Transporting;
using UnityEngine;

namespace Chopper.Networking
{
    // Picks how this copy of the game connects before the NetworkManager wakes up (it stays inactive in the Boot
    // scene until then): Steam relays when Steam is running, so friends join by Steam with no IP addresses; the local
    // UDP transport for command line runs (tests, tours, headless smoke) and as a solo fallback without Steam.
    public class NetworkBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject _networkManager;
        [SerializeField] private Transport _steam;
        [SerializeField] private Transport _local;
        [SerializeField] private NetworkLatencySimulation _latencySimulation;

        // True when this session plays over Steam.
        public static bool UsingSteam { get; private set; }

        private void Awake()
        {
            if (_networkManager == null) return;
            string[] args = Environment.GetCommandLineArgs();
            bool commandLine = Application.isBatchMode || Array.IndexOf(args, "-chopperHost") >= 0
                               || Array.IndexOf(args, "-chopperJoin") >= 0 || Array.IndexOf(args, "-chopperNoSteam") >= 0;
            bool steam = _steam != null && !commandLine && SteamPlatform.TryInit();
            if (steam) SteamLobby.Init();
            else if (!commandLine) Debug.Log("[Steam] Not used: " + SteamPlatform.Problem);
            UsingSteam = steam;

            TransportManager transports = _networkManager.GetComponent<TransportManager>();
            Transport chosen = steam ? _steam : _local;
            if (transports != null && chosen != null) transports.Transport = chosen;
            if (!steam && _latencySimulation != null) _latencySimulation.Configure(transports);
            // Only the chosen transport runs; the other would poll an uninitialized socket.
            if (_steam != null) _steam.enabled = steam;
            if (_local != null) _local.enabled = !steam;
            _networkManager.SetActive(true);
        }
    }
}
