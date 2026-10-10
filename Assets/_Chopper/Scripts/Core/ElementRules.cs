namespace Chopper.Core
{
    // Counter cycle: Water beats Fire, Fire beats Wind, Wind beats Earth, Earth beats Water.
    // Neutral never beats anything and nothing beats Neutral.
    public static class ElementRules
    {
        public static bool Beats(Element attacker, Element defender)
        {
            switch (attacker)
            {
                case Element.Water: return defender == Element.Fire;
                case Element.Fire: return defender == Element.Wind;
                case Element.Wind: return defender == Element.Earth;
                case Element.Earth: return defender == Element.Water;
                default: return false;
            }
        }
    }
}
