using System;
using System.Runtime.InteropServices;
using Steamworks;
using UnityEngine;

namespace Chopper.Networking
{
    // Steam for playing together without IP addresses or ports: players connect through Valve's relay network by
    // Steam ID. Peer to peer ICE (direct connections) is switched off, so nobody ever sees another player's IP address;
    // all game traffic goes through Steam's relays. Development uses Valve's public test app id (480, steam_appid.txt);
    // the store release gets Chopper's own id.
    public static class SteamPlatform
    {
        public static bool Initialized { get; private set; }
        public static string Problem { get; private set; } = string.Empty;

        public static ulong LocalId => Initialized ? SteamUser.GetSteamID().m_SteamID : 0;
        public static string PersonaName => Initialized ? SteamFriends.GetPersonaName() : string.Empty;

        // Starts Steam for this session. False (with Problem set) when Steam is not running or the library is missing.
        public static bool TryInit()
        {
            if (Initialized) return true;
            try
            {
                if (!Packsize.Test() || !DllCheck.Test())
                {
                    Problem = "The Steam library does not match this build.";
                    return false;
                }
                Initialized = SteamAPI.Init();
                if (!Initialized)
                {
                    Problem = "Steam is not running. Start Steam, then Chopper, to play with friends.";
                    return false;
                }
                RelayOnly();
                Debug.Log($"[Steam] Ready as {PersonaName} (app {SteamUtils.GetAppID().m_AppId}), relay only.");
                return true;
            }
            catch (Exception e)
            {
                Problem = "Steam could not start: " + e.Message;
                Initialized = false;
                return false;
            }
        }

        // No direct peer to peer connections: traffic always goes through Steam Datagram Relay, which hides IPs.
        private static void RelayOnly()
        {
            IntPtr value = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                Marshal.WriteInt32(value, 0);
                SteamNetworkingUtils.SetConfigValue(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_P2P_Transport_ICE_Enable,
                    ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global, IntPtr.Zero,
                    ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32, value);
            }
            finally
            {
                Marshal.FreeHGlobal(value);
            }
            SteamNetworkingUtils.InitRelayNetworkAccess();
        }

        // Every frame, so Steam's callbacks (lobbies, invites) arrive.
        public static void Tick()
        {
            if (Initialized) SteamAPI.RunCallbacks();
        }

        public static void Shutdown()
        {
            if (!Initialized) return;
            SteamLobby.Leave();
            SteamAPI.Shutdown();
            Initialized = false;
        }
    }
}
