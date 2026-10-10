using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Development helper: prints the clips, bones and meshes inside an imported model to the log.
    // Batch: -executeMethod Chopper.Editor.ModelInspector.DumpPlayerModel
    public static class ModelInspector
    {
        private const string PlayerModelPath = "Assets/ThirdParty/Quaternius_UniversalAnimationLibrary/UAL1_Standard.fbx";
        private const string WeaponsFolder = "Assets/ThirdParty/Quaternius_MedievalWeapons/FBX";

        // Every bone name with its depth, to find the arm, spine and leg bones for procedural poses.
        public static void DumpBones()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            var text = new StringBuilder();
            void Walk(Transform t, int depth)
            {
                text.AppendLine($"[Bones] {new string(' ', depth * 2)}{t.name}  local {t.localPosition.x:F3},{t.localPosition.y:F3},{t.localPosition.z:F3}");
                foreach (Transform child in t) Walk(child, depth + 1);
            }
            Walk(root.transform, 0);
            Debug.Log(text.ToString());
        }

        [MenuItem("Chopper/Setup/Dump Player Model Info")]
        public static void DumpPlayerModel()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(PlayerModelPath);
            var text = new StringBuilder();
            text.AppendLine($"[ModelInspector] {PlayerModelPath} animationType={importer.animationType} clips={importer.clipAnimations.Length} default={importer.defaultClipAnimations.Length}");

            Object[] all = AssetDatabase.LoadAllAssetsAtPath(PlayerModelPath);
            var rootGo = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            Animator rootAnimator = rootGo.GetComponent<Animator>();
            string rootAvatar = rootAnimator == null ? "no root animator" : (rootAnimator.avatar == null ? "root animator has NO avatar" : rootAnimator.avatar.name);
            text.AppendLine($"  avatarSetup={importer.avatarSetup} avatarAssets={all.OfType<Avatar>().Count()} {rootAvatar}");
            foreach (AnimationClip clip in all.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")))
            {
                text.AppendLine($"  clip {clip.name} length={clip.length:F2} loop={clip.isLooping}");
            }

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                text.AppendLine($"  mesh {smr.name} bounds={smr.bounds.size} bones={smr.bones.Length} materials={string.Join(",", smr.sharedMaterials.Select(m => m != null ? m.name : "null"))}");
            }
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
            {
                string lower = t.name.ToLowerInvariant();
                if (lower.Contains("hand") || lower.Contains("head") || lower == "root" || lower.Contains("pelvis")) text.AppendLine($"  bone {t.name}");
            }
            text.AppendLine("  all transforms: " + string.Join(", ", root.GetComponentsInChildren<Transform>().Select(t => $"{t.name}@({t.position.x:F2},{t.position.y:F2},{t.position.z:F2})")));
            Debug.Log(text.ToString());

            var axe = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponsFolder + "/Axe.fbx");
            if (axe != null)
            {
                foreach (MeshFilter mf in axe.GetComponentsInChildren<MeshFilter>())
                {
                    Debug.Log($"[ModelInspector] axe mesh {mf.name} bounds center={mf.sharedMesh.bounds.center} size={mf.sharedMesh.bounds.size} scale={mf.transform.lossyScale}");
                }
            }

            var weapons = new StringBuilder("[ModelInspector] weapons:");
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { WeaponsFolder }))
            {
                weapons.Append(' ').Append(System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
            }
            Debug.Log(weapons.ToString());
        }
    }
}
