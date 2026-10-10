using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chopper.UI
{
    // Small helpers that build uGUI elements in code, so the whole interface needs no prefabs or imported fonts.
    public static class UiKit
    {
        public static readonly Color Ink = new Color(0.96f, 0.93f, 0.85f);
        public static readonly Color Dim = new Color(0.72f, 0.70f, 0.64f);
        public static readonly Color PanelColor = new Color(0.035f, 0.03f, 0.025f, 0.86f);
        public static readonly Color PanelInnerLine = new Color(0.85f, 0.65f, 0.32f, 0.38f);
        public static readonly Color Accent = new Color(0.93f, 0.72f, 0.30f);
        // A button is only its text until it is hovered or selected; then a soft warm glow sits behind the words.
        public static readonly Color ButtonGlow = new Color(0.95f, 0.66f, 0.28f, 0.42f);
        public static readonly Color ButtonText = new Color(0.86f, 0.82f, 0.74f);
        public static readonly Color ButtonTextActive = new Color(1f, 0.86f, 0.52f);

        private static Font _font;
        private static Font _display;
        private static Sprite _circle;
        private static Sprite _white;
        private static Sprite _softPanel;
        private static Sprite _glow;
        private static Sprite _fadeLine;

        // The small body font: the built-in sans, which stays readable at HUD sizes.
        public static Font DefaultFont
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        // The headline and button font: Cinzel (SIL Open Font License, see Resources/Fonts), with the body font as a
        // fallback if the file is ever missing.
        public static Font DisplayFont
        {
            get
            {
                if (_display == null) _display = Resources.Load<Font>("Fonts/Cinzel");
                return _display != null ? _display : DefaultFont;
            }
        }

        public static Sprite WhiteSprite
        {
            get
            {
                if (_white != null) return _white;
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                _white = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                return _white;
            }
        }

        // A soft edged filled circle, used as the minimap mask and for round dots.
        public static Sprite CircleSprite
        {
            get
            {
                if (_circle != null) return _circle;
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                float radius = size * 0.5f - 1f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                        float alpha = Mathf.Clamp01(radius - d + 0.5f);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }
                texture.Apply();
                _circle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return _circle;
            }
        }

        // A rounded dark slab whose edge fades out over a few pixels: a panel without a drawn border. Nine sliced.
        public static Sprite SoftPanelSprite
        {
            get
            {
                if (_softPanel != null) return _softPanel;
                const int size = 64;
                const float corner = 10f;
                const float feather = 6f;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        // Signed distance to a rounded square inset by the feather width.
                        float px = Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - corner - feather);
                        float py = Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - corner - feather);
                        float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude + Mathf.Min(Mathf.Max(px, py), 0f) - corner;
                        float alpha = Mathf.Clamp01(-outside / feather);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha * (3f - 2f * alpha)));
                    }
                }
                texture.Apply();
                float border = corner + feather + 2f;
                _softPanel = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(border, border, border, border));
                return _softPanel;
            }
        }

        // A soft horizontal streak, strongest in the middle and fading to nothing at the ends and the top and bottom.
        // A soft round glow that fades to nothing at the edge (spotlights, halos). Stretch it for an ellipse.
        public static Sprite RadialGlowSprite
        {
            get
            {
                if (_radialGlow != null) return _radialGlow;
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2((x + 0.5f) / size, (y + 0.5f) / size), new Vector2(0.5f, 0.5f)) * 2f;
                        float alpha = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }
                texture.Apply();
                _radialGlow = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return _radialGlow;
            }
        }

        private static Sprite _radialGlow;

        public static Sprite GlowSprite
        {
            get
            {
                if (_glow != null) return _glow;
                const int width = 128;
                const int height = 32;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float u = Mathf.Abs((x + 0.5f) / width * 2f - 1f);
                        float v = Mathf.Abs((y + 0.5f) / height * 2f - 1f);
                        float alpha = (1f - u * u) * (1f - v * v * v * v);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
                    }
                }
                texture.Apply();
                _glow = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
                return _glow;
            }
        }

        // A one pixel tall line that fades out toward both ends.
        public static Sprite FadeLineSprite
        {
            get
            {
                if (_fadeLine != null) return _fadeLine;
                const int width = 128;
                var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int x = 0; x < width; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / width * 2f - 1f);
                    texture.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - u * u)));
                }
                texture.Apply();
                _fadeLine = Sprite.Create(texture, new Rect(0, 0, width, 1), new Vector2(0.5f, 0.5f), 100f);
                return _fadeLine;
            }
        }

        // ---------- Layout ----------

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        // ---------- Elements ----------

        public static Image Box(Transform parent, string name, Color color, Sprite sprite = null)
        {
            RectTransform rt = Rect(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : WhiteSprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor anchor, Color color, bool bold = false)
        {
            RectTransform rt = Rect(parent, "Text");
            var label = rt.gameObject.AddComponent<Text>();
            label.font = DefaultFont;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.alignment = anchor;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return label;
        }

        // Same as Label but in the display font, for titles, tabs and buttons.
        public static Text Heading(Transform parent, string text, int size, TextAnchor anchor, Color color)
        {
            Text label = Label(parent, text, size, anchor, color);
            label.font = DisplayFont;
            return label;
        }

        // A dark translucent slab with soft rounded edges and no drawn border, like Valheim's menus.
        public static RectTransform Panel(Transform parent, string name, Vector2 size)
        {
            Image fill = Box(parent, name, PanelColor, SoftPanelSprite);
            fill.type = Image.Type.Sliced;
            fill.gameObject.AddComponent<PanelOpacity>();
            fill.rectTransform.sizeDelta = size;
            return fill.rectTransform;
        }

        // Four thin lines inset from the edges of a parent, an outline without a border sprite.
        public static void Frame(RectTransform parent, float inset, float thickness, Color color)
        {
            for (int side = 0; side < 4; side++)
            {
                Image line = Box(parent, "Frame" + side, color);
                RectTransform rt = line.rectTransform;
                bool horizontal = side < 2;
                bool start = side % 2 == 0;
                rt.anchorMin = horizontal ? new Vector2(0f, start ? 1f : 0f) : new Vector2(start ? 0f : 1f, 0f);
                rt.anchorMax = horizontal ? new Vector2(1f, start ? 1f : 0f) : new Vector2(start ? 0f : 1f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = horizontal ? new Vector2(-2f * inset, thickness) : new Vector2(thickness, -2f * inset);
                rt.anchoredPosition = horizontal ? new Vector2(0f, start ? -inset : inset) : new Vector2(start ? inset : -inset, 0f);
            }
        }

        // A thin rule under headings that fades out toward both ends.
        public static RectTransform Divider(Transform parent, float width)
        {
            Image line = Box(parent, "Divider", PanelInnerLine, FadeLineSprite);
            line.rectTransform.sizeDelta = new Vector2(width, 1.5f);
            return line.rectTransform;
        }

        // A tab: display font text with a gold underline when it is the selected one.
        public static Button MakeTab(Transform parent, string text, Vector2 size, Action onClick)
        {
            Image image = Box(parent, "Tab_" + text, new Color(0f, 0f, 0f, 0.0f));
            image.raycastTarget = true;
            image.rectTransform.sizeDelta = size;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.5f, 1.3f, 0.9f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.8f, 0.7f, 0.5f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            Text label = Heading(image.transform, text, 22, TextAnchor.MiddleCenter, Dim);
            Stretch(label.rectTransform);
            Image underline = Box(image.transform, "Underline", Accent, FadeLineSprite);
            underline.rectTransform.anchorMin = new Vector2(0.1f, 0f);
            underline.rectTransform.anchorMax = new Vector2(0.9f, 0f);
            underline.rectTransform.pivot = new Vector2(0.5f, 0f);
            underline.rectTransform.sizeDelta = new Vector2(0f, 3f);
            underline.enabled = false;
            return button;
        }

        public static void SetTabActive(Button tab, bool active)
        {
            Text label = tab.GetComponentInChildren<Text>();
            if (label != null) label.color = active ? Accent : Dim;
            Transform underline = tab.transform.Find("Underline");
            if (underline != null) underline.GetComponent<Image>().enabled = active;
        }

        public struct Bar
        {
            public RectTransform Root;
            public Image Fill;
            public Text Label;
        }

        // A horizontal bar whose fill is driven by setting Fill.fillAmount.
        public static Bar MakeBar(Transform parent, string name, Vector2 size, Color fillColor, bool withLabel)
        {
            Image back = Box(parent, name, new Color(0f, 0f, 0f, 0.55f));
            back.rectTransform.sizeDelta = size;
            Image inner = Box(back.transform, "Inner", new Color(0.04f, 0.035f, 0.03f, 0.8f));
            Stretch(inner.rectTransform, 2f);
            Image fill = Box(inner.transform, "Fill", fillColor);
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;

            Text label = null;
            if (withLabel)
            {
                label = Label(back.transform, string.Empty, 13, TextAnchor.MiddleCenter, Ink, true);
                Stretch(label.rectTransform);
            }
            return new Bar { Root = back.rectTransform, Fill = fill, Label = label };
        }

        public static Button MakeButton(Transform parent, string text, Vector2 size, Action onClick, int fontSize = 18)
        {
            // The glow image is the button's target graphic: invisible at rest, warm when hovered or selected.
            Image image = Box(parent, "Button_" + text, Color.white, GlowSprite);
            image.raycastTarget = true;
            image.rectTransform.sizeDelta = size;
            // A fully transparent mesh would be culled, and a culled graphic no longer receives clicks.
            image.canvasRenderer.cullTransparentMesh = false;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = ButtonGlow;
            // The gamepad moves a selection instead of a mouse pointer, so it must look as obvious as a hover.
            colors.selectedColor = ButtonGlow;
            colors.pressedColor = new Color(1f, 0.75f, 0.4f, 0.65f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            Text label = Heading(image.transform, text, fontSize, TextAnchor.MiddleCenter, ButtonText);
            Stretch(label.rectTransform);
            var tint = image.gameObject.AddComponent<UiButtonLabel>();
            tint.Init(button, label, ButtonText, ButtonTextActive);
            return button;
        }

        // A button that holds a value (a key binding, an option): the same text button with a faint rounded field
        // behind it at rest, so it reads as something to click.
        public static Button MakeFieldButton(Transform parent, string text, Vector2 size, Action onClick, int fontSize = 18)
        {
            Button button = MakeButton(parent, text, size, onClick, fontSize);
            var image = (Image)button.targetGraphic;
            image.sprite = SoftPanelSprite;
            image.type = Image.Type.Sliced;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 0.9f, 0.75f, 0.07f);
            colors.highlightedColor = new Color(0.95f, 0.66f, 0.28f, 0.30f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            return button;
        }

        // Lights up a whole settings row while its control is selected or hovered (see UiRowHighlight).
        public static void HighlightRow(RectTransform row, Selectable target, Text label)
        {
            Image band = Box(row, "RowBand", Color.clear, SoftPanelSprite);
            band.type = Image.Type.Sliced;
            RectTransform bandRect = band.rectTransform;
            bandRect.anchorMin = Vector2.zero;
            bandRect.anchorMax = Vector2.one;
            bandRect.offsetMin = new Vector2(-16f, -4f);
            bandRect.offsetMax = new Vector2(16f, 4f);
            band.transform.SetAsFirstSibling();

            Image bar = Box(row, "RowBar", Color.clear);
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.anchoredPosition = new Vector2(-10f, 0f);
            barRect.sizeDelta = new Vector2(4f, -6f);

            row.gameObject.AddComponent<UiRowHighlight>().Init(target, label, band, bar);
        }

        public static void SetButtonText(Button button, string text)
        {
            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = text;
        }

        public static Slider MakeSlider(Transform parent, float min, float max, float value, Action<float> onChange, Vector2 size)
        {
            RectTransform root = Rect(parent, "Slider");
            root.sizeDelta = size;
            var slider = root.gameObject.AddComponent<Slider>();

            Image back = Box(root, "Background", new Color(0f, 0f, 0f, 0.7f));
            back.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            back.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            back.rectTransform.sizeDelta = new Vector2(0f, 8f);

            RectTransform fillArea = Rect(root, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-10f, 8f);
            Image fill = Box(fillArea, "Fill", Accent);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.sizeDelta = Vector2.zero;

            RectTransform handleArea = Rect(root, "Handle Slide Area");
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(5f, 0f);
            handleArea.offsetMax = new Vector2(-5f, 0f);
            Image handle = Box(handleArea, "Handle", Ink, CircleSprite);
            handle.raycastTarget = true;
            // The slider stretches the handle to its own height; take that back off so the handle stays round.
            handle.rectTransform.sizeDelta = new Vector2(20f, 20f - size.y);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            // The handle turns gold when the slider is hovered or selected with the gamepad.
            ColorBlock sliderColors = slider.colors;
            sliderColors.normalColor = Color.white;
            sliderColors.highlightedColor = new Color(1f, 0.78f, 0.3f, 1f);
            sliderColors.selectedColor = sliderColors.highlightedColor;
            sliderColors.pressedColor = new Color(0.9f, 0.65f, 0.2f, 1f);
            slider.colors = sliderColors;
            // Left and right must change the value, not jump to a neighbouring control: with automatic navigation
            // Unity moves the selection whenever anything lies to the left or right. Up and down still move between rows.
            slider.navigation = new Navigation { mode = Navigation.Mode.Vertical };
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return slider;
        }

        // Stacks children of a parent top to bottom with a fixed gap, starting at the top.
        public static void Stack(RectTransform parent, IList<RectTransform> children, float gap, float startY = 0f)
        {
            float y = -startY;
            foreach (RectTransform child in children)
            {
                child.anchorMin = new Vector2(0.5f, 1f);
                child.anchorMax = new Vector2(0.5f, 1f);
                child.pivot = new Vector2(0.5f, 1f);
                child.anchoredPosition = new Vector2(0f, y);
                y -= child.sizeDelta.y + gap;
            }
        }
    }
}
