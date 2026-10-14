using System.Collections.Generic;
using System.IO;
using Chopper.Core;
using Chopper.World;
using Chopper.Trees;
using Chopper.World.Generation;
using Chopper.World.Voxel;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Batch: Chopper.Editor.PlacementMap.Draw. A top-down picture of what the generator puts in the world for seeds
    // 1337 and 4242: ground, water, dry hollows below the water line (orange), paths, trees by size, rocks, plants,
    // branches, crypts and mines.
    // Writes Logs/homework/placement-<seed>.png and logs counts per zone.
    public static class PlacementMap
    {
        const int Size = 1040;
        const float Half = 260f;

        public static void Draw()
        {
            WorldAssets assets = null;
            foreach (string guid in AssetDatabase.FindAssets("t:WorldAssets"))
                assets = AssetDatabase.LoadAssetAtPath<WorldAssets>(AssetDatabase.GUIDToAssetPath(guid));
            if (assets == null) throw new System.InvalidOperationException("No WorldAssets asset");
            foreach (int seed in new[] { 1337, 4242 }) DrawSeed(seed, assets);
        }

        static void DrawSeed(int seed, WorldAssets assets)
        {
            var gen = new WorldGen(seed, 110f);
            VoxelColumns columns = VoxelColumns.Build(gen);
            var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            var pixels = new Color32[Size * Size];
            // Dry hollows: ground below the water line with no water surface over it, in m2 by where they lie.
            float px2 = (2f * Half / Size) * (2f * Half / Size);
            float dryHub = 0f, dryRing = 0f, dryPass = 0f, dryForest = 0f;
            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    float x = PxToWorld(px), z = PxToWorld(py);
                    float h = gen.HeightAt(x, z);
                    Color c;
                    if (h < WorldGen.WaterHeight && !Wet(gen, x, z))
                    {
                        c = new Color(0.90f, 0.45f, 0.10f);
                        float r = Mathf.Sqrt(x * x + z * z);
                        if (gen.DistanceToPath(x, z) < WorldGen.PassHalfWidth + WorldGen.PassWallWidth + 12f && Mathf.Abs(r - gen.RingRadius) < WorldGen.RingFootHalfWidth + 16f) dryPass += px2;
                        else if (Mathf.Abs(r - gen.RingRadius) < WorldGen.RingFootHalfWidth + 16f) dryRing += px2;
                        else if (r < gen.RingRadius) dryHub += px2;
                        else dryForest += px2;
                    }
                    else if (h < WorldGen.WaterHeight) c = new Color(0.20f, 0.33f, 0.42f);
                    else
                    {
                        int cx = Mathf.FloorToInt(x + WorldGen.HalfSize), cz = Mathf.FloorToInt(z + WorldGen.HalfSize);
                        BlockType type = columns.Inside(cx, cz) ? columns.TypeAt(cx, cz) : BlockType.Grass;
                        switch (type)
                        {
                            case BlockType.Stone: c = new Color(0.50f, 0.50f, 0.50f); break;
                            case BlockType.Snow: c = new Color(0.92f, 0.92f, 0.95f); break;
                            case BlockType.Sand: c = new Color(0.80f, 0.74f, 0.55f); break;
                            case BlockType.Path: c = new Color(0.62f, 0.52f, 0.38f); break;
                            case BlockType.Dirt: c = new Color(0.45f, 0.33f, 0.22f); break;
                            default: c = new Color(0.52f, 0.66f, 0.38f); break;
                        }
                        float shade = Mathf.Clamp01(0.75f + (h - gen.HeightAt(x - 1f, z - 1f)) * 0.25f);
                        c *= shade;
                    }
                    pixels[py * Size + px] = c;
                }
            }

            List<TreeEntry> trees = gen.GenerateTrees(assets.SpeciesArrays(), assets.TreeModelHeights);
            var perZone = new Dictionary<Element, int[]>();
            foreach (TreeEntry tree in trees)
            {
                if (!perZone.TryGetValue(tree.Zone, out int[] sizes)) perZone[tree.Zone] = sizes = new int[3];
                sizes[(int)tree.Size]++;
                float r = 0.9f + (int)tree.Size * 0.9f;
                Disc(pixels, tree.Position.x, tree.Position.z, r * 1.6f, new Color(0.10f, 0.30f + 0.08f * (int)tree.ModelIndex % 3, 0.10f));
            }
            foreach (RockSpot rock in gen.GenerateRocks(assets.RockModels.Length))
                Square(pixels, rock.Position.x, rock.Position.y, rock.Size * 0.5f, new Color(0.30f, 0.30f, 0.32f));
            foreach (Vector2 p in gen.PlantSpots(WorldGen.PlantKind.Bush)) Disc(pixels, p.x, p.y, 0.9f, new Color(0.55f, 0.85f, 0.35f));
            foreach (Vector2 p in gen.PlantSpots(WorldGen.PlantKind.Fern)) Disc(pixels, p.x, p.y, 0.8f, new Color(0.30f, 0.75f, 0.70f));
            foreach (Vector2 p in gen.PlantSpots(WorldGen.PlantKind.Herb)) Disc(pixels, p.x, p.y, 0.8f, new Color(1f, 0.92f, 0.25f));
            foreach (Vector2 p in gen.BranchSpots(trees)) Disc(pixels, p.x, p.y, 0.6f, new Color(0.55f, 0.30f, 0.12f));
            foreach (CryptSite site in gen.Crypts) Square(pixels, site.Position.x, site.Position.y, 4f, new Color(0.85f, 0.15f, 0.15f));
            foreach (MineSite site in gen.Mines) Square(pixels, site.Position.x, site.Position.y, 4f, new Color(0.60f, 0.20f, 0.85f));

            var field = new Color32[Size * Size];
            for (int py = 0; py < Size; py += 2)
                for (int px = 0; px < Size; px += 2)
                {
                    float w = gen.Woodland(PxToWorld(px), PxToWorld(py));
                    float st = gen.Stoniness(PxToWorld(px), PxToWorld(py));
                    var c = new Color(st, w, 0f);
                    for (int k = 0; k < 4; k++) field[Mathf.Min(Size - 1, py + k / 2) * Size + Mathf.Min(Size - 1, px + k % 2)] = c;
                }
            var fieldTex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            fieldTex.SetPixels32(field);
            fieldTex.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "Logs", "homework", $"placement-{seed}-fields.png"), fieldTex.EncodeToPNG());
            Object.DestroyImmediate(fieldTex);

            tex.SetPixels32(pixels);
            tex.Apply();
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "homework");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, $"placement-{seed}.png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            var line = new System.Text.StringBuilder($"[Placement] seed {seed}: {trees.Count} trees");
            foreach (KeyValuePair<Element, int[]> zone in perZone)
                line.Append($" | {zone.Key} {zone.Value[0]}/{zone.Value[1]}/{zone.Value[2]}");
            line.Append($" | rocks {gen.GenerateRocks(assets.RockModels.Length).Count}");
            Debug.Log(line + $" -> {file}");
            Debug.Log($"[Placement] seed {seed}: dry hollows below the water line (orange), m2: hub {dryHub:F0}, ring foot {dryRing:F0}, passes {dryPass:F0}, forests {dryForest:F0}");
            var sum = new Dictionary<Element, float>();
            var n = new Dictionary<Element, int>();
            var woody = new Dictionary<Element, int>();
            for (float wz = -240f; wz <= 240f; wz += 4f)
                for (float wx = -240f; wx <= 240f; wx += 4f)
                {
                    if (wx * wx + wz * wz > 240f * 240f) continue;
                    Element e = ZoneMap.ElementAt(wx, wz, 110f);
                    float w = gen.Woodland(wx, wz);
                    sum[e] = (sum.TryGetValue(e, out float s0) ? s0 : 0f) + w;
                    n[e] = (n.TryGetValue(e, out int n0) ? n0 : 0) + 1;
                    woody[e] = (woody.TryGetValue(e, out int w0) ? w0 : 0) + (w > 0.5f ? 1 : 0);
                }
            var cov = new System.Text.StringBuilder("[Placement] woodland per zone (mean, share over 0.5, area m2):");
            foreach (Element e in n.Keys) cov.Append($" | {e} {sum[e] / n[e]:F2} {woody[e] / (float)n[e]:F2} {n[e] * 16}");
            Debug.Log(cov.ToString());
        }

        // Under a pool's swimmable surface or out at sea.
        static bool Wet(WorldGen gen, float x, float z)
        {
            if (x * x + z * z > WorldGen.BeachStartRadius * WorldGen.BeachStartRadius) return true;
            foreach (WaterBody body in gen.WaterBodies)
            {
                float shore = WorldGen.ShoreEdge(body) * 1.02f;
                if ((new Vector2(x, z) - body.Center).sqrMagnitude < shore * shore) return true;
            }
            return false;
        }

        static float PxToWorld(int p) => -Half + (p + 0.5f) * (2f * Half / Size);
        static int WorldToPx(float w) => Mathf.FloorToInt((w + Half) / (2f * Half) * Size);

        static void Disc(Color32[] pixels, float x, float z, float radius, Color color)
        {
            int cx = WorldToPx(x), cz = WorldToPx(z);
            int r = Mathf.Max(1, Mathf.RoundToInt(radius * Size / (2f * Half)));
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                    if (dx * dx + dz * dz <= r * r) Put(pixels, cx + dx, cz + dz, color);
        }

        static void Square(Color32[] pixels, float x, float z, float half, Color color)
        {
            int cx = WorldToPx(x), cz = WorldToPx(z);
            int r = Mathf.Max(1, Mathf.RoundToInt(half * Size / (2f * Half)));
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                    Put(pixels, cx + dx, cz + dz, color);
        }

        static void Put(Color32[] pixels, int x, int z, Color color)
        {
            if (x < 0 || z < 0 || x >= Size || z >= Size) return;
            pixels[z * Size + x] = color;
        }
    }
}
