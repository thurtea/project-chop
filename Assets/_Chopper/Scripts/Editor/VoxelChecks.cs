using Chopper.World.Generation;
using Chopper.World.Voxel;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Batch: Chopper.Editor.VoxelChecks.RunColumns
    public static class VoxelChecks
    {
        public static void RunColumns()
        {
            BlockPalette.Ensure();
            var first = VoxelColumns.Build(new WorldGen(4242, 110f));
            var second = VoxelColumns.Build(new WorldGen(4242, 110f));
            var third = VoxelColumns.Build(new WorldGen(4242, 110f));
            bool match = first.Crc == second.Crc && second.Crc == third.Crc;
            int triangles = VoxelWorld.CountTriangles(first);
            bool pass = match && triangles > 100000 && BlockPalette.LoadedFromFile;
            Debug.Log($"[Voxel] CRC {first.Crc:X8} three runs match {match}. First build {first.BuildMilliseconds} ms. Triangles {triangles}. Palette file {BlockPalette.LoadedFromFile}.");
            Debug.Log("[Voxel] RESULT " + (pass ? "PASS" : "FAIL"));
            if (!pass) EditorApplication.Exit(1);
        }

        // Batch: Chopper.Editor.VoxelChecks.ZoneSeal (W5-08 step 9). The mountains keep the zones apart only if no
        // block wall on them can be climbed: on blocks nothing slides, a double jump lifts the feet 2.4 m and the
        // controller steps 0.65 m more. Flood fill over the columns that climbs up to ClimbReach and drops any depth,
        // with the ring passes walled off, from the hub and from each zone. Nothing may leave the hub, and no zone may
        // reach another. Logs "[ZoneSeal] RESULT".
        const float ClimbReach = 3.05f;

        public static void ZoneSeal()
        {
            bool pass = true;
            foreach (int seed in new[] { 1337, 4242 })
            {
                var gen = new WorldGen(seed, 110f);
                VoxelColumns columns = VoxelColumns.Build(gen);
                int n = VoxelColumns.Resolution;
                var blocked = new bool[n * n];
                for (int z = 0; z < n; z++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float wx = VoxelColumns.WorldX(x) + 0.5f, wz = VoxelColumns.WorldZ(z) + 0.5f;
                        float r = Mathf.Sqrt(wx * wx + wz * wz);
                        bool ringBand = Mathf.Abs(r - gen.RingRadius) < WorldGen.RingFootHalfWidth + 6f;
                        blocked[z * n + x] = ringBand && gen.DistanceToPath(wx, wz) < WorldGen.PassHalfWidth + WorldGen.PassWallWidth + 3f;
                    }
                }
                float outside = gen.RingRadius + WorldGen.RingFootHalfWidth + 4f;
                (int reached, int maxUpIndex, float maxUp, int exampleIndex) hub = Flood(columns, blocked, Vector2.zero,
                    i => Radius(i, n) > outside);
                bool hubOk = hub.reached == 0;
                Debug.Log($"[ZoneSeal] seed {seed} hub: {(hubOk ? "sealed" : $"LEAKS, {hub.reached} columns outside the ring, e.g. {Where(hub.exampleIndex, n)}, highest climb on the way {hub.maxUp:F1} m at {Where(hub.maxUpIndex, n)}")}");
                pass &= hubOk;

                // And the other way round: with the passes open, the hub reaches every zone.
                var open = new bool[n * n];
                for (int k = 0; k < 4; k++)
                {
                    int want = k;
                    (int reached, int maxUpIndex, float maxUp, int exampleIndex) through = Flood(columns, open, Vector2.zero,
                        i => Radius(i, n) > outside + 20f && Quadrant(i, n) == want);
                    bool reachable = through.reached > 0;
                    Debug.Log($"[ZoneSeal] seed {seed} pass to zone {k}: {(reachable ? "open" : "CLOSED")}");
                    pass &= reachable;
                }

                float zoneR = gen.RingRadius + WorldGen.RingFootHalfWidth + 40f;
                for (int k = 0; k < 4; k++)
                {
                    float a = k * Mathf.PI * 0.5f;
                    var start = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * zoneR;
                    int own = k;
                    (int reached, int maxUpIndex, float maxUp, int exampleIndex) zone = Flood(columns, blocked, start,
                        i => Radius(i, n) > outside && Quadrant(i, n) != own && Quadrant(i, n) >= 0);
                    bool ok = zone.reached == 0;
                    Debug.Log($"[ZoneSeal] seed {seed} zone {k} from {start}: {(ok ? "sealed" : $"LEAKS into another zone, {zone.reached} columns, e.g. {Where(zone.exampleIndex, n)}, highest climb on the way {zone.maxUp:F1} m at {Where(zone.maxUpIndex, n)}")}");
                    pass &= ok;
                }
            }
            Debug.Log("[ZoneSeal] RESULT " + (pass ? "PASS" : "FAIL"));
            if (!pass) EditorApplication.Exit(1);
        }

        // Batch: Chopper.Editor.VoxelChecks.EntranceReach. Every crypt and mine door must be reachable on foot from the
        // path network with a single jump (1.2 m) and the 0.65 m step, no double jump and no building. Logs "[Entrances] RESULT".
        const float WalkClimb = 1.85f;

        public static void EntranceReach()
        {
            bool pass = true;
            foreach (int seed in new[] { 1337, 4242, 7, 2026 })
            {
                var gen = new WorldGen(seed, 110f);
                VoxelColumns columns = VoxelColumns.Build(gen);
                int n = VoxelColumns.Resolution;
                var onPath = new System.Collections.Generic.List<int>();
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                        if (gen.DistanceToPath(VoxelColumns.WorldX(x) + 0.5f, VoxelColumns.WorldZ(z) + 0.5f) < WorldGen.PathHalfWidth) onPath.Add(z * n + x);
                bool[] reached = Reach(columns, onPath, WalkClimb);
                int count = 0, outsideRing = 0;
                for (int i = 0; i < reached.Length; i++) if (reached[i]) { count++; if (Radius(i, n) > gen.RingRadius + WorldGen.RingFootHalfWidth + 4f) outsideRing++; }
                Debug.Log($"[Entrances] seed {seed}: {count} columns reached on foot from {onPath.Count} path columns, {outsideRing} outside the ring (ring {gen.RingRadius:F0} m), start top {columns.TopY(n / 2, n / 2):F1}");
                var doors = new System.Collections.Generic.List<(string, Vector3)>();
                for (int i = 0; i < gen.Crypts.Length; i++) doors.Add(($"crypt {i}", new CryptPlan(gen, i).DoorSpot));
                for (int i = 0; i < gen.Mines.Length; i++) doors.Add(($"mine {i}", new MinePlan(gen, i).DoorSpot));
                foreach ((string name, Vector3 door) in doors)
                {
                    bool ok = false;
                    int dx0 = Mathf.FloorToInt(door.x + WorldGen.HalfSize), dz0 = Mathf.FloorToInt(door.z + WorldGen.HalfSize);
                    for (int z = dz0 - 2; z <= dz0 + 2 && !ok; z++)
                        for (int x = dx0 - 2; x <= dx0 + 2 && !ok; x++)
                            ok = columns.Inside(x, z) && reached[z * n + x] && Mathf.Abs(columns.TopY(x, z) - door.y) < 1.2f;
                    float best = float.MaxValue, bestTop = 0f;
                    for (int z = dz0 - 30; z <= dz0 + 30; z++)
                        for (int x = dx0 - 30; x <= dx0 + 30; x++)
                        {
                            if (!columns.Inside(x, z) || !reached[z * n + x]) continue;
                            float dd = (x - dx0) * (x - dx0) + (z - dz0) * (z - dz0);
                            if (dd < best) { best = dd; bestTop = columns.TopY(x, z); }
                        }
                    string near = best < float.MaxValue ? $"nearest reached column {Mathf.Sqrt(best):F0} m away, top {bestTop:F1}" : "nothing reached within 30 m";
                    float doorTop = columns.Inside(dx0, dz0) ? columns.TopY(dx0, dz0) : 0f;
                    float lowest = doorTop;
                    for (int z = dz0 - 3; z <= dz0 + 3; z++)
                        for (int x = dx0 - 3; x <= dx0 + 3; x++)
                            if (columns.Inside(x, z)) lowest = Mathf.Min(lowest, columns.TopY(x, z));
                    bool raised = door.y - lowest > 1.85f;
                    ok &= !raised;
                    Debug.Log($"[Entrances] seed {seed} {name} at ({door.x:F0}, {door.y:F1}, {door.z:F0}), column top {doorTop:F1}, lowest ground within 3 m {lowest:F1}{(raised ? " (door raised above the ground)" : "")}: {(ok ? "on foot" : "UNREACHABLE without building")}, {near}");
                    pass &= ok;
                }
            }
            Debug.Log("[Entrances] RESULT " + (pass ? "PASS" : "FAIL"));
            if (!pass) EditorApplication.Exit(1);
        }

        static bool[] Reach(VoxelColumns columns, System.Collections.Generic.List<int> starts, float climb)
        {
            int n = VoxelColumns.Resolution;
            var seen = new bool[n * n];
            var queue = new System.Collections.Generic.Queue<int>();
            foreach (int s in starts)
            {
                seen[s] = true;
                queue.Enqueue(s);
            }
            int[] dx = { 1, -1, 0, 0 }, dz = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                int cx = c % n, cz = c / n;
                float top = columns.TopY(cx, cz);
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + dx[d], nz = cz + dz[d];
                    if (!columns.Inside(nx, nz)) continue;
                    int ni = nz * n + nx;
                    if (seen[ni] || columns.TopY(nx, nz) - top > climb) continue;
                    seen[ni] = true;
                    queue.Enqueue(ni);
                }
            }
            return seen;
        }

        static float Radius(int i, int n)
        {
            float x = VoxelColumns.WorldX(i % n) + 0.5f, z = VoxelColumns.WorldZ(i / n) + 0.5f;
            return Mathf.Sqrt(x * x + z * z);
        }

        // Which zone (0..3, around angles 0, 90, 180, 270 degrees) a column lies in; -1 within 30 m of a diagonal,
        // the ridge itself, which belongs to neither.
        static int Quadrant(int i, int n)
        {
            float x = VoxelColumns.WorldX(i % n) + 0.5f, z = VoxelColumns.WorldZ(i / n) + 0.5f;
            if (Mathf.Abs(Mathf.Abs(x) - Mathf.Abs(z)) * 0.70710678f < 30f) return -1;
            float deg = Mathf.Repeat(Mathf.Atan2(z, x) * Mathf.Rad2Deg + 45f, 360f);
            return (int)(deg / 90f);
        }

        static string Where(int i, int n) => i < 0 ? "-" : $"({VoxelColumns.WorldX(i % n) + 0.5f:F0}, {VoxelColumns.WorldZ(i / n) + 0.5f:F0})";

        static (int, int, float, int) Flood(VoxelColumns columns, bool[] blocked, Vector2 start, System.Func<int, bool> bad)
        {
            int n = VoxelColumns.Resolution;
            var parent = new int[n * n];
            for (int i = 0; i < parent.Length; i++) parent[i] = -2;
            int sx = Mathf.Clamp(Mathf.FloorToInt(start.x + WorldGen.HalfSize), 0, n - 1);
            int sz = Mathf.Clamp(Mathf.FloorToInt(start.y + WorldGen.HalfSize), 0, n - 1);
            var queue = new System.Collections.Generic.Queue<int>();
            int s = sz * n + sx;
            parent[s] = -1;
            queue.Enqueue(s);
            int reached = 0, example = -1;
            int[] dx = { 1, -1, 0, 0 }, dz = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                if (bad(c))
                {
                    reached++;
                    if (example < 0) example = c;
                    continue;
                }
                int cx = c % n, cz = c / n;
                float top = columns.TopY(cx, cz);
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + dx[d], nz = cz + dz[d];
                    if (!columns.Inside(nx, nz)) continue;
                    int ni = nz * n + nx;
                    if (parent[ni] != -2 || blocked[ni]) continue;
                    if (columns.TopY(nx, nz) - top > ClimbReach) continue;
                    parent[ni] = c;
                    queue.Enqueue(ni);
                }
            }
            int maxUpIndex = -1;
            float maxUp = 0f;
            var climbs = new System.Text.StringBuilder();
            for (int c = example; c >= 0 && parent[c] >= 0; c = parent[c])
            {
                int p = parent[c];
                float up = columns.TopY(c % n, c / n) - columns.TopY(p % n, p / n);
                if (up > maxUp) { maxUp = up; maxUpIndex = c; }
                if (up > 1.5f && climbs.Length < 600) climbs.Append($" {Where(c, n)}+{up:F1}@{columns.TopY(c % n, c / n):F1}");
            }
            if (example >= 0) Debug.Log("[ZoneSeal] climbs over 1.5 m on the way (newest first):" + climbs);
            return (reached, maxUpIndex, maxUp, example);
        }

        // Batch: Chopper.Editor.VoxelChecks.CheckBlockShader. Lists Chopper/Block compile errors for this platform. A
        // shader with errors draws nothing in a player, which hid the whole block ground.
        public static void CheckBlockShader()
        {
            Shader shader = Shader.Find("Chopper/Block");
            bool pass = shader != null;
            if (shader != null)
            {
                for (int i = 0; i < shader.passCount; i++)
                    ShaderUtil.CompilePass(new Material(shader), i, true);
                foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                {
                    Debug.Log($"[Voxel] shader {message.severity} line {message.line}: {message.message}");
                    if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) pass = false;
                }
                bool hasError = ShaderUtil.ShaderHasError(shader);
                Debug.Log($"[Voxel] shader passes {shader.passCount}, has error {hasError}");
                if (hasError) pass = false;
            }
            Debug.Log("[Voxel] SHADER " + (pass ? "PASS" : "FAIL"));
            EditorApplication.Exit(pass ? 0 : 1);
        }
    }
}
