using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace Chopper.Networking
{
    // Friends only Steam lobbies, so players find each other through Steam instead of addresses. The host opens a
    // lobby that carries its Steam ID and world name. Friends join from the Join a Friend list, from an invite (Steam
    // overlay, Shift+Tab), or with Join Game on the host in their Steam friends list. Joining the lobby gives the
    // host's Steam ID, and the game connects to it through Steam's relays.
    public static class SteamLobby
    {
        public const int MaxPlayers = 8;
        private const string KeyGame = "chopper";
        private const string KeyHost = "host";
        private const string KeyName = "name";
        private const string KeyVersion = "version";
        private const string KeyCode = "code";

        public struct FriendWorld
        {
            public ulong Friend;
            public string FriendName;
            public ulong Lobby;
            public string WorldName;
        }

        private static Callback<LobbyEnter_t> _entered;
        private static Callback<GameLobbyJoinRequested_t> _joinRequested;
        private static Callback<GameRichPresenceJoinRequested_t> _presenceJoin;
        private static CallResult<LobbyCreated_t> _created;
        private static CallResult<LobbyMatchList_t> _found;
        private static string _pendingWorldName;
        private static bool _hosting;

        public static CSteamID Current { get; private set; } = CSteamID.Nil;
        public static bool InLobby => Current.IsValid() && Current != CSteamID.Nil;

        // Raised when a lobby we joined names its host: connect to that Steam ID.
        public static event System.Action<ulong> HostFound;
        // Raised with a short message when joining fails (old version, lobby gone).
        public static event System.Action<string> JoinFailed;
        // The host's join code is ready (shown as a note and on the pause menu).
        public static event System.Action<string> CodeReady;

        public static void Init()
        {
            if (!SteamPlatform.Initialized || _entered != null) return;
            _entered = Callback<LobbyEnter_t>.Create(OnEntered);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(r => Join(r.m_steamIDLobby.m_SteamID));
            _presenceJoin = Callback<GameRichPresenceJoinRequested_t>.Create(r => JoinFromConnectString(r.m_rgchConnect));
            _created = CallResult<LobbyCreated_t>.Create(OnCreated);
            _found = CallResult<LobbyMatchList_t>.Create(OnFound);
        }

        // Launched by Steam from a friend's invite: the command line carries "+connect_lobby <id>".
        public static bool JoinFromCommandLine(string[] args)
        {
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out ulong lobby))
                {
                    Join(lobby);
                    return true;
                }
            }
            return false;
        }

        private static void JoinFromConnectString(string connect)
        {
            if (string.IsNullOrEmpty(connect)) return;
            JoinFromCommandLine(connect.Split(' '));
        }

        // ---------- Host ----------

        // The code friends type to join this world (empty until the lobby exists).
        public static string Code { get; private set; } = string.Empty;

        // Called once the host's server is running: opens a lobby for this world with a fresh join code. The lobby is
        // public so the code can find it, but it never shows in a browser and the host lets each player in.
        public static void Host(string worldName)
        {
            if (!SteamPlatform.Initialized) return;
            Init();
            _pendingWorldName = string.IsNullOrEmpty(worldName) ? "World" : worldName;
            _hosting = true;
            Code = string.Empty;
            _created.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, MaxPlayers));
        }

        private static void OnCreated(LobbyCreated_t result, bool failure)
        {
            if (failure || result.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogWarning($"[Steam] Lobby could not be created ({result.m_eResult}).");
                return;
            }
            Current = new CSteamID(result.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(Current, KeyGame, "1");
            SteamMatchmaking.SetLobbyData(Current, KeyHost, SteamPlatform.LocalId.ToString());
            SteamMatchmaking.SetLobbyData(Current, KeyName, _pendingWorldName);
            SteamMatchmaking.SetLobbyData(Current, KeyVersion, Application.version);
            Code = Chopper.Core.JoinCode.Generate(new System.Random());
            SteamMatchmaking.SetLobbyData(Current, KeyCode, Code);
            SteamFriends.SetRichPresence("connect", "+connect_lobby " + Current.m_SteamID);
            SteamFriends.SetRichPresence("status", "In " + _pendingWorldName);
            Debug.Log($"[Steam] Hosting lobby {Current.m_SteamID} for \"{_pendingWorldName}\", join code {Code}.");
            CodeReady?.Invoke(Code);
        }

        // Opens Steam's invite window for the current lobby (pause menu).
        public static bool OpenInviteDialog()
        {
            if (!SteamPlatform.Initialized || !InLobby) return false;
            SteamFriends.ActivateGameOverlayInviteDialog(Current);
            return true;
        }

        // ---------- Join ----------

        // Looks up the world with this join code anywhere in the world and joins its lobby.
        public static void JoinByCode(string typed)
        {
            string code = Chopper.Core.JoinCode.Normalize(typed);
            if (!Chopper.Core.JoinCode.IsValid(code))
            {
                JoinFailed?.Invoke($"A join code is {Chopper.Core.JoinCode.Length} letters and numbers.");
                return;
            }
            if (!SteamPlatform.Initialized)
            {
                JoinFailed?.Invoke("Playing together needs Steam. Start Steam, then restart Chopper.");
                return;
            }
            Init();
            SteamMatchmaking.AddRequestLobbyListStringFilter(KeyCode, code, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(KeyGame, "1", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(1);
            _found.Set(SteamMatchmaking.RequestLobbyList());
        }

        private static void OnFound(LobbyMatchList_t result, bool failure)
        {
            if (failure || result.m_nLobbiesMatching == 0)
            {
                JoinFailed?.Invoke("No world has that code. Check it with the host; a new code comes with every hosted game.");
                return;
            }
            Join(SteamMatchmaking.GetLobbyByIndex(0).m_SteamID);
        }

        public static void Join(ulong lobby)
        {
            if (!SteamPlatform.Initialized || lobby == 0) return;
            Init();
            _hosting = false;
            SteamMatchmaking.JoinLobby(new CSteamID(lobby));
        }

        private static void OnEntered(LobbyEnter_t entered)
        {
            Current = new CSteamID(entered.m_ulSteamIDLobby);
            if (_hosting) return;
            if (entered.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Leave();
                JoinFailed?.Invoke("That world is full or no longer open.");
                return;
            }
            string version = SteamMatchmaking.GetLobbyData(Current, KeyVersion);
            if (SteamMatchmaking.GetLobbyData(Current, KeyGame) != "1" || !ulong.TryParse(SteamMatchmaking.GetLobbyData(Current, KeyHost), out ulong host))
            {
                Leave();
                JoinFailed?.Invoke("That is not a Chopper world.");
                return;
            }
            if (!string.IsNullOrEmpty(version) && version != Application.version)
            {
                Leave();
                JoinFailed?.Invoke($"The host plays version {version}; you have {Application.version}. Both need the same version.");
                return;
            }
            HostFound?.Invoke(host);
        }

        public static void Leave()
        {
            if (!SteamPlatform.Initialized) return;
            if (InLobby) SteamMatchmaking.LeaveLobby(Current);
            Current = CSteamID.Nil;
            _hosting = false;
            Code = string.Empty;
            SteamFriends.ClearRichPresence();
        }

        // ---------- Friends ----------

        // Steam friends who are in a Chopper world right now (playing this app and sitting in a lobby).
        public static List<FriendWorld> FriendsInWorlds()
        {
            var list = new List<FriendWorld>();
            if (!SteamPlatform.Initialized) return list;
            uint app = SteamUtils.GetAppID().m_AppId;
            int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            for (int i = 0; i < count; i++)
            {
                CSteamID friend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                if (!SteamFriends.GetFriendGamePlayed(friend, out FriendGameInfo_t game)) continue;
                if (game.m_gameID.AppID().m_AppId != app || !game.m_steamIDLobby.IsValid()) continue;
                // Lobby data arrives a moment after asking; until then the world shows without its name.
                SteamMatchmaking.RequestLobbyData(game.m_steamIDLobby);
                string world = SteamMatchmaking.GetLobbyData(game.m_steamIDLobby, KeyName);
                list.Add(new FriendWorld
                {
                    Friend = friend.m_SteamID,
                    FriendName = SteamFriends.GetFriendPersonaName(friend),
                    Lobby = game.m_steamIDLobby.m_SteamID,
                    WorldName = string.IsNullOrEmpty(world) ? "a world" : world
                });
            }
            return list;
        }
    }
}
