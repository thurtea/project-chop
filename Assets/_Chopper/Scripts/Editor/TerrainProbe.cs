using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Development helper: prints what the painted terrain contains at a few places, to check the zone layers.
    // Batch: -executeMethod Chopper.Editor.TerrainProbe.Run
    public static class TerrainProbe
    {
        [MenuItem("Chopper/Setup/Probe Terrain Layers")]
        public static void Run()
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/_Chopper/World/MeadowTerrain.asset");
            if (data == null) { Debug.Log("[TerrainProbe] no terrain data"); return; }

            for (int i = 0; i < data.terrainLayers.Length; i++)
            {
                TerrainLayer layer = data.terrainLayers[i];
                Texture2D tex = layer != null ? layer.diffuseTexture : null;
                string pixel = "none";
                if (tex != null)
                {
                    string path = AssetDatabase.GetAssetPath(tex);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    Color c = tex.isReadable ? tex.GetPixel(10, 10) : Color.clear;
                    pixel = $"{path} readable={tex.isReadable} srgb={(importer != null ? importer.sRGBTexture.ToString() : "?")} px={c}";
                }
                Debug.Log($"[TerrainProbe] layer {i} {(layer != null ? layer.name : "null")} diffuse={pixel} smoothness={(layer != null ? layer.smoothness : -1f)}");
            }

            float half = data.size.x * 0.5f;
            var spots = new (string name, float x, float z)[]
            {
                ("hub", 40f, 40f), ("water", 0f, 160f), ("earth", 160f, 0f), ("fire", 0f, -160f), ("wind", -160f, 0f), ("path north", 0f, 60f)
            };
            foreach (var spot in spots)
            {
                float u = (spot.x + half) / data.size.x;
                float v = (spot.z + half) / data.size.z;
                int ax = Mathf.Clamp(Mathf.RoundToInt(u * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
                int az = Mathf.Clamp(Mathf.RoundToInt(v * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);
                float[,,] alpha = data.GetAlphamaps(ax, az, 1, 1);
                var text = new System.Text.StringBuilder();
                for (int l = 0; l < data.alphamapLayers; l++) text.Append($" L{l}={alpha[0, 0, l]:F2}");
                Debug.Log($"[TerrainProbe] {spot.name}:{text}");
            }
        }
    }
}
