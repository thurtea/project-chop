namespace Chopper.World.Saving
{
    // Which world file the current game belongs to. The start screen sets it before hosting: a new world gets a new
    // file, a loaded world keeps its own and hands over its contents to apply. Games hosted from the command line
    // (tests, screenshot tours) leave it empty, so they never write a save.
    public static class SaveSystem
    {
        // Where this game saves, or null for a game that is never saved.
        public static string Path { get; private set; }
        public static string WorldName { get; private set; } = "World";
        // A save to apply once the world has been built; taken (and cleared) by WorldSaver.
        public static WorldSave Pending { get; private set; }

        public static bool Saving => !string.IsNullOrEmpty(Path);

        public static void PrepareNew(string worldName)
        {
            WorldName = string.IsNullOrWhiteSpace(worldName) ? SaveFiles.SuggestName() : worldName.Trim();
            Path = SaveFiles.NewPath(WorldName);
            Pending = null;
        }

        public static void PrepareLoad(string path, WorldSave save)
        {
            WorldName = save.Name;
            Path = path;
            Pending = save;
        }

        // Tests: save to a scratch file without touching the player's worlds.
        public static void UseScratchFile(string path, string worldName)
        {
            WorldName = worldName;
            Path = path;
        }

        public static WorldSave TakePending()
        {
            WorldSave pending = Pending;
            Pending = null;
            return pending;
        }

        // Back at the start screen: the next game picks its own world.
        public static void Clear()
        {
            Path = null;
            Pending = null;
            WorldName = "World";
        }
    }
}
