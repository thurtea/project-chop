using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chopper.UI
{
    // Draws PickupFeed as small popups on the left middle of the HUD, newest at the bottom: each one slides in, pops
    // when more of the same item arrives, and fades out. One line each: an icon and "+3 Wood". Plain class owned by
    // GameUi.
    public class PickupFeedView
    {
        private const int Slots = 5;
        private const float CardWidth = 210f;
        private const float CardHeight = 40f;
        private const float Gap = 6f;
        private const float BaseY = 0f;
        private const float SlideSeconds = 0.18f;
        private const float PopSeconds = 0.25f;

        private class Card
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Icon;
            public Text Title;
        }

        private readonly List<Card> _cards = new List<Card>();

        public PickupFeedView(Transform hud)
        {
            for (int i = 0; i < Slots; i++)
            {
                var card = new Card();
                // A light dark strip, so the popup sits softly on the world.
                Image back = UiKit.Box(hud, "Pickup" + i, new Color(0f, 0f, 0f, 0.42f));
                card.Root = back.rectTransform;
                UiKit.Place(card.Root, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(24f, BaseY + i * (CardHeight + Gap)), new Vector2(CardWidth, CardHeight));
                card.Group = card.Root.gameObject.AddComponent<CanvasGroup>();
                card.Group.blocksRaycasts = false;
                card.Icon = UiKit.Box(card.Root, "Icon", Color.white);
                card.Icon.preserveAspect = true;
                UiKit.Place(card.Icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(32f, 32f));
                card.Title = UiKit.Label(card.Root, string.Empty, 20, TextAnchor.MiddleLeft, UiKit.Ink, true);
                UiKit.Place(card.Title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(CardWidth - 52f, CardHeight));
                card.Root.gameObject.SetActive(false);
                _cards.Add(card);
            }
        }

        public void Refresh()
        {
            PickupFeed.Prune();
            IReadOnlyList<PickupFeed.Entry> entries = PickupFeed.Entries;
            for (int i = 0; i < _cards.Count; i++)
            {
                Card card = _cards[i];
                // Slot 0 is the bottom card and shows the newest entry.
                int index = entries.Count - 1 - i;
                if (index < 0)
                {
                    card.Root.gameObject.SetActive(false);
                    continue;
                }
                PickupFeed.Entry entry = entries[index];
                card.Root.gameObject.SetActive(true);
                card.Icon.sprite = entry.IconId != null ? HotbarIcons.Get(entry.IconId) : IconFor(entry.Kind);
                card.Title.text = $"+{entry.Amount} {entry.Name}";

                float age = Time.time - entry.ShownAt;
                float sinceUpdate = Time.time - entry.UpdatedAt;
                float slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / SlideSeconds));
                float fade = 1f - Mathf.Clamp01((sinceUpdate - (PickupFeed.Lifetime - 0.6f)) / 0.6f);
                card.Group.alpha = slide * fade;
                float pop = 1f + 0.12f * (1f - Mathf.Clamp01(sinceUpdate / PopSeconds));
                card.Root.localScale = Vector3.one * pop;
                card.Root.anchoredPosition = new Vector2(24f - (1f - slide) * 60f, BaseY + i * (CardHeight + Gap));
            }
        }

        // Also used by the inventory's bag grid and the chest (HotbarIcons holds the icon set).
        internal static Sprite IconFor(PickupFeed.Kind kind)
        {
            switch (kind)
            {
                case PickupFeed.Kind.Iron: return HotbarIcons.Iron;
                case PickupFeed.Kind.Essence: return HotbarIcons.Essence;
                case PickupFeed.Kind.Shard: return HotbarIcons.Shard;
                case PickupFeed.Kind.Herb: return HotbarIcons.Herb;
                case PickupFeed.Kind.Torch: return HotbarIcons.Torch;
                case PickupFeed.Kind.Fish: return HotbarIcons.Fish;
                case PickupFeed.Kind.Gear: return HotbarIcons.Rod;
                case PickupFeed.Kind.Bandage: return HotbarIcons.Bandage;
                case PickupFeed.Kind.Fabric: return HotbarIcons.Fiber;
                default: return HotbarIcons.Wood;
            }
        }
    }
}
