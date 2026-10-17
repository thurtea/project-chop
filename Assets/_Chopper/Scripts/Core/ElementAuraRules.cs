namespace Chopper.Core
{
    public struct AuraStyle
    {
        public bool On;
        public float Rate;
        public float Speed;
        public float Gravity;
        public float Size;
        public float Life;
        public float Swirl;
        public bool Glow;
        public float LightIntensity;
    }

    public static class ElementAuraRules
    {
        public static AuraStyle Style(Element element)
        {
            switch (element)
            {
                case Element.Fire: return new AuraStyle { On = true, Rate = 26f, Speed = 0.9f, Gravity = -0.6f, Size = 0.07f, Life = 0.7f, Swirl = 0.3f, Glow = true, LightIntensity = 1.6f };
                case Element.Water: return new AuraStyle { On = true, Rate = 14f, Speed = 0.25f, Gravity = 1.2f, Size = 0.06f, Life = 0.9f, Swirl = 0.1f, Glow = true, LightIntensity = 0.7f };
                case Element.Wind: return new AuraStyle { On = true, Rate = 18f, Speed = 0.4f, Gravity = 0f, Size = 0.05f, Life = 1.1f, Swirl = 2.4f, Glow = true, LightIntensity = 0f };
                case Element.Earth: return new AuraStyle { On = true, Rate = 10f, Speed = 0.15f, Gravity = 1.6f, Size = 0.08f, Life = 0.8f, Swirl = 0f, Glow = false, LightIntensity = 0f };
                default: return new AuraStyle { On = false };
            }
        }
    }
}
