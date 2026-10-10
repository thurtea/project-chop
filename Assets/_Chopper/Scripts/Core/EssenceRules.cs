namespace Chopper.Core
{
    // One small count per element packed into a single int, so it syncs and saves as one number: essence (a byte per
    // element) and ward levels use the same packing. Neutral has no slot.
    public static class EssenceRules
    {
        public static readonly Element[] Elements = { Element.Water, Element.Fire, Element.Earth, Element.Wind };
        private const int Bits = 8;
        private const int Mask = (1 << Bits) - 1;

        private static int Shift(Element element) => ((int)element - 1) * Bits;

        public static int Get(int packed, Element element)
        {
            if (element == Element.Neutral) return 0;
            return (packed >> Shift(element)) & Mask;
        }

        public static int Set(int packed, Element element, int value)
        {
            if (element == Element.Neutral) return packed;
            int clamped = value < 0 ? 0 : (value > Mask ? Mask : value);
            int shift = Shift(element);
            return (packed & ~(Mask << shift)) | (clamped << shift);
        }
    }
}
