using System;
using System.Globalization;
using Chopper.World.Generation;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Chopper.Networking
{
    // Minimal connection flow. Lives on the NetworkManager object, which FishNet keeps alive across
    // scene loads, so it can return the player to Boot when the connection ends.
    // Host: start server, start client, server loads World as a global scene (clients follow automatically).
    // Join: start client to an address; FishNet loads World when the server sends its global scenes.
    // Any local disconnect loads Boot again.
    [RequireComponent(typeof(NetworkManager))]
    public class BootMenu : MonoBehaviour
    {
        // Command line flags so builds can be driven without clicking (used for local smoke tests).
        private const string HostArg = "-chopperHost";
        private const string JoinArg = "-chopperJoin";
        private const string StopAfterArg = "-chopperStopAfter";
        private const string SeedArg = "-chopperSeed";

        [SerializeField] private string _bootScene = "Boot";
        [SerializeField] private string _worldScene = "World";
        [SerializeField] private string _defaultJoinAddress = "127.0.0.1";

        private NetworkManager _networkManager;
        private string _status = string.Empty;
        private float _autoStopTime = -1f;
        private bool _returningToBoot;

        public string Status => _status;
        public string DefaultJoinAddress => _defaultJoinAddress;
        public bool Offline => _networkManager != null && !_networkManager.ServerManager.Started && !_networkManager.ClientManager.Started;
        public bool InBootScene => UnitySceneManager.GetActiveScene().name == _bootScene;

        private void Start()
        {
            _networkManager = GetComponent<NetworkManager>();
            // A duplicate NetworkManager from a reloaded Boot scene is destroyed by FishNet and never initializes.
            if (_networkManager == null || !_networkManager.Initialized)
            {
                enabled = false;
                return;
            }

            // Every joining player waits for the host to let them in (JoinApproval).
            _networkManager.ServerManager.SetAuthenticator(gameObject.AddComponent<JoinApproval>());
            gameObject.AddComponent<Chopper.UI.TitleScreen>();
            gameObject.AddComponent<Chopper.UI.LoadingScreen>();
            _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
            _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            _networkManager.ServerManager.OnAuthenticationResult += OnAuthenticationResult;
            _networkManager.ClientManager.RegisterBroadcast<WorldSeedBroadcast>(OnWorldSeed);
            UnitySceneManager.sceneLoaded += OnUnitySceneLoaded;
            SteamLobby.HostFound += OnSteamHostFound;
            SteamLobby.JoinFailed += OnSteamJoinFailed;

            ApplyCommandLine();
            // Started by Steam from a friend's invite: join their world straight away.
            if (NetworkBootstrap.UsingSteam && SteamLobby.JoinFromCommandLine(Environment.GetCommandLineArgs()))
            {
                _status = "Joining your friend...";
                Chopper.UI.LoadingScreen.Show("Joining a friend", "Through Steam");
            }
#if UNITY_EDITOR
            // Editor convenience (Chopper > Play > Auto Host On Play): start hosting without clicking.
            // Multiplayer Play Mode clones run from a virtual project folder and never auto host.
            bool isVirtualPlayer = Application.dataPath.Replace('\\', '/').Contains("/Library/VP/");
            if (!isVirtualPlayer && !_networkManager.ServerManager.Started && !_networkManager.ClientManager.Started &&
                UnityEditor.EditorPrefs.GetBool("Chopper.AutoHost", false))
            {
                StartHost();
            }
#endif
        }

        private void OnDestroy()
        {
            UnitySceneManager.sceneLoaded -= OnUnitySceneLoaded;
            SteamLobby.HostFound -= OnSteamHostFound;
            SteamLobby.JoinFailed -= OnSteamJoinFailed;
            if (_networkManager == null || !_networkManager.Initialized) return;

            _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
            _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            _networkManager.ServerManager.OnAuthenticationResult -= OnAuthenticationResult;
            _networkManager.ClientManager.UnregisterBroadcast<WorldSeedBroadcast>(OnWorldSeed);
        }

        // Server: every player who gets in is told the world seed first, so they build the host's world.
        private void OnAuthenticationResult(NetworkConnection connection, bool authenticated)
        {
            if (!authenticated) return;
            _networkManager.ServerManager.Broadcast(connection, new WorldSeedBroadcast { Seed = WorldSession.Seed, TreeChecksum = WorldSession.TreeChecksum });
        }

        // Client: remember the seed. The host already has it; a joiner builds its world from it when World loads.
        private void OnWorldSeed(WorldSeedBroadcast message, FishNet.Transporting.Channel channel)
        {
            if (_networkManager.IsServerStarted) return;
            WorldSession.SetSeed(message.Seed);
            Debug.Log($"[Boot] World seed {message.Seed} received from the host.");
            if (message.TreeChecksum != 0) StartCoroutine(CheckTrees(message.TreeChecksum));
        }

        // Floating point rounding can in theory differ between machines; if this world's trees do not match the host's,
        // say so plainly instead of letting chopping go out of step silently.
        private System.Collections.IEnumerator CheckTrees(int hostChecksum)
        {
            while (!WorldSession.Ready) yield return null;
            if (WorldSession.TreeChecksum != hostChecksum)
                Debug.LogError($"[Boot] This world's trees differ from the host's (checksum {WorldSession.TreeChecksum} vs {hostChecksum}).");
            else
                Debug.Log("[Boot] World matches the host.");
        }

        // The Steam transport talks to Steam while it closes, so it shuts down before Steam does.
        private void OnApplicationQuit()
        {
            if (_networkManager != null && _networkManager.TransportManager != null && _networkManager.TransportManager.Transport != null)
            {
                _networkManager.TransportManager.Transport.Shutdown();
            }
            SteamPlatform.Shutdown();
        }

        // A lobby we joined (from the friends list, an invite or the command line) named its host: connect to it.
        private void OnSteamHostFound(ulong host)
        {
            if (!Offline) return;
            Chopper.World.Saving.SaveSystem.Clear();
            if (!Chopper.UI.LoadingScreen.Visible) Chopper.UI.LoadingScreen.Show("Joining a friend", "Through Steam");
            JoinSteam(host);
        }

        private void OnSteamJoinFailed(string reason)
        {
            _status = reason;
        }

        // Connects to a host by Steam ID, through Steam's relays (no address or port involved).
        public void JoinSteam(ulong hostSteamId)
        {
            _status = "Joining your friend...";
            _networkManager.TransportManager.Transport.SetClientAddress(hostSteamId.ToString());
            _networkManager.ClientManager.StartConnection();
        }

        private void Update()
        {
            SteamPlatform.Tick();
            if (_autoStopTime >= 0f && Time.unscaledTime >= _autoStopTime)
            {
                _autoStopTime = -1f;
                Debug.Log("[Boot] Auto stop requested from command line.");
                Disconnect();
            }
        }

        // A message for the start screen's status line.
        public void SetStatus(string text) => _status = text;

        // Hosts a new game on a world made from this seed.
        public void StartHost(int seed)
        {
            WorldSession.SetSeed(seed);
            StartHost();
        }

        public void StartHost()
        {
            if (!WorldSession.HasSeed) WorldSession.SetSeed(SeedFromCommandLine());
            _status = "Starting host...";
            _networkManager.ServerManager.StartConnection();
            _networkManager.ClientManager.StartConnection();
        }

        public void Join(string address)
        {
            _status = $"Joining {address}...";
            _networkManager.ClientManager.StartConnection(address);
        }

        public void Disconnect()
        {
            // Client first so the host path only loads Boot once, from the server stop.
            if (_networkManager.ClientManager.Started) _networkManager.ClientManager.StopConnection();
            if (_networkManager.ServerManager.Started) _networkManager.ServerManager.StopConnection(true);
        }

        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            Debug.Log($"[Boot] Server {args.ConnectionState}");
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                // Multiple transports could each report Started; load the world only once.
                if (!_networkManager.ServerManager.IsOnlyOneServerStarted()) return;

                SceneLoadData load = new SceneLoadData(_worldScene) { ReplaceScenes = ReplaceOption.All };
                _networkManager.SceneManager.LoadGlobalScenes(load);
                // Friends find this world through a Steam lobby (no addresses).
                if (NetworkBootstrap.UsingSteam && !Chopper.World.TutorialSession.Active) SteamLobby.Host(Chopper.World.Saving.SaveSystem.WorldName);
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped && !_networkManager.ServerManager.IsAnyServerStarted())
            {
                ReturnToBoot();
            }
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            Debug.Log($"[Boot] Client {args.ConnectionState}");
            if (args.ConnectionState == LocalConnectionState.Started && !_networkManager.IsServerStarted && JoinApproval.RequireApproval)
                Chopper.UI.LoadingScreen.Show("Joining", "Waiting for the host to let you in");
            if (args.ConnectionState != LocalConnectionState.Stopped) return;

            // On a host the server stop handles the scene change.
            if (_networkManager.IsServerStarted) return;

            _status = string.IsNullOrEmpty(JoinApproval.LastDenial) ? "Disconnected." : JoinApproval.LastDenial;
            ReturnToBoot();
        }

        private void ReturnToBoot()
        {
            // Server and client stop callbacks can both arrive before the load completes.
            if (_returningToBoot || UnitySceneManager.GetActiveScene().name == _bootScene) return;
            _returningToBoot = true;
            SteamLobby.Leave();
            WorldSession.Reset();
            UnitySceneManager.LoadScene(_bootScene);
        }

        private void OnUnitySceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            Debug.Log($"[Boot] Scene loaded: {scene.name}");
            if (scene.name == _bootScene)
            {
                _returningToBoot = false;
                WorldSession.Reset();
                Chopper.World.Saving.SaveSystem.Clear();
                Chopper.World.TutorialSession.End();
            }
            if (scene.name == _worldScene) _status = string.Empty;
        }

        // -chopperSeed N picks the world; without it command line hosts (tests, tours) use the default world.
        private static int SeedFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == SeedArg) return WorldSession.ParseSeed(args[i + 1]);
            }
            return WorldSession.DefaultSeed;
        }

        private void ApplyCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                bool hasValue = i + 1 < args.Length;
                if (args[i] == StopAfterArg && hasValue &&
                    float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds))
                {
                    _autoStopTime = Time.unscaledTime + seconds;
                }
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == HostArg)
                {
                    // -chopperTutorial hosts the practice yard instead (tests and tours).
                    if (Array.IndexOf(args, "-chopperTutorial") >= 0)
                    {
                        Chopper.World.TutorialSession.Begin();
                        StartHost(Chopper.World.TutorialSession.Seed);
                        return;
                    }
                    StartHost();
                    return;
                }
                if (args[i] == JoinArg)
                {
                    Join(i + 1 < args.Length ? args[i + 1] : _defaultJoinAddress);
                    return;
                }
            }
        }
    }
}
