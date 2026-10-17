namespace Chopper.Core
{
    public static class SwimHolster
    {
        public static bool MayDraw(bool swimming) => !swimming;

        public static bool Step(bool swimming, bool holding, ref bool remembered)
        {
            if (swimming)
            {
                if (!holding || remembered) return false;
                remembered = true;
                return true;
            }
            if (!remembered) return false;
            remembered = false;
            return !holding;
        }
    }
}
