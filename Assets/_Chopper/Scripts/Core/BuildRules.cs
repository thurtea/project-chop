using UnityEngine;

namespace Chopper.Core
{
    // How a piece snaps to the building grid (Valheim style, on 2 m cells).
    public enum PieceSnap
    {
        // Fills a 2 by 2 m cell: floors, roofs and stairs.
        Cell = 0,
        // Stands on the edge between two cells: walls and doorways.
        Edge = 1,
        // Anywhere, on a fine half meter grid: the workbench, torches and campfires.
        Free = 2
    }

    // Pure building rules: snapping to the grid, turning in quarter steps, overlap between pieces, support (nothing
    // floats) and the slope a piece can stand on. Pieces are boxes: size is width (local X), height and depth (local Z)
    // with the pivot at the bottom center.
    public static class BuildRules
    {
        public const float Cell = 2f;
        public const float FreeStep = 0.5f;
        public const float HeightStep = 0.05f;
        // Boxes may touch: this much overlap on every axis still counts as touching, not overlapping. Half a wall's
        // thickness, so two walls meet at a corner.
        public const float Touch = 0.16f;
        // How far above the ground a piece's bottom may sit and still stand on it.
        public const float GroundTolerance = 0.45f;
        public const float MaxGroundSlope = 32f;
        // On block ground (W5-08 step 10) the slope rule is the step under the footprint: one half block at most.
        public const float MaxFootprintStep = 0.5f;

        public static bool TooUneven(float lowestTop, float highestTop) => highestTop - lowestTop > MaxFootprintStep + 0.01f;
        // How many pieces away from one on the ground a piece may be (Valheim's structural support, by count): a floor
        // on the ground is 0, a wall on it 1, a second storey wall 2, and so on. Enough for a two storey house and a
        // roof, not for towers or long bridges into the air.
        public const int MaxSupportSteps = 8;

        // ---------- Snap points (Valheim) ----------

        // A new piece's snap point joins a placed piece's snap point when the two are this close (meters).
        public const float SnapRadius = 1.25f;

        // A snap point of a piece placed at position, turned by yaw (local points: x right, y up, z forward).
        public static Vector3 WorldPoint(Vector3 position, float yaw, Vector3 local) => position + Quaternion.Euler(0f, yaw, 0f) * local;

        // Every way the new piece (its local snap points, placed at position and yaw) can join the given placed snap
        // points, nearest first: each is the move that puts one of its points exactly on one of theirs. Pairs further
        // apart than SnapRadius are left out. Pure, so it is unit tested; the hammer tries them in order and takes the
        // first that fits.
        public static System.Collections.Generic.List<Vector3> SnapMoves(Vector3 position, float yaw, Vector3[] ownPoints, System.Collections.Generic.IList<Vector3> targets)
        {
            var moves = new System.Collections.Generic.List<(float distance, Vector3 move)>();
            if (ownPoints == null) return new System.Collections.Generic.List<Vector3>();
            foreach (Vector3 local in ownPoints)
            {
                Vector3 own = WorldPoint(position, yaw, local);
                foreach (Vector3 target in targets)
                {
                    Vector3 move = target - own;
                    float distance = move.magnitude;
                    if (distance <= SnapRadius) moves.Add((distance, move));
                }
            }
            moves.Sort((a, b) => a.distance.CompareTo(b.distance));
            var result = new System.Collections.Generic.List<Vector3>(moves.Count);
            foreach (var m in moves)
            {
                bool same = false;
                foreach (Vector3 seen in result) if ((seen - m.move).sqrMagnitude < 1e-4f) same = true;
                if (!same) result.Add(m.move);
            }
            return result;
        }

        // Yaw snapped to quarter turns, 0 to 270.
        public static float SnapYaw(float yaw)
        {
            float step = Mathf.Round(yaw / 90f) * 90f;
            step %= 360f;
            if (step < 0f) step += 360f;
            return step;
        }

        // True when the piece runs along Z (turned a quarter or three quarters).
        public static bool Turned(float yaw) => Mathf.Abs(Mathf.DeltaAngle(SnapYaw(yaw), 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(SnapYaw(yaw), 270f)) < 1f;

        // Where a piece goes when the player aims at `aim` (the surface point under the crosshair).
        public static Vector3 Snap(Vector3 aim, PieceSnap snap, float yaw)
        {
            float y = Mathf.Round(aim.y / HeightStep) * HeightStep;
            switch (snap)
            {
                case PieceSnap.Cell:
                    return new Vector3(CellCenter(aim.x), y, CellCenter(aim.z));
                case PieceSnap.Edge:
                    // A wall along X sits on a line between rows of cells (z on the grid), centered on a cell in x; a
                    // turned wall the other way round.
                    return Turned(yaw)
                        ? new Vector3(GridLine(aim.x), y, CellCenter(aim.z))
                        : new Vector3(CellCenter(aim.x), y, GridLine(aim.z));
                default:
                    return new Vector3(Mathf.Round(aim.x / FreeStep) * FreeStep, y, Mathf.Round(aim.z / FreeStep) * FreeStep);
            }
        }

        public static float CellCenter(float v) => Mathf.Floor(v / Cell) * Cell + Cell * 0.5f;

        public static float GridLine(float v) => Mathf.Round(v / Cell) * Cell;

        // The box a piece fills in the world, for a quarter turn yaw.
        public static Bounds BoundsOf(Vector3 position, Vector3 size, float yaw)
        {
            Vector3 world = Turned(yaw) ? new Vector3(size.z, size.y, size.x) : size;
            return new Bounds(position + Vector3.up * world.y * 0.5f, world);
        }

        // Overlap of more than a touch on every axis.
        public static bool Overlaps(Bounds a, Bounds b)
        {
            return a.max.x - Touch > b.min.x && b.max.x - Touch > a.min.x
                   && a.max.y - Touch > b.min.y && b.max.y - Touch > a.min.y
                   && a.max.z - Touch > b.min.z && b.max.z - Touch > a.min.z;
        }

        // Touching or overlapping (within a small gap): a piece leaning on another is held up by it.
        public static bool Touches(Bounds a, Bounds b, float gap = 0.1f)
        {
            a.Expand(gap * 2f);
            return a.Intersects(b);
        }

        // Nothing floats: a piece stands on the ground, or rests against a piece that is already there.
        public static bool Supported(float bottom, float ground, bool touchesPiece)
        {
            return touchesPiece || Mathf.Abs(bottom - ground) <= GroundTolerance;
        }

        // Ground steeper than this cannot take a piece (normalY is the surface normal's up part).
        public static bool TooSteep(float normalY) => normalY < Mathf.Cos(MaxGroundSlope * Mathf.Deg2Rad);

        public static bool InRange(Vector3 player, Vector3 piece, float range)
        {
            Vector3 offset = piece - player;
            return offset.sqrMagnitude <= range * range;
        }

        // Taking a piece down gives back its cost by how whole it is, so a refund plus the repair of the same damage is
        // exactly the cost (taking a damaged piece down is never cheaper than mending it). A broken piece gives nothing.
        public static int Refund(int cost, int health, int maxHealth)
        {
            if (cost <= 0 || health <= 0) return 0;
            return Mathf.Max(0, cost - RepairCost(health, maxHealth, cost));
        }

        // Support steps for every piece: 0 on the ground, then one more for each piece it leans on, taking the shortest
        // way down; -1 when nothing connects it to the ground. Pieces hold each other up where their boxes touch.
        public static int[] SupportSteps(Bounds[] boxes, bool[] grounded)
        {
            int count = boxes.Length;
            var steps = new int[count];
            var queue = new System.Collections.Generic.Queue<int>();
            for (int i = 0; i < count; i++)
            {
                steps[i] = grounded[i] ? 0 : -1;
                if (grounded[i]) queue.Enqueue(i);
            }
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                for (int other = 0; other < count; other++)
                {
                    if (steps[other] >= 0 || !Touches(boxes[at], boxes[other])) continue;
                    steps[other] = steps[at] + 1;
                    queue.Enqueue(other);
                }
            }
            return steps;
        }

        // True when a piece with these steps stands (connected, and not too far from the ground).
        public static bool Stands(int steps) => steps >= 0 && steps <= MaxSupportSteps;

        // Wood to mend a damaged piece: its share of the cost, at least one when anything is missing.
        public static int RepairCost(int health, int maxHealth, int woodCost)
        {
            if (maxHealth <= 0 || health >= maxHealth) return 0;
            float missing = (maxHealth - Mathf.Max(0, health)) / (float)maxHealth;
            return Mathf.Max(1, Mathf.CeilToInt(missing * woodCost));
        }
    }
}
