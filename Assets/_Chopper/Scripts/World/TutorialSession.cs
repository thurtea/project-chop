namespace Chopper.World
{
    // The practice yard (Tutorial on the start screen). While it is active the world is built from a fixed seed, is
    // never saved, opens no Steam lobby, stays at day, and TutorialYard hands the player everything and puts up a sign
    // at each thing to learn. It ends when the game returns to the start screen.
    public static class TutorialSession
    {
        public const int Seed = 2026;

        public static bool Active { get; private set; }

        public static void Begin() => Active = true;

        public static void End() => Active = false;
    }
}
