using UnityEngine;

namespace Chopper.Core
{
    public enum RhythmHit { OnBeat, OffBeat, Rest }

    public static class RhythmRules
    {
        public const float Beat = 0.5f;
        public const float Window = 0.12f;
        public const int BarBeats = 4;
        public const int RestBeat = 2;
        public const float LeadIn = BarBeats * Beat;
        public const int FullBar = (1 << 0) | (1 << 1) | (1 << 3);

        public static int BeatIndex(float sinceStart) => Mathf.RoundToInt(sinceStart / Beat);

        public static int Slot(float sinceStart) => ((BeatIndex(sinceStart) % BarBeats) + BarBeats) % BarBeats;

        public static int Bar(float sinceStart) => Mathf.FloorToInt((float)BeatIndex(sinceStart) / BarBeats);

        public static RhythmHit Judge(float sinceStart)
        {
            if (Slot(sinceStart) == RestBeat) return RhythmHit.Rest;
            float offset = Mathf.Abs(sinceStart - BeatIndex(sinceStart) * Beat);
            return offset <= Window ? RhythmHit.OnBeat : RhythmHit.OffBeat;
        }

        public static float Pulse(float sinceStart)
        {
            float phase = sinceStart / Beat;
            float since = phase - Mathf.Floor(phase);
            int slot = ((Mathf.FloorToInt(phase) % BarBeats) + BarBeats) % BarBeats;
            float peak = slot == RestBeat ? 0.2f : 1f;
            return peak * Mathf.Pow(1f - since, 3f);
        }

        public static bool Register(ref int bar, ref int mask, float sinceStart, RhythmHit hit)
        {
            int now = Bar(sinceStart);
            if (now != bar)
            {
                bar = now;
                mask = 0;
            }
            if (hit != RhythmHit.OnBeat)
            {
                mask = -1;
                return false;
            }
            if (mask < 0) return false;
            mask |= 1 << Slot(sinceStart);
            if (mask != FullBar) return false;
            mask = 0;
            return true;
        }
    }
}
