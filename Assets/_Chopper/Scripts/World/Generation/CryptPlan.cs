using System.Collections.Generic;
using Chopper.Core;
using UnityEngine;

namespace Chopper.World.Generation
{
    // Where a crypt's entrance stands on the surface: a small stone mausoleum whose door faces the hut.
    public struct CryptSite
    {
        public Vector2 Position;
        public float Yaw;
        public Element Zone;
        // 1 to 3: more rooms, more guardians and better loot.
        public int Tier;
        // The height of the level pad the entrance stands on (the higher of its centre and the ground before its door).
        public float Ground;
    }

    // Timber is only used by the mines (posts, beams and the portal).
    public enum CryptMaterial { Wall = 0, Floor = 1, Ceiling = 2, Void = 3, Timber = 4 }

    // One solid block of crypt stone (walls, floors, ceilings, steps). Every block is also a collider.
    public struct CryptBox
    {
        public Vector3 Center;
        public Vector3 Size;
        public Quaternion Rotation;
        public CryptMaterial Material;
        // Which room the block belongs to, so each room is its own mesh (lighting and culling stay local).
        public int Group;
    }

    public struct CryptChest
    {
        public Vector3 Position;
        public float Yaw;
        public int Iron;
        public int Shards;
        public bool Grand;
    }

    public struct CryptGuardian
    {
        public Vector3 Position;
        public TreeSize Size;
    }

    public struct CryptLantern
    {
        public Vector3 Position;
        public float Yaw;
    }

    // The whole of one crypt, made from the world seed: the mausoleum on the surface and, far below it, a set of
    // stone rooms joined by short corridors. The first room holds the stairs back up; the deepest one is the vault
    // with the grand chest. Pure C# apart from Unity math types, so it is unit tested and identical on every peer.
    public sealed class CryptPlan
    {
        // The rooms lie this far below the hut floor, under their entrance, well out of sight of the surface.
        public const float FloorY = -200f;
        // Reach 2 keeps a crypt within 50 m of its stairs, under WorldGen.CryptMinSpacing / 2.
        public const float CellSize = 20f;
        public const float RoomSize = 15f;
        public const float RoomHeight = 6f;
        public const float WallThickness = 0.6f;
        public const float CorridorWidth = 5f;
        public const float DoorHeight = 4.4f;
        private const int MaxReach = 2;
        private const int StairSteps = 7;
        private const float StairRise = 0.4f;
        private const float StairRun = 0.55f;
        private const float StairWidth = 2.6f;
        private const float LanternHeight = 2.6f;

        // The mausoleum on the surface.
        public const float EntranceWidth = 5.6f;
        public const float EntranceDepth = 6.4f;
        public const float EntranceHeight = 3.8f;
        private const float EntranceFoundation = 3f;
        private const float DoorWidth = 1.9f;
        private const float DoorOpeningHeight = 2.7f;
        // Nothing else (trees, rocks, wake up spots) is placed this close to an entrance.
        public const float ClearRadius = 8f;

        private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public readonly CryptSite Site;
        public readonly int Index;
        // Interior origin: the center of the first room's floor.
        public readonly Vector3 Origin;
        public readonly List<Vector2Int> Rooms = new List<Vector2Int>();
        public readonly List<(int a, int b)> Links = new List<(int a, int b)>();
        public readonly List<int> Depths = new List<int>();
        public readonly List<CryptBox> Boxes = new List<CryptBox>();
        public readonly List<CryptBox> EntranceBoxes = new List<CryptBox>();
        public readonly List<CryptChest> Chests = new List<CryptChest>();
        public readonly List<CryptGuardian> Guardians = new List<CryptGuardian>();
        public readonly List<CryptLantern> Lanterns = new List<CryptLantern>();
        public readonly List<CryptLantern> EntranceLanterns = new List<CryptLantern>();
        public int Vault { get; private set; }
        public Vector2Int StairsSide { get; private set; }

        // Interact here (on the surface) to go down; climbing out puts the player at ReturnSpot facing ReturnYaw.
        public Vector3 DoorSpot { get; private set; }
        public Vector3 ReturnSpot { get; private set; }
        public float ReturnYaw => Site.Yaw;
        // Interact here (at the foot of the stairs) to climb out; going down puts the player at ArrivalSpot.
        public Vector3 ExitSpot { get; private set; }
        public Vector3 ArrivalSpot { get; private set; }
        public float ArrivalYaw { get; private set; }

        // ---------- The arena under the vault ----------

        // The arena hall lies this far below the rooms, under the vault.
        public const float ArenaDepth = 30f;
        public const float ArenaSize = 26f;
        public const float ArenaHeight = 9f;
        private const float ArenaWall = 1f;
        private const float ArenaSpawnRadius = 9f;
        public const int ArenaGroup = 1000;

        // Interact here (a dark archway in the vault) to go down to the arena; ArenaEntry is where players arrive.
        public Vector3 ArenaDoorSpot { get; private set; }
        public Vector3 ArenaCenter { get; private set; }
        public Vector3 ArenaEntry { get; private set; }
        public float ArenaEntryYaw => 0f;
        // Interact here (the archway behind the arrival point) to climb back up to the vault.
        public Vector3 ArenaExitSpot { get; private set; }
        public Vector3 VaultReturnSpot { get; private set; }
        public float VaultReturnYaw { get; private set; }
        public Vector3 BossSpot { get; private set; }
        public readonly List<Vector3> ArenaSpawns = new List<Vector3>();
        public readonly List<CryptLantern> ArenaLanterns = new List<CryptLantern>();
        private Vector2Int _arenaDoorSide;

        public string Name => NameFor(Site.Zone);

        public static string NameFor(Element zone)
        {
            switch (zone)
            {
                case Element.Water: return "Drowned Crypt";
                case Element.Earth: return "Root Crypt";
                case Element.Fire: return "Ash Crypt";
                case Element.Wind: return "Gale Crypt";
                default: return "Crypt";
            }
        }

        public CryptPlan(WorldGen gen, int index)
        {
            Site = gen.Crypts[index];
            Index = index;
            Origin = new Vector3(Site.Position.x, FloorY, Site.Position.y);
            var random = new System.Random(gen.Seed * 97 + index * 7919 + 3);
            LayOutRooms(random);
            BuildRooms();
            BuildStairs();
            PlaceChests(random);
            PlaceArenaDoor(random);
            PlaceGuardians(random);
            PlaceLanterns(random);
            BuildArena();
            BuildEntrance(gen);
        }

        public Vector3 RoomCenter(int room) => Origin + new Vector3(Rooms[room].x * CellSize, 0f, Rooms[room].y * CellSize);

        public bool Linked(int a, int b)
        {
            foreach ((int x, int y) in Links) if ((x == a && y == b) || (x == b && y == a)) return true;
            return false;
        }

        private int RoomAt(Vector2Int cell) => Rooms.IndexOf(cell);

        private bool SideOpen(int room, Vector2Int side)
        {
            int other = RoomAt(Rooms[room] + side);
            return other >= 0 && Linked(room, other);
        }

        private int LinkCount(int room)
        {
            int count = 0;
            foreach ((int a, int b) in Links) if (a == room || b == room) count++;
            return count;
        }

        // ---------- Layout ----------

        private void LayOutRooms(System.Random random)
        {
            int roomCount = 3 + Mathf.Clamp(Site.Tier, 1, 3);
            StairsSide = Directions[random.Next(Directions.Length)];
            Rooms.Add(Vector2Int.zero);
            Depths.Add(0);
            int guard = 0;
            while (Rooms.Count < roomCount && guard++ < 1000)
            {
                // Mostly grow from the newest room, so the crypt reaches deep instead of clumping around the stairs.
                int from = random.NextDouble() < 0.6 ? Rooms.Count - 1 : random.Next(Rooms.Count);
                Vector2Int side = Directions[random.Next(Directions.Length)];
                Vector2Int cell = Rooms[from] + side;
                if (Rooms.Contains(cell) || Mathf.Abs(cell.x) > MaxReach || Mathf.Abs(cell.y) > MaxReach) continue;
                if (from == 0 && (side == StairsSide || LinkCount(0) >= 2)) continue;
                Rooms.Add(cell);
                Depths.Add(Depths[from] + 1);
                Links.Add((from, Rooms.Count - 1));
            }

            Vault = 0;
            for (int i = 1; i < Rooms.Count; i++) if (Depths[i] >= Depths[Vault]) Vault = i;
        }

        private static Vector3 Normal(Vector2Int side) => new Vector3(side.x, 0f, side.y);

        private static Vector3 Tangent(Vector2Int side) => new Vector3(side.y, 0f, -side.x);

        private static float YawOf(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        // An axis aligned block: thickness along the side's normal, length along the wall, height up.
        private static Vector3 SizeOn(Vector2Int side, float thickness, float length, float height)
        {
            Vector3 n = Normal(side);
            Vector3 t = Tangent(side);
            return new Vector3(Mathf.Abs(n.x) * thickness + Mathf.Abs(t.x) * length, height, Mathf.Abs(n.z) * thickness + Mathf.Abs(t.z) * length);
        }

        private int _group;

        private void Add(Vector3 center, Vector3 size, CryptMaterial material)
        {
            Boxes.Add(new CryptBox { Center = center, Size = size, Rotation = Quaternion.identity, Material = material, Group = _group });
        }

        private void BuildRooms()
        {
            float outer = RoomSize + 2f * WallThickness;
            for (int room = 0; room < Rooms.Count; room++)
            {
                _group = room;
                Vector3 c = RoomCenter(room);
                Add(c + Vector3.down * 0.25f, new Vector3(outer, 0.5f, outer), CryptMaterial.Floor);
                Add(c + Vector3.up * (RoomHeight + 0.25f), new Vector3(outer, 0.5f, outer), CryptMaterial.Ceiling);
                foreach (Vector2Int side in Directions)
                {
                    Vector3 n = Normal(side);
                    Vector3 t = Tangent(side);
                    Vector3 wall = c + n * (RoomSize * 0.5f + WallThickness * 0.5f) + Vector3.up * (RoomHeight * 0.5f);
                    if (!SideOpen(room, side))
                    {
                        Add(wall, SizeOn(side, WallThickness, outer, RoomHeight), CryptMaterial.Wall);
                        continue;
                    }
                    // A doorway in the middle of the wall, with a lintel over it.
                    float segment = (outer - CorridorWidth) * 0.5f;
                    float shift = CorridorWidth * 0.5f + segment * 0.5f;
                    Add(wall + t * shift, SizeOn(side, WallThickness, segment, RoomHeight), CryptMaterial.Wall);
                    Add(wall - t * shift, SizeOn(side, WallThickness, segment, RoomHeight), CryptMaterial.Wall);
                    float lintel = RoomHeight - DoorHeight;
                    Vector3 lintelCenter = c + n * (RoomSize * 0.5f + WallThickness * 0.5f) + Vector3.up * (DoorHeight + lintel * 0.5f);
                    Add(lintelCenter, SizeOn(side, WallThickness, CorridorWidth, lintel), CryptMaterial.Wall);
                }
            }

            // Corridors fill only the gap between two rooms' outer wall faces. Reaching into the walls would put their
            // floor, ceiling and sides on the same planes as the room's floor, lintel and door jambs, which flickers.
            float length = CellSize - RoomSize - 2f * WallThickness;
            foreach ((int a, int b) in Links)
            {
                _group = a;
                Vector2Int side = Rooms[b] - Rooms[a];
                Vector3 t = Tangent(side);
                Vector3 mid = (RoomCenter(a) + RoomCenter(b)) * 0.5f;
                Add(mid + Vector3.down * 0.25f, SizeOn(side, length, CorridorWidth + 2f * WallThickness, 0.5f), CryptMaterial.Floor);
                Add(mid + Vector3.up * (DoorHeight + 0.25f), SizeOn(side, length, CorridorWidth + 2f * WallThickness, 0.5f), CryptMaterial.Ceiling);
                foreach (float s in new[] { 1f, -1f })
                {
                    Vector3 wall = mid + t * s * (CorridorWidth * 0.5f + WallThickness * 0.5f) + Vector3.up * (DoorHeight * 0.5f);
                    Add(wall, SizeOn(side, length, WallThickness, DoorHeight), CryptMaterial.Wall);
                }
            }
        }

        // Steps up against one wall of the first room, ending in a dark passage: the way back to the surface.
        private void BuildStairs()
        {
            _group = 0;
            Vector3 c = RoomCenter(0);
            Vector3 n = Normal(StairsSide);
            for (int i = 0; i < StairSteps; i++)
            {
                float height = (i + 1) * StairRise;
                float fromWall = (StairSteps - 1 - i + 0.5f) * StairRun;
                Vector3 center = c + n * (RoomSize * 0.5f - fromWall) + Vector3.up * (height * 0.5f);
                Add(center, SizeOn(StairsSide, StairRun, StairWidth, height), CryptMaterial.Floor);
            }
            float top = StairSteps * StairRise;
            // The dark passage starts a little above the top step, so the two never share a surface.
            Add(c + n * (RoomSize * 0.5f - 0.02f) + Vector3.up * (top + 0.05f + 1.25f), SizeOn(StairsSide, 0.06f, StairWidth - 0.2f, 2.5f), CryptMaterial.Void);

            float foot = RoomSize * 0.5f - StairSteps * StairRun;
            ExitSpot = c + n * (foot - 0.8f);
            ArrivalSpot = c + n * (foot - 2.2f);
            ArrivalYaw = YawOf(-n);
        }

        // ---------- Contents ----------

        // Sides of a room with no doorway (and, in the first room, not the stairs).
        private List<Vector2Int> SolidSides(int room)
        {
            var sides = new List<Vector2Int>();
            foreach (Vector2Int side in Directions)
            {
                if (SideOpen(room, side) || (room == 0 && side == StairsSide)) continue;
                sides.Add(side);
            }
            return sides;
        }

        private readonly Dictionary<int, Vector2Int> _chestSide = new Dictionary<int, Vector2Int>();

        private bool AddChest(System.Random random, int room, bool grand)
        {
            List<Vector2Int> sides = SolidSides(room);
            if (sides.Count == 0) return false;
            Vector2Int side = sides[random.Next(sides.Count)];
            _chestSide[room] = side;
            Vector3 n = Normal(side);
            int tier = Mathf.Clamp(Site.Tier, 1, 3);
            Chests.Add(new CryptChest
            {
                Position = RoomCenter(room) + n * (RoomSize * 0.5f - 1.1f),
                Yaw = YawOf(-n),
                Iron = grand ? 4 + tier : 1 + random.Next(tier + 1),
                Shards = grand ? 3 + tier : random.Next(2 + tier / 2),
                Grand = grand
            });
            return true;
        }

        private void PlaceChests(System.Random random)
        {
            AddChest(random, Vault, true);
            int ordinary = 0;
            for (int room = 1; room < Rooms.Count; room++)
            {
                if (room == Vault || random.NextDouble() > 0.55) continue;
                if (AddChest(random, room, false)) ordinary++;
            }
            if (ordinary > 0) return;
            for (int room = 1; room < Rooms.Count; room++)
            {
                if (room == Vault) continue;
                if (AddChest(random, room, false)) return;
            }
        }

        private TreeSize GuardianSize(System.Random random, bool vault)
        {
            double roll = random.NextDouble();
            int size;
            switch (Mathf.Clamp(Site.Tier, 1, 3))
            {
                case 1: size = roll < 0.6 ? 0 : 1; break;
                case 2: size = roll < 0.3 ? 0 : 1; break;
                default: size = roll < 0.5 ? 1 : 2; break;
            }
            if (vault) size = Mathf.Min(2, size + 1);
            return (TreeSize)size;
        }

        private void PlaceGuardians(System.Random random)
        {
            int perRoom = Site.Tier >= 2 ? 2 : 1;
            for (int room = 1; room < Rooms.Count; room++)
            {
                int count = perRoom + (room == Vault ? 1 : 0);
                for (int i = 0; i < count; i++)
                {
                    var offset = new Vector3(((float)random.NextDouble() * 2f - 1f) * 3f, 0.1f, ((float)random.NextDouble() * 2f - 1f) * 3f);
                    Guardians.Add(new CryptGuardian { Position = RoomCenter(room) + offset, Size = GuardianSize(random, room == Vault) });
                }
            }
        }

        private void PlaceLanterns(System.Random random)
        {
            for (int room = 0; room < Rooms.Count; room++)
            {
                Vector3 c = RoomCenter(room);
                if (room == 0)
                {
                    // Either side of the stairs.
                    Vector3 n0 = Normal(StairsSide);
                    Vector3 t0 = Tangent(StairsSide);
                    foreach (float s in new[] { 1f, -1f })
                    {
                        Lanterns.Add(new CryptLantern { Position = c + n0 * (RoomSize * 0.5f - 0.15f) + t0 * s * (StairWidth * 0.5f + 0.8f) + Vector3.up * LanternHeight, Yaw = YawOf(-n0) });
                    }
                    continue;
                }

                var candidates = new List<CryptLantern>();
                foreach (Vector2Int side in Directions)
                {
                    if (_chestSide.TryGetValue(room, out Vector2Int chestSide) && chestSide == side) continue;
                    if (room == Vault && side == _arenaDoorSide) continue;
                    Vector3 n = Normal(side);
                    Vector3 t = Tangent(side);
                    // Beside a doorway, or in the middle of a solid wall.
                    Vector3 along = SideOpen(room, side) ? t * (CorridorWidth * 0.5f + 1.3f) : Vector3.zero;
                    candidates.Add(new CryptLantern { Position = c + n * (RoomSize * 0.5f - 0.15f) + along + Vector3.up * LanternHeight, Yaw = YawOf(-n) });
                }
                for (int i = 0; i < 3 && candidates.Count > 0; i++)
                {
                    int pick = random.Next(candidates.Count);
                    Lanterns.Add(candidates[pick]);
                    candidates.RemoveAt(pick);
                }
            }
        }

        // ---------- The arena ----------

        // A dark archway on a free wall of the vault leads down to the arena.
        private void PlaceArenaDoor(System.Random random)
        {
            var sides = SolidSides(Vault);
            if (_chestSide.TryGetValue(Vault, out Vector2Int chestSide)) sides.Remove(chestSide);
            if (sides.Count == 0) sides = SolidSides(Vault);
            _arenaDoorSide = sides[random.Next(sides.Count)];
            Vector3 c = RoomCenter(Vault);
            Vector3 n = Normal(_arenaDoorSide);
            _group = Vault;
            Add(c + n * (RoomSize * 0.5f - 0.02f) + Vector3.up * 1.4f, SizeOn(_arenaDoorSide, 0.06f, 2.2f, 2.8f), CryptMaterial.Void);
            // Two stone jambs frame it, so it reads as a way through rather than a dark patch on the wall.
            foreach (float s in new[] { 1f, -1f })
            {
                Add(c + n * (RoomSize * 0.5f - 0.2f) + Tangent(_arenaDoorSide) * s * 1.35f + Vector3.up * 1.6f, SizeOn(_arenaDoorSide, 0.4f, 0.5f, 3.2f), CryptMaterial.Floor);
            }
            ArenaDoorSpot = c + n * (RoomSize * 0.5f - 1.2f);
            VaultReturnSpot = c + n * (RoomSize * 0.5f - 2.4f);
            VaultReturnYaw = YawOf(-n);
        }

        // A square hall with four pillars, lanterns all round, an archway back up behind the arrival point and room
        // for the waves to gather. It lies straight under the vault, so the minimap and the crypt lookups agree.
        private void BuildArena()
        {
            Vector3 c = RoomCenter(Vault) + Vector3.down * ArenaDepth;
            ArenaCenter = c;
            _group = ArenaGroup;
            float half = ArenaSize * 0.5f;
            float outer = ArenaSize + 2f * ArenaWall;
            Add(c + Vector3.down * 0.5f, new Vector3(outer, 1f, outer), CryptMaterial.Floor);
            Add(c + Vector3.up * (ArenaHeight + 0.5f), new Vector3(outer, 1f, outer), CryptMaterial.Ceiling);
            foreach (Vector2Int side in Directions)
            {
                Vector3 wall = c + Normal(side) * (half + ArenaWall * 0.5f) + Vector3.up * (ArenaHeight * 0.5f);
                Add(wall, SizeOn(side, ArenaWall, outer, ArenaHeight), CryptMaterial.Wall);
            }
            foreach (Vector2 p in new[] { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) })
            {
                Add(c + new Vector3(p.x * 6f, ArenaHeight * 0.5f, p.y * 6f), new Vector3(1.6f, ArenaHeight, 1.6f), CryptMaterial.Wall);
            }

            // The way back up: an archway in the south wall, the arrival point a few steps in front of it.
            Add(c + new Vector3(0f, 1.6f, -half + 0.02f), new Vector3(2.4f, 3.2f, 0.06f), CryptMaterial.Void);
            ArenaExitSpot = c + new Vector3(0f, 0f, -half + 1.4f);
            ArenaEntry = c + new Vector3(0f, 0f, -half + 3.4f);
            BossSpot = c + new Vector3(0f, 0.1f, 5f);
            for (int i = 0; i < 8; i++)
            {
                float angle = (22.5f + i * 45f) * Mathf.Deg2Rad;
                ArenaSpawns.Add(c + new Vector3(Mathf.Cos(angle) * ArenaSpawnRadius, 0.1f, Mathf.Sin(angle) * ArenaSpawnRadius));
            }
            foreach (Vector2Int side in Directions)
            {
                Vector3 n = Normal(side);
                Vector3 t = Tangent(side);
                foreach (float s in new[] { 1f, -1f })
                {
                    ArenaLanterns.Add(new CryptLantern { Position = c + n * (half - 0.15f) + t * s * 7f + Vector3.up * 3.6f, Yaw = YawOf(-n) });
                }
            }
        }

        // True for a position inside this crypt's arena hall.
        public bool InArena(Vector3 position)
        {
            Vector3 d = position - ArenaCenter;
            float half = ArenaSize * 0.5f;
            return Mathf.Abs(d.x) < half && Mathf.Abs(d.z) < half && d.y > -2f && d.y < ArenaHeight;
        }

        // ---------- The entrance ----------

        private void BuildEntrance(WorldGen gen)
        {
            Quaternion turn = Quaternion.Euler(0f, Site.Yaw, 0f);
            Vector3 forward = turn * Vector3.forward;
            Vector2 center2 = Site.Position;
            Vector3 front = new Vector3(center2.x, 0f, center2.y) + forward * (EntranceDepth * 0.5f + 1.5f);
            float ground = Mathf.Max(gen.HeightAt(center2.x, center2.y), gen.HeightAt(front.x, front.z));
            Vector3 origin = new Vector3(center2.x, ground, center2.y);

            void AddLocal(Vector3 local, Vector3 size, CryptMaterial material)
            {
                EntranceBoxes.Add(new CryptBox { Center = origin + turn * local, Size = size, Rotation = turn, Material = material });
            }

            float halfDepth = EntranceDepth * 0.5f;
            AddLocal(new Vector3(0f, (EntranceHeight - EntranceFoundation) * 0.5f, 0f), new Vector3(EntranceWidth, EntranceHeight + EntranceFoundation, EntranceDepth), CryptMaterial.Wall);
            AddLocal(new Vector3(0f, EntranceHeight + 0.2f, 0f), new Vector3(EntranceWidth + 0.8f, 0.4f, EntranceDepth + 0.8f), CryptMaterial.Ceiling);
            AddLocal(new Vector3(0f, EntranceHeight + 0.55f, 0f), new Vector3(EntranceWidth - 1f, 0.3f, EntranceDepth - 1f), CryptMaterial.Ceiling);
            AddLocal(new Vector3(0f, DoorOpeningHeight * 0.5f, halfDepth + 0.02f), new Vector3(DoorWidth, DoorOpeningHeight, 0.06f), CryptMaterial.Void);
            float pillarX = DoorWidth * 0.5f + 0.3f;
            foreach (float s in new[] { 1f, -1f })
            {
                AddLocal(new Vector3(s * pillarX, (DoorOpeningHeight + 0.3f) * 0.5f, halfDepth + 0.2f), new Vector3(0.5f, DoorOpeningHeight + 0.3f, 0.4f), CryptMaterial.Floor);
                EntranceLanterns.Add(new CryptLantern { Position = origin + turn * new Vector3(s * pillarX, 2.1f, halfDepth + 0.48f), Yaw = Site.Yaw });
            }
            AddLocal(new Vector3(0f, DoorOpeningHeight + 0.35f, halfDepth + 0.2f), new Vector3(DoorWidth + 1.2f, 0.5f, 0.5f), CryptMaterial.Floor);
            // A broad step in front of the door, deep enough to meet the ground on a slope.
            AddLocal(new Vector3(0f, -0.85f, halfDepth + 0.8f), new Vector3(DoorWidth + 1.8f, 2f, 1.6f), CryptMaterial.Floor);

            DoorSpot = origin + turn * new Vector3(0f, 0.15f, halfDepth + 1.2f);
            Vector3 back = new Vector3(center2.x, 0f, center2.y) + forward * (halfDepth + 2.8f);
            ReturnSpot = new Vector3(back.x, gen.HeightAt(back.x, back.z) + 0.3f, back.z);
        }
    }
}
