using System.Collections.Generic;
using UnityEngine;

namespace Chopper.UI
{
    // Short pickup and event messages shown down the left of the screen, newest last.
    public static class Toasts
    {
        public struct Item
        {
            public string Text;
            public Color Color;
            public float Until;
        }

        private const float Lifetime = 4f;
        private const int MaxItems = 6;

        private static readonly List<Item> _items = new List<Item>();

        public static IReadOnlyList<Item> Items => _items;

        public static void Show(string text, Color color)
        {
            _items.Add(new Item { Text = text, Color = color, Until = Time.time + Lifetime });
            if (_items.Count > MaxItems) _items.RemoveAt(0);
        }

        public static void Prune()
        {
            _items.RemoveAll(item => Time.time >= item.Until);
        }
    }
}
