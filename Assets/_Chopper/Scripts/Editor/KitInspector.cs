using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Development helper: prints the size and pivot of Medieval Village kit pieces so buildings can be assembled from them.
    // Batch: -executeMethod Chopper.Editor.KitInspector.Dump
    public static class KitInspector
    {
        private const string Folder = "Assets/ThirdParty/Quaternius_MedievalVillageMegaKit/FBX/";

        [MenuItem("Chopper/Setup/Dump Village Kit Sizes")]
        public static void Dump()
        {
            string[] names =
            {
                "Wall_Plaster_Straight", "Wall_Plaster_Door_Flat", "Wall_Plaster_Door_Round", "Wall_Plaster_Window_Wide_Flat", "Wall_Plaster_Straight_Base",
                "Wall_UnevenBrick_Straight", "Wall_UnevenBrick_Door_Flat", "Wall_Plaster_WoodGrid", "Corner_Exterior_Wood", "Corner_ExteriorWide_Wood",
                "Floor_WoodDark", "Floor_WoodLight", "Floor_UnevenBrick", "Roof_RoundTiles_6x6", "Roof_RoundTiles_6x8", "Roof_RoundTiles_8x8", "Roof_Wooden_2x1",
                "Roof_Front_Brick6", "Roof_Front_Brick8", "Prop_WoodenFence_Single", "Prop_WoodenFence_Extension1", "Prop_WoodenFence_Extension2",
                "Prop_Chimney", "Prop_Crate", "Prop_Wagon", "Stairs_Exterior_Straight", "Door_1_Flat", "Prop_Support", "Roof_Log", "Prop_ExteriorBorder_Straight1"
            };
            var text = new StringBuilder();
            foreach (string name in names)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".fbx");
                if (prefab == null) { text.AppendLine($"{name}: missing"); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
                Bounds bounds = default;
                bool first = true;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                {
                    if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
                }
                text.AppendLine($"{name}: world size=({bounds.size.x:F2},{bounds.size.y:F2},{bounds.size.z:F2}) min=({bounds.min.x:F2},{bounds.min.y:F2},{bounds.min.z:F2}) max=({bounds.max.x:F2},{bounds.max.y:F2},{bounds.max.z:F2}) rootRot={prefab.transform.eulerAngles} childRot={(prefab.transform.childCount > 0 ? prefab.transform.GetChild(0).localEulerAngles.ToString() : "none")}");
                Object.DestroyImmediate(go);
            }
            Debug.Log("[KitInspector]\n" + text);
        }
    }
}
