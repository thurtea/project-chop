using FishNet;
using FishNet.Managing.Timing;
using UnityEngine;

namespace Chopper.World
{
    // The clock the veins glow by, the same on every peer: the network's tick time (a client's estimate of the
    // server's), so the glint a player sees is the glint the strike is judged against. Falls back to local time
    // before the network is up.
    public static class MiningClock
    {
        public static float Now
        {
            get
            {
                TimeManager time = InstanceFinder.TimeManager;
                if (time == null) return Time.time;
                return (float)time.TicksToTime(time.Tick);
            }
        }
    }
}
