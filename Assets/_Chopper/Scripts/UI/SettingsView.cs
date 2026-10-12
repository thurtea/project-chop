using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

namespace Chopper.UI
{
    // The settings panel shared by the title screen and the pause menu: four tabs.
    //   General           volume, field of view, display mode, ambient particles
    //   Graphics          render scale, anti aliasing, shadow quality, vsync, pixel look
    //   Keyboard and mouse  mouse sensitivity and every action rebindable
    //   Gamepad           stick sensitivity, invert, and every action rebindable on the pad
    // Built in code like the rest of the interface. Rebinding listens for the next key, mouse button or pad button.
    public class SettingsView
    {
        public enum Tab { General, Graphics, Keyboard, Gamepad }

        private const float PanelWidth = 1160f;
        private const float PanelHeight = 840f;
        private const float PageWidth = 960f;
        private const float PageHeight = 590f;
        private const float ColumnWidth = 460f;
        private const float ColumnGap = 40f;
        private const float BindRowStep = 36f;
        private const float BindRowHeight = 33f;
        private const float WideColumn = 760f;
        // Every action fits in two columns, however many there are.
        private static int BindsPerColumn => (Keybinds.Count + 1) / 2;
        private static readonly string[] TabNames = { "General", "Graphics", "Keyboard and mouse", "Gamepad" };

        private RectTransform _root;
        private readonly RectTransform[] _pages = new RectTransform[4];
        private readonly Button[] _tabs = new Button[4];
        private readonly List<(KeyAction action, bool pad, Button button)> _bindButtons = new List<(KeyAction, bool, Button)>();
        private Text _padStatus;
        private bool _listening;
        private Tab _current;
        private static int _listenEndedFrame = -10;

        public RectTransform Root => _root;

        // True while waiting for a key, and for a frame after, so the Esc that cancels does not also close the menu.
        public static bool RebindActive { get; private set; }
        public static bool RebindRecent => RebindActive || Time.frameCount - _listenEndedFrame <= 1;

        public static SettingsView Build(Transform parent, Action onBack)
        {
            var view = new SettingsView();
            view.BuildPanel(parent, onBack);
            return view;
        }

        // Moves to the next or previous tab (the gamepad bumpers), keeping the selection on the tab button.
        public void StepTab(int delta)
        {
            int count = _pages.Length;
            int next = (((int)_current + delta) % count + count) % count;
            ShowTab((Tab)next);
            if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(_tabs[next].gameObject);
        }

        public void ShowTab(Tab tab)
        {
            _current = tab;
            for (int i = 0; i < _pages.Length; i++)
            {
                _pages[i].gameObject.SetActive(i == (int)tab);
                UiKit.SetTabActive(_tabs[i], i == (int)tab);
            }
            if (tab == Tab.Gamepad) RefreshPadStatus();
            RefreshBinds();
            LinkTabs();
        }

        // Left and right move along the tabs; down from any tab goes to the topmost control of the open page.
        private void LinkTabs()
        {
            Selectable first = null;
            float bestY = float.MinValue;
            foreach (Selectable candidate in _pages[(int)_current].GetComponentsInChildren<Selectable>())
            {
                if (!candidate.IsInteractable() || candidate.navigation.mode == Navigation.Mode.None) continue;
                float y = candidate.transform.position.y;
                if (y > bestY + 0.5f)
                {
                    bestY = y;
                    first = candidate;
                }
            }
            for (int i = 0; i < _tabs.Length; i++)
            {
                _tabs[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? _tabs[i - 1] : null,
                    selectOnRight = i < _tabs.Length - 1 ? _tabs[i + 1] : null,
                    selectOnDown = first
                };
            }
        }

        private void BuildPanel(Transform parent, Action onBack)
        {
            _root = UiKit.Panel(parent, "Settings", new Vector2(PanelWidth, PanelHeight));
            UiKit.Place(_root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, _root.sizeDelta);

            Text title = UiKit.Heading(_root, "Settings", 38, TextAnchor.MiddleCenter, UiKit.Accent);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(PanelWidth, 52f));

            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                Button tab = UiKit.MakeTab(_root, TabNames[i], new Vector2(280f, 52f), () => ShowTab((Tab)index));
                UiKit.Place(tab.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2((i - 1.5f) * 285f, -84f), new Vector2(280f, 52f));
                _tabs[i] = tab;
            }
            RectTransform rule = UiKit.Divider(_root, PageWidth);
            UiKit.Place(rule, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -142f), rule.sizeDelta);

            for (int i = 0; i < _pages.Length; i++)
            {
                RectTransform page = UiKit.Rect(_root, "Page_" + TabNames[i]);
                UiKit.Place(page, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -154f), new Vector2(PageWidth, PageHeight));
                _pages[i] = page;
            }
            BuildGeneral(_pages[0]);
            BuildGraphics(_pages[1]);
            BuildKeyboard(_pages[2]);
            BuildGamepad(_pages[3]);

            Button back = UiKit.MakeButton(_root, "Back", new Vector2(300f, 52f), onBack, 24);
            UiKit.Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(300f, 52f));

            // Controller hints, shown only while a pad is connected, and the bumper tab switching.
            Text padHint = UiKit.Label(_root, string.Empty, 16, TextAnchor.MiddleLeft, UiKit.Dim);
            UiKit.Place(padHint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 36f), new Vector2(420f, 30f));
            _root.gameObject.AddComponent<SettingsPager>().Init(this, padHint);
            ShowTab(Tab.General);
        }

        // ---------- General ----------

        private void BuildGeneral(RectTransform page)
        {
            float x = (PageWidth - WideColumn) * 0.5f;
            Slider master = SliderRow(page, "Master volume", 0f, 1f, UserSettings.Volume, v => UserSettings.Volume = v, "P0", x, 0f).GetComponentInChildren<Slider>();
            Slider effects = SliderRow(page, "Effects volume", 0f, 1f, UserSettings.EffectsVolume, v => UserSettings.EffectsVolume = v, "P0", x, -76f).GetComponentInChildren<Slider>();
            Slider music = SliderRow(page, "Music volume", 0f, 1f, UserSettings.MusicVolume, v => UserSettings.MusicVolume = v, "P0", x, -152f).GetComponentInChildren<Slider>();
            Slider fov = SliderRow(page, "Field of view", UserSettings.MinFov, UserSettings.MaxFov, UserSettings.FieldOfView, v => UserSettings.FieldOfView = v, "0", x, -228f).GetComponentInChildren<Slider>();
            ToggleRow(page, "Display", () => UserSettings.Fullscreen ? "Fullscreen" : "Windowed", () => UserSettings.Fullscreen = !UserSettings.Fullscreen, x, -316f);
            ToggleRow(page, "Ambient particles", () => UserSettings.AmbientParticles ? "On" : "Off", () => UserSettings.AmbientParticles = !UserSettings.AmbientParticles, x, -376f);
            Slider opacity = SliderRow(page, "Window opacity", UserSettings.MinMenuOpacity, 1f, UserSettings.MenuOpacity, v => UserSettings.MenuOpacity = v, "P0", x, -436f).GetComponentInChildren<Slider>();
            // Saved settings override the defaults, so this puts the sound and view back to the recommended values.
            Button reset = UiKit.MakeButton(page, "Reset to defaults", new Vector2(340f, 44f), () =>
            {
                master.value = UserSettings.DefaultVolume;
                effects.value = UserSettings.DefaultEffectsVolume;
                music.value = UserSettings.DefaultMusicVolume;
                fov.value = UserSettings.DefaultFov;
                opacity.value = UserSettings.DefaultMenuOpacity;
            }, 20);
            UiKit.Place(reset.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -526f), new Vector2(340f, 44f));
        }

        // ---------- Graphics ----------

        private void BuildGraphics(RectTransform page)
        {
            float x = (PageWidth - WideColumn) * 0.5f;
            SliderRow(page, "Render scale", UserSettings.MinRenderScale, UserSettings.MaxRenderScale, UserSettings.RenderScale, v => UserSettings.RenderScale = Mathf.Round(v * 20f) / 20f, "P0", x, 0f);
            Hint(page, "100% is the screen's own resolution. Above it the world is drawn larger and scaled down: the crispest picture, and the heaviest.", -78f);
            ToggleRow(page, "Anti-aliasing", () => UserSettings.AntiAliasingNames[UserSettings.AntiAliasing],
                () => UserSettings.AntiAliasing = (UserSettings.AntiAliasing + 1) % UserSettings.AntiAliasingNames.Length, x, -118f);
            ToggleRow(page, "Shadow quality", () => UserSettings.ShadowQualityNames[UserSettings.ShadowQuality],
                () => UserSettings.ShadowQuality = (UserSettings.ShadowQuality + 1) % UserSettings.ShadowQualityNames.Length, x, -184f);
            ToggleRow(page, "Vertical sync", () => UserSettings.VSync ? "On" : "Off", () => UserSettings.VSync = !UserSettings.VSync, x, -250f);
            ToggleRow(page, "Frame rate cap", () => UserSettings.FrameCapNames[UserSettings.FrameCap],
                () => UserSettings.FrameCap = (UserSettings.FrameCap + 1) % UserSettings.FrameCapNames.Length, x, -316f);
            ToggleRow(page, "Look", () => UserSettings.PixelLook ? "Pixel (low resolution)" : "Smooth (full resolution)", () => UserSettings.PixelLook = !UserSettings.PixelLook, x, -382f);
            Hint(page, "Click an option to step to the next value. Pixel look ignores the render scale. For smooth recordings, cap the frame rate at the recording's rate (60).", -446f);
        }

        // ---------- Keyboard and mouse ----------

        private void BuildKeyboard(RectTransform page)
        {
            float x = (PageWidth - WideColumn) * 0.5f;
            SliderRow(page, "Mouse sensitivity", UserSettings.MinSensitivity, UserSettings.MaxSensitivity, UserSettings.Sensitivity, v => UserSettings.Sensitivity = v, "0.00", x, 0f);
            Hint(page, "Click a control, then press a key or mouse button. Esc cancels.", -86f);
            BindColumns(page, false, -120f);
            Hint(page, "Move: WASD or arrows. Look: mouse. Zoom: scroll wheel. Menu: Esc.", -452f);
            Hint(page, "Jump again in the air to double jump. Hold Block and press Jump to dodge.", -478f);
            Button reset = UiKit.MakeButton(page, "Reset to defaults", new Vector2(340f, 44f), () => { Keybinds.ResetAll(); RefreshBinds(); }, 20);
            UiKit.Place(reset.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -516f), new Vector2(340f, 44f));
        }

        // ---------- Gamepad ----------

        private void BuildGamepad(RectTransform page)
        {
            float x = (PageWidth - WideColumn) * 0.5f;
            _padStatus = UiKit.Label(page, string.Empty, 20, TextAnchor.MiddleCenter, UiKit.Accent);
            UiKit.Place(_padStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(PageWidth, 28f));

            SliderRow(page, "Look sensitivity", UserSettings.MinSensitivity, UserSettings.MaxSensitivity, UserSettings.PadSensitivity, v => UserSettings.PadSensitivity = v, "0.00", x, -40f);
            ToggleRow(page, "Invert look up and down", () => UserSettings.InvertPadY ? "On" : "Off", () => UserSettings.InvertPadY = !UserSettings.InvertPadY, x, -122f);
            Hint(page, "Click a control, then press a button on the pad. Start (Options) or Esc cancels.", -170f);
            BindColumns(page, true, -200f);
            Hint(page, "Move: left stick. Look: right stick. Hotbar: D-pad left or right, then up. Zoom: D-pad down. Menu: Start.", -526f);
            Button reset = UiKit.MakeButton(page, "Reset to defaults", new Vector2(340f, 44f), () => { Keybinds.ResetAllPad(); RefreshBinds(); }, 20);
            UiKit.Place(reset.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -556f), new Vector2(340f, 44f));
        }

        private void RefreshPadStatus()
        {
            if (_padStatus == null) return;
            Gamepad pad = Gamepad.current;
            _padStatus.text = pad != null
                ? "Controller detected: " + pad.displayName
                : "No controller detected. Plug one in; you can still set the buttons below.";
            _padStatus.color = pad != null ? UiKit.Accent : UiKit.Dim;
        }

        // ---------- Rows ----------

        private static void Hint(RectTransform page, string text, float y)
        {
            Text hint = UiKit.Label(page, text, 17, TextAnchor.MiddleCenter, UiKit.Dim);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(PageWidth, 26f));
        }

        // The actions in two columns.
        private void BindColumns(RectTransform page, bool pad, float top)
        {
            float left = (PageWidth - 2f * ColumnWidth - ColumnGap) * 0.5f;
            for (int i = 0; i < Keybinds.Count; i++)
            {
                var action = (KeyAction)i;
                int column = i / BindsPerColumn;
                int row = i % BindsPerColumn;
                float x = left + column * (ColumnWidth + ColumnGap);
                float y = top - row * BindRowStep;

                RectTransform line = UiKit.Rect(page, "Bind_" + action + (pad ? "_Pad" : "_Key"));
                UiKit.Place(line, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(ColumnWidth, BindRowHeight));
                Text name = UiKit.Label(line, Keybinds.Label(action), 18, TextAnchor.MiddleLeft, UiKit.Ink);
                UiKit.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(250f, BindRowHeight));
                Button button = UiKit.MakeFieldButton(line, string.Empty, new Vector2(200f, BindRowHeight), () => BeginRebind(action, pad), 17);
                UiKit.Place(button.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(200f, BindRowHeight));
                // On a pad the hotbar items are picked with the D-pad, so their buttons only say so.
                if (pad && PadHotbar.OnHotbar(action)) button.interactable = false;
                UiKit.HighlightRow(line, button, name);
                _bindButtons.Add((action, pad, button));
            }
        }

        internal static RectTransform SliderRow(Transform parent, string label, float min, float max, float value, Action<float> onChange, string format, float x, float y)
        {
            RectTransform row = UiKit.Rect(parent, "Row_" + label);
            UiKit.Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(WideColumn, 70f));

            Text name = UiKit.Label(row, label, 20, TextAnchor.MiddleLeft, UiKit.Ink);
            UiKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(500f, 30f));
            Text valueText = UiKit.Label(row, value.ToString(format), 20, TextAnchor.MiddleRight, UiKit.Accent);
            UiKit.Place(valueText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(200f, 30f));

            Slider slider = UiKit.MakeSlider(row, min, max, value, v =>
            {
                onChange(v);
                valueText.text = v.ToString(format);
            }, new Vector2(WideColumn, 30f));
            UiKit.Place(slider.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(WideColumn, 30f));
            UiKit.HighlightRow(row, slider, name);
            return row;
        }

        // A label on the left and a button on the right that shows the current value and flips it on click.
        internal static RectTransform ToggleRow(Transform parent, string label, Func<string> valueText, Action flip, float x, float y)
        {
            RectTransform row = UiKit.Rect(parent, "Row_" + label);
            UiKit.Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(WideColumn, 50f));

            Text name = UiKit.Label(row, label, 20, TextAnchor.MiddleLeft, UiKit.Ink);
            UiKit.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(360f, 44f));
            Button button = null;
            button = UiKit.MakeFieldButton(row, valueText(), new Vector2(360f, 44f), () =>
            {
                flip();
                UiKit.SetButtonText(button, valueText());
            }, 19);
            UiKit.Place(button.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(360f, 44f));
            UiKit.HighlightRow(row, button, name);
            return row;
        }

        // ---------- Rebinding ----------

        private void RefreshBinds()
        {
            foreach ((KeyAction action, bool pad, Button button) in _bindButtons)
            {
                UiKit.SetButtonText(button, pad ? Keybinds.PadNameOf(action) : Keybinds.NameOf(action));
            }
        }

        private void EndListening()
        {
            _listening = false;
            RebindActive = false;
            _listenEndedFrame = Time.frameCount;
        }

        private void BeginRebind(KeyAction action, bool pad)
        {
            if (_listening) return;
            _listening = true;
            RebindActive = true;
            foreach ((KeyAction a, bool p, Button button) in _bindButtons)
            {
                if (a == action && p == pad) UiKit.SetButtonText(button, pad ? "Press a button..." : "Press a key...");
            }
            ListenOnce(action, pad);
        }

        private void ListenOnce(KeyAction action, bool pad)
        {
            InputSystem.onAnyButtonPress.CallOnce(control =>
            {
                bool cancel = control.path == "/Keyboard/escape" || (control.device is Gamepad g && control == g.startButton);
                if (cancel)
                {
                    EndListening();
                    RefreshBinds();
                    return;
                }
                if (pad ? !Keybinds.PadBindable(control) : !Keybinds.Bindable(control))
                {
                    ListenOnce(action, pad);
                    return;
                }
                if (pad) Keybinds.SetPad(action, Keybinds.ToBindingPath(control));
                else Keybinds.Set(action, Keybinds.ToBindingPath(control));
                EndListening();
                RefreshBinds();
            });
        }
    }
}
