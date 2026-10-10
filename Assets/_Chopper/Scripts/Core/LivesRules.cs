namespace Chopper.Core
{
    // Pure lives rules. By default lives are infinite: going down only costs the respawn wait. A finite mode (a
    // number of lives per night) is kept here so it can be offered as a game mode later.
    public static class LivesRules
    {
        // Lives left after a player goes down.
        public static int AfterFall(int lives, bool finite)
        {
            if (!finite) return lives;
            return lives > 0 ? lives - 1 : 0;
        }

        // True when a downed player cannot respawn until dawn.
        public static bool IsOut(int lives, bool finite)
        {
            return finite && lives <= 0;
        }
    }
}
