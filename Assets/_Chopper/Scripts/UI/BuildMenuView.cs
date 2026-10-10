using System.Collections.Generic;
using Chopper.Player;
using Chopper.World.Building;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chopper.UI
{
    // The hammer's piece list (Block while building): a slot grid of every building piece with its cost. Picking one
    // puts it on the hammer and closes the list. Pieces cost carried wood (and fiber for some); a piece the player
    // cannot pay for is dimmed.
    public class BuildMenuView
    {
        private const int Columns = 4;
        private const float SlotWidth = 220f;
        private const float SlotHeight = 150f;
        private const float Gap = 12f;

        private sealed class Slot
        {
            public Button Button;
            public Text Name;
            public Text Cost;
            public Image Frame;
            public Image Icon;
        }

        private RectTransform _root;
        private RectTransform _window;
        private readonly List<Slot> _slots = new List<Slot>();
        private System.Action _onPicked;

        public RectTransform Root => _root;

        public bool Visible
        {
            get => _root != null && _root.gameObject.activeSelf;
            set
            {
                if (_root == null) return;
                _root.gameObject.SetActive(value);
                if (value) Open();
            }
        }

        public void Build(Transform parent, System.Action onPicked)
        {
            _onPicked = onPicked;
            _root = UiKit.Rect(parent, "BuildMenu");
            UiKit.Stretch(_root);
            Image dim = UiKit.Box(_root, "Dim", new Color(0f, 0f, 0f, 0.25f));
            UiKit.Stretch(dim.rectTransform);
            _window = UiKit.Panel(_root, "Window", new Vector2(Columns * (SlotWidth + Gap) + 60f, 420f));
            UiKit.Place(_window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), _window.sizeDelta);
            Text title = UiKit.Heading(_root, "Build", 34, TextAnchor.MiddleCenter, UiKit.Accent);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, 176f), new Vector2(600f, 46f));
            _root.gameObject.SetActive(false);
        }

        private void Open()
        {
            BuildManager manager = BuildManager.Instance;
            PieceCatalog catalog = manager != null ? manager.Catalog : null;
            int count = catalog != null ? catalog.Count : 0;
            while (_slots.Count < count) _slots.Add(MakeSlot(_slots.Count));
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)Columns));
            _window.sizeDelta = new Vector2(Columns * (SlotWidth + Gap) + 60f, rows * (SlotHeight + Gap) + 60f);
            for (int i = 0; i < _slots.Count; i++) _slots[i].Button.gameObject.SetActive(i < count);
            Refresh();
            int current = PlayerBuilder.Local != null ? PlayerBuilder.Local.Kind : 0;
            if (EventSystem.current != null && count > 0)
                EventSystem.current.SetSelectedGameObject(_slots[Mathf.Clamp(current, 0, count - 1)].Button.gameObject);
        }

        private Slot MakeSlot(int index)
        {
            int column = index % Columns;
            int row = index / Columns;
            var go = new GameObject("Piece" + index, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_window, false);
            var rect = go.GetComponent<RectTransform>();
            float left = -(Columns * (SlotWidth + Gap) - Gap) * 0.5f;
            UiKit.Place(rect, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(left + column * (SlotWidth + Gap), -30f - row * (SlotHeight + Gap)), new Vector2(SlotWidth, SlotHeight));
            var frame = go.GetComponent<Image>();
            frame.sprite = UiKit.WhiteSprite;
            frame.color = new Color(0.55f, 0.43f, 0.22f, 0.9f);
            var button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.15f, 0.9f);
            colors.selectedColor = new Color(1.35f, 1.2f, 0.85f);
            button.colors = colors;
            Image inner = UiKit.Box(go.transform, "Inner", new Color(0.08f, 0.08f, 0.09f, 0.92f));
            UiKit.Stretch(inner.rectTransform, 3f);
            inner.raycastTarget = false;
            Image icon = UiKit.Box(go.transform, "Icon", UiKit.Ink);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            UiKit.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(56f, 56f));
            Text name = UiKit.Heading(go.transform, string.Empty, 21, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(SlotWidth - 12f, 34f));
            Text cost = UiKit.Label(go.transform, string.Empty, 17, TextAnchor.MiddleCenter, UiKit.Dim);
            UiKit.Place(cost.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(SlotWidth - 12f, 26f));
            int kind = index;
            button.onClick.AddListener(() => Pick(kind));
            return new Slot { Button = button, Name = name, Cost = cost, Frame = frame, Icon = icon };
        }

        private void Pick(int kind)
        {
            if (PlayerBuilder.Local != null) PlayerBuilder.Local.Select(kind);
            _onPicked?.Invoke();
        }

        public void Refresh()
        {
            if (!Visible) return;
            BuildManager manager = BuildManager.Instance;
            PieceCatalog catalog = manager != null ? manager.Catalog : null;
            GameObject player = PlayerWood.Local != null ? PlayerWood.Local.gameObject : null;
            int current = PlayerBuilder.Local != null ? PlayerBuilder.Local.Kind : -1;
            for (int i = 0; i < _slots.Count; i++)
            {
                PieceDefinition piece = catalog != null ? catalog.Get(i) : null;
                if (piece == null) continue;
                Slot slot = _slots[i];
                slot.Name.text = piece.DisplayName;
                slot.Cost.text = piece.FiberCost > 0 ? $"{piece.WoodCost} wood, {piece.FiberCost} fiber" : $"{piece.WoodCost} wood";
                bool affordable = PlayerItems.Get(player, (int)Chopper.Core.ItemKind.Wood) >= piece.WoodCost
                                  && PlayerItems.Get(player, (int)Chopper.Core.ItemKind.Fiber) >= piece.FiberCost;
                slot.Name.color = affordable ? UiKit.Ink : UiKit.Dim;
                slot.Icon.sprite = HotbarIcons.Get(piece.Icon);
                slot.Icon.color = affordable ? UiKit.Ink : UiKit.Dim;
                slot.Frame.color = i == current ? UiKit.Accent : new Color(0.55f, 0.43f, 0.22f, 0.9f);
            }
        }
    }
}
