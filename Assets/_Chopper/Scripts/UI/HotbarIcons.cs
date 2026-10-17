using Chopper.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Chopper.UI
{
    // Every icon the interface shows: white silhouettes from game-icons.net (CC BY 3.0, credited on the Credits
    // page), rendered at 256 px into Assets/ThirdParty/GameIcons_net/Resources/GameIcons and tinted where they are
    // drawn. One style for the hotbar, the bag, the chest, pickups, status effects and building pieces. The list of
    // icons and their sources is GameIcons_net/SOURCES.md.
    public static class HotbarIcons
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // An icon by its file name (without .png). A missing one logs once and draws nothing, never a stand-in.
        public static Sprite Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Cache.TryGetValue(id, out Sprite sprite)) return sprite;
            sprite = Resources.Load<Sprite>("GameIcons/" + id);
            if (sprite == null) Debug.LogWarning($"[Icons] Missing icon GameIcons/{id}.");
            Cache[id] = sprite;
            return sprite;
        }

        // Tools and the hotbar.
        public static Sprite Axe => Get("axe");
        public static Sprite Fists => Get("fists");
        public static Sprite Torch => Get("torch");
        public static Sprite Bandage => Get("bandage");
        public static Sprite Rod => Get("rod");
        public static Sprite Fish => Get("fish");
        public static Sprite Hammer => Get("hammer");
        public static Sprite Pick => Get("find-pickhead");
        public static Sprite Sword => Get("sword");

        // Materials.
        public static Sprite Wood => Get("wood");
        public static Sprite Herb => Get("herb");
        public static Sprite Fiber => Get("fiber");
        public static Sprite Iron => Get("iron");
        public static Sprite Shard => Get("shard");
        public static Sprite Essence => Get("essence");

        // Status effects.
        public static Sprite Wet => Get("wet");
        public static Sprite Fed => Get("fed");

        // The element an axe is charged with.
        public static Sprite ElementIcon(Element element)
        {
            switch (element)
            {
                case Element.Water: return Get("water");
                case Element.Fire: return Get("fire");
                case Element.Earth: return Get("earth");
                case Element.Wind: return Get("wind");
                default: return null;
            }
        }
    }
}
