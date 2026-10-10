using System;

namespace Chopper.Core
{
    public enum DayPhase
    {
        Day = 0,
        Dusk = 1,
        Night = 2,
        Dawn = 3
    }

    // Pure day and night timing (plan T23). The server ticks one clock; clients only receive phase, day, and
    // the seconds left, then run a local copy to stay smooth between updates.
    public class DayNightClock
    {
        private const int PhaseCount = 4;

        private readonly float[] _durations;

        public DayPhase Phase { get; private set; } = DayPhase.Day;
        public float Elapsed { get; private set; }
        public int Day { get; private set; } = 1;

        // Raised for every phase change, including the ones inside a single large Advance call.
        public event Action<DayPhase> PhaseChanged;

        public DayNightClock(float day, float dusk, float night, float dawn)
        {
            _durations = new[] { day, dusk, night, dawn };
        }

        public float PhaseDuration(DayPhase phase) => _durations[(int)phase];

        public float Remaining => Math.Max(0f, PhaseDuration(Phase) - Elapsed);

        public void Advance(float seconds)
        {
            Elapsed += Math.Max(0f, seconds);
            while (Elapsed >= PhaseDuration(Phase))
            {
                Elapsed -= PhaseDuration(Phase);
                NextPhase();
            }
        }

        // Jumps straight to the start of the next phase. Used by debug controls and restarts.
        public void SkipPhase()
        {
            Elapsed = 0f;
            NextPhase();
        }

        public void Restart()
        {
            Phase = DayPhase.Day;
            Elapsed = 0f;
            Day = 1;
        }

        // Applies a state received over the network without raising events.
        public void Set(DayPhase phase, int day, float elapsed)
        {
            Phase = phase;
            Day = day;
            Elapsed = elapsed;
        }

        // 0 in full daylight, 1 in full night, ramping through dusk and dawn.
        public float Darkness
        {
            get
            {
                float t = PhaseDuration(Phase) > 0f ? Elapsed / PhaseDuration(Phase) : 0f;
                switch (Phase)
                {
                    case DayPhase.Dusk: return t;
                    case DayPhase.Night: return 1f;
                    case DayPhase.Dawn: return 1f - t;
                    default: return 0f;
                }
            }
        }

        // Days elapsed since the start, as a fraction. The boss sapling growth is measured with this.
        public float WorldDays
        {
            get
            {
                float cycle = _durations[0] + _durations[1] + _durations[2] + _durations[3];
                float before = 0f;
                for (int i = 0; i < (int)Phase; i++) before += _durations[i];
                return (Day - 1) + (before + Elapsed) / cycle;
            }
        }

        private void NextPhase()
        {
            int next = ((int)Phase + 1) % PhaseCount;
            if (next == (int)DayPhase.Day) Day++;
            Phase = (DayPhase)next;
            PhaseChanged?.Invoke(Phase);
        }
    }
}
