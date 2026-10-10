using System;
using UnityEngine;

namespace Chopper.Core
{
    public enum SchoolFishState
    {
        // Drifting about where it was found.
        Idle = 0,
        // It has noticed the lure and swims after it.
        Following = 1,
        // It took the lure.
        Biting = 2,
        // Spooked: it swims off and will not come back this cast.
        Fled = 3
    }

    // One fish of the school under a cast. Positions are on the water plane (x, z).
    public struct SchoolFish
    {
        public FishKind Kind;
        public float Kg;
        public Vector2 Position;
        public Vector2 Home;
        public Vector2 Wander;
        public float Interest;
        public SchoolFishState State;
        // How hard it is to tempt: 1 for a common fish, more for the rare ones.
        public float Shyness;
    }

    // The Ocarina of Time side of fishing. A cast lands near a few fish you can see. Work the lure (tap to twitch it,
    // hold to reel it in slowly) and the fish that notice it come after it; keep it moving gently and the keenest one
    // bites. Twitching too fast spooks them. A lure left still is taken in the end too, only later. Every fish has a
    // weight; bigger ones fight harder, and the heaviest of each kind is kept as a record.
    public static class LureRules
    {
        public const int MinSchool = 2;
        public const int MaxSchool = 4;
        public const float SchoolRadius = 3.5f;
        public const float TwitchStep = 0.7f;
        public const float ReelSpeed = 1.1f;
        // Twitches closer together than this are a splashy mess that spooks the fish.
        public const float SpamSeconds = 0.35f;
        // The lure counts as worked for this long after the last twitch.
        public const float ActionMemory = 1.4f;
        public const float FollowAt = 0.15f;
        public const float BiteDistance = 0.7f;
        // The lure is home (the cast is over) this close to the player.
        public const float ShoreStop = 2.5f;
        private const float BaseNotice = 4.5f;
        private const float NoticePerLure = 0.6f;
        private const float GainPerSecond = 0.22f;
        private const float TwitchGain = 0.12f;
        private const float SpamLoss = 0.45f;
        private const float IdleDecay = 0.08f;
        private const float FleeBelow = -0.2f;
        private const float FollowSpeed = 1.7f;
        private const float IdleSpeed = 0.45f;
        private const float FleeSpeed = 3.2f;
        private const float TrailDistance = 0.45f;
        private const float WanderRadius = 2.2f;

        public static float NoticeRadius(int lure, bool hotspot)
        {
            return (BaseNotice + NoticePerLure * Mathf.Clamp(lure, 0, FishingRules.MaxLureLevel)) * (hotspot ? 1.25f : 1f);
        }

        public static float Shyness(int rarity) => 1f + 0.45f * Mathf.Clamp(rarity, 0, 3);

        // How many fish are under a cast: a hotspot holds one more.
        public static int SchoolSize(double roll, bool hotspot)
        {
            int n = MinSchool + (roll < 0.5 ? 0 : 1) + (hotspot ? 1 : 0);
            return Mathf.Clamp(n, MinSchool, MaxSchool);
        }

        // ---------- Weight ----------

        // The lightest and heaviest a kind grows, from how much it heals: a perch is under a kilo, Old Mossback is huge.
        public static float MinKg(FishSpecies s) => 0.25f + 0.02f * Mathf.Max(0, s.Heal);
        public static float MaxKg(FishSpecies s) => MinKg(s) * (2.6f + 0.3f * Mathf.Clamp(s.Rarity, 0, 3));

        // Most fish are small; a big one is rare (the roll is squared). Rounded to tens of grams.
        public static float Weigh(FishSpecies s, double roll)
        {
            float r = Mathf.Clamp01((float)roll);
            float kg = Mathf.Lerp(MinKg(s), MaxKg(s), r * r);
            return Mathf.Round(kg * 100f) / 100f;
        }

        // 0 for the smallest of its kind, 1 for the biggest.
        public static float SizeFactor(FishSpecies s, float kg)
        {
            float min = MinKg(s);
            float max = MaxKg(s);
            return max > min ? Mathf.Clamp01((kg - min) / (max - min)) : 0f;
        }

        // A bigger fish fights harder: up to 40 percent more strain and pull than the smallest of its kind.
        public static float Strength(FishSpecies s, float kg) => 0.85f + 0.4f * SizeFactor(s, kg);

        public static bool IsRecord(float kg, float best) => kg > best + 0.001f;

        // ---------- The lure ----------

        // One twitch: the lure hops toward the shore.
        public static Vector2 Twitch(Vector2 lure, Vector2 shore) => Toward(lure, shore, TwitchStep);

        public static Vector2 Reel(Vector2 lure, Vector2 shore, float dt) => Toward(lure, shore, ReelSpeed * dt);

        public static bool Home(Vector2 lure, Vector2 shore) => Vector2.Distance(lure, shore) <= ShoreStop;

        private static Vector2 Toward(Vector2 from, Vector2 to, float step)
        {
            Vector2 offset = to - from;
            float d = offset.magnitude;
            return d <= step ? to : from + offset / d * step;
        }

        // A twitch draws in the fish that see it, or, too soon after the last one, scares them.
        public static void OnTwitch(SchoolFish[] fish, Vector2 lure, float sinceLastTwitch, float notice)
        {
            bool spam = sinceLastTwitch < SpamSeconds;
            for (int i = 0; i < fish.Length; i++)
            {
                if (fish[i].State == SchoolFishState.Fled || fish[i].State == SchoolFishState.Biting) continue;
                float d = Vector2.Distance(fish[i].Position, lure);
                if (spam)
                {
                    if (d > notice * 1.5f) continue;
                    fish[i].Interest -= SpamLoss;
                    if (fish[i].Interest < FleeBelow) fish[i].State = SchoolFishState.Fled;
                }
                else if (d <= notice)
                {
                    fish[i].Interest += TwitchGain / Mathf.Max(0.1f, fish[i].Shyness);
                }
            }
        }

        // One step of the school. sinceAction: seconds since the lure last moved (twitch or reel). canSwim tells
        // whether a spot is water. Returns the index of the fish that bit, or -1.
        public static int Step(SchoolFish[] fish, Vector2 lure, float sinceAction, int lureLevel, bool hotspot, float dt, Func<Vector2, bool> canSwim, System.Random rng)
        {
            float notice = NoticeRadius(lureLevel, hotspot);
            bool worked = sinceAction < ActionMemory;
            float keen = (1f + 0.25f * Mathf.Clamp(lureLevel, 0, FishingRules.MaxLureLevel)) * (hotspot ? 1.4f : 1f);
            int biter = -1;
            for (int i = 0; i < fish.Length; i++)
            {
                SchoolFish f = fish[i];
                if (f.State == SchoolFishState.Biting)
                {
                    f.Position = lure;
                    fish[i] = f;
                    continue;
                }
                float d = Vector2.Distance(f.Position, lure);
                if (f.State != SchoolFishState.Fled)
                {
                    bool sees = d <= notice || f.State == SchoolFishState.Following;
                    if (worked && sees) f.Interest += GainPerSecond * keen / Mathf.Max(0.1f, f.Shyness) * dt;
                    else if (!worked) f.Interest -= IdleDecay * dt;
                    f.Interest = Mathf.Min(f.Interest, 1.2f);
                    f.State = f.Interest >= FollowAt ? SchoolFishState.Following : SchoolFishState.Idle;
                }

                Vector2 target;
                float speed;
                switch (f.State)
                {
                    case SchoolFishState.Following:
                        // It trails just behind the lure until it is keen enough to take it.
                        Vector2 back = d > 0.01f ? (f.Position - lure) / d : Vector2.right;
                        target = f.Interest >= 1f ? lure : lure + back * TrailDistance;
                        speed = FollowSpeed;
                        break;
                    case SchoolFishState.Fled:
                        Vector2 away = d > 0.01f ? (f.Position - lure) / d : Vector2.up;
                        target = f.Position + away * 2f;
                        speed = FleeSpeed;
                        break;
                    default:
                        if (Vector2.Distance(f.Position, f.Wander) < 0.3f || f.Wander == Vector2.zero)
                        {
                            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                            float r = (float)rng.NextDouble() * WanderRadius;
                            Vector2 next = f.Home + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                            f.Wander = canSwim == null || canSwim(next) ? next : f.Home;
                        }
                        target = f.Wander;
                        speed = IdleSpeed;
                        break;
                }
                Vector2 moved = Toward(f.Position, target, speed * dt);
                if (canSwim == null || canSwim(moved) || f.State == SchoolFishState.Following) f.Position = moved;

                if (biter < 0 && f.State == SchoolFishState.Following && f.Interest >= 1f && Vector2.Distance(f.Position, lure) <= BiteDistance)
                {
                    f.State = SchoolFishState.Biting;
                    f.Position = lure;
                    biter = i;
                }
                fish[i] = f;
            }
            return biter;
        }

        // The fish that takes a lure left still: the keenest one that has not fled, or -1.
        public static int LazyBiter(SchoolFish[] fish)
        {
            int best = -1;
            for (int i = 0; i < fish.Length; i++)
            {
                if (fish[i].State == SchoolFishState.Fled) continue;
                if (best < 0 || fish[i].Interest > fish[best].Interest) best = i;
            }
            return best;
        }
    }
}
