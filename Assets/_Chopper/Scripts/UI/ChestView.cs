using System.Collections.Generic;
using Chopper.Core;
using Chopper.Player;
using Chopper.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chopper.UI
{
    // The storage chest menu: the player's bag on the left and the shared chest on the right, both Valheim style slot
    // grids. A click (or A on a pad) moves the whole stack across; the server moves what fits (SharedChest). The chest is
    // the same for every player. The line under the grids names the focused stack. It closes when the player walks away.
    public class ChestView
    {
        private const float Width = 1440f;
        private const float Height = 760f;
        private const int Columns = 6;
        private const int Rows = 5;
        private const float SlotSize = 92f;
        private const float SlotGap = 9f;

        private sealed class Slot
        {
            public Button Button;
            public Image Icon;
            public Text Count;
            public int Kind = -1;
        }

        private RectTransform _root;
        private readonly List<Slot> _bag = new List<Slot>();
        private readonly List<Slot> _chest = new List<Slot>();
        private readonly List<int> _kinds = new List<int>();
        private Text _focusName;
        private Text _bagTitle;
        private Text _chestTitle;

        public RectTransform Root => _root;

        public bool Visible
        {
            get => _root != null && _root.gameObject.activeSelf;
            set
            {
                if (_root == null) return;
                _root.gameObject.SetActive(value);
                if (value) Refresh();
            }
        }

        public void Build(Transform parent)
        {
            _root = UiKit.Rect(parent, "Chest");
            UiKit.Stretch(_root);
            Image dim = UiKit.Box(_root, "Dim", new Color(0f, 0f, 0f, 0.35f));
            UiKit.Stretch(dim.rectTransform);

            RectTransform window = UiKit.Panel(_root, "Window", new Vector2(Width, Height));
            UiKit.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, window.sizeDelta);
            Text title = UiKit.Heading(_root, "Storage chest", 34, TextAnchor.MiddleCenter, UiKit.Accent);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, Height * 0.5f + 6f), new Vector2(600f, 46f));

            float gridWidth = Columns * SlotSize + (Columns - 1) * SlotGap;
            float leftX = -Width * 0.5f + 60f;
            float rightX = Width * 0.5f - 60f - gridWidth;
            _bagTitle = Heading(window, "Bag", leftX, gridWidth);
            _chestTitle = Heading(window, "Chest", rightX, gridWidth);
            BuildGrid(window, leftX, _bag, true);
            BuildGrid(window, rightX, _chest, false);

            // Between the grids: which way a click moves things.
            Text arrows = UiKit.Label(window, "→\n←", 34, TextAnchor.MiddleCenter, UiKit.Dim);
            UiKit.Place(arrows.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(120f, 120f));

            _focusName = UiKit.Label(window, string.Empty, 20, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Place(_focusName.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(Width - 100f, 30f));
            _root.gameObject.SetActive(false);
        }

        private static Text Heading(RectTransform window, string text, float x, float width)
        {
            Text heading = UiKit.Heading(window, text, 26, TextAnchor.MiddleLeft, UiKit.Ink);
            UiKit.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(x, -26f), new Vector2(width, 40f));
            return heading;
        }

        private void BuildGrid(RectTransform window, float left, List<Slot> slots, bool bag)
        {
            for (int i = 0; i < Columns * Rows; i++)
            {
                int column = i % Columns;
                int row = i / Columns;
                var slotRoot = new GameObject("Slot" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                slotRoot.transform.SetParent(window, false);
                var rect = slotRoot.GetComponent<RectTransform>();
                UiKit.Place(rect, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(left + column * (SlotSize + SlotGap), -80f - row * (SlotSize + SlotGap)), new Vector2(SlotSize, SlotSize));
                var frame = slotRoot.GetComponent<Image>();
                frame.sprite = UiKit.WhiteSprite;
                frame.color = new Color(0.55f, 0.43f, 0.22f, 0.9f);
                var button = slotRoot.GetComponent<Button>();
                ColorBlock colors = button.colors;
                colors.highlightedColor = new Color(1.25f, 1.15f, 0.9f);
                colors.selectedColor = new Color(1.35f, 1.2f, 0.85f);
                button.colors = colors;
                Image inner = UiKit.Box(slotRoot.transform, "Inner", new Color(0.08f, 0.08f, 0.09f, 0.92f));
                UiKit.Stretch(inner.rectTransform, 3f);
                inner.raycastTarget = false;
                Image icon = UiKit.Box(slotRoot.transform, "Icon", Color.white);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                UiKit.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(60f, 60f));
                Text count = UiKit.Label(slotRoot.transform, string.Empty, 17, TextAnchor.LowerRight, UiKit.Accent, true);
                UiKit.Place(count.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-6f, 4f), new Vector2(80f, 22f));
                var slot = new Slot { Button = button, Icon = icon, Count = count };
                button.onClick.AddListener(() => Move(slot, bag));
                slots.Add(slot);
            }
        }

        // The whole stack goes across; the server moves what fits.
        private static void Move(Slot slot, bool fromBag)
        {
            SharedChest chest = SharedChest.Instance;
            if (chest == null || slot.Kind < 0) return;
            chest.RequestMove(slot.Kind, ItemRules.ChestCap, fromBag);
        }

        public void Refresh()
        {
            if (!Visible) return;
            GameObject player = PlayerWood.Local != null ? PlayerWood.Local.gameObject : null;
            SharedChest chest = SharedChest.Instance;
            Fill(_bag, kind => PlayerItems.Get(player, kind));
            Fill(_chest, kind => chest != null ? chest.Count(kind) : 0);
            _chestTitle.text = chest != null && chest.Total == 0 ? "Chest  (empty)" : "Chest";

            // The focused stack's name and count, under the grids.
            GameObject focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            string line = string.Empty;
            foreach (Slot slot in _bag) if (slot.Button.gameObject == focused && slot.Kind >= 0) line = $"{ItemLook.Name(slot.Kind)}  {slot.Count.text}";
            foreach (Slot slot in _chest) if (slot.Button.gameObject == focused && slot.Kind >= 0) line = $"{ItemLook.Name(slot.Kind)}  {slot.Count.text}";
            _focusName.text = line;
        }

        private void Fill(List<Slot> slots, System.Func<int, int> count)
        {
            _kinds.Clear();
            for (int kind = 0; kind < ItemRules.Count; kind++) if (count(kind) > 0) _kinds.Add(kind);
            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                bool filled = i < _kinds.Count;
                slot.Kind = filled ? _kinds[i] : -1;
                slot.Icon.enabled = filled;
                slot.Button.interactable = true;
                slot.Count.text = filled ? count(slot.Kind).ToString() : string.Empty;
                if (!filled) continue;
                slot.Icon.sprite = ItemLook.Icon(slot.Kind);
                slot.Icon.color = ItemLook.Tint(slot.Kind);
            }
        }

        // Development and screenshots: the first bag slot takes the pad focus.
        public void Focus()
        {
            if (EventSystem.current != null && _bag.Count > 0) EventSystem.current.SetSelectedGameObject(_bag[0].Button.gameObject);
        }
    }
}
