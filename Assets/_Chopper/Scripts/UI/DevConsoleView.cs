using System.Collections.Generic;
using System.Text;
using Chopper.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Chopper.UI
{
    // The developer console: a dark band across the top of the screen with the recent output and a line to type in.
    // F5 or the key left of 1 opens and closes it, Esc closes it, Enter runs the line, up and down recall earlier
    // lines, Page Up and Page Down (or the mouse wheel) scroll the output. Built in code like the other screens; the
    // commands live in DevConsole.
    public class DevConsoleView
    {
        private const float Height = 500f;
        private const int VisibleLines = 18;
        private const int MaxHistory = 32;

        private readonly DevConsole _console;
        private readonly List<string> _history = new List<string>();
        private RectTransform _root;
        private Text _log;
        private InputField _input;
        private int _historyIndex;
        private int _shownVersion = -1;
        // Lines scrolled back from the newest.
        private int _scroll;

        public DevConsoleView(GameConfig config)
        {
            _console = new DevConsole(config);
            _console.Print("Developer console. Type help for the commands. Cheats are host only.");
        }

        public RectTransform Root => _root;

        public bool Visible
        {
            get => _root != null && _root.gameObject.activeSelf;
            set
            {
                if (_root == null) return;
                _root.gameObject.SetActive(value);
                if (!value) return;
                _input.text = string.Empty;
                _historyIndex = _history.Count;
                FocusInput();
            }
        }

        public void Build(Transform parent)
        {
            Image band = UiKit.Box(parent, "DevConsole", new Color(0.02f, 0.02f, 0.02f, 0.88f));
            band.raycastTarget = true;
            _root = band.rectTransform;
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(1f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = Vector2.zero;
            _root.sizeDelta = new Vector2(0f, Height);
            // Long output grows upward out of the log area; the mask cuts it off at the top of the band.
            _root.gameObject.AddComponent<RectMask2D>();

            _log = UiKit.Label(_root, string.Empty, 18, TextAnchor.LowerLeft, UiKit.Ink);
            _log.rectTransform.anchorMin = new Vector2(0f, 0f);
            _log.rectTransform.anchorMax = new Vector2(1f, 1f);
            _log.rectTransform.offsetMin = new Vector2(24f, 64f);
            _log.rectTransform.offsetMax = new Vector2(-24f, -12f);
            _log.horizontalOverflow = HorizontalWrapMode.Wrap;
            _log.verticalOverflow = VerticalWrapMode.Overflow;

            Image line = UiKit.Box(_root, "Line", new Color(1f, 0.9f, 0.75f, 0.08f));
            line.raycastTarget = true;
            line.rectTransform.anchorMin = new Vector2(0f, 0f);
            line.rectTransform.anchorMax = new Vector2(1f, 0f);
            line.rectTransform.pivot = new Vector2(0.5f, 0f);
            line.rectTransform.offsetMin = new Vector2(16f, 12f);
            line.rectTransform.offsetMax = new Vector2(-16f, 54f);
            Text value = UiKit.Label(line.transform, string.Empty, 20, TextAnchor.MiddleLeft, UiKit.Accent);
            UiKit.Stretch(value.rectTransform, 10f);
            value.supportRichText = false;

            _input = line.gameObject.AddComponent<InputField>();
            _input.targetGraphic = line;
            _input.textComponent = value;
            _input.lineType = InputField.LineType.SingleLine;
            _input.characterLimit = 120;
            // The keys that open the console must not end up in the line.
            _input.onValidateInput = (text, index, c) => c == '`' || c == '~' ? '\0' : c;
            _input.onEndEdit.AddListener(OnEndEdit);
            _root.gameObject.SetActive(false);
        }

        // Development and screenshots: runs a line as if it was typed.
        public void Run(string line) => _console.Run(line);

        private void OnEndEdit(string text)
        {
            Keyboard keyboard = Keyboard.current;
            bool enter = keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
            if (!enter || !Visible) return;
            if (!string.IsNullOrWhiteSpace(text))
            {
                _console.Run(text);
                if (_history.Count == 0 || _history[_history.Count - 1] != text) _history.Add(text);
                if (_history.Count > MaxHistory) _history.RemoveAt(0);
            }
            _historyIndex = _history.Count;
            _input.text = string.Empty;
            FocusInput();
        }

        private void FocusInput()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_input.gameObject);
            _input.ActivateInputField();
        }

        // Called every frame while the console is open.
        public void Refresh()
        {
            if (!Visible) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && _history.Count > 0)
            {
                int step = keyboard.upArrowKey.wasPressedThisFrame ? -1 : (keyboard.downArrowKey.wasPressedThisFrame ? 1 : 0);
                if (step != 0)
                {
                    _historyIndex = Mathf.Clamp(_historyIndex + step, 0, _history.Count);
                    _input.text = _historyIndex < _history.Count ? _history[_historyIndex] : string.Empty;
                    _input.caretPosition = _input.text.Length;
                }
            }
            if (!_input.isFocused) FocusInput();

            IReadOnlyList<string> output = _console.Output;
            int scroll = _scroll;
            if (keyboard != null && keyboard.pageUpKey.wasPressedThisFrame) scroll += VisibleLines / 2;
            if (keyboard != null && keyboard.pageDownKey.wasPressedThisFrame) scroll -= VisibleLines / 2;
            Mouse mouse = Mouse.current;
            float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            if (Mathf.Abs(wheel) > 0.01f) scroll += wheel > 0f ? 3 : -3;
            if (_console.Version != _shownVersion) scroll = 0;
            scroll = Mathf.Clamp(scroll, 0, Mathf.Max(0, output.Count - VisibleLines));
            if (_console.Version == _shownVersion && scroll == _scroll) return;
            _shownVersion = _console.Version;
            _scroll = scroll;

            var text = new StringBuilder();
            int end = output.Count - _scroll;
            for (int i = Mathf.Max(0, end - VisibleLines); i < end; i++) text.AppendLine(output[i]);
            _log.text = text.ToString();
        }
    }
}
