using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Development helper: prints the meshes, bones and materials of the outfit, body and hair models, so they can be
    // matched to the animated skeleton. Batch: -executeMethod Chopper.Editor.OutfitInspector.Dump
    public static class OutfitInspector
    {
        [MenuItem("Chopper/Setup/Dump Outfit Model Info")]
        public static void Dump()
        {
            var text = new StringBuilder();
            string[] folders =
            {
                "Assets/ThirdParty/Quaternius_UniversalBaseCharacters/Bodies",
                "Assets/ThirdParty/Quaternius_UniversalBaseCharacters/Hair",
                "Assets/ThirdParty/Quaternius_ModularOutfits/Parts",
                "Assets/ThirdParty/Quaternius_ModularOutfits/Outfits"
            };
            foreach (string guid in AssetDatabase.FindAssets("t:Model", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                text.AppendLine($"{System.IO.Path.GetFileName(path)} type={importer.animationType} scale={importer.globalScale}");
                foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    text.AppendLine($"  smr {smr.name} verts={smr.sharedMesh.vertexCount} sub={smr.sharedMesh.subMeshCount} bones={smr.bones.Length} root={(smr.rootBone != null ? smr.rootBone.name : "none")} size={smr.bounds.size} mats={string.Join(",", smr.sharedMaterials.Select(m => m != null ? m.name : "null"))}");
                }
                foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>())
                {
                    text.AppendLine($"  mesh {mr.name} mats={string.Join(",", mr.sharedMaterials.Select(m => m != null ? m.name : "null"))}");
                }
            }

            var male = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Quaternius_UniversalBaseCharacters/Bodies/Superhero_Male_FullBody.fbx");
            if (male != null) text.AppendLine("male bones: " + string.Join(",", male.GetComponentsInChildren<Transform>().Select(t => t.name)));
            var ual = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Quaternius_UniversalAnimationLibrary/UAL1_Standard.fbx");
            if (ual != null) text.AppendLine("ual bones: " + string.Join(",", ual.GetComponentsInChildren<Transform>().Select(t => t.name)));
            string[] paths =
            {
                "Assets/ThirdParty/Quaternius_UniversalAnimationLibrary/UAL1_Standard.fbx",
                "Assets/ThirdParty/Quaternius_UniversalBaseCharacters/Bodies/Superhero_Male_FullBody.fbx",
                "Assets/ThirdParty/Quaternius_ModularOutfits/Outfits/Male_Peasant.fbx",
                "Assets/ThirdParty/Quaternius_ModularOutfits/Outfits/Male_Ranger.fbx"
            };
            foreach (string path in paths)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                text.Append("rest " + System.IO.Path.GetFileName(path) + ":");
                foreach (string bone in new[] { "pelvis", "head", "hand_r", "foot_l", "upperarm_l", "spine_03" })
                {
                    Transform t = go.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == bone);
                    if (t != null) text.Append($" {bone}=({t.position.x:F3},{t.position.y:F3},{t.position.z:F3})");
                }
                text.AppendLine();
            }
            Debug.Log("[OutfitInspector]\n" + text);
        }
    }
}
