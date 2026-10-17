using System.Collections.Generic;
using Chopper.Core;
using Chopper.Trees;
using UnityEngine;

namespace Chopper.World.Generation
{
    // Where things grow and lie. One woodland field decides where the forests and their glades are; trees, plants,
    // rocks and branches follow it instead of being scattered evenly:
    // - trees stand in groves, sparse at the edges, the oldest and tallest in the middle, the same kind in stands;
    // - the hub is an open meadow that thickens into trees toward the mountains, with a few lone trees;
    // - rocks lie in groups at the foot of the mountains, on slopes and on lake shores, rarely in a meadow;
    // - bushes line forest edges, paths and shores; ferns grow in shade; herbs grow in patches in glades;
    // - branches lie under trees.
    public sealed partial class WorldGen
    {
        public enum PlantKind { Herb, Fern, Bush }

        private SeededNoise _woods;
        private SeededNoise _stands;
        private SeededNoise Woods => _woods ??= new SeededNoise(Seed * 31 + 21);
        private SeededNoise Stands => _stands ??= new SeededNoise(Seed * 31 + 22);

        private const float GroveScale = 48f;
        private const float StandScale = 26f;

        private static float CoverFor(Element zone)
        {
            switch (zone)
            {
                case Element.Earth: return 0.8f;
                case Element.Water: return 0.7f;
                case Element.Wind: return 0.58f;
                case Element.Fire: return 0.52f;
                default: return 0.5f;
            }
        }

        // 0 open ground .. 1 the heart of a grove.
        public float Woodland(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            Element zone = ZoneMap.ElementAt(x, z, HubRadius);
            float t = GroveThreshold(zone);
            float w = Smooth(t - 0.06f, t + 0.06f, GroveNoise(x, z));
            if (zone == Element.Neutral) w *= Smooth(HubRadius * 0.5f, HubRadius * 0.92f, r);
            w *= Usable(x, z, r);
            if (w <= 0f) return 0f;
            float shore = DistanceToShore(x, z);
            if (shore < 2.5f) return 0f;
            if (zone == Element.Water && shore < 16f) w = Mathf.Max(w, 0.8f * (1f - (shore - 2.5f) / 13.5f));
            return w;
        }

        private float GroveNoise(float x, float z) => Woods.Fbm(x / GroveScale, z / GroveScale, 3);

        // 0 at a grove's edge .. 1 deep inside it.
        public float GroveDepth(float x, float z)
        {
            float t = GroveThreshold(ZoneMap.ElementAt(x, z, HubRadius));
            return Smooth(t, t + 0.12f, GroveNoise(x, z));
        }

        // Land trees could stand on at all: off the paths, the mountains and the beach.
        private float Usable(float x, float z, float r)
        {
            float u = Smooth(PathHalfWidth + 1.5f, PathHalfWidth + 7f, DistanceToPath(x, z));
            u *= 1f - Smooth(0.06f, 0.2f, MountainWeight(x, z));
            return u * (1f - Smooth(BeachStartRadius - 12f, BeachStartRadius, r));
        }

        // The grove noise value above which a zone is woodland, chosen so the zone's usable land is covered to its
        // share whatever the noise of this seed looks like.
        private readonly Dictionary<Element, float> _groveThresholds = new Dictionary<Element, float>();

        private float GroveThreshold(Element zone)
        {
            if (_groveThresholds.TryGetValue(zone, out float t)) return t;
            var values = new List<float>();
            for (float z = -BeachStartRadius; z <= BeachStartRadius; z += 6f)
                for (float x = -BeachStartRadius; x <= BeachStartRadius; x += 6f)
                {
                    float r = Mathf.Sqrt(x * x + z * z);
                    if (r > BeachStartRadius || ZoneMap.ElementAt(x, z, HubRadius) != zone) continue;
                    if (zone == Element.Neutral && r < HubRadius * 0.5f) continue;
                    if (Usable(x, z, r) < 0.5f) continue;
                    values.Add(GroveNoise(x, z));
                }
            values.Sort();
            t = values.Count > 0 ? values[Mathf.Clamp((int)((1f - CoverFor(zone)) * values.Count), 0, values.Count - 1)] : 0.5f;
            _groveThresholds[zone] = t;
            return t;
        }

        // Meters from the nearest lake or pond edge (negative inside the water).
        public float DistanceToShore(float x, float z)
        {
            float best = float.MaxValue;
            foreach (WaterBody body in WaterBodies)
                best = Mathf.Min(best, Vector2.Distance(new Vector2(x, z), body.Center) - ShoreEdge(body));
            return best;
        }

        private bool OpenGround(Vector2 p, float pathMargin)
        {
            if (DistanceToShore(p.x, p.y) < 1.2f || InWater(p, 1.05f)) return false;
            if (SurfaceAt(p.x, p.y) < WaterHeight + 0.3f) return false;
            if (MountainWeight(p.x, p.y) > 0.2f) return false;
            if (IsInsideYard(p) || OnPath(p.x, p.y, pathMargin)) return false;
            if (NearZoneGate(p, 3f) || NearCrypt(p, 3f) || NearMine(p, 4f)) return false;
            return Mathf.Atan(SlopeAt(p.x, p.y, 2f)) * Mathf.Rad2Deg < 30f;
        }

        // ---------- Trees ----------

        // The model for a tree here: one kind per stand, a few strays.
        private int StandModel(System.Random random, int[] zoneSpecies, Vector2 p)
        {
            if (zoneSpecies.Length == 1 || random.NextDouble() < 0.15) return zoneSpecies[random.Next(zoneSpecies.Length)];
            float s = Mathf.Clamp01((Stands.Sample(p.x / StandScale, p.y / StandScale) - 0.5f) * 2.5f + 0.5f);
            return zoneSpecies[Mathf.Min(zoneSpecies.Length - 1, (int)(s * zoneSpecies.Length))];
        }

        // Older, taller trees toward the heart of a grove; saplings at its edge.
        private static TreeSize GroveSize(System.Random random, float depth, Element zone)
        {
            float age = depth + ((float)random.NextDouble() - 0.5f) * 0.4f;
            if (zone == Element.Fire || zone == Element.Wind) age -= 0.1f;
            return age > 0.78f ? TreeSize.Large : age > 0.42f ? TreeSize.Medium : TreeSize.Small;
        }

        // ---------- Rocks ----------

        // 0..1: rocks gather at the foot of the mountains, on slopes and on lake shores.
        public float Stoniness(float x, float z)
        {
            float mw = MountainWeight(x, z);
            float foot = Smooth(0.0f, 0.06f, mw) * (1f - Smooth(0.16f, 0.25f, mw));
            float slope = Smooth(0.2f, 0.5f, SlopeAt(x, z, 2f));
            float shore = DistanceToShore(x, z);
            float bank = shore > 1.5f && shore < 6f ? 0.45f : 0f;
            return Mathf.Clamp01(Mathf.Max(Mathf.Max(foot, slope * 0.7f), bank) + 0.015f);
        }

        // ---------- Plants and branches ----------

        public List<Vector2> PlantSpots(PlantKind kind)
        {
            int seed = kind == PlantKind.Herb ? 4711 : kind == PlantKind.Fern ? 90210 : 12011;
            int count = kind == PlantKind.Herb ? 240 : kind == PlantKind.Fern ? 160 : 700;
            int patchMin = kind == PlantKind.Herb ? 3 : 2;
            int patchMax = kind == PlantKind.Herb ? 6 : kind == PlantKind.Fern ? 4 : 5;
            float patchRadius = kind == PlantKind.Bush ? 3f : 2.2f;
            var random = new System.Random(seed ^ Seed);
            var list = new List<Vector2>();
            for (int tries = 0; tries < count * 60 && list.Count < count; tries++)
            {
                Vector2 c = RandomPointInDisc(random, 22f, BeachStartRadius - 6f);
                if (random.NextDouble() > PlantChance(kind, c)) continue;
                int patch = random.Next(patchMin, patchMax + 1);
                for (int i = 0; i < patch && list.Count < count; i++)
                {
                    Vector2 p = i == 0 ? c : c + RandomPointInDisc(random, 0.6f, patchRadius);
                    if (OpenGround(p, 0.8f)) list.Add(p);
                }
            }
            return list;
        }

        private float PlantChance(PlantKind kind, Vector2 p)
        {
            float w = Woodland(p.x, p.y);
            switch (kind)
            {
                case PlantKind.Herb:
                    // Glades and the sunny edge of a grove, and the hub meadow.
                    float glade = 1f - Mathf.Abs(w - 0.25f) / 0.25f;
                    bool meadow = ZoneMap.ElementAt(p.x, p.y, HubRadius) == Element.Neutral && w < 0.2f;
                    return Mathf.Clamp01(Mathf.Max(glade, meadow ? 0.25f : 0f));
                case PlantKind.Fern:
                    float shade = Smooth(0.5f, 0.85f, w);
                    float wet = DistanceToShore(p.x, p.y) < 8f ? 0.6f : 0f;
                    return Mathf.Max(shade, wet);
                default:
                    float edge = 4f * w * (1f - w);
                    float path = DistanceToPath(p.x, p.y);
                    float verge = path > PathHalfWidth + 1.2f && path < PathHalfWidth + 5f ? 0.45f : 0f;
                    float shore = DistanceToShore(p.x, p.y);
                    float bank = shore > 1.5f && shore < 6f ? 0.4f : 0f;
                    return Mathf.Max(edge, Mathf.Max(verge, bank)) * 0.9f + 0.02f;
            }
        }

        // Fallen branches under the trees, bigger trees dropping more.
        public List<Vector2> BranchSpots(IReadOnlyList<TreeEntry> trees, int count = 302)
        {
            var random = new System.Random(9157 ^ Seed);
            var list = new List<Vector2>();
            if (trees == null || trees.Count == 0) return list;
            for (int tries = 0; tries < count * 30 && list.Count < count; tries++)
            {
                TreeEntry tree = trees[random.Next(trees.Count)];
                if (random.NextDouble() > 0.3 + 0.3 * (int)tree.Size) continue;
                Vector2 trunk = new Vector2(tree.Position.x, tree.Position.z);
                Vector2 p = trunk + RandomPointInDisc(random, 1.2f, 2.5f + (int)tree.Size);
                if (OpenGround(p, 0.5f)) list.Add(p);
            }
            return list;
        }
    }
}
