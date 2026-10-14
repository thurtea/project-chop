using System.Collections.Generic;
using Chopper.Core;
using UnityEngine;

namespace Chopper.World.Generation
{
    // Where a mine's entrance is cut into a mountain: on the lower slope of a ridge, its timber portal facing out into
    // the forest. Trees and rocks never grow on that slope, so placing a mine moves nothing else in the world.
    public struct MineSite
    {
        public Vector2 Position;
        public float Yaw;
        public Element Forest;
        // The height of the flat pad cut for the portal, and the pad's center (a little in front of the portal).
        public float Ground;
        public Vector2 PadCenter;
    }

    // A vein in a chamber wall: Position is on the wall's face, Normal points out into the chamber.
    public struct MineVein
    {
        public Vector3 Position;
        public Vector3 Normal;
        public float Size;
    }

    // A prop standing on the mine floor (a cart, a barrel, a stretch of rails).
    public struct MineProp
    {
        public Vector3 Position;
        public float Yaw;
        public float Length;
    }

    // The whole of one mine, made from the world seed: a timber portal in the mountainside and, far below, a few
    // rough chambers joined by timbered galleries with rails along them. Veins sit in the chamber walls, lanterns hang
    // on posts, a ladder in the first chamber climbs back out. There are no enemies: a mine is for quiet work like the
    // pond. Pure C# apart from Unity math types, so it is unit tested and identical on every peer.
    public sealed class MinePlan
    {
        public const int MineCount = 4;
        // Below the crypts (-200) and their arenas (-230).
        public const float FloorY = -320f;
        public const float CellSize = 24f;
        public const float RoomSize = 16f;
        public const float RoomHeight = 6.5f;
        public const float WallThickness = 0.8f;
        public const float GalleryWidth = 5.5f;
        public const float GalleryHeight = 4.5f;
        private const int MaxReach = 2;
        private const float PostSize = 0.32f;
        private const float BeamDepth = 0.36f;
        private const float LanternHeight = 2.3f;
        public const float VeinMinHeight = 0.9f;
        public const float VeinMaxHeight = 1.6f;

        // The portal on the surface.
        public const float PortalWidth = 3.2f;
        public const float PortalHeight = 3.1f;
        private const float PortalDepth = 2.2f;
        private const float PortalSink = 1.2f;
        public const float ClearRadius = 6f;

        private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public readonly MineSite Site;
        public readonly int Index;
        public readonly MineZone Zone;
        public readonly Vector3 Origin;
        public readonly List<Vector2Int> Rooms = new List<Vector2Int>();
        public readonly List<(int a, int b)> Links = new List<(int a, int b)>();
        public readonly List<CryptBox> Boxes = new List<CryptBox>();
        public readonly List<CryptBox> EntranceBoxes = new List<CryptBox>();
        public readonly List<MineVein> Veins = new List<MineVein>();
        public readonly List<CryptLantern> Lanterns = new List<CryptLantern>();
        public readonly List<CryptLantern> EntranceLanterns = new List<CryptLantern>();
        public readonly List<MineProp> Rails = new List<MineProp>();
        public readonly List<MineProp> Carts = new List<MineProp>();
        public readonly List<MineProp> Barrels = new List<MineProp>();
        public readonly List<CavePiece> Dressing = new List<CavePiece>();
        public Vector3 LadderSpot { get; private set; }
        public float LadderYaw { get; private set; }

        public Vector3 DoorSpot { get; private set; }
        public Vector3 ReturnSpot { get; private set; }
        public float ReturnYaw => Site.Yaw;
        public Vector3 ExitSpot { get; private set; }
        public Vector3 ArrivalSpot { get; private set; }
        public float ArrivalYaw { get; private set; }
        public Vector2Int LadderSide { get; private set; }

        public string Name => MiningRules.MineName(Zone);

        public MinePlan(WorldGen gen, int index)
        {
            Site = gen.Mines[index];
            Index = index;
            Zone = MiningRules.ZoneOf(Site.Forest);
            Origin = new Vector3(Site.Position.x, FloorY, Site.Position.y);
            var random = new System.Random(gen.Seed * 131 + index * 6151 + 11);
            LayOutRooms(random);
            BuildRooms();
            BuildLadder();
            PlaceVeins(random);
            PlaceProps();
            BuildEntrance(gen);
            PlaceDressing();
        }

        public Vector3 RoomCenter(int room) => Origin + new Vector3(Rooms[room].x * CellSize, 0f, Rooms[room].y * CellSize);

        public bool Linked(int a, int b)
        {
            foreach ((int x, int y) in Links) if ((x == a && y == b) || (x == b && y == a)) return true;
            return false;
        }

        private bool SideOpen(int room, Vector2Int side)
        {
            int other = Rooms.IndexOf(Rooms[room] + side);
            return other >= 0 && Linked(room, other);
        }

        // True inside any chamber or gallery of this mine (a little slack around the walls).
        public bool Contains(Vector3 position)
        {
            if (position.y < FloorY - 2f || position.y > FloorY + RoomHeight + 2f) return false;
            for (int room = 0; room < Rooms.Count; room++)
            {
                Vector3 c = RoomCenter(room);
                if (Mathf.Abs(position.x - c.x) < CellSize * 0.5f + 1f && Mathf.Abs(position.z - c.z) < CellSize * 0.5f + 1f) return true;
            }
            return false;
        }

        // ---------- Layout ----------

        private void LayOutRooms(System.Random random)
        {
            int roomCount = 4 + random.Next(2);
            LadderSide = Directions[random.Next(Directions.Length)];
            Rooms.Add(Vector2Int.zero);
            int guard = 0;
            while (Rooms.Count < roomCount && guard++ < 1000)
            {
                int from = random.NextDouble() < 0.65 ? Rooms.Count - 1 : random.Next(Rooms.Count);
                Vector2Int side = Directions[random.Next(Directions.Length)];
                Vector2Int cell = Rooms[from] + side;
                if (Rooms.Contains(cell) || Mathf.Abs(cell.x) > MaxReach || Mathf.Abs(cell.y) > MaxReach) continue;
                if (from == 0 && side == LadderSide) continue;
                Rooms.Add(cell);
                Links.Add((from, Rooms.Count - 1));
            }
        }

        private static Vector3 Normal(Vector2Int side) => new Vector3(side.x, 0f, side.y);

        private static Vector3 Tangent(Vector2Int side) => new Vector3(side.y, 0f, -side.x);

        private bool OnLadder(Vector3 local)
        {
            float along = Vector3.Dot(local, Normal(LadderSide));
            float side = Vector3.Dot(local, Tangent(LadderSide));
            return along > RoomSize * 0.5f - 2.2f && Mathf.Abs(side) < 1.4f;
        }

        private static float YawOf(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        private static Vector3 SizeOn(Vector2Int side, float thickness, float length, float height)
        {
            Vector3 n = Normal(side);
            Vector3 t = Tangent(side);
            return new Vector3(Mathf.Abs(n.x) * thickness + Mathf.Abs(t.x) * length, height, Mathf.Abs(n.z) * thickness + Mathf.Abs(t.z) * length);
        }

        private int _group;

        private void Add(List<CryptBox> into, Vector3 center, Vector3 size, CryptMaterial material, Quaternion rotation)
        {
            into.Add(new CryptBox { Center = center, Size = size, Rotation = rotation, Material = material, Group = _group });
        }

        private void Add(Vector3 center, Vector3 size, CryptMaterial material) => Add(Boxes, center, size, material, Quaternion.identity);

        private void BuildRooms()
        {
            float outer = RoomSize + 2f * WallThickness;
            for (int room = 0; room < Rooms.Count; room++)
            {
                _group = room;
                Vector3 c = RoomCenter(room);
                Add(c + Vector3.down * 0.25f, new Vector3(outer, 0.5f, outer), CryptMaterial.Floor);
                Add(c + Vector3.up * (RoomHeight + 0.25f), new Vector3(outer, 0.5f, outer), CryptMaterial.Ceiling);
                // A timber beam across the middle of every chamber, on two posts, holding the roof up.
                Vector2Int across = room % 2 == 0 ? Vector2Int.up : Vector2Int.right;
                Vector3 a = Normal(across);
                Vector3 b = Tangent(across);
                foreach (float s in new[] { 1f, -1f })
                    Add(c + b * s * (RoomSize * 0.5f - PostSize) + Vector3.up * (RoomHeight * 0.5f), new Vector3(PostSize, RoomHeight, PostSize), CryptMaterial.Timber);
                Add(c + Vector3.up * (RoomHeight - BeamDepth * 0.5f), SizeOn(across, BeamDepth, RoomSize, BeamDepth), CryptMaterial.Timber);

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
                    float segment = (outer - GalleryWidth) * 0.5f;
                    float shift = GalleryWidth * 0.5f + segment * 0.5f;
                    Add(wall + t * shift, SizeOn(side, WallThickness, segment, RoomHeight), CryptMaterial.Wall);
                    Add(wall - t * shift, SizeOn(side, WallThickness, segment, RoomHeight), CryptMaterial.Wall);
                    float lintel = RoomHeight - GalleryHeight;
                    Add(c + n * (RoomSize * 0.5f + WallThickness * 0.5f) + Vector3.up * (GalleryHeight + lintel * 0.5f), SizeOn(side, WallThickness, GalleryWidth, lintel), CryptMaterial.Wall);
                    CaveShell.MouthSteps(Boxes, _group, c + n * (RoomSize * 0.5f), t, n, GalleryWidth, GalleryHeight);
                }
                int shellSeed = Index * 6151 + room * 503 + Mathf.RoundToInt(c.x);
                CaveShell.Chamfers(Boxes, _group, c, RoomSize * 0.5f, RoomHeight, CaveShell.Cut);
                CaveShell.FloorRelief(Boxes, _group, c, RoomSize * 0.5f, shellSeed, 3.4f, 2.4f, local =>
                    room == 0 && OnLadder(local));
                CaveShell.WallRelief(Boxes, _group, c, RoomSize * 0.5f, RoomHeight, shellSeed, side =>
                    !SideOpen(room, side) && !(room == 0 && side == LadderSide));
                CaveShell.CeilingNibs(Boxes, _group, c, RoomSize * 0.5f, RoomHeight, CaveShell.Cut);
                CaveShell.CeilingCourses(Boxes, _group, c, RoomSize * 0.5f, RoomHeight);
            }

            // Galleries fill only the gap between the chambers' outer wall faces, with a timber frame halfway along.
            float length = CellSize - RoomSize - 2f * WallThickness;
            foreach ((int a, int b) in Links)
            {
                _group = a;
                Vector2Int side = Rooms[b] - Rooms[a];
                Vector3 t = Tangent(side);
                Vector3 mid = (RoomCenter(a) + RoomCenter(b)) * 0.5f;
                Add(mid + Vector3.down * 0.25f, SizeOn(side, length, GalleryWidth + 2f * WallThickness, 0.5f), CryptMaterial.Floor);
                Add(mid + Vector3.up * (GalleryHeight + 0.25f), SizeOn(side, length, GalleryWidth + 2f * WallThickness, 0.5f), CryptMaterial.Ceiling);
                foreach (float s in new[] { 1f, -1f })
                {
                    Add(mid + t * s * (GalleryWidth * 0.5f + WallThickness * 0.5f) + Vector3.up * (GalleryHeight * 0.5f), SizeOn(side, length, WallThickness, GalleryHeight), CryptMaterial.Wall);
                }
                CaveShell.MouthSteps(Boxes, _group, mid, t, Normal(side), GalleryWidth, GalleryHeight);
                CaveShell.CorridorJog(Boxes, _group, mid, t, Normal(side), length, GalleryWidth, GalleryHeight);
                // One gallery-length rail. The mesh is placed from Dressing; this record stays for the plan.
                Rails.Add(new MineProp { Position = mid, Yaw = YawOf(Normal(side)), Length = 6.4f });
            }
        }

        // A ladder up the first chamber's wall into a dark shaft: the way back to the surface.
        private void BuildLadder()
        {
            _group = 0;
            Vector3 c = RoomCenter(0);
            Vector3 n = Normal(LadderSide);
            LadderSpot = c + n * (RoomSize * 0.5f - 0.15f);
            LadderYaw = YawOf(-n);
            // The shaft: a dark opening in the ceiling over the ladder.
            Add(c + n * (RoomSize * 0.5f - 0.7f) + Vector3.up * (RoomHeight + 0.02f), SizeOn(LadderSide, 1.2f, 1.4f, 0.06f), CryptMaterial.Void);
            ExitSpot = c + n * (RoomSize * 0.5f - 1.0f);
            ArrivalSpot = c + n * (RoomSize * 0.5f - 2.4f);
            ArrivalYaw = YawOf(-n);
        }

        // ---------- Contents ----------

        private List<Vector2Int> SolidSides(int room)
        {
            var sides = new List<Vector2Int>();
            foreach (Vector2Int side in Directions)
            {
                if (SideOpen(room, side) || (room == 0 && side == LadderSide)) continue;
                sides.Add(side);
            }
            return sides;
        }

        // Two or three veins in every chamber (one fewer by the ladder), each on a solid wall, never two on one wall.
        private void PlaceVeins(System.Random random)
        {
            for (int room = 0; room < Rooms.Count; room++)
            {
                List<Vector2Int> sides = SolidSides(room);
                for (int i = sides.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (sides[i], sides[j]) = (sides[j], sides[i]);
                }
                int count = Mathf.Min(sides.Count, (room == 0 ? 1 : 2) + random.Next(2));
                Vector3 c = RoomCenter(room);
                for (int v = 0; v < count; v++)
                {
                    Vector2Int side = sides[v];
                    Vector3 n = Normal(side);
                    Vector3 t = Tangent(side);
                    float along = Mathf.Lerp(-RoomSize * 0.3f, RoomSize * 0.3f, (float)random.NextDouble());
                    float height = Mathf.Lerp(VeinMinHeight, VeinMaxHeight, (float)random.NextDouble());
                    Veins.Add(new MineVein
                    {
                        Position = c + n * (RoomSize * 0.5f) + t * along + Vector3.up * height,
                        Normal = -n,
                        Size = Mathf.Lerp(0.95f, 1.35f, (float)random.NextDouble())
                    });
                }
            }
        }

        // Lanterns on the chamber posts. The cart and corner clutter are kit meshes (PlaceDressing).
        private void PlaceProps()
        {
            for (int room = 0; room < Rooms.Count; room++)
            {
                Vector3 c = RoomCenter(room);
                Vector2Int across = room % 2 == 0 ? Vector2Int.up : Vector2Int.right;
                Vector3 b = Tangent(across);
                Vector3 post = c + b * (RoomSize * 0.5f - PostSize);
                Lanterns.Add(new CryptLantern { Position = post - b * (PostSize * 0.5f + 0.05f) + Vector3.up * LanternHeight, Yaw = YawOf(-b) });
                Vector3 otherPost = c - b * (RoomSize * 0.5f - PostSize);
                Lanterns.Add(new CryptLantern { Position = otherPost + b * (PostSize * 0.5f + 0.05f) + Vector3.up * LanternHeight, Yaw = YawOf(b) });
            }
        }

        // Kit meshes. Its own seed, so veins (placed earlier on the layout stream) do not move.
        private void PlaceDressing()
        {
            int seed = Index * 104729 + Mathf.RoundToInt(Origin.x) * 9176 + Mathf.RoundToInt(Origin.z) * 131 + 52;
            int deepest = Rooms.Count - 1;
            foreach ((int a, int b) in Links)
            {
                Vector2Int side = Rooms[b] - Rooms[a];
                Vector3 mid = (RoomCenter(a) + RoomCenter(b)) * 0.5f + Vector3.up * 0.08f;
                CaveDress.Add(Dressing, CaveDress.Rail, mid, YawOf(Normal(side)), false);
            }
            for (int room = 0; room < Rooms.Count; room++)
            {
                var random = new System.Random(seed + room * 503);
                Vector3 origin = RoomCenter(room);
                float face = RoomSize * 0.5f + 0.08f;
                foreach (Vector2Int side in Directions)
                {
                    if (!SideOpen(room, side)) continue;
                    CaveDress.AddWall(Dressing, CaveDress.MineFrame, origin, Normal(side), face, 0f, 0f, false);
                }

                List<Vector2Int> solid = SolidSides(room);
                if (solid.Count > 0)
                {
                    Vector2Int oreSide = solid[random.Next(solid.Count)];
                    float oreSign = random.Next(2) == 0 ? 1f : -1f;
                    // Proud of the timber so the seam is not painted on the same plane.
                    CaveDress.AddWall(Dressing, CaveDress.MineOre, origin, Normal(oreSide), RoomSize * 0.5f - 0.55f, 4.0f * oreSign, 1.25f, true);
                }

                int corner = random.Next(4);
                if (room == deepest)
                {
                    Vector3 cart = CaveDress.Corner(CaveDress.MineCart, corner);
                    if (!CaveDress.BlocksWalkingCross(cart, 2.75f))
                        CaveDress.Add(Dressing, CaveDress.MineCart, origin + cart, 0f, true);
                    corner = (corner + 2) % 4;
                }
                string clutter = CaveDress.MineClutter[random.Next(CaveDress.MineClutter.Length)];
                Vector3 local = CaveDress.Corner(clutter, corner);
                if (!CaveDress.BlocksWalkingCross(local, 2.75f))
                    CaveDress.Add(Dressing, clutter, origin + local, 0f, true);
            }
        }

        // The portal: two timber posts and a lintel in a frame of rough stone, set into the slope, with a dark adit
        // behind and a lantern on the post. Its foot sinks into the ground so it stands on any slope.
        private void BuildEntrance(WorldGen gen)
        {
            _group = 0;
            float ground = Site.Ground;
            Quaternion turn = Quaternion.Euler(0f, Site.Yaw, 0f);
            Vector3 forward = turn * Vector3.forward;
            Vector3 right = turn * Vector3.right;
            Vector3 foot = new Vector3(Site.Position.x, ground, Site.Position.y);
            float postHeight = PortalHeight + PortalSink;
            foreach (float s in new[] { 1f, -1f })
            {
                Add(EntranceBoxes, foot + right * s * (PortalWidth * 0.5f + PostSize * 0.5f) + Vector3.up * (postHeight * 0.5f - PortalSink), new Vector3(PostSize * 1.3f, postHeight, PostSize * 1.3f), CryptMaterial.Timber, turn);
                // Rough stone cheeks either side of the posts, deep into the slope.
                Add(EntranceBoxes, foot + right * s * (PortalWidth * 0.5f + 1.1f) - forward * (PortalDepth * 0.5f) + Vector3.up * (postHeight * 0.5f - PortalSink), new Vector3(1.4f, postHeight + 0.6f, PortalDepth + 0.6f), CryptMaterial.Wall, turn);
            }
            Add(EntranceBoxes, foot + Vector3.up * (PortalHeight + BeamDepth * 0.7f), new Vector3(PortalWidth + 1.2f, BeamDepth * 1.4f, PostSize * 1.5f), CryptMaterial.Timber, turn);
            Add(EntranceBoxes, foot - forward * (PortalDepth * 0.5f) + Vector3.up * (PortalHeight + 1f), new Vector3(PortalWidth + 3.6f, 1.6f, PortalDepth + 0.6f), CryptMaterial.Wall, turn);
            // The dark adit going into the mountain.
            Add(EntranceBoxes, foot - forward * PortalDepth + Vector3.up * (PortalHeight * 0.5f - 0.3f), new Vector3(PortalWidth, PortalHeight + 0.6f, 0.1f), CryptMaterial.Void, turn);
            Add(EntranceBoxes, foot - forward * (PortalDepth * 0.5f) + Vector3.down * 0.25f, new Vector3(PortalWidth, 0.5f, PortalDepth), CryptMaterial.Floor, turn);
            EntranceLanterns.Add(new CryptLantern { Position = foot + right * (PortalWidth * 0.5f + PostSize) + forward * (PostSize + 0.1f) + Vector3.up * 2.2f, Yaw = Site.Yaw });

            DoorSpot = foot + forward * 0.6f;
            ReturnSpot = foot + forward * 2.6f + Vector3.up * 0.2f;
        }
    }
}
