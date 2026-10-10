using System.Collections.Generic;
using UnityEngine;

namespace Chopper.UI
{
    // What the player just got, shown as small popups on the left middle of the screen for a few seconds (like
    // Valheim's pickup list): an icon and "+4 Wood". Picking up more of the same thing while its card is showing adds
    // to that card and makes it pop again, so a felled tree reads as one clear "+8 Wood".
    public static class PickupFeed
    {
        public enum Kind { Wood, Herb, Iron, Shard, Essence, Fabric, Torch, Bandage, Fish, Gear, Find }

        public class Entry
        {
            public Kind Kind;
            public string Name;
            // An icon of its own (a mining find), or null for the kind's icon.
            public string IconId;
            public int Amount;
            public int Total;
            public float ShownAt;
            public float UpdatedAt;
        }

        public const float Lifetime = 3.5f;
        private const int MaxEntries = 5;

        private static readonly List<Entry> _entries = new List<Entry>();

        public static IReadOnlyList<Entry> Entries => _entries;

        public static void Add(Kind kind, string name, int amount, int total) => Add(kind, name, amount, total, null);

        public static void Add(Kind kind, string name, int amount, int total, string iconId)
        {
            if (amount <= 0) return;
            foreach (Entry entry in _entries)
            {
                if (entry.Kind != kind || Time.time - entry.UpdatedAt > Lifetime) continue;
                if (kind == Kind.Find && entry.Name != name) continue;
                entry.Amount += amount;
                entry.Total = total;
                entry.UpdatedAt = Time.time;
                return;
            }
            _entries.Add(new Entry { Kind = kind, Name = name, IconId = iconId, Amount = amount, Total = total, ShownAt = Time.time, UpdatedAt = Time.time });
            if (_entries.Count > MaxEntries) _entries.RemoveAt(0);
        }

        public static void Prune()
        {
            _entries.RemoveAll(entry => Time.time - entry.UpdatedAt > Lifetime);
        }
    }
}
