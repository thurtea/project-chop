using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Prepares the "100+ Pixel Lowpoly Assets 3D" pack for URP and the crunchy pixel look. Safe to run repeatedly.
    // - Textures: Point filter, no mipmaps, clamp off (tiles), no compression artifacts.
    // - Materials: built-in Standard becomes URP Lit with the same texture, color, cutout and emission.
    // Menu: Chopper > Setup > Prepare Pixel Pack. Batch: -executeMethod Chopper.Editor.PixelPackSetup.Run
    public static class PixelPackSetup
    {
        public const string Root = "Assets/ThirdParty/PixelLowpoly_100Assets";
        private const string MeshFolder = Root + "/Essential/Meshes";

        [MenuItem("Chopper/Setup/Prepare Pixel Pack")]
        public static void Run()
        {
            PrepareTextures();
            PrepareMaterials();
            BalanceMaterials();
            PrepareModels();
            AssetDatabase.SaveAssets();
            Debug.Log("[Chopper] Pixel pack prepared.");
        }

        // The FBX files carry embedded materials without the cutout setup; the pack's prefabs assign the real ones.
        // Remap every embedded material to the pack material of the same name so the FBX can be used directly.
        private static void PrepareModels()
        {
            var packMaterials = new Dictionary<string, Material>();
            foreach (string path in Find("t:Material", Root + "/Essential/Materials"))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null) packMaterials[material.name] = material;
            }

            // Wanted remaps per model: model path -> (embedded material name -> pack material).
            var wanted = new Dictionary<string, Dictionary<string, Material>>();
            foreach (string path in Find("t:Model", MeshFolder))
            {
                var map = new Dictionary<string, Material>();
                foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    if (packMaterials.TryGetValue(embedded.name, out Material same)) map[embedded.name] = same;
                }
                wanted[path] = map;
            }

            // The pack's prefabs say which pack material goes in which slot of which model.
            foreach (string prefabPath in Find("t:Prefab", Root + "/_PREFABS"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (filter.sharedMesh == null || renderer == null) continue;
                    string modelPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    if (!wanted.TryGetValue(modelPath, out Dictionary<string, Material> map)) continue;

                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                    foreach (MeshFilter source in model.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (source.sharedMesh != filter.sharedMesh) continue;
                        Material[] embedded = source.GetComponent<MeshRenderer>().sharedMaterials;
                        Material[] chosen = renderer.sharedMaterials;
                        for (int i = 0; i < embedded.Length && i < chosen.Length; i++)
                        {
                            if (embedded[i] == null || chosen[i] == null) continue;
                            if (!map.ContainsKey(embedded[i].name)) map[embedded[i].name] = chosen[i];
                        }
                    }
                }
            }

            foreach (KeyValuePair<string, Dictionary<string, Material>> pair in wanted)
            {
                var importer = AssetImporter.GetAtPath(pair.Key) as ModelImporter;
                if (importer == null) continue;
                bool changed = false;
                // Stale remaps (missing target, or a self reference left by the pack's export) leave null materials.
                foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, Object> stale in importer.GetExternalObjectMap().ToList())
                {
                    if (stale.Key.type != typeof(Material)) continue;
                    if (stale.Value != null && AssetDatabase.GetAssetPath(stale.Value) != pair.Key) continue;
                    importer.RemoveRemap(stale.Key);
                    changed = true;
                }
                foreach (KeyValuePair<string, Material> remap in pair.Value)
                {
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), remap.Key);
                    if (importer.GetExternalObjectMap().TryGetValue(id, out Object current) && current == remap.Value) continue;
                    importer.AddRemap(id, remap.Value);
                    changed = true;
                }
                if (changed) importer.SaveAndReimport();
            }
        }

        // Point filter for every texture in the pack; anything else would blur the pixels.
        private static void PrepareTextures()
        {
            foreach (string path in Find("t:Texture2D", Root + "/Essential/Textures"))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                bool changed = false;
                if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    changed = true;
                }
                if (importer.DoesSourceTextureHaveAlpha() && !importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    changed = true;
                }
                if (changed) importer.SaveAndReimport();
            }
        }

        // Brightness of the pack in the game's lighting. The pack's own colors are dark: wood and roof textures sit
        // near 25 percent and many materials carry a further dark tint, which turned the house and the tree trunks
        // black. Dark tints are normalized to their brightest channel and structures get a gain (in linear terms; the
        // property is converted from gamma, so the gain is raised to 1/2.2 first). Emission is clamped: the pack's
        // lanterns glow at 11 times, which the bloom turns into a white square.
        private const float MaxEmission = 2.4f;
        private const float StructureGain = 3.0f;
        private const float TrunkGain = 2.5f;
        private const float FurnitureGain = 1.8f;
        private static readonly string[] StructurePrefixes = { "pixel_brick", "pixel_roof", "pixel_stone", "pixel_wood", "pixel_cabin", "psx_wall", "pixel_support", "pixel_rope" };
        private static readonly string[] TrunkPrefixes = { "tree_oak_log", "tree_eucalyptus_log", "pier_oak_log" };
        private static readonly string[] FurniturePrefixes = { "psx_wood", "psx_barrel", "psx_cabinet", "psx_chest_base", "psx_frame" };

        private static float LinearGainFor(string name)
        {
            if (StartsWithAny(name, StructurePrefixes)) return StructureGain;
            if (StartsWithAny(name, TrunkPrefixes)) return TrunkGain;
            if (StartsWithAny(name, FurniturePrefixes)) return FurnitureGain;
            return 1f;
        }

        private static bool StartsWithAny(string name, string[] prefixes)
        {
            foreach (string prefix in prefixes) if (name.StartsWith(prefix)) return true;
            return false;
        }

        private static void BalanceMaterials()
        {
            foreach (string path in Find("t:Material", Root + "/Essential/Materials"))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || !material.HasProperty("_BaseColor")) continue;
                bool changed = false;

                float gain = LinearGainFor(material.name);
                if (gain > 1f)
                {
                    Color c = material.GetColor("_BaseColor");
                    float brightest = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    if (brightest > 0.001f)
                    {
                        float scale = Mathf.Pow(gain, 1f / 2.2f) / brightest;
                        var balanced = new Color(c.r * scale, c.g * scale, c.b * scale, 1f);
                        if (balanced != c) { material.SetColor("_BaseColor", balanced); changed = true; }
                    }
                }

                if (material.HasProperty("_EmissionColor"))
                {
                    Color e = material.GetColor("_EmissionColor");
                    float strongest = Mathf.Max(e.r, Mathf.Max(e.g, e.b));
                    if (strongest > MaxEmission)
                    {
                        float k = MaxEmission / strongest;
                        material.SetColor("_EmissionColor", new Color(e.r * k, e.g * k, e.b * k, 1f));
                        changed = true;
                    }
                }
                if (changed) EditorUtility.SetDirty(material);
            }
        }

        private static void PrepareMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (string path in Find("t:Material", Root + "/Essential/Materials"))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;
                if (material.shader == lit)
                {
                    if (material.name.StartsWith("tree_foliage") && material.GetColor("_BaseColor") != Color.white)
                    {
                        material.SetColor("_BaseColor", Color.white);
                        EditorUtility.SetDirty(material);
                    }
                    continue;
                }

                Texture mainTex = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                float cutoff = material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : 0.5f;
                bool cutout = material.IsKeywordEnabled("_ALPHATEST_ON")
                              || (material.HasProperty("_Mode") && Mathf.RoundToInt(material.GetFloat("_Mode")) == 1);
                bool transparent = material.HasProperty("_Mode") && Mathf.RoundToInt(material.GetFloat("_Mode")) >= 2;
                bool emissive = material.IsKeywordEnabled("_EMISSION");
                Color emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
                Texture emissionMap = material.HasProperty("_EmissionMap") ? material.GetTexture("_EmissionMap") : null;

                material.shader = lit;
                material.SetTexture("_BaseMap", mainTex);
                // The foliage cards carry a dark tint that turned the green tree nearly black; the texture alone is right.
                if (material.name.StartsWith("tree_foliage")) color = Color.white;
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", 0f);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_SpecularHighlights", 0f);
                material.SetFloat("_EnvironmentReflections", 0f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                material.enableInstancing = true;

                if (cutout)
                {
                    material.SetFloat("_AlphaClip", 1f);
                    material.SetFloat("_Cutoff", cutoff);
                    material.SetFloat("_Cull", 0f);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.SetOverrideTag("RenderType", "TransparentCutout");
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    material.doubleSidedGI = true;
                }
                if (transparent)
                {
                    material.SetFloat("_Surface", 1f);
                    material.SetFloat("_Blend", 0f);
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0f);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                if (emissive)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission);
                    if (emissionMap != null) material.SetTexture("_EmissionMap", emissionMap);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                EditorUtility.SetDirty(material);
            }
        }

        // Writes the size of every mesh and prefab so the builder can scale the pack against the 1.9 m player.
        [MenuItem("Chopper/Setup/Probe Pixel Pack Sizes")]
        public static void Probe()
        {
            var text = new StringBuilder();
            foreach (string path in Find("t:Model", MeshFolder).OrderBy(p => p))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var renderers = model.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                Bounds b = renderers[0].bounds;
                foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                int tris = model.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null)
                    .Sum(f => f.sharedMesh.triangles.Length / 3);
                foreach (Renderer r in renderers)
                {
                    foreach (Material m in r.sharedMaterials)
                    {
                        text.AppendLine($"    mat {(m == null ? "null" : m.name)} shader {(m == null ? "-" : m.shader.name)} clip {(m != null && m.HasProperty("_AlphaClip") ? m.GetFloat("_AlphaClip") : -1)} path {(m == null ? "-" : AssetDatabase.GetAssetPath(m))}");
                    }
                }
                text.AppendLine($"{Path.GetFileNameWithoutExtension(path)}\tsize {b.size.x:F2} {b.size.y:F2} {b.size.z:F2}\tcenter {b.center.x:F2} {b.center.y:F2} {b.center.z:F2}\tmin y {b.min.y:F2}\ttris {tris}\tmats {renderers.Sum(r => r.sharedMaterials.Length)}");
            }
            string output = Path.Combine(Path.GetDirectoryName(Application.dataPath), "pixel_pack_sizes.txt");
            File.WriteAllText(output, text.ToString());
            Debug.Log("[Chopper] Pixel pack sizes written to " + output);
        }

        private static IEnumerable<string> Find(string filter, string folder)
        {
            return AssetDatabase.FindAssets(filter, new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Distinct();
        }
    }
}
