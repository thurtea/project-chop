using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chopper.Editor
{
    // T08: prepares the CC0 packs in Assets/ThirdParty for URP. Safe to run repeatedly.
    // - Stylized Nature MegaKit: leaf and flower materials (textures with alpha) are replaced by project
    //   materials in Assets/_Chopper/Materials/Nature: URP Lit, alpha clip, two sided, low smoothness.
    // - Medieval Weapons: import scale so the axe is about one meter long.
    // Menu: Chopper > Setup > Prepare Third Party Assets. Batch: -executeMethod Chopper.Editor.AssetImportTools.Run
    public static class AssetImportTools
    {
        private const string NatureRoot = "Assets/ThirdParty/Quaternius_StylizedNatureMegaKit";
        private const string WeaponsRoot = "Assets/ThirdParty/Quaternius_MedievalWeapons";
        private const string NatureMaterialsFolder = "Assets/_Chopper/Materials/Nature";

        // The weapons pack is authored about 5.3 m long; 0.17 gives a 0.9 m axe next to the 1.9 m mannequin.
        private const float WeaponsImportScale = 0.17f;
        private const float AlphaCutoff = 0.5f;
        private const float StylizedSmoothness = 0.1f;
        private const string NormalMapSuffix = "_Normal";
        // Blender exports OpenGL style normal maps, which is what Unity expects.
        private const bool FlipNormalGreen = false;
        private const int AlphaTestQueue = (int)RenderQueue.AlphaTest;

        [MenuItem("Chopper/Setup/Prepare Third Party Assets")]
        public static void Run()
        {
            PrepareNatureTextures();
            PrepareNatureMaterials();
            PrepareWeapons();
            AssetDatabase.SaveAssets();
            Debug.Log("[Chopper] Third party assets prepared.");
        }

        private static void PrepareNatureTextures()
        {
            foreach (string path in FindAssets("t:Texture2D", NatureRoot + "/Textures"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool isNormal = Path.GetFileNameWithoutExtension(path).EndsWith(NormalMapSuffix);
                bool changed = false;
                if (isNormal && (importer.textureType != TextureImporterType.NormalMap || importer.flipGreenChannel != FlipNormalGreen))
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.flipGreenChannel = FlipNormalGreen;
                    changed = true;
                }
                if (!isNormal && importer.DoesSourceTextureHaveAlpha() && !importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    changed = true;
                }
                if (changed) importer.SaveAndReimport();
            }
        }

        // Only materials whose texture has alpha (leaves, flowers) are replaced; the importer's own URP
        // materials for bark, rocks, and grass already render correctly.
        private static void PrepareNatureMaterials()
        {
            EnsureFolder(NatureMaterialsFolder);
            var projectMaterials = new Dictionary<string, Material>();
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");

            foreach (string modelPath in FindAssets("t:Model", NatureRoot + "/FBX"))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);

                bool removed = false;
                foreach (var pair in importer.GetExternalObjectMap())
                {
                    if (pair.Key.type != typeof(Material) || IsCutout(pair.Value as Material)) continue;
                    importer.RemoveRemap(pair.Key);
                    removed = true;
                }
                if (removed) importer.SaveAndReimport();

                bool remapped = false;
                foreach (Material source in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>())
                {
                    if (!(source.mainTexture is Texture2D baseMap) || !HasAlpha(baseMap)) continue;
                    if (!projectMaterials.TryGetValue(source.name, out Material target))
                    {
                        target = GetOrCreateCutoutMaterial(source.name, baseMap, lit);
                        projectMaterials[source.name] = target;
                    }
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), target);
                    remapped = true;
                }
                if (remapped) importer.SaveAndReimport();
            }

            // Remove project materials that are no longer used (non-cutout ones from earlier runs).
            foreach (string path in FindAssets("t:Material", NatureMaterialsFolder))
            {
                if (!IsCutout(AssetDatabase.LoadAssetAtPath<Material>(path))) AssetDatabase.DeleteAsset(path);
            }
        }

        private static Material GetOrCreateCutoutMaterial(string sourceName, Texture2D baseMap, Shader lit)
        {
            string path = $"{NatureMaterialsFolder}/M_{sourceName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(lit);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", baseMap);
            // The kit is cheerful and saturated; a grey green tint settles it into the muted look of the world.
            material.SetColor("_BaseColor", new Color(0.80f, 0.82f, 0.70f));
            material.SetFloat("_Smoothness", StylizedSmoothness);
            material.SetTexture("_BumpMap", null);
            material.DisableKeyword("_NORMALMAP");
            material.enableInstancing = true;

            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", AlphaCutoff);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = AlphaTestQueue;
            material.doubleSidedGI = true;

            EditorUtility.SetDirty(material);
            return material;
        }

        private static bool IsCutout(Material material)
        {
            return material != null && material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f;
        }

        private static void PrepareWeapons()
        {
            foreach (string modelPath in FindAssets("t:Model", WeaponsRoot + "/FBX"))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
                if (Mathf.Approximately(importer.globalScale, WeaponsImportScale)) continue;
                importer.globalScale = WeaponsImportScale;
                importer.SaveAndReimport();
            }
        }

        private static bool HasAlpha(Texture2D texture)
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            return importer != null && importer.DoesSourceTextureHaveAlpha();
        }

        private static IEnumerable<string> FindAssets(string filter, string folder)
        {
            return AssetDatabase.FindAssets(filter, new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Distinct();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
