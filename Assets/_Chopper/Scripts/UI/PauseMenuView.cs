using System;
using Chopper.Networking;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chopper.UI
{
    // The Esc menu: Resume, Settings, Save game, How to play, Save and quit to title, Save and quit game. Settings is the
    // same tabbed SettingsView the title screen uses (the controls are its keyboard and gamepad tabs). While someone asks
    // to join, a small panel above it lets the host answer with buttons (the way a pad answers; Y and N still work).
    public class PauseMenuView
    {
        private const float PanelWidth = 520f;
        private static readonly Vector2 ButtonSize = new Vector2(380f, 56f);

        private RectTransform _dim;
        private RectTransform _main;
        private SettingsView _settings;
        private Action _onResume;

        public RectTransform Root => _dim;

        public bool Visible
        {
            get => _dim != null && _dim.gameObject.activeSelf;
            set
            {
                if (_dim == null) return;
                _dim.gameObject.SetActive(value);
                if (value) ShowMain();
            }
        }

        public void Build(Transform parent, Action onResume)
        {
            _onResume = onResume;

            Image dim = UiKit.Box(parent, "PauseDim", new Color(0f, 0f, 0f, 0.62f));
            dim.raycastTarget = true;
            UiKit.Stretch(dim.rectTransform);
            _dim = dim.rectTransform;

            BuildMain();
            BuildJoin();
            _settings = SettingsView.Build(_dim, ShowMain);
            _settings.Root.gameObject.SetActive(false);
            _dim.gameObject.SetActive(false);
        }

        private void BuildMain()
        {
            _main = UiKit.Panel(_dim, "Chopper", new Vector2(PanelWidth, 750f));
            UiKit.Place(_main, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, _main.sizeDelta);
            Text heading = UiKit.Heading(_main, "Chopper", 40, TextAnchor.MiddleCenter, UiKit.Accent);
            UiKit.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(PanelWidth, 56f));
            RectTransform rule = UiKit.Divider(_main, PanelWidth - 80f);
            UiKit.Place(rule, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), rule.sizeDelta);

            var rows = new[]
            {
                Row("Resume", () => _onResume?.Invoke()),
                Row("Settings", () => ShowSettings(SettingsView.Tab.General)),
                Row("Save game", SaveGame),
                Row("Copy join code", CopyJoinCode),
                Row("Invite friends", InviteFriends),
                Row("How to play", UiState.RequestHowToPlay),
                Row("Save and quit to title", Disconnect),
                Row("Save and quit game", Quit)
            };
            UiKit.Stack(_main, rows, 14f, 112f);

            // The world seed, so players can share a world they liked (type it into New Game).
            _seedLabel = UiKit.Label(_main, string.Empty, 17, TextAnchor.MiddleCenter, UiKit.Dim);
            UiKit.Place(_seedLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(PanelWidth, 26f));
        }

        private Text _seedLabel;
        private RectTransform _join;
        private Text _joinTitle;
        private Button _letIn;
        private bool _joinShown;

        private void BuildJoin()
        {
            _join = UiKit.Panel(_dim, "JoinAnswer", new Vector2(PanelWidth, 150f));
            UiKit.Place(_join, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, 390f), _join.sizeDelta);
            _joinTitle = UiKit.Heading(_join, string.Empty, 24, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Place(_joinTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(PanelWidth - 20f, 36f));
            _letIn = UiKit.MakeButton(_join, "Let in", new Vector2(200f, 52f), () => JoinApproval.AnswerFirst(true), 22);
            UiKit.Place(_letIn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-110f, 20f), new Vector2(200f, 52f));
            Button away = UiKit.MakeButton(_join, "Turn away", new Vector2(200f, 52f), () => JoinApproval.AnswerFirst(false), 22);
            UiKit.Place(away.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(110f, 20f), new Vector2(200f, 52f));
            _join.gameObject.SetActive(false);
        }

        // Once a frame while the menu is open: the join panel follows the waiting list. When it appears, Let in takes
        // the pad focus; when the last request is answered the focus goes back to the menu.
        public void Refresh()
        {
            if (!Visible || _join == null) return;
            var pending = JoinApproval.Pending;
            bool show = pending.Count > 0 && !InSettings;
            if (show) _joinTitle.text = pending.Count > 1 ? $"{pending[0].Name} wants to join (+{pending.Count - 1})" : $"{pending[0].Name} wants to join";
            if (show == _joinShown) return;
            _joinShown = show;
            _join.gameObject.SetActive(show);
            if (EventSystem.current == null) return;
            if (show) EventSystem.current.SetSelectedGameObject(_letIn.gameObject);
            else if (!InSettings) Focus(_main);
        }

        private RectTransform Row(string text, Action onClick)
        {
            return UiKit.MakeButton(_main, text, ButtonSize, onClick, 24).GetComponent<RectTransform>();
        }

        // Screenshots: opens the settings on a tab.
        public void DebugShowSettings(SettingsView.Tab tab) => ShowSettings(tab);

        // True while the settings are showing, so B can step back to this menu instead of closing everything.
        public bool InSettings => _settings != null && _settings.Root.gameObject.activeSelf;

        public void Back() => ShowMain();

        private void ShowMain()
        {
            _main.gameObject.SetActive(true);
            if (_seedLabel != null)
            {
                string code = SteamLobby.Code;
                _seedLabel.text = "World seed  " + Chopper.World.Generation.WorldSession.Seed + (string.IsNullOrEmpty(code) ? string.Empty : "      Join code  " + code);
            }
            _settings.Root.gameObject.SetActive(false);
            Focus(_main);
        }

        private void ShowSettings(SettingsView.Tab tab)
        {
            _main.gameObject.SetActive(false);
            _settings.Root.gameObject.SetActive(true);
            _settings.ShowTab(tab);
            Focus(_settings.Root);
        }

        // Puts the gamepad selection on the first control of the panel that just appeared.
        private static void Focus(RectTransform panel)
        {
            if (EventSystem.current == null || panel == null) return;
            Selectable first = panel.GetComponentInChildren<Selectable>();
            EventSystem.current.SetSelectedGameObject(first != null ? first.gameObject : null);
        }

        // The host saves the world; a joining player's things are kept in the host's save.
        private static void SaveGame()
        {
            Chopper.World.Saving.WorldSaver saver = Chopper.World.Saving.WorldSaver.Instance;
            bool host = saver != null && saver.IsServerInitialized;
            if (!host) Toasts.Show("The host saves the world", UiKit.Dim);
            else if (!Chopper.World.Saving.SaveSystem.Saving) Toasts.Show("Not saved (test game)", UiKit.Dim);
            else if (saver.ServerSave()) Toasts.Show("Saved", UiKit.Accent);
            else Toasts.Show("Could not save", new Color(1f, 0.5f, 0.45f));
        }

        // Puts the join code on the clipboard, to paste into a chat with friends.
        private static void CopyJoinCode()
        {
            string code = SteamLobby.Code;
            if (string.IsNullOrEmpty(code))
            {
                Toasts.Show(NetworkBootstrap.UsingSteam ? "Host only" : "Needs Steam", UiKit.Dim);
                return;
            }
            GUIUtility.systemCopyBuffer = code;
            Toasts.Show("Copied " + code, UiKit.Accent);
        }

        // Steam's invite window: friends get an invite and join through Steam, no addresses involved.
        private static void InviteFriends()
        {
            if (!NetworkBootstrap.UsingSteam) Toasts.Show("Needs Steam", UiKit.Dim);
            else if (!SteamLobby.OpenInviteDialog()) Toasts.Show("Host only", UiKit.Dim);
        }

        private static void SaveIfHost()
        {
            Chopper.World.Saving.WorldSaver saver = Chopper.World.Saving.WorldSaver.Instance;
            if (saver != null && saver.IsServerInitialized) saver.ServerSave();
        }

        private static void Disconnect()
        {
            SaveIfHost();
            UiState.CloseAll();
            BootMenu boot = UnityEngine.Object.FindAnyObjectByType<BootMenu>();
            if (boot != null) boot.Disconnect();
        }

        private static void Quit()
        {
            SaveIfHost();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
