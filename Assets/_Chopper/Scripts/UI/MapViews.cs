using System.Collections.Generic;
using Chopper.Core;
using Chopper.Enemies;
using Chopper.Player;
using Chopper.World;
using Chopper.World.Generation;
using UnityEngine;
using UnityEngine.UI;

namespace Chopper.UI
{
    // Draws icons (hut, forest markers, players, enemies) on top of a map image. Icons are pooled.
    internal class MapIcons
    {
        private readonly RectTransform _parent;
        private readonly List<Image> _pool = new List<Image>();
        private int _used;

        public MapIcons(RectTransform parent)
        {
            _parent = parent;
        }

        public void Begin() => _used = 0;

        public void Add(Vector2 position, Color color, float size, Sprite sprite)
        {
            if (_used == _pool.Count)
            {
                Image image = UiKit.Box(_parent, "Icon", color, sprite);
                image.rectTransform.anchorMin = Vector2.zero;
                image.rectTransform.anchorMax = Vector2.zero;
                _pool.Add(image);
            }
            Image icon = _pool[_used++];
            icon.gameObject.SetActive(true);
            icon.sprite = sprite;
            icon.color = color;
            icon.rectTransform.sizeDelta = new Vector2(size, size);
            icon.rectTransform.anchoredPosition = position;
        }

        public void End()
        {
            for (int i = _used; i < _pool.Count; i++) _pool[i].gameObject.SetActive(false);
        }
    }

    // Shared logic for the minimap and the full map: which icons exist and where they go.
    internal static class MapContent
    {
        private static readonly Element[] Forests = { Element.Water, Element.Earth, Element.Fire, Element.Wind };
        private const float MarkerOffset = 8f;
        private const float IconSize = 11f;

        public static void Draw(MapIcons icons, MapService map, GameConfig config, float pixelsPerMeter, float iconScale)
        {
            icons.Begin();
            Vector2 Pos(Vector3 world) => map.WorldToMap(world) * map.Size * pixelsPerMeter;

            Hut hut = Hut.Instance;
            if (hut != null) icons.Add(Pos(hut.transform.position), new Color(1f, 0.86f, 0.45f), IconSize * 2f * iconScale, MapSprites.House);

            foreach (Element forest in Forests)
            {
                Vector2 axis = ZoneMap.Axis(forest) * (config.HubRadius + MarkerOffset);
                icons.Add(Pos(new Vector3(axis.x, 0f, axis.y)), ZonePalette.Label(forest), IconSize * iconScale, MapSprites.Dot);
            }

            // Crypt entrances, once found.
            foreach (CryptPlan plan in CryptWorld.Plans)
            {
                var door = new Vector3(plan.Site.Position.x, 0f, plan.Site.Position.y);
                if (map.IsExplored(door)) icons.Add(Pos(door), new Color(0.78f, 0.74f, 0.66f), IconSize * 1.7f * iconScale, MapSprites.Crypt);
            }

            // Only the enemies on the same level as the player: the surface, or the crypt they are in.
            bool below = PlayerWood.Local != null && CryptWorld.IsUnderground(PlayerWood.Local.transform.position);
            foreach (Treant treant in Treant.ClientAll)
            {
                if (treant == null || !treant.IsActive) continue;
                if (CryptWorld.IsUnderground(treant.transform.position) != below) continue;
                float size = treant.IsBoss ? IconSize * 1.6f : IconSize * 0.7f;
                Color color = treant.IsBoss ? new Color(0.8f, 0.3f, 1f) : new Color(1f, 0.25f, 0.2f);
                icons.Add(Pos(treant.transform.position), color, size * 1.2f * iconScale, MapSprites.Dot);
            }

            foreach (PlayerWood player in PlayerWood.Active)
            {
                if (player == null || player == PlayerWood.Local) continue;
                icons.Add(Pos(player.transform.position), new Color(0.4f, 0.85f, 1f), IconSize * 1.2f * iconScale, MapSprites.Dot);
            }
            icons.End();
        }
    }

    // Square minimap in the top right corner with a thin border, like Valheim's, and the zone name under it. North is
    // up; the map scrolls under the player arrow.
    public class MiniMapView
    {
        private const float Diameter = 220f;
        private const float WindowMeters = 170f;

        private readonly MapService _map;
        private readonly GameConfig _config;
        private RectTransform _content;
        private RectTransform _arrow;
        private MapIcons _icons;
        private Text _zoneLabel;
        private RectTransform _root;

        // Hidden while the Tab screen's workbench panel covers that corner.
        public bool Visible
        {
            set { if (_root != null && _root.gameObject.activeSelf != value) _root.gameObject.SetActive(value); }
        }

        public MiniMapView(MapService map, GameConfig config)
        {
            _map = map;
            _config = config;
        }

        public void Build(Transform parent)
        {
            RectTransform root = UiKit.Rect(parent, "MiniMap");
            _root = root;
            UiKit.Place(root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -34f), new Vector2(Diameter, Diameter));

            // A faint shadow under the map lifts it off the world behind it.
            Image shadow = UiKit.Box(root, "Shadow", new Color(0f, 0f, 0f, 0.35f));
            UiKit.Stretch(shadow.rectTransform, -4f);

            Image mask = UiKit.Box(root, "Mask", new Color(0.05f, 0.06f, 0.08f, 1f));
            UiKit.Stretch(mask.rectTransform);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            float pixelsPerMeter = Diameter / WindowMeters;
            var contentImage = new GameObject("Map", typeof(RectTransform), typeof(RawImage));
            contentImage.transform.SetParent(mask.transform, false);
            _content = (RectTransform)contentImage.transform;
            _content.anchorMin = new Vector2(0.5f, 0.5f);
            _content.anchorMax = new Vector2(0.5f, 0.5f);
            _content.pivot = Vector2.zero;
            _content.sizeDelta = new Vector2(_map.Size * pixelsPerMeter, _map.Size * pixelsPerMeter);
            var raw = contentImage.GetComponent<RawImage>();
            raw.texture = _map.Texture;
            raw.raycastTarget = false;
            _icons = new MapIcons(_content);

            // Player arrow at the center, drawn above the map.
            Image arrow = UiKit.Box(mask.transform, "Arrow", Color.white, MapSprites.Arrow);
            arrow.rectTransform.sizeDelta = new Vector2(22f, 22f);
            _arrow = arrow.rectTransform;

            // A thin light border.
            UiKit.Frame(root, 0f, 2f, new Color(0.85f, 0.8f, 0.7f, 0.85f));

            // North marked in gold just inside the top edge.
            Compass(root, "N", new Vector2(0f, 1f), UiKit.Accent);

            _zoneLabel = UiKit.Heading(root, string.Empty, 18, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Place(_zoneLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(320f, 24f));
        }

        private static void Compass(RectTransform root, string letter, Vector2 direction, Color color)
        {
            Text text = UiKit.Heading(root, letter, 15, TextAnchor.MiddleCenter, color);
            float inset = Diameter * 0.5f - 14f;
            UiKit.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), direction * inset, new Vector2(22f, 20f));
        }

        public void Refresh(Transform player, Element zone)
        {
            float pixelsPerMeter = Diameter / WindowMeters;
            Vector2 uv = _map.WorldToMap(player.position) * _map.Size * pixelsPerMeter;
            _content.anchoredPosition = -uv;
            _arrow.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
            MapContent.Draw(_icons, _map, _config, pixelsPerMeter, 1f);
            // The mines lie below the crypts, so a mine is checked first.
            int mine = MineWorld.MineAt(player.position);
            int crypt = mine >= 0 ? -1 : CryptWorld.CryptAt(player.position);
            _zoneLabel.text = mine >= 0 ? MineWorld.Plans[mine].Name : crypt >= 0 ? CryptWorld.Plans[crypt].Name : ZonePalette.Name(zone);
            _zoneLabel.color = crypt >= 0 ? new Color(0.85f, 0.78f, 0.62f) : ZonePalette.Label(zone);
        }

        internal static Sprite ArrowSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Upward pointing arrowhead with a notch at the bottom.
                    float v = y / (float)(size - 1);
                    float halfWidth = (1f - v) * 0.5f;
                    float u = Mathf.Abs(x / (float)(size - 1) - 0.5f);
                    bool inside = v > 0.05f && u < halfWidth;
                    bool notch = v < 0.22f && u < (0.22f - v) * 0.5f;
                    texture.SetPixel(x, y, inside && !notch ? Color.white : Color.clear);
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    // Full screen map (key M): the whole world, with zone names.
    public class FullMapView
    {
        private const float MapPixels = 760f;

        private readonly MapService _map;
        private readonly GameConfig _config;
        private RectTransform _panel;
        private RectTransform _content;
        private RectTransform _arrow;
        private MapIcons _icons;

        public FullMapView(MapService map, GameConfig config)
        {
            _map = map;
            _config = config;
        }

        public bool Visible
        {
            get => _panel != null && _panel.gameObject.activeSelf;
            set { if (_panel != null) _panel.gameObject.SetActive(value); }
        }

        public void Build(Transform parent)
        {
            _panel = UiKit.Panel(parent, "MapScreen", new Vector2(MapPixels + 40f, MapPixels + 90f));
            UiKit.Place(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, _panel.sizeDelta);

            Text title = UiKit.Heading(_panel, "World Map", 30, TextAnchor.MiddleCenter, UiKit.Accent);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(400f, 34f));

            var mapGo = new GameObject("Map", typeof(RectTransform), typeof(RawImage));
            mapGo.transform.SetParent(_panel, false);
            _content = (RectTransform)mapGo.transform;
            UiKit.Place(_content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -16f), new Vector2(MapPixels, MapPixels));
            var raw = mapGo.GetComponent<RawImage>();
            raw.texture = _map.Texture;
            raw.raycastTarget = false;

            _icons = new MapIcons(_content);

            // Zone names sit over the middle of each forest.
            foreach (Element forest in new[] { Element.Water, Element.Earth, Element.Fire, Element.Wind })
            {
                Vector2 axis = ZoneMap.Axis(forest) * (_config.HubRadius + 55f);
                Vector2 uv = _map.WorldToMap(new Vector3(axis.x, 0f, axis.y));
                Text label = UiKit.Label(_content, ZonePalette.Name(forest), 17, TextAnchor.MiddleCenter, ZonePalette.Label(forest), true);
                label.rectTransform.anchorMin = uv;
                label.rectTransform.anchorMax = uv;
                label.rectTransform.sizeDelta = new Vector2(260f, 24f);
                label.rectTransform.anchoredPosition = Vector2.zero;
            }

            Image arrow = UiKit.Box(_content, "Arrow", Color.white, MapSprites.Arrow);
            arrow.rectTransform.anchorMin = Vector2.zero;
            arrow.rectTransform.anchorMax = Vector2.zero;
            arrow.rectTransform.sizeDelta = new Vector2(30f, 30f);
            _arrow = arrow.rectTransform;

            Text legend = UiKit.Label(_panel, "Gold: hut    Grey: crypt    Red: enemies    Blue: players    Dark: unexplored    M or Esc to close", 15, TextAnchor.MiddleCenter, UiKit.Dim);
            UiKit.Place(legend.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(MapPixels, 26f));
            _panel.gameObject.SetActive(false);
        }

        public void Refresh(Transform player)
        {
            if (!Visible) return;
            float pixelsPerMeter = MapPixels / _map.Size;
            MapContent.Draw(_icons, _map, _config, pixelsPerMeter, 1.2f);
            _arrow.anchoredPosition = _map.WorldToMap(player.position) * MapPixels;
            _arrow.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
        }
    }
}
