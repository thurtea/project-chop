namespace Chopper.Core
{
    // The music's moods: one per place by day, the calm night, and the fight.
    public enum MusicMood
    {
        Hub = 0,
        Water = 1,
        Fire = 2,
        Earth = 3,
        Wind = 4,
        Night = 5,
        Fight = 6
    }

    // Which music plays (pure, so it can be tested). By day each place has its own: the hub is cozy and quiet (players
    // who never chop in the forests spend their days there fishing, talking and building), each forest its own tune.
    // At night the calm night music plays, and while awake enemies are close the fight music takes over, by day too
    // (a grove warden, a crypt arena). Each mood is a playlist of numbered files in Resources/Audio named after it
    // (music-hub-1, music-fire-1, music-fight-1 ...); until those exist it falls back to the AlkaKrab tracks already there.
    public static class MusicRules
    {
        public const int Count = 7;
        // How close an awake enemy must be for the fight music.
        public const float FightRange = 35f;

        public static MusicMood DayMood(Element zone)
        {
            switch (zone)
            {
                case Element.Water: return MusicMood.Water;
                case Element.Fire: return MusicMood.Fire;
                case Element.Earth: return MusicMood.Earth;
                case Element.Wind: return MusicMood.Wind;
                default: return MusicMood.Hub;
            }
        }

        // How much of each mood is heard: the fight drowns everything; otherwise the place's day music and the night
        // music cross with the darkness (dusk and dawn are in between).
        public static float Weight(MusicMood mood, Element zone, float darkness, bool fight)
        {
            if (fight) return mood == MusicMood.Fight ? 1f : 0f;
            if (mood == MusicMood.Night) return Clamp01(darkness);
            if (mood == DayMood(zone)) return 1f - Clamp01(darkness);
            return 0f;
        }

        // The name its own files go by.
        // (The night's are music-calm-night: "music-night" already names the two AlkaKrab night tracks, and one of them is
        // the hub's fallback.)
        public static string Name(MusicMood mood) => mood == MusicMood.Night ? "music-calm-night" : "music-" + mood.ToString().ToLowerInvariant();

        // What plays until a mood has its own files: the six AlkaKrab tracks, picked by feel. The calmest ("Odin's
        // Whisper", music-night-1) is the hub's cozy day; "Throne of the Fjords" (music-night-2) is the night;
        // "Northern Lights" the Water forest, "Saga of the Sea Wolves" the Wind, "Frostbound Horizons" the Earth,
        // "Echoes of Valhalla" (the title music) the Fire, and the two loudest carry the fight.
        public static string[] Fallback(MusicMood mood)
        {
            switch (mood)
            {
                case MusicMood.Hub: return new[] { "music-night-1" };
                case MusicMood.Water: return new[] { "music-day-1" };
                case MusicMood.Wind: return new[] { "music-day-2" };
                case MusicMood.Earth: return new[] { "music-day-3" };
                case MusicMood.Fire: return new[] { "music-title" };
                case MusicMood.Night: return new[] { "music-night-2" };
                default: return new[] { "music-day-3", "music-title" };
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
