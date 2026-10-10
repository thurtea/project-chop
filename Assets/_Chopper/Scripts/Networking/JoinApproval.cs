using System;
using System.Collections.Generic;
using FishNet.Authenticating;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Chopper.Networking
{
    // The host lets each player in. A joining client connects (by join code, invite or the friends list), says who it
    // is, and waits; the host sees "Name wants to join" and lets them in (Y) or turns them away (N). An unanswered
    // request is turned away after a minute. The host's own client and, without Steam (development, LAN and the
    // automated tests), every client are let in at once.
    public class JoinApproval : Authenticator
    {
        public struct Request
        {
            public NetworkConnection Connection;
            public string Name;
            public float Expires;
        }

        private const float AnswerSeconds = 60f;

        public override event Action<NetworkConnection, bool> OnAuthenticationResult;

        private static readonly List<Request> _pending = new List<Request>();
        // Turned away players and when their connection closes: a moment after the reason is sent, so it arrives.
        private static readonly List<(NetworkConnection connection, float at)> _closing = new List<(NetworkConnection, float)>();
        private const float CloseDelay = 0.75f;
        private static string _hostToken = string.Empty;

        public static JoinApproval Instance { get; private set; }
        public static IReadOnlyList<Request> Pending => _pending;
        // Why this client was turned away, for the start screen after the connection closes.
        public static string LastDenial { get; private set; }

        // Steam games ask the host; -chopperAutoApprove lets everyone in (for testing with a friend on Steam).
        // Tests: -chopperRequireApproval asks even without Steam, and -chopperTestAnswer yes|no makes the host answer
        // by itself two seconds after a request.
        public static bool RequireApproval =>
            (NetworkBootstrap.UsingSteam || HasArg("-chopperRequireApproval")) && !HasArg("-chopperAutoApprove");

        private static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        private static string TestAnswer()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-chopperTestAnswer");
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        public override void InitializeOnce(NetworkManager networkManager)
        {
            base.InitializeOnce(networkManager);
            Instance = this;
            networkManager.ServerManager.OnServerConnectionState += OnServerState;
            networkManager.ServerManager.OnRemoteConnectionState += OnRemoteState;
            networkManager.ServerManager.RegisterBroadcast<JoinRequestBroadcast>(OnRequest, false);
            networkManager.ClientManager.OnClientConnectionState += OnClientState;
            networkManager.ClientManager.RegisterBroadcast<JoinDeniedBroadcast>(OnDenied);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- Server ----------

        private void OnServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started) _hostToken = Guid.NewGuid().ToString("N");
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                _hostToken = string.Empty;
                _pending.Clear();
                _closing.Clear();
            }
        }

        private void OnRemoteState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped) _pending.RemoveAll(r => r.Connection == connection);
        }

        private void OnRequest(NetworkConnection connection, JoinRequestBroadcast request, Channel channel)
        {
            if (connection.IsAuthenticated) return;
            bool isHost = !string.IsNullOrEmpty(_hostToken) && request.HostToken == _hostToken;
            if (isHost || !RequireApproval)
            {
                Pass(connection);
                return;
            }
            if (_pending.Exists(r => r.Connection == connection)) return;
            string name = string.IsNullOrWhiteSpace(request.Name) ? "A player" : request.Name.Trim();
            if (name.Length > 32) name = name.Substring(0, 32);
            _pending.Add(new Request { Connection = connection, Name = name, Expires = Time.unscaledTime + AnswerSeconds });
            Debug.Log($"[Join] {name} asks to join (connection {connection.ClientId}).");
        }

        private void Update()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime >= _pending[i].Expires) Deny(_pending[i].Connection, "The host did not answer.");
            }
            for (int i = _closing.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < _closing[i].at) continue;
                if (_closing[i].connection.IsActive) _closing[i].connection.Disconnect(false);
                _closing.RemoveAt(i);
            }
            string answer = _pending.Count > 0 ? TestAnswer() : null;
            if (answer != null && Time.unscaledTime >= _pending[0].Expires - AnswerSeconds + 2f) AnswerFirst(answer == "yes");
        }

        // Host: lets the oldest waiting player in, or turns them away.
        public static void AnswerFirst(bool letIn)
        {
            if (Instance == null || _pending.Count == 0) return;
            NetworkConnection connection = _pending[0].Connection;
            if (letIn) Instance.Pass(connection);
            else Instance.Deny(connection, "The host turned you away.");
        }

        private void Pass(NetworkConnection connection)
        {
            _pending.RemoveAll(r => r.Connection == connection);
            OnAuthenticationResult?.Invoke(connection, true);
        }

        private void Deny(NetworkConnection connection, string reason)
        {
            _pending.RemoveAll(r => r.Connection == connection);
            Debug.Log($"[Join] Connection {connection.ClientId} turned away: {reason}");
            NetworkManager.ServerManager.Broadcast(connection, new JoinDeniedBroadcast { Reason = reason }, false);
            _closing.Add((connection, Time.unscaledTime + CloseDelay));
        }

        // ---------- Client ----------

        private void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started) return;
            LastDenial = null;
            NetworkManager.ClientManager.Broadcast(new JoinRequestBroadcast
            {
                Name = SteamPlatform.Initialized ? SteamPlatform.PersonaName : "A player",
                HostToken = NetworkManager.IsServerStarted ? _hostToken : string.Empty
            });
        }

        private void OnDenied(JoinDeniedBroadcast message, Channel channel)
        {
            LastDenial = message.Reason;
            Debug.Log($"[Join] Turned away by the host: {message.Reason}");
        }
    }
}
