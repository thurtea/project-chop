using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Chopper.UI
{
    // The row of control hints in the bottom right corner, like Valheim's: an action name followed by its key in a
    // small key cap ("Attack [Mouse 1]   Block [Mouse 2]   Dodge [Mouse 2] + [Space]"). It follows what is on screen
    // (playing, the inventory, the map) and the device in use, and only rebuilds when the text would change.
    public class ControlHintsView
    {
        private const float Gap = 30f;
        private const float CapPadding = 8f;
        private const float CapHeight = 30f;
        private const int FontSize = 18;
        private static readonly Color CapColor = new Color(0.05f, 0.05f, 0.05f, 0.55f);
        private static readonly Color CapLine = new Color(1f, 1f, 1f, 0.55f);

        private struct Hint
        {
            public string Label;
            public KeyAction[] Keys;
            public string Fixed;
        }

        private RectTransform _root;
        private string _shown;

        public void Build(Transform parent)
        {
            _root = UiKit.Rect(parent, "ControlHints");
            UiKit.Place(_root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, 22f), new Vector2(1400f, CapHeight));
        }

        public void Refresh()
        {
            List<Hint> hints = Current();
            var signature = new StringBuilder(InputHints.UsingGamepad ? "pad" : "keys");
            foreach (Hint hint in hints) signature.Append('|').Append(hint.Label).Append(':').Append(KeysText(hint));
            string text = signature.ToString();
            if (text == _shown) return;
            _shown = text;
            Rebuild(hints);
        }

        private static List<Hint> Current()
        {
            var hints = new List<Hint>();
            if (UiState.PauseOpen || UiState.ConsoleOpen || UiState.DevOpen || UiState.HowToPlayOpen) return hints;
            if (UiState.InventoryOpen)
            {
                hints.Add(new Hint { Label = "Pages", Fixed = InputHints.UsingGamepad ? "LB  RB" : "Q  E" });
                hints.Add(new Hint { Label = "Close", Keys = new[] { KeyAction.Inventory } });
                return hints;
            }
            if (UiState.MapOpen)
            {
                hints.Add(new Hint { Label = "Close", Keys = new[] { KeyAction.Map } });
                return hints;
            }
            // While playing there are no control hints at all: the keys are in the settings.
            return hints;
        }

        private static string KeyName(KeyAction action) => InputHints.Name(action);

        private static string KeysText(Hint hint)
        {
            if (hint.Keys == null) return hint.Fixed;
            var names = new List<string>();
            foreach (KeyAction key in hint.Keys) names.Add(KeyName(key));
            return string.Join("+", names);
        }

        // Lays the hints out from the right edge leftward.
        private void Rebuild(List<Hint> hints)
        {
            for (int i = _root.childCount - 1; i >= 0; i--) Object.Destroy(_root.GetChild(i).gameObject);
            float x = 0f;
            for (int h = hints.Count - 1; h >= 0; h--)
            {
                Hint hint = hints[h];
                var caps = new List<string>();
                if (hint.Keys == null) caps.Add(hint.Fixed);
                else foreach (KeyAction key in hint.Keys) caps.Add(KeyName(key));

                for (int c = caps.Count - 1; c >= 0; c--)
                {
                    x -= Cap(caps[c], x);
                    if (c > 0) x -= Word("+", x, UiKit.Ink) + 6f;
                    else x -= 8f;
                }
                x -= Word(hint.Label, x, UiKit.Ink);
                x -= Gap;
            }
        }

        // A key cap ending at x (measured from the right edge); returns its width.
        private float Cap(string key, float x)
        {
            Image cap = UiKit.Box(_root, "Cap_" + key, CapColor);
            Text label = UiKit.Label(cap.transform, key, FontSize, TextAnchor.MiddleCenter, UiKit.Ink, true);
            float width = Mathf.Max(CapHeight, label.preferredWidth + CapPadding * 2f);
            UiKit.Place(cap.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(width, CapHeight));
            UiKit.Stretch(label.rectTransform);
            UiKit.Frame(cap.rectTransform, 0f, 1.5f, CapLine);
            return width;
        }

        // A plain word ending at x; returns its width.
        private float Word(string text, float x, Color color)
        {
            Text label = UiKit.Label(_root, text, FontSize, TextAnchor.MiddleRight, color, true);
            float width = label.preferredWidth + 2f;
            UiKit.Place(label.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(width, CapHeight));
            return width;
        }
    }
}
