using System.Globalization;
using System.IO;
using UnityEngine;

namespace Chopper.World.Voxel
{
    public enum BlockType : byte
    {
        Grass = 0,
        Dirt = 1,
        Stone = 2,
        Sand = 3,
        Snow = 4,
        Path = 5
    }

    // Face colours for the block mesh. Linear RGB, matching terrain_palette.txt when that file is present.
    public static class BlockPalette
    {
        public struct FaceColors
        {
            public Color Top;
            public Color Side;
        }

        public static FaceColors[] Faces { get; private set; }
        public static bool LoadedFromFile { get; private set; }

        public static void Ensure()
        {
            if (Faces != null) return;
            Faces = Defaults();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Chopper", "Art", "Export", "terrain_palette.txt"));
            LoadedFromFile = TryLoad(path);
        }

        public static FaceColors Of(BlockType type) => Faces[(int)type];

        static FaceColors[] Defaults()
        {
            var faces = new FaceColors[6];
            faces[(int)BlockType.Grass] = Face(0.1070f, 0.2623f, 0.0296f, 0.2159f, 0.0931f, 0.0343f);
            faces[(int)BlockType.Dirt] = Face(0.2384f, 0.1070f, 0.0395f, 0.2159f, 0.0931f, 0.0343f);
            faces[(int)BlockType.Stone] = Face(0.2232f, 0.2232f, 0.2086f, 0.1878f, 0.1878f, 0.1746f);
            faces[(int)BlockType.Sand] = Face(0.6867f, 0.5647f, 0.2705f, 0.6172f, 0.4910f, 0.2232f);
            faces[(int)BlockType.Snow] = Face(0.8550f, 0.8879f, 0.9387f, 0.7605f, 0.8070f, 0.8879f);
            faces[(int)BlockType.Path] = Face(0.1878f, 0.1746f, 0.1500f, 0.1500f, 0.1441f, 0.1170f);
            return faces;
        }

        static FaceColors Face(float tr, float tg, float tb, float sr, float sg, float sb)
        {
            return new FaceColors { Top = new Color(tr, tg, tb), Side = new Color(sr, sg, sb) };
        }

        // Reads the "base" linear triple on each block's top and side lines.
        static bool TryLoad(string path)
        {
            if (!File.Exists(path)) return false;
            BlockType current = BlockType.Grass;
            bool any = false;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    switch (line.Substring(1, line.Length - 2))
                    {
                        case "grass": current = BlockType.Grass; break;
                        case "dirt": current = BlockType.Dirt; break;
                        case "stone": current = BlockType.Stone; break;
                        case "sand": current = BlockType.Sand; break;
                        case "snow": current = BlockType.Snow; break;
                        default: current = (BlockType)255; break;
                    }
                    continue;
                }
                if ((byte)current > (byte)BlockType.Path) continue;
                bool top = line.StartsWith("top");
                bool side = line.StartsWith("side");
                if (!top && !side) continue;
                if (!TryBase(line, out Color color)) continue;
                FaceColors face = Faces[(int)current];
                if (top) face.Top = color;
                else face.Side = color;
                Faces[(int)current] = face;
                any = true;
            }
            return any;
        }

        static bool TryBase(string line, out Color color)
        {
            color = default;
            int bas = line.IndexOf("base", System.StringComparison.Ordinal);
            if (bas < 0) return false;
            int open = line.IndexOf('(', bas);
            int close = open >= 0 ? line.IndexOf(')', open) : -1;
            if (open < 0 || close < 0) return false;
            string[] parts = line.Substring(open + 1, close - open - 1).Split(' ');
            if (parts.Length < 3) return false;
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r)) return false;
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g)) return false;
            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b)) return false;
            color = new Color(r, g, b);
            return true;
        }
    }
}
