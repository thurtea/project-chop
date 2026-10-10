using System;
using Chopper.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Chopper.UI
{
    // The developer panel (F1): skip the clock, god mode, flying, free wood, spawn enemies. Host only; other players
    // just get a message. The console (F5) has the same cheats and more. Built in code like the other screens.
    public class DevPanelView
    {
        private const float PanelWidth = 720f;
        private const float PanelHeight = 690f;
        private static readonly Vector2 ButtonSize = new Vector2(300f, 50f);

        private RectTransform _dim;
        private Button _godButton;
        private Button _flyButton;

        public RectTransform Root => _dim;

        public bool Visible
        {
            get => _dim != null && _dim.gameObject.activeSelf;
            set
            {
                if (_dim == null) return;
                _dim.gameObject.SetActive(value);
                if (value) RefreshGod();
            }
        }

        public void Build(Transform parent, Action onClose)
        {
            Image dim = UiKit.Box(parent, "DevDim", new Color(0f, 0f, 0f, 0.6f));
            dim.raycastTarget = true;
            UiKit.Stretch(dim.rectTransform);
            _dim = dim.rectTransform;

            RectTransform panel = UiKit.Panel(_dim, "Developer tools", new Vector2(PanelWidth, PanelHeight));
            UiKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, panel.sizeDelta);
            Text heading = UiKit.Heading(panel, "Developer tools", 34, TextAnchor.MiddleCenter, UiKit.Accent);
            UiKit.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(PanelWidth, 44f));
            Text note = UiKit.Label(panel, "Host only. Changes the world for everyone.", 18, TextAnchor.MiddleCenter, UiKit.Dim);
            UiKit.Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(PanelWidth, 26f));

            (string label, DevCommand command, int value)[] buttons =
            {
                ("Skip to night", DevCommand.ToNight, 0),
                ("Skip to day", DevCommand.ToDay, 0),
                ("Next phase", DevCommand.NextPhase, 0),
                ("God mode", DevCommand.GodMode, 1),
                ("+100 wood", DevCommand.GiveWood, 100),
                ("Give axe", DevCommand.GiveAxe, 0),
                ("Heal fully", DevCommand.HealFull, 0),
                ("Repair the hut", DevCommand.RepairHut, 0),
                ("Spawn 8 enemies", DevCommand.SpawnEnemies, 8),
                ("Spawn a boss", DevCommand.SpawnBoss, 0),
                ("Random boon", DevCommand.RandomBoon, 0),
                ("Fly", DevCommand.Fly, 1)
            };
            for (int i = 0; i < buttons.Length; i++)
            {
                (string label, DevCommand command, int value) b = buttons[i];
                Button button = UiKit.MakeButton(panel, b.label, ButtonSize, () => Run(b.command, b.value), 20);
                int column = i % 2;
                int row = i / 2;
                UiKit.Place(button.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2((column == 0 ? -1f : 1f) * 165f, -112f - row * 62f), ButtonSize);
                if (b.command == DevCommand.GodMode) _godButton = button;
                if (b.command == DevCommand.Fly) _flyButton = button;
            }

            Button close = UiKit.MakeButton(panel, "Close", ButtonSize, () => onClose?.Invoke(), 20);
            UiKit.Place(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), ButtonSize);
            _dim.gameObject.SetActive(false);
        }

        private void Run(DevCommand command, int value)
        {
            PlayerWood local = PlayerWood.Local;
            if (local == null) return;
            var tools = local.GetComponent<PlayerDevTools>();
            if (tools == null) return;
            if (!tools.IsHostPlayer)
            {
                Toasts.Show("Developer tools only work for the host.", new Color(0.9f, 0.6f, 0.5f));
                return;
            }

            if (command == DevCommand.GodMode)
            {
                var combat = local.GetComponent<PlayerCombat>();
                bool next = combat != null && !combat.God;
                tools.Request(command, next ? 1 : 0);
                SetGodLabel(next);
                return;
            }
            if (command == DevCommand.Fly)
            {
                bool next = !tools.Flying;
                tools.Request(command, next ? 1 : 0);
                SetFlyLabel(next);
                return;
            }
            tools.Request(command, value);
        }

        private void RefreshGod()
        {
            PlayerWood local = PlayerWood.Local;
            var combat = local != null ? local.GetComponent<PlayerCombat>() : null;
            SetGodLabel(combat != null && combat.God);
            var tools = local != null ? local.GetComponent<PlayerDevTools>() : null;
            SetFlyLabel(tools != null && tools.Flying);
        }

        private void SetFlyLabel(bool on)
        {
            if (_flyButton != null) UiKit.SetButtonText(_flyButton, on ? "Fly: ON" : "Fly: off");
        }

        private void SetGodLabel(bool on)
        {
            if (_godButton != null) UiKit.SetButtonText(_godButton, on ? "God mode: ON" : "God mode: off");
        }
    }
}
