using System;

namespace Chopper.World.Generation
{
    // Classic gradient (Perlin) noise with its own permutation table built from a seed, so every world seed gets its
    // own hills, and pure C# so it is the same on every peer and safe to run on worker threads (the terrain is
    // generated in parallel). Values are in 0..1 with 0.5 as the average, like Mathf.PerlinNoise.
    public sealed class SeededNoise
    {
        private readonly int[] _perm = new int[512];

        public SeededNoise(int seed)
        {
            var p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;
            var random = new Random(seed);
            for (int i = 255; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }
            for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
        }

        public float Sample(float x, float y)
        {
            int xi = FastFloor(x);
            int yi = FastFloor(y);
            float xf = x - xi;
            float yf = y - yi;
            xi &= 255;
            yi &= 255;
            float u = Fade(xf);
            float v = Fade(yf);
            int aa = _perm[_perm[xi] + yi];
            int ab = _perm[_perm[xi] + yi + 1];
            int ba = _perm[_perm[xi + 1] + yi];
            int bb = _perm[_perm[xi + 1] + yi + 1];
            float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1f, yf), u);
            float x2 = Lerp(Grad(ab, xf, yf - 1f), Grad(bb, xf - 1f, yf - 1f), u);
            // 2D gradient noise stays within about -0.75..0.75; scale it to fill 0..1 around 0.5.
            float value = Lerp(x1, x2, v) * 0.66f + 0.5f;
            return value < 0f ? 0f : (value > 1f ? 1f : value);
        }

        // Octaves of the noise, each twice as fine and half as strong. Result in 0..1.
        public float Fbm(float x, float y, int octaves)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float total = 0f;
            float frequency = 1f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amplitude * Sample(x * frequency + o * 17.17f, y * frequency + o * 9.31f);
                total += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.03f;
            }
            return sum / total;
        }

        private static int FastFloor(float x)
        {
            int i = (int)x;
            return x < i ? i - 1 : i;
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Grad(int hash, float x, float y)
        {
            switch (hash & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }
    }
}
