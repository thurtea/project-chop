using Chopper.Networking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Chopper.UI
{
    // Host only: "Name wants to join" at the top of the screen, with Y to let them in and N to turn them away, while
    // anyone is waiting (JoinApproval). On a pad it says to press Start: the pause menu then shows Let in and Turn away
    // buttons (PauseMenuView). Also shows the world's join code as a note once it exists.
    public class JoinRequestView
    {
        private const string KeyColor = "#F2C14E";
        private readonly RectTransform _root;
        private readonly Text _title;
        private readonly Text _keys;
        private string _announcedCode = string.Empty;
        private bool _padShown;

        public JoinRequestView(Transform canvas)
        {
            _root = UiKit.Panel(canvas, "JoinRequest", new Vector2(560f, 104f));
            UiKit.Place(_root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), _root.sizeDelta);
            _title = UiKit.Heading(_root, string.Empty, 26, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(540f, 36f));
            _keys = UiKit.Label(_root, $"<color={KeyColor}>[Y]</color> Let in      <color={KeyColor}>[N]</color> Turn away", 20, TextAnchor.MiddleCenter, UiKit.Ink);
            _keys.supportRichText = true;
            UiKit.Place(_keys.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(540f, 30f));
            _root.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (!string.IsNullOrEmpty(SteamLobby.Code) && SteamLobby.Code != _announcedCode)
            {
                _announcedCode = SteamLobby.Code;
                Toasts.Show("Join code  " + _announcedCode, UiKit.Accent);
            }

            var pending = JoinApproval.Pending;
            bool show = pending.Count > 0;
            // While the pause menu is open it shows its own Let in and Turn away buttons instead.
            bool panel = show && !UiState.PauseOpen;
            if (_root.gameObject.activeSelf != panel) _root.gameObject.SetActive(panel);
            if (!show) return;
            _title.text = pending.Count > 1 ? $"{pending[0].Name} wants to join (+{pending.Count - 1} waiting)" : $"{pending[0].Name} wants to join";
            bool pad = InputHints.UsingGamepad;
            if (pad != _padShown || _keys.text.Length == 0)
            {
                _padShown = pad;
                _keys.text = pad
                    ? $"<color={KeyColor}>[{Keybinds.PadDisplayName("<Gamepad>/start")}]</color> Answer"
                    : $"<color={KeyColor}>[Y]</color> Let in      <color={KeyColor}>[N]</color> Turn away";
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || SettingsView.RebindActive || UiState.ConsoleOpen) return;
            if (keyboard.yKey.wasPressedThisFrame) JoinApproval.AnswerFirst(true);
            else if (keyboard.nKey.wasPressedThisFrame) JoinApproval.AnswerFirst(false);
        }
    }
}
