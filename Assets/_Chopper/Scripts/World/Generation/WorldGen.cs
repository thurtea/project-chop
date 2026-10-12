using System.Collections.Generic;
using Chopper.Core;
using Chopper.Trees;
using UnityEngine;

namespace Chopper.World.Generation
{
    // A pond, marsh pool or forest lake. Radius is the whole bowl. For a deep lake, Depth is how far the bottom sits
    // under the water line and SurfaceRadius is the water itself; the bank outside that is dry ground. A pond or marsh
    // pool leaves SurfaceRadius at 0 and uses Depth as how far the ground is dug down.
    public struct WaterBody
    {
        public Vector2 Center;
        public float Radius;
        public float Depth;
        public float SurfaceRadius;
        public bool Marsh;
        public bool Deep;
    }

    // A boulder: where it stands, which rock model, how wide (meters) and its turn.
    public struct RockSpot
    {
        public Vector2 Position;
        public int Model;
        public float Size;
        public float Yaw;
    }

    // Where a new player may wake up, outside the house.
    public struct SpawnSpot
    {
        public Vector3 Position;
        public float Yaw;
    }

    // The world, described from one seed, the way survival games make each world: every shape (hills, ponds, paths,
    // where the forests grow, rocks, spawn points, the grass map) comes from this class and the seed alone, so every
    // peer that knows the seed builds the same island. The layout stays the same from world to world (the hut on its
    // mound at the center, the four elemental forests around it, the sea at the edge); what changes is the land.
    // Pure C# apart from Unity math types, safe to call from worker threads (WorldBuilder fills the terrain in parallel).
    public sealed class WorldGen
    {
        // ---------- Layout (meters) ----------
        public const float TerrainSize = 640f;
        public const float HalfSize = TerrainSize * 0.5f;
        public const float TerrainMaxHeight = 110f;
        // Terrain heights are stored from this base up, so valleys and the sea floor can sit below the hut floor.
        public const float TerrainBaseY = -12f;
        public const int HeightmapResolution = 1025;
        public const int AlphamapResolution = 512;
        public const int GrassMapResolution = 512;

        // The hub stays flat near the hut, which sits on a low mound.
        public const float FlatRadius = 45f;
        public const float MoundHeight = 4.5f;
        public const float MoundFlatRadius = 19f;
        public const float MoundEndRadius = 55f;
        public const float HillsStartRadius = 130f;
        public const float HillAmplitude = 9f;
        public const float DetailAmplitude = 1.6f;

        // The edge of the world is the sea: low ground near the edge is lifted into a beach, then slopes under water.
        public const float BeachStartRadius = 212f;
        public const float BeachEndRadius = 248f;
        public const float BeachHeight = 0.6f;
        public const float CoastStartRadius = 256f;
        public const float CoastEndRadius = 300f;
        public const float SeaFloorHeight = -7f;
        public const float SeaInnerRadius = 246f;
        public const float SeaOuterRadius = 900f;
        // Halfway between two block tops (VoxelColumns.Step 0.5), W5-08 step 8: a -1.0 top is a 0.25 m wade, a -0.5
        // bank stands 0.25 m clear, and no block top lies in the water plane (at -1.0 they would z-fight).
        public const float WaterHeight = -0.75f;
        public const float BoundaryOffset = 314f;

        public const float PondInnerRadius = 12f;
        public const float PondOuterRadius = 24f;
        public const float PondDepth = 2.5f;

        public const float PathStartRadius = 13f;
        public const float PathHalfWidth = 2.2f;
        public const float PathIntoForest = 70f;
        public const float ZoneGateOffset = 18f;
        public const float ZoneGateClearRadius = 18f;

        // Mountains wall the zones off: a ring around the hub, cut by one narrow pass on each path, and a high ridge
        // along each diagonal out to the corners, so no forest can be seen or reached from the next. Their sides are
        // far steeper than the 45 degrees players and enemies can climb.
        public const float RingOffset = 18f;
        public const float RingCrestHalfWidth = 4f;
        public const float RingFootHalfWidth = 20f;
        public const float RingHeight = 20f;
        public const float RidgeCrestHalfWidth = 5f;
        public const float RidgeFootHalfWidth = 26f;
        public const float RidgeHeight = 28f;
        public const float PassHalfWidth = 5f;
        public const float PassWallWidth = 9f;
        public const float MountainCragHeight = 7f;
        // The walls are rock, not a smooth bank: they climb in this many ledges, each riser a short cliff.
        public const int MountainLedges = 3;
        // Every mountain riser is this tall: above the 3.05 m a player reaches (double jump 2.4 m plus the 0.65 step)
        // even where the ground under it already falls 1 m (4 m leaked there on seed 4242).
        public const float MountainStep = 4.5f;
        // Block tops above this are snow: only the ridge tops (up to 27 m); the 18 m ring crest stays stone (concept 2.0 caps the tips).
        public const float SnowLine = 22f;
        // How much of the wall follows the ledges (the rest stays the plain slope, so ledges tilt a little).
        private const float LedgeStrength = 0.7f;
        // How far the ledges drift up and down along a wall, as a share of the wall profile.
        private const float LedgeWander = 0.14f;
        // Gullies: grooves running down the faces, deepest halfway up.
        private const float GullyDepth = 4.5f;
        // The crest breaks into teeth and notches this tall.
        private const float CrestBreakHeight = 5f;
        // Rubble at the foot: low lumps that start inside the foot and run out this far past it.
        private const float ApronHeight = 2f;
        private const float ApronReach = 6f;

        // The hut at the center; the yard torches stand on a ring of WallRadius (where the palisade stood).
        public const float WallRadius = 14f;
        // The hub house is 18 m x 12 m (Hut.HouseHalfSize), with log steps out of the front: no grass inside or on them.
        public const float HutHalfWidth = 9.5f;
        public const float HutHalfDepth = 6.5f;

        // The house terrace (concept 2.0: the hall stands on a raised grass terrace with stone steps). Around the 18 m x
        // 12 m footprint the ground is raised TerraceHeight, TerraceMargin wide (wider at the front for the porch), then
        // steps down TerraceStep at a time, one metre a step. Inside the footprint the ground stays at the hut floor, so
        // the basement storey is under the terrace.
        public const float HouseHalfX = 9f;
        public const float HouseHalfZ = 6f;
        public const float TerraceHeight = 2.5f;
        public const float TerraceMargin = 3f;
        public const float TerraceFrontMargin = 5f;
        public const float TerraceStep = 0.5f;
        // The stone steps run down the front from the porch, this wide either side of the door (x = 1.5 m).
        public const float TerraceStairsX = 1.5f;
        public const float TerraceStairsHalfWidth = 2f;

        // How far outside the terrace top a point is, in metres (0 or less on the top).
        static float TerraceDistance(float x, float z)
        {
            float dx = Mathf.Abs(x) - HouseHalfX;
            float dz = z > 0f ? z - HouseHalfZ - (TerraceFrontMargin - TerraceMargin) : -z - HouseHalfZ;
            return Mathf.Max(dx, dz) - TerraceMargin;
        }

        public static float TerraceRaise(float x, float z)
        {
            if (Mathf.Abs(x) < HouseHalfX && Mathf.Abs(z) < HouseHalfZ) return 0f;
            float d = TerraceDistance(x, z);
            if (d <= 0f) return TerraceHeight;
            int steps = Mathf.CeilToInt(d);
            return Mathf.Max(0f, TerraceHeight - steps * TerraceStep);
        }

        // The front steps from the terrace down to the yard are stone.
        public static bool TerraceStairs(float x, float z)
        {
            return z > HouseHalfZ + TerraceFrontMargin - 0.5f && Mathf.Abs(x - TerraceStairsX) <= TerraceStairsHalfWidth
                   && TerraceDistance(x, z) <= TerraceHeight / TerraceStep;
        }

        // Forest order used for terrain layers 1 to 4 (after the hub layer).
        public static readonly Element[] Forests = { Element.Water, Element.Earth, Element.Fire, Element.Wind };

        public readonly int Seed;
        public readonly float HubRadius;
        public readonly Vector2 PondCenter;
        public readonly WaterBody[] WaterBodies;
        public readonly CryptSite[] Crypts;
        public readonly MineSite[] Mines;

        private readonly SeededNoise _warpX;
        private readonly SeededNoise _warpZ;
        private readonly SeededNoise _hills;
        private readonly SeededNoise _detail;
        private readonly SeededNoise _ridges;
        private readonly SeededNoise _swells;
        private readonly SeededNoise _patches;
        private readonly float _pathPhaseA;
        private readonly float _pathPhaseB;

        public WorldGen(int seed, float hubRadius)
        {
            Seed = seed;
            HubRadius = hubRadius;
            _warpX = new SeededNoise(seed * 31 + 1);
            _warpZ = new SeededNoise(seed * 31 + 2);
            _hills = new SeededNoise(seed * 31 + 3);
            _detail = new SeededNoise(seed * 31 + 4);
            _ridges = new SeededNoise(seed * 31 + 5);
            _swells = new SeededNoise(seed * 31 + 6);
            _patches = new SeededNoise(seed * 31 + 7);
            var random = new System.Random(seed);
            _pathPhaseA = (float)random.NextDouble() * 20f;
            _pathPhaseB = (float)random.NextDouble() * 20f;

            // The pond sits somewhere in the open hub, clear of the paths and the hut.
            Vector2 pond = new Vector2(60f, -45f);
            for (int tries = 0; tries < 200; tries++)
            {
                Vector2 candidate = RandomPointInDisc(random, 55f, hubRadius - PondOuterRadius - 10f);
                if (DistanceToPath(candidate.x, candidate.y) < PondOuterRadius + 6f) continue;
                pond = candidate;
                break;
            }
            PondCenter = pond;
            WaterBodies = BuildWaterBodies(new System.Random(seed + 7)).ToArray();
            Crypts = BuildCrypts(new System.Random(seed + 13)).ToArray();
            Mines = BuildMines(new System.Random(seed + 29)).ToArray();
        }

        // ---------- Shape helpers ----------

        public static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        public static Vector2 RandomPointInDisc(System.Random random, float minRadius, float maxRadius)
        {
            // Uniform by area between the two radii.
            float radius = Mathf.Sqrt(Mathf.Lerp(minRadius * minRadius, maxRadius * maxRadius, (float)random.NextDouble()));
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        // How strongly a point belongs to one forest's direction, with a fairly sharp border so each zone reads as its
        // own place: 0 outside the forest's quadrant, 1 well inside it.
        private static float ZoneWeight(Element forest, float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            if (r < 1f) return 0f;
            Vector2 axis = ZoneMap.Axis(forest);
            float alignment = Mathf.Max(0f, (x * axis.x + z * axis.y) / r);
            return Smooth(0.62f, 0.82f, alignment);
        }

        private float OutsideHub(float r) => Smooth(HubRadius - 10f, HubRadius + 50f, r);

        // ---------- Paths ----------

        private float PathOffset(float along)
        {
            // A gentle wobble so the paths are not ruler straight; each world wobbles differently.
            return Mathf.Sin(along * 0.035f + _pathPhaseA) * 5f + Mathf.Sin(along * 0.09f + 1.3f + _pathPhaseB) * 1.5f;
        }

        // Distance from a point to the nearest path center line, or a large number if no path is near.
        public float DistanceToPath(float x, float z)
        {
            float best = 1000f;
            float r = Mathf.Sqrt(x * x + z * z);
            if (r < PathStartRadius || r > HubRadius + PathIntoForest) return best;
            foreach (Element forest in Forests)
            {
                Vector2 axis = ZoneMap.Axis(forest);
                float along = x * axis.x + z * axis.y;
                if (along < PathStartRadius) continue;
                float perp = Mathf.Abs(x * axis.y - z * axis.x);
                best = Mathf.Min(best, Mathf.Abs(perp - PathOffset(along)));
            }
            return best;
        }

        public bool OnPath(float x, float z, float margin) => DistanceToPath(x, z) < PathHalfWidth + margin;

        // 1 on the path, fading to 0 just outside it.
        public float PathWeight(float x, float z) => 1f - Smooth(PathHalfWidth, PathHalfWidth + 1.6f, DistanceToPath(x, z));

        // Where each path leaves the hub through its mountain gorge (the "zone gate", kept clear of trees). Its offset
        // uses the same wobble as the path, so every gate stays centered on its path for every seed.
        public Vector2 ZoneGatePosition(Element forest)
        {
            Vector2 axis = ZoneMap.Axis(forest);
            float along = HubRadius + ZoneGateOffset;
            Vector2 perpendicular = new Vector2(axis.y, -axis.x);
            return axis * along + perpendicular * PathOffset(along);
        }

        public bool NearZoneGate(Vector2 position, float margin)
        {
            foreach (Element forest in Forests)
            {
                if (Vector2.Distance(position, ZoneGatePosition(forest)) < ZoneGateClearRadius + margin) return true;
            }
            return false;
        }

        // ---------- Mountains ----------

        public float RingRadius => HubRadius + RingOffset;

        private static float DiagonalDistance(float x, float z) => Mathf.Abs(Mathf.Abs(x) - Mathf.Abs(z)) * 0.70710678f;

        private float RingWeight(float x, float z, float r)
        {
            float ring = 1f - Smooth(RingCrestHalfWidth, RingFootHalfWidth, Mathf.Abs(r - RingRadius));
            // The pass: the path cuts a steep sided gorge through the ring.
            if (ring > 0f) ring *= Smooth(PassHalfWidth, PassHalfWidth + PassWallWidth, DistanceToPath(x, z));
            return ring;
        }

        private float RidgeWeight(float x, float z, float r)
        {
            return (1f - Smooth(RidgeCrestHalfWidth, RidgeFootHalfWidth, DiagonalDistance(x, z))) * Smooth(RingRadius - 12f, RingRadius, r);
        }

        // 0 on open ground, 1 on a crest. Trees, rocks, pools, crypts and spawns keep off the mountains.
        public float MountainWeight(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            return Mathf.Max(RingWeight(x, z, r), RidgeWeight(x, z, r));
        }

        // True on or within margin meters of a mountain's foot (the pass floor does not count).
        public bool NearMountain(Vector2 p, float margin)
        {
            float r = p.magnitude;
            bool ring = Mathf.Abs(r - RingRadius) < RingFootHalfWidth + margin && DistanceToPath(p.x, p.y) > PassHalfWidth + PassWallWidth * 0.5f;
            bool ridge = DiagonalDistance(p.x, p.y) < RidgeFootHalfWidth + margin && r > RingRadius - 12f - margin;
            return ring || ridge;
        }

        private float MountainHeight(float x, float z, float r)
        {
            float ring = RingWeight(x, z, r);
            float ridge = RidgeWeight(x, z, r);
            float apron = ApronWeight(x, z, r);
            float weight = Mathf.Max(ring, ridge);
            if (weight <= 0f && apron <= 0f) return 0f;

            // Ledges: the plain wall profile is bent into flat shelves with short cliffs between them. The shelves wander
            // up and down along the wall (and now and then merge), so they do not read as contour lines.
            float wander = (_ridges.Sample(x * 0.021f + 13f, z * 0.021f - 7f) - 0.5f) * 2f * LedgeWander;
            float wall = Mathf.Max(Ledged(Wandered(ring, wander)) * RingHeight, Ledged(Wandered(ridge, wander)) * RidgeHeight);
            float crag = (_ridges.Fbm(x * 0.03f, z * 0.03f, 2) - 0.5f) * 2f * MountainCragHeight * weight * weight;

            // Gullies run downhill: across the ring that is outward, across a ridge it is sideways, so the grooves
            // follow the angle around the ring and the distance along a ridge.
            float across = ring >= ridge ? Mathf.Atan2(z, x) * RingRadius : (Mathf.Abs(x) + Mathf.Abs(z)) * 0.70710678f;
            float down = ring >= ridge ? r : DiagonalDistance(x, z);
            float groove = 1f - Mathf.Abs(2f * _ridges.Sample(across * 0.11f + 311f, down * 0.015f) - 1f);
            float gully = groove * groove * groove * GullyDepth * 4f * weight * (1f - weight);

            float crest = (_ridges.Fbm(x * 0.09f + 57f, z * 0.09f, 2) - 0.5f) * 2f * CrestBreakHeight * weight * weight * weight;
            float rubble = _ridges.Fbm(x * 0.12f - 91f, z * 0.12f, 2) * ApronHeight * apron;
            // Block mountains (W5-08 step 9): the whole mountain share moves in MountainStep risers, so on a mountain
            // two neighbouring columns differ by the ground's own half-block wobble (a shelf) or by a full riser that
            // a double jump plus the step cannot clear (VoxelChecks.ZoneSeal). Stacked blocks, as in concept 2.0.
            return Mathf.Round((wall + crag - gully + crest + rubble) / MountainStep) * MountainStep;
        }

        // Shifts a 0 to 1 wall profile by up to offset in the middle of the wall, never at its foot or crest.
        private static float Wandered(float weight, float offset) => Mathf.Clamp01(weight + offset * 4f * weight * (1f - weight));

        // A 0 to 1 wall profile turned into MountainLedges shelves, each ending in a steep riser.
        private static float Ledged(float weight)
        {
            if (weight <= 0f) return 0f;
            float t = weight * MountainLedges;
            float step = Mathf.Floor(t);
            float stepped = (step + Smooth(0.55f, 1f, t - step)) / MountainLedges;
            return Mathf.Lerp(weight, stepped, LedgeStrength);
        }

        // 1 around the foot of a wall (ring or ridge), fading out ApronReach meters past it. Never on a pass floor.
        private float ApronWeight(float x, float z, float r)
        {
            float ringFoot = Mathf.Abs(r - RingRadius);
            float ring = 1f - Smooth(RingFootHalfWidth - 8f, RingFootHalfWidth + ApronReach, ringFoot);
            ring *= Smooth(PassHalfWidth + 2f, PassHalfWidth + PassWallWidth + 4f, DistanceToPath(x, z));
            float ridge = (1f - Smooth(RidgeFootHalfWidth - 8f, RidgeFootHalfWidth + ApronReach, DiagonalDistance(x, z))) * Smooth(RingRadius - 12f, RingRadius, r);
            return Mathf.Max(ring, ridge);
        }

        // ---------- Water ----------

        private List<WaterBody> BuildWaterBodies(System.Random random)
        {
            var bodies = new List<WaterBody>
            {
                new WaterBody { Center = PondCenter, Radius = PondOuterRadius, Depth = PondDepth, Marsh = false }
            };
            // The Water forest is a marsh: a scatter of pools across the north.
            int guard = 0;
            while (bodies.Count < 11 && guard++ < 2000)
            {
                Vector2 c = RandomPointInDisc(random, HubRadius + 30f, BeachStartRadius - 30f);
                if (ZoneMap.ElementAt(c.x, c.y, HubRadius) != Element.Water) continue;
                float radius = Mathf.Lerp(13f, 26f, (float)random.NextDouble());
                if (DistanceToPath(c.x, c.y) < radius + 6f) continue;
                if (NearMountain(c, radius + 4f)) continue;
                bool overlaps = false;
                foreach (WaterBody other in bodies)
                {
                    if (Vector2.Distance(other.Center, c) < other.Radius + radius + 8f) overlaps = true;
                }
                if (overlaps) continue;
                bodies.Add(new WaterBody { Center = c, Radius = radius, Depth = 2.8f, Marsh = true });
            }
            // The other forests get a few wide, deep lakes. The bowl is broad enough that the shore stays walkable.
            foreach (Element forest in new[] { Element.Fire, Element.Earth, Element.Wind })
                PlaceLakes(bodies, random, forest, 1);
            return bodies;
        }

        private void PlaceLakes(List<WaterBody> bodies, System.Random random, Element forest, int count)
        {
            int placed = 0;
            int guard = 0;
            while (placed < count && guard++ < 8000)
            {
                // The forests are a band between the mountain ring and the beach, so a lake has to be wide without
                // spilling into either. The bowl is dug out, and the bank climbs gently whatever the hills were.
                Vector2 c = RandomPointInDisc(random, HubRadius + 22f, BeachStartRadius - 24f);
                if (ZoneMap.ElementAt(c.x, c.y, HubRadius) != forest) continue;
                if (NearMountain(c, 2f)) continue;
                float room = Mathf.Min(c.magnitude - HubRadius - 6f, BeachStartRadius - 8f - c.magnitude);
                room = Mathf.Min(room, 46f);
                if (room < 34f) continue;
                if (DistanceToPath(c.x, c.y) < 16f) continue;
                // Prefer a low spot. After many misses, take higher ground: the bowl is dug either way.
                if (guard < 4000 && HeightAt(c.x, c.y) > 6f) continue;
                float depth = Mathf.Lerp(4.8f, 6.6f, (float)random.NextDouble());
                float inner = Mathf.Lerp(13f, 16f, (float)random.NextDouble());
                // The bank rises at half a meter up per meter out, under the 45 degree limit.
                float surface = inner + depth / 0.5f;
                const float skirt = 10f;
                float lakeRadius = surface + skirt;
                if (surface < 22f || lakeRadius > room) continue;
                bool overlaps = false;
                foreach (WaterBody other in bodies)
                {
                    if (Vector2.Distance(other.Center, c) < other.Radius + lakeRadius + 10f) overlaps = true;
                }
                if (overlaps) continue;
                bodies.Add(new WaterBody
                {
                    Center = c,
                    Radius = lakeRadius,
                    Depth = depth,
                    SurfaceRadius = surface,
                    Marsh = false,
                    Deep = true
                });
                placed++;
            }
        }

        public FishWater FishingWater(WaterBody body)
        {
            if (!body.Deep && !body.Marsh) return FishWater.Pond;
            return FishingRules.FromElement(ZoneMap.ElementAt(body.Center.x, body.Center.y, HubRadius));
        }

        public static float ShoreEdge(WaterBody body) => body.SurfaceRadius > 1f ? body.SurfaceRadius : body.Radius;

        // True on a bank: the hub pond, a marsh pool, a forest lake, or the sea beach. The caller still rejects
        // swimming and the crypts.
        public bool TryFishingSpot(Vector2 player, out FishWater water, out Vector2 center, out float edge)
        {
            water = FishWater.Pond;
            center = PondCenter;
            edge = PondOuterRadius;
            float best = float.MaxValue;
            bool found = false;
            foreach (WaterBody body in WaterBodies)
            {
                float shore = ShoreEdge(body);
                float distance = Vector2.Distance(player, body.Center);
                if (distance < shore * 0.82f || distance > shore + 4.5f) continue;
                float score = Mathf.Abs(distance - shore);
                if (score >= best) continue;
                best = score;
                found = true;
                water = FishingWater(body);
                center = body.Center;
                edge = shore;
            }
            if (found) return true;

            float r = player.magnitude;
            if (r >= SeaInnerRadius - 16f && r <= SeaInnerRadius + 2f && !NearMountain(player, 8f))
            {
                water = FishWater.Sea;
                center = Vector2.zero;
                edge = SeaInnerRadius;
                return true;
            }
            return false;
        }

        // Pulls a cast into the water the player is standing at.
        public Vector3 ClampBobber(Vector3 playerPosition, Vector3 target)
        {
            var player = new Vector2(playerPosition.x, playerPosition.z);
            var aim = new Vector2(target.x, target.z);
            if (!TryFishingSpot(player, out FishWater water, out Vector2 center, out float edge))
            {
                target.y = WaterHeight;
                return target;
            }
            if (water == FishWater.Sea)
            {
                Vector2 outward = player.sqrMagnitude > 4f ? player.normalized : Vector2.up;
                Vector2 cast = aim - player;
                if (cast.sqrMagnitude < 0.25f || Vector2.Dot(cast.normalized, outward) < 0.15f) cast = outward;
                else cast.Normalize();
                float distance = Mathf.Clamp(Vector2.Distance(player, aim), 6f, 18f);
                Vector2 bob = player + cast * distance;
                if (bob.magnitude < SeaInnerRadius + 4f) bob = bob.normalized * (SeaInnerRadius + 4f);
                if (bob.magnitude > SeaInnerRadius + 22f) bob = bob.normalized * (SeaInnerRadius + 22f);
                return new Vector3(bob.x, WaterHeight, bob.y);
            }
            Vector2 offset = aim - center;
            float limit = edge * 0.72f;
            if (offset.sqrMagnitude < 0.01f || offset.magnitude > limit)
                offset = (offset.sqrMagnitude > 0.01f ? offset.normalized : Vector2.right) * limit;
            return new Vector3(center.x + offset.x, WaterHeight, center.y + offset.y);
        }

        public bool InWater(Vector2 p, float radiusFraction)
        {
            foreach (WaterBody body in WaterBodies)
            {
                if (Vector2.Distance(p, body.Center) < body.Radius * radiusFraction) return true;
            }
            return false;
        }

        // ---------- Crypts ----------

        public const int CryptCount = 3;
        private const float CryptMinRadius = 145f;
        private const float CryptMaxRadius = BeachStartRadius - 22f;
        private const float CryptMinSpacing = 110f;
        private const float CryptMaxSlope = 0.22f;

        // Three crypt entrances, each in a different forest, out past the hub where the woods are thick. Their doors
        // face the hut. The tiers (more rooms, more guardians, better loot) are shuffled across them.
        private List<CryptSite> BuildCrypts(System.Random random)
        {
            var forests = new List<Element>(Forests);
            for (int i = forests.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (forests[i], forests[j]) = (forests[j], forests[i]);
            }

            var sites = new List<CryptSite>();
            foreach (Element forest in forests)
            {
                if (sites.Count >= CryptCount) break;
                for (int tries = 0; tries < 600; tries++)
                {
                    Vector2 p = RandomPointInDisc(random, CryptMinRadius, CryptMaxRadius);
                    if (ZoneMap.ElementAt(p.x, p.y, HubRadius) != forest) continue;
                    if (DistanceToPath(p.x, p.y) < CryptPlan.ClearRadius + 3f) continue;
                    if (InWaterMargin(p, CryptPlan.ClearRadius + 6f)) continue;
                    if (NearMountain(p, CryptPlan.ClearRadius + 4f)) continue;
                    if (SlopeAt(p.x, p.y, 4f) > CryptMaxSlope) continue;
                    if (HeightAt(p.x, p.y) < WaterHeight + 1f) continue;
                    bool crowded = false;
                    foreach (CryptSite other in sites) if (Vector2.Distance(other.Position, p) < CryptMinSpacing) crowded = true;
                    if (crowded) continue;
                    float yaw = Mathf.Atan2(-p.x, -p.y) * Mathf.Rad2Deg;
                    sites.Add(new CryptSite { Position = p, Yaw = yaw, Zone = forest, Tier = sites.Count + 1 });
                    break;
                }
            }
            return sites;
        }

        // ---------- Mines ----------

        private const float MineMinRadius = 150f;
        private const float MineMaxRadius = BeachStartRadius - 30f;
        // How far up a ridge's lower slope the portal is cut: where trees (above 0.2) never stand, so no tree moves and
        // saved worlds keep their felled trees. Rocks (not saved) keep clear of the portals. That slope is a steep ledged wall, so a flat pad is cut
        // into it (HeightAt) at the height of the ground just in front, like a real adit's notch.
        private const float MineMinWeight = 0.21f;
        private const float MineMaxWeight = 0.5f;
        // The flat pad cut into the slope: full height inside the radius, blending back to the slope by the blend.
        public const float MinePadRadius = 6.5f;
        public const float MinePadBlend = 10.5f;
        // How far in front of the portal the pad's center and its height are taken (downhill, so the pad cuts in).
        private const float MinePadAhead = 2.5f;
        private const float MineGroundAhead = 5f;
        private const float MineCryptSpacing = 35f;

        // One mine per forest, cut into the foot of one of the two ridges beside it, its portal facing out into the forest.
        private List<MineSite> BuildMines(System.Random random)
        {
            var sites = new List<MineSite>();
            foreach (Element forest in Forests)
            {
                // The strict pass first; if the forest's ridges give no such spot, a looser one (a deeper notch, closer
                // to a crypt, nearer the coast), so every world has all four mines.
                for (int pass = 0; pass < 5; pass++)
                {
                    if (TryPlaceMine(random, forest, pass, out MineSite site))
                    {
                        sites.Add(site);
                        break;
                    }
                }
            }
            return sites;
        }

        private bool TryPlaceMine(System.Random random, Element forest, int pass, out MineSite site)
        {
            site = default;
            float maxDrop = pass < 4 ? 10f + pass * 4f : 60f;
            float cryptSpacing = Mathf.Max(12f, MineCryptSpacing - pass * 8f);
            float maxRadius = MineMaxRadius + pass * 8f;
            for (int tries = 0; tries < 40000; tries++)
            {
                Vector2 p = RandomPointInDisc(random, MineMinRadius - pass * 8f, maxRadius);
                if (ZoneMap.ElementAt(p.x, p.y, HubRadius) != forest) continue;
                // The ridges first; from the third pass on, the outer foot of the mountain ring as well.
                if (pass < 2 && DiagonalDistance(p.x, p.y) > RidgeFootHalfWidth) continue;
                float weight = MountainWeight(p.x, p.y);
                if (weight < MineMinWeight || weight > MineMaxWeight + pass * 0.1f) continue;
                if (HeightAt(p.x, p.y) < WaterHeight + 1f) continue;
                if (DistanceToPath(p.x, p.y) < 14f) continue;
                bool last = pass >= 4;
                if (InWaterMargin(p, last ? 3f : 8f)) continue;
                bool nearCrypt = false;
                foreach (CryptSite crypt in Crypts) if (Vector2.Distance(crypt.Position, p) < cryptSpacing) nearCrypt = true;
                if (nearCrypt) continue;
                // Facing away from the mountain: down the slope of its weight.
                float e = 1.5f;
                Vector2 away = -new Vector2(MountainWeight(p.x + e, p.y) - MountainWeight(p.x - e, p.y), MountainWeight(p.x, p.y + e) - MountainWeight(p.x, p.y - e));
                if (away.sqrMagnitude < 1e-6f) continue;
                away.Normalize();
                Vector2 ahead = p + away * MineGroundAhead;
                if (MountainWeight(ahead.x, ahead.y) > (last ? 0.35f : 0.2f)) continue;
                float ground = HeightAt(ahead.x, ahead.y);
                if (ground < WaterHeight + (last ? 0.4f : 1f) || HeightAt(p.x, p.y) - ground > maxDrop) continue;
                site = new MineSite { Position = p, Yaw = Mathf.Atan2(away.x, away.y) * Mathf.Rad2Deg, Forest = forest, Ground = ground, PadCenter = p + away * MinePadAhead };
                return true;
            }
            return false;
        }

        // Trees standing in front of a portal step aside to the edge of its clearing. They are moved, never removed, so
        // every tree keeps its index and saved worlds keep their felled trees.
        public void PushTreesOffMines(List<Chopper.Trees.TreeEntry> trees)
        {
            float clear = MinePlan.ClearRadius + 3.5f;
            for (int i = 0; i < trees.Count; i++)
            {
                Chopper.Trees.TreeEntry tree = trees[i];
                var p = new Vector2(tree.Position.x, tree.Position.z);
                foreach (MineSite site in Mines)
                {
                    Vector2 from = p - site.PadCenter;
                    if (from.magnitude >= clear) continue;
                    Vector2 dir = from.sqrMagnitude > 1e-4f ? from.normalized : Quaternion.Euler(0f, 0f, i * 37f) * Vector2.right;
                    p = site.PadCenter + dir * clear;
                }
                if (p.x == tree.Position.x && p.y == tree.Position.z) continue;
                tree.Position = new Vector3(p.x, HeightAt(p.x, p.y), p.y);
                trees[i] = tree;
            }
        }

        // The ground in front of each portal, bare of grass.
        public List<RockSpot> MineClearings()
        {
            var spots = new List<RockSpot>();
            foreach (MineSite site in Mines) spots.Add(new RockSpot { Position = site.Position, Size = MinePlan.ClearRadius * 1.4f });
            return spots;
        }

        // True near a mine portal (plus margin).
        public bool NearMine(Vector2 p, float margin)
        {
            foreach (MineSite site in Mines)
            {
                if (Vector2.Distance(p, site.Position) < MinePlan.ClearRadius + margin) return true;
            }
            return false;
        }

        // Trees stand at least this far back from the water's edge, so no trunk rises out of a pond.
        private const float TreeShoreClearance = 2.5f;

        // True on the water or within margin meters of its edge (a deep lake's dry bank is not water).
        public bool NearShore(Vector2 p, float margin)
        {
            foreach (WaterBody body in WaterBodies)
            {
                if (Vector2.Distance(p, body.Center) < ShoreEdge(body) + margin) return true;
            }
            return false;
        }

        private bool InWaterMargin(Vector2 p, float margin)
        {
            foreach (WaterBody body in WaterBodies)
            {
                if (Vector2.Distance(p, body.Center) < body.Radius + margin) return true;
            }
            return false;
        }

        // True within the cleared ground around a crypt entrance (plus margin).
        public bool NearCrypt(Vector2 p, float margin)
        {
            foreach (CryptSite site in Crypts)
            {
                if (Vector2.Distance(p, site.Position) < CryptPlan.ClearRadius + margin) return true;
            }
            return false;
        }

        // The clearings as rock spots, so the grass map can be stamped bare around each entrance.
        public List<RockSpot> CryptClearings()
        {
            var spots = new List<RockSpot>();
            foreach (CryptSite site in Crypts) spots.Add(new RockSpot { Position = site.Position, Size = CryptPlan.ClearRadius * 1.6f });
            return spots;
        }

        // ---------- Height ----------

        // Height in meters relative to the hut floor at world position (x, z). Every zone has its own terrain: the hub
        // is gentle and open, the marsh is flat and pooled, the deep woods roll, the ashlands are craggy, the highlands
        // swell, and the edge drops into the sea.
        public float HeightAt(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float outside = OutsideHub(r);
            float water = ZoneWeight(Element.Water, x, z) * outside;
            float earth = ZoneWeight(Element.Earth, x, z) * outside;
            float fire = ZoneWeight(Element.Fire, x, z) * outside;
            float wind = ZoneWeight(Element.Wind, x, z) * outside;
            float other = Mathf.Clamp01(1f - water - earth - fire - wind);

            float zoneScale = water * 0.15f + earth * 1.0f + fire * 0.8f + wind * 1.3f + other * 1.0f;
            // Layered noise over warped coordinates: the warp bends the hills into meandering ridges and valleys.
            float warpX = x + (_warpX.Fbm(x * 0.0045f, z * 0.0045f, 2) - 0.5f) * 70f;
            float warpZ = z + (_warpZ.Fbm(x * 0.0045f, z * 0.0045f, 2) - 0.5f) * 70f;
            float hills = (_hills.Fbm(warpX * 0.0058f, warpZ * 0.0058f, 4) - 0.5f) * 2.6f * HillAmplitude * zoneScale
                          + (_detail.Sample(x * 0.025f, z * 0.025f) - 0.5f) * 2f * DetailAmplitude * Mathf.Lerp(1f, 0.4f, water);
            float rise = Smooth(FlatRadius, HillsStartRadius, r);
            float height = hills * rise;

            // Ashlands: craggy ridges. Highlands: long windswept swells.
            float ridge = 1f - Mathf.Abs(2f * _ridges.Sample(x * 0.018f, z * 0.018f) - 1f);
            height += fire * ridge * ridge * 13f * rise;
            height += wind * (_swells.Sample(x * 0.0035f, z * 0.0035f) - 0.5f) * 2f * 14f;

            if (WaterBodies != null)
            {
                foreach (WaterBody body in WaterBodies)
                {
                    float dx = x - body.Center.x;
                    float dz = z - body.Center.y;
                    float distance = Mathf.Sqrt(dx * dx + dz * dz);
                    if (distance >= body.Radius) continue;
                    if (body.Deep)
                    {
                        float floor = WaterHeight - body.Depth;
                        float shore = body.SurfaceRadius > 1f ? body.SurfaceRadius : body.Radius * 0.6f;
                        float flat = Mathf.Max(4f, shore - body.Depth / 0.5f);
                        float carved = distance <= flat ? floor : floor + (distance - flat) * 0.5f;
                        float lip = Smooth(body.Radius - 2f, body.Radius, distance);
                        height = Mathf.Lerp(carved, height, lip);
                    }
                    else height -= body.Depth * (1f - Smooth(body.Radius * 0.45f, body.Radius, distance));
                }
            }

            height = Mathf.Lerp(height, Mathf.Max(height * 0.3f, BeachHeight), Smooth(BeachStartRadius, BeachEndRadius, r));
            height = Mathf.Lerp(height, SeaFloorHeight, Smooth(CoastStartRadius, CoastEndRadius, r));
            // After the coast, so the ridges run on into the sea and nobody swims round them.
            height += MountainHeight(x, z, r);
            height += MoundHeight * (1f - Smooth(MoundFlatRadius, MoundEndRadius, r));
            height += TerraceRaise(x, z);
            // Each mine's notch: the slope cut back to a flat pad for the portal and the ground in front of it.
            if (Mines != null)
            {
                foreach (MineSite site in Mines)
                {
                    float d = Vector2.Distance(new Vector2(x, z), site.PadCenter);
                    if (d >= MinePadBlend) continue;
                    // The notch walls rise in mountain risers too: a smooth blend made a stair of 2 to 3 m steps up
                    // into the ridge on seed 4242 (VoxelChecks.ZoneSeal).
                    float lift = (height - site.Ground) * Smooth(MinePadRadius, MinePadBlend, d);
                    height = site.Ground + Mathf.Round(lift / MountainStep) * MountainStep;
                }
            }
            return height;
        }

        public float SlopeAt(float x, float z, float step)
        {
            float dx = HeightAt(x + step, z) - HeightAt(x - step, z);
            float dz = HeightAt(x, z + step) - HeightAt(x, z - step);
            return Mathf.Sqrt(dx * dx + dz * dz) / (2f * step);
        }

        // One row of the heightmap, normalized to the terrain's height range (row zi, all columns).
        public void FillHeightRow(int zi, float[,] heights)
        {
            float z = -HalfSize + zi / (float)(HeightmapResolution - 1) * TerrainSize;
            for (int xi = 0; xi < HeightmapResolution; xi++)
            {
                float x = -HalfSize + xi / (float)(HeightmapResolution - 1) * TerrainSize;
                heights[zi, xi] = Mathf.Clamp01((HeightAt(x, z) - TerrainBaseY) / TerrainMaxHeight);
            }
        }

        // ---------- Ground paint ----------

        public const int LayerCount = 7;
        public const int DirtLayer = 5;
        public const int RockLayer = 6;

        // One row of the splat map. Layer order: hub grass, the four forests (Water, Earth, Fire, Wind), dirt, rock.
        public void FillAlphaRow(int zi, float[,,] alpha)
        {
            const float hubBlendWidth = 40f;
            float z = -HalfSize + zi / (float)(AlphamapResolution - 1) * TerrainSize;
            var forestWeights = new float[4];
            for (int xi = 0; xi < AlphamapResolution; xi++)
            {
                float x = -HalfSize + xi / (float)(AlphamapResolution - 1) * TerrainSize;
                float r = Mathf.Sqrt(x * x + z * z);
                float slope = SlopeAt(x, z, 1f);

                float rockWeight = Mathf.Clamp01(Smooth(0.5f, 1.2f, slope) + Smooth(CoastStartRadius + 14f, CoastEndRadius, r)
                                                 + Smooth(0.15f, 0.45f, MountainWeight(x, z)));
                float pondDistance = Vector2.Distance(new Vector2(x, z), PondCenter);
                float dirtWeight = Mathf.Clamp01((1f - Smooth(PondInnerRadius, PondOuterRadius + 3f, pondDistance))
                                                 + (1f - Smooth(10f, 20f, r)) * 0.8f);
                if (WaterBodies != null)
                {
                    var ground = new Vector2(x, z);
                    foreach (WaterBody body in WaterBodies)
                    {
                        float edge = ShoreEdge(body);
                        float d = Vector2.Distance(ground, body.Center);
                        dirtWeight = Mathf.Max(dirtWeight, 1f - Smooth(edge * 0.7f, edge + 5f, d));
                    }
                }
                dirtWeight = Mathf.Max(dirtWeight, PathWeight(x, z));
                dirtWeight = Mathf.Max(dirtWeight, Smooth(BeachStartRadius + 18f, BeachEndRadius + 4f, r));
                dirtWeight *= 1f - rockWeight;
                float groundWeight = Mathf.Max(0f, 1f - rockWeight - dirtWeight);

                float hubShare = 1f - Smooth(HubRadius, HubRadius + hubBlendWidth, r);
                Vector2 dir = r > 0.01f ? new Vector2(x, z) / r : Vector2.up;
                float forestSum = 0f;
                for (int f = 0; f < Forests.Length; f++)
                {
                    float align = Mathf.Max(0f, Vector2.Dot(dir, ZoneMap.Axis(Forests[f])));
                    forestWeights[f] = align * align * align;
                    forestSum += forestWeights[f];
                }

                alpha[zi, xi, 0] = groundWeight * hubShare;
                for (int f = 0; f < Forests.Length; f++)
                {
                    float share = forestSum > 0f ? forestWeights[f] / forestSum : 0.25f;
                    alpha[zi, xi, 1 + f] = groundWeight * (1f - hubShare) * share;
                }
                alpha[zi, xi, DirtLayer] = dirtWeight;
                alpha[zi, xi, RockLayer] = rockWeight;
            }
        }

        // ---------- Grass map ----------

        // One row of the grass map (R density, G dryness, B lushness, A height). Lush in the hub, deep and dark in the
        // marsh, thin in the deep woods, tall and golden on the highlands, almost none on the ash; bare on paths, in
        // the yard, under the hut, on the beach, in water and on steep slopes. Rocks are stamped out afterwards.
        public void FillGrassRow(int zi, Color32[] pixels)
        {
            float z = -HalfSize + (zi + 0.5f) / GrassMapResolution * TerrainSize;
            var share = new float[4];
            for (int xi = 0; xi < GrassMapResolution; xi++)
            {
                float x = -HalfSize + (xi + 0.5f) / GrassMapResolution * TerrainSize;
                float r = Mathf.Sqrt(x * x + z * z);
                var p = new Vector2(x, z);

                float hub = 1f - Smooth(HubRadius, HubRadius + 40f, r);
                Vector2 dir = r > 0.01f ? p / r : Vector2.up;
                float sum = 0f;
                for (int f = 0; f < 4; f++)
                {
                    float align = Mathf.Max(0f, Vector2.Dot(dir, ZoneMap.Axis(Forests[f])));
                    share[f] = align * align * align;
                    sum += share[f];
                }
                for (int f = 0; f < 4; f++) share[f] = (sum > 0f ? share[f] / sum : 0.25f) * (1f - hub);
                float water = share[0], earth = share[1], fire = share[2], wind = share[3];

                float density = hub * 1.0f + water * 0.9f + earth * 0.5f + fire * 0.05f + wind * 0.95f;
                float dry = hub * 0.08f + earth * 0.15f + fire * 0.9f + wind * 0.75f;
                float lush = hub * 0.15f + water * 1.0f + earth * 0.65f;
                float tall = hub * 0.45f + water * 0.7f + earth * 0.35f + fire * 0.2f + wind * 0.8f;

                density *= Smooth(PathHalfWidth * 0.5f, PathHalfWidth + 1.4f, DistanceToPath(x, z));
                density *= Mathf.Lerp(0.12f, 1f, Smooth(11.5f, 17f, r));
                if (Mathf.Abs(x) < HutHalfWidth + 0.6f && Mathf.Abs(z) < HutHalfDepth + 0.6f) density = 0f;
                density *= 1f - Smooth(BeachStartRadius + 10f, BeachEndRadius - 4f, r);
                foreach (WaterBody body in WaterBodies)
                {
                    density *= Smooth(body.Radius * 1.0f, body.Radius * 1.15f, Vector2.Distance(p, body.Center));
                }
                density *= 1f - Smooth(0.45f, 0.8f, SlopeAt(x, z, 1f));
                density *= 1f - Smooth(0.1f, 0.35f, MountainWeight(x, z));
                // Patchy, so meadows have thicker and thinner stretches instead of an even lawn.
                density *= Mathf.Lerp(0.6f, 1.15f, _patches.Sample(x * 0.05f, z * 0.05f));

                pixels[zi * GrassMapResolution + xi] = new Color32(ToByte(density), ToByte(dry), ToByte(lush), ToByte(tall));
            }
        }

        // Clears the grass around each rock.
        public static void StampRocks(Color32[] pixels, IReadOnlyList<RockSpot> rocks)
        {
            float pixelMeters = TerrainSize / GrassMapResolution;
            foreach (RockSpot rock in rocks)
            {
                float radius = rock.Size * 0.45f;
                float outer = radius * 1.3f;
                int x0 = Mathf.FloorToInt((rock.Position.x - outer + HalfSize) / pixelMeters);
                int x1 = Mathf.CeilToInt((rock.Position.x + outer + HalfSize) / pixelMeters);
                int z0 = Mathf.FloorToInt((rock.Position.y - outer + HalfSize) / pixelMeters);
                int z1 = Mathf.CeilToInt((rock.Position.y + outer + HalfSize) / pixelMeters);
                for (int zi = Mathf.Max(0, z0); zi <= Mathf.Min(GrassMapResolution - 1, z1); zi++)
                {
                    for (int xi = Mathf.Max(0, x0); xi <= Mathf.Min(GrassMapResolution - 1, x1); xi++)
                    {
                        float x = -HalfSize + (xi + 0.5f) * pixelMeters;
                        float z = -HalfSize + (zi + 0.5f) * pixelMeters;
                        float d = Vector2.Distance(new Vector2(x, z), rock.Position);
                        if (d >= outer) continue;
                        int index = zi * GrassMapResolution + xi;
                        Color32 c = pixels[index];
                        c.r = (byte)(c.r * Smooth(radius * 0.7f, outer, d));
                        pixels[index] = c;
                    }
                }
            }
        }

        private static byte ToByte(float value) => (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);

        // ---------- Rocks ----------

        // Rock models, counts, sizes and zone weights (Element order: hub, water, fire, earth, wind).
        private static readonly (int count, float min, float max)[] RockKinds = { (55, 2.2f, 5.5f), (55, 2.0f, 5.0f), (55, 2.0f, 5.0f) };
        private static readonly float[] RockZoneWeights = { 0.9f, 0.7f, 1f, 1f, 0.55f };
        private const float RockMinRadius = 14f;
        private const float RockMaxRadius = 246f;

        public List<RockSpot> GenerateRocks(int modelCount)
        {
            var random = new System.Random(Seed + 1);
            var rocks = new List<RockSpot>();
            for (int kind = 0; kind < RockKinds.Length; kind++)
            {
                (int count, float min, float max) = RockKinds[kind];
                int placed = 0;
                int attempts = 0;
                while (placed < count && attempts < count * 20)
                {
                    attempts++;
                    Vector2 p = RandomPointInDisc(random, RockMinRadius, RockMaxRadius);
                    if (Vector2.Distance(p, PondCenter) < PondOuterRadius) continue;
                    if (InWater(p, 1.05f)) continue;
                    if (IsInsideYard(p)) continue;
                    if (OnPath(p.x, p.y, 3.2f)) continue;
                    if (NearZoneGate(p, 3f)) continue;
                    if (NearCrypt(p, 3f)) continue;
                    if (NearMine(p, 4f)) continue;
                    if (MountainWeight(p.x, p.y) > 0.25f) continue;
                    int zoneIndex = (int)ZoneMap.ElementAt(p.x, p.y, HubRadius);
                    if (random.NextDouble() > RockZoneWeights[Mathf.Clamp(zoneIndex, 0, RockZoneWeights.Length - 1)]) continue;
                    float size = Mathf.Lerp(min, max, (float)random.NextDouble());
                    float yaw = (float)random.NextDouble() * 360f;
                    rocks.Add(new RockSpot { Position = p, Model = modelCount > 0 ? kind % modelCount : 0, Size = size, Yaw = yaw });
                    placed++;
                }
            }
            return rocks;
        }

        public static bool IsInsideYard(Vector2 p)
        {
            if (p.magnitude < WallRadius + 4f) return true;
            return Mathf.Abs(p.x) < HutHalfWidth + 2f && Mathf.Abs(p.y) < HutHalfDepth + 2f;
        }

        // ---------- Where players wake up ----------

        private const int SpawnCount = 24;
        private const float SpawnMinRadius = 60f;
        private const float SpawnMaxRadius = 210f;
        private const float SpawnMaxSlopeDegrees = 16f;

        // Spread out spots in the hub meadow and the forests, never in water or on steep ground.
        public List<SpawnSpot> GenerateSpawns()
        {
            var random = new System.Random(Seed + 5);
            var spots = new List<SpawnSpot>();
            for (int tries = 0; tries < 2000 && spots.Count < SpawnCount; tries++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(SpawnMinRadius, SpawnMaxRadius, (float)random.NextDouble());
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                if (InWater(new Vector2(x, z), 1f) || Vector2.Distance(new Vector2(x, z), PondCenter) < PondOuterRadius + 5f) continue;
                if (NearCrypt(new Vector2(x, z), 4f)) continue;
                // Wide enough that friends who join, placed a few meters around the first player, stay off the cliffs.
                if (NearMountain(new Vector2(x, z), 8f)) continue;
                float slope = Mathf.Atan(SlopeAt(x, z, 2f)) * Mathf.Rad2Deg;
                if (slope > SpawnMaxSlopeDegrees) continue;
                float height = HeightAt(x, z);
                if (height < WaterHeight + 0.4f) continue;
                // Facing the hut, so the camera looks toward home at first.
                float yaw = Mathf.Atan2(-x, -z) * Mathf.Rad2Deg;
                spots.Add(new SpawnSpot { Position = new Vector3(x, height + 0.3f, z), Yaw = yaw });
            }
            return spots;
        }

        // ---------- Trees ----------

        public static readonly float[] TreeHeights = { 5f, 9f, 14f };
        private const float TreeHeightJitter = 0.15f;
        private const float TreeMinRadius = 30f;
        private const float TreeMaxRadius = 240f;
        private const float ForestMinMargin = 3f;
        private const int ClustersPerForest = 9;
        private const float ClusterRadius = 38f;

        // How many trees each zone gets and how they are laid out. The deep woods are dense and large, the marsh is wet,
        // the ashlands and highlands are sparse with wide gaps, the hub is an open meadow with lone trees.
        private static int QuotaFor(Element zone)
        {
            switch (zone)
            {
                case Element.Water: return 380;
                case Element.Earth: return 520;
                case Element.Fire: return 190;
                case Element.Wind: return 220;
                default: return 120;
            }
        }

        private static float SpacingFor(Element zone)
        {
            switch (zone)
            {
                case Element.Water: return 4.5f;
                case Element.Earth: return 4f;
                case Element.Fire: return 9f;
                case Element.Wind: return 10f;
                default: return 13f;
            }
        }

        private static float LoneFractionFor(Element zone)
        {
            switch (zone)
            {
                case Element.Fire: return 0.7f;
                case Element.Wind: return 0.6f;
                case Element.Earth: return 0.12f;
                default: return 0.25f;
            }
        }

        private static float[] SizeWeightsFor(Element zone)
        {
            switch (zone)
            {
                case Element.Earth: return new[] { 0.2f, 0.4f, 0.4f };
                case Element.Water: return new[] { 0.3f, 0.4f, 0.3f };
                case Element.Neutral: return new[] { 0.5f, 0.4f, 0.1f };
                default: return new[] { 0.4f, 0.4f, 0.2f };
            }
        }

        // Every standing tree. species maps each zone (Element as int) to the model indices that may grow there, and
        // modelHeights gives each model's natural height so trees can be scaled to their size class. The list index
        // is the tree id every peer agrees on.
        public List<TreeEntry> GenerateTrees(int[][] species, float[] modelHeights)
        {
            var allZones = new List<Element> { Element.Neutral };
            allZones.AddRange(Forests);
            var random = new System.Random(Seed + 11);
            var trees = new List<TreeEntry>();
            var grid = new SpacingGrid(16f);
            var counts = new Dictionary<Element, int>();
            int total = 0;
            foreach (Element zone in allZones) { counts[zone] = 0; total += QuotaFor(zone); }

            // Clusters per forest, each inside its own quadrant.
            var clusterCenters = new Dictionary<Element, List<Vector2>>();
            foreach (Element forest in Forests)
            {
                var centers = new List<Vector2>();
                int guard = 0;
                while (centers.Count < ClustersPerForest && guard++ < 1000)
                {
                    Vector2 c = RandomPointInDisc(random, HubRadius + ForestMinMargin + ClusterRadius * 0.5f, TreeMaxRadius - ClusterRadius * 0.5f);
                    if (ZoneMap.ElementAt(c.x, c.y, HubRadius) == forest) centers.Add(c);
                }
                clusterCenters[forest] = centers;
            }

            int attempts = 0;
            while (trees.Count < total && attempts < total * 400)
            {
                attempts++;
                // Pick a zone that still has room, weighted by how many trees it still needs.
                int remainingTotal = 0;
                foreach (Element zone in allZones) remainingTotal += Mathf.Max(0, QuotaFor(zone) - counts[zone]);
                if (remainingTotal <= 0) break;
                int roll = random.Next(remainingTotal);
                Element chosen = Element.Neutral;
                foreach (Element zone in allZones)
                {
                    int remaining = Mathf.Max(0, QuotaFor(zone) - counts[zone]);
                    if (roll < remaining) { chosen = zone; break; }
                    roll -= remaining;
                }

                int[] zoneSpecies = species[(int)chosen];
                if (zoneSpecies == null || zoneSpecies.Length == 0) { counts[chosen] = QuotaFor(chosen); continue; }

                Vector2 p;
                if (chosen == Element.Neutral)
                {
                    p = RandomPointInDisc(random, TreeMinRadius, HubRadius - ForestMinMargin);
                }
                else
                {
                    List<Vector2> centers = clusterCenters[chosen];
                    if (centers.Count == 0) { counts[chosen] = QuotaFor(chosen); continue; }
                    Vector2 center = centers[random.Next(centers.Count)];
                    p = random.NextDouble() < LoneFractionFor(chosen)
                        ? RandomPointInDisc(random, HubRadius + ForestMinMargin, TreeMaxRadius)
                        : center + RandomPointInDisc(random, 0f, ClusterRadius);
                    if (ZoneMap.ElementAt(p.x, p.y, HubRadius) != chosen) continue;
                    if (p.magnitude < HubRadius + ForestMinMargin) continue;
                }

                if (p.magnitude > TreeMaxRadius) continue;
                if (InWater(p, 0.85f) || NearShore(p, TreeShoreClearance)) continue;
                if (OnPath(p.x, p.y, 4.5f)) continue;
                if (NearZoneGate(p, 3f)) continue;
                // Wide enough that no canopy hangs over a crypt door.
                if (NearCrypt(p, 9f)) continue;
                if (MountainWeight(p.x, p.y) > 0.2f) continue;
                if (grid.TooClose(p, SpacingFor(chosen))) continue;

                TreeSize size = PickSize(random, SizeWeightsFor(chosen));
                int modelIndex = zoneSpecies[random.Next(zoneSpecies.Length)];
                float targetHeight = TreeHeights[(int)size] * (1f + ((float)random.NextDouble() * 2f - 1f) * TreeHeightJitter);

                grid.Add(p);
                counts[chosen]++;
                trees.Add(new TreeEntry
                {
                    Position = new Vector3(p.x, HeightAt(p.x, p.y), p.y),
                    Yaw = (float)random.NextDouble() * 360f,
                    Scale = targetHeight / Mathf.Max(0.1f, modelHeights[modelIndex]),
                    Size = size,
                    Zone = chosen,
                    ModelIndex = modelIndex
                });
            }
            return trees;
        }

        private static TreeSize PickSize(System.Random random, float[] weights)
        {
            double roll = random.NextDouble();
            float acc = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (roll < acc) return (TreeSize)i;
            }
            return TreeSize.Large;
        }

        // A coarse grid of placed points, so the spacing test only looks at nearby trees.
        private sealed class SpacingGrid
        {
            private readonly float _cell;
            private readonly Dictionary<long, List<Vector2>> _cells = new Dictionary<long, List<Vector2>>();

            public SpacingGrid(float cell) => _cell = cell;

            private long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

            public void Add(Vector2 p)
            {
                long key = Key(Mathf.FloorToInt(p.x / _cell), Mathf.FloorToInt(p.y / _cell));
                if (!_cells.TryGetValue(key, out List<Vector2> list))
                {
                    list = new List<Vector2>();
                    _cells[key] = list;
                }
                list.Add(p);
            }

            public bool TooClose(Vector2 p, float spacing)
            {
                float sqr = spacing * spacing;
                int cx = Mathf.FloorToInt(p.x / _cell);
                int cz = Mathf.FloorToInt(p.y / _cell);
                int reach = Mathf.CeilToInt(spacing / _cell);
                for (int x = cx - reach; x <= cx + reach; x++)
                {
                    for (int z = cz - reach; z <= cz + reach; z++)
                    {
                        if (!_cells.TryGetValue(Key(x, z), out List<Vector2> list)) continue;
                        foreach (Vector2 q in list) if ((q - p).sqrMagnitude < sqr) return true;
                    }
                }
                return false;
            }
        }

        // A short fingerprint of the tree layout, so a joining player can confirm it built the same forest as the host.
        public static int Checksum(IReadOnlyList<TreeEntry> trees)
        {
            unchecked
            {
                int hash = trees.Count;
                foreach (TreeEntry t in trees)
                {
                    hash = hash * 31 + Mathf.RoundToInt(t.Position.x * 10f);
                    hash = hash * 31 + Mathf.RoundToInt(t.Position.z * 10f);
                    hash = hash * 31 + t.ModelIndex;
                    hash = hash * 31 + (int)t.Size;
                }
                return hash;
            }
        }
    }
}
