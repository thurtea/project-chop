using Chopper.Core;
using Chopper.Player;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    public static class BoatSetup
    {
        const string PlayerPrefab = "Assets/_Chopper/Prefabs/Player/Player.prefab";

        public static void AddToPlayer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            PlayerBoat boat = root.GetComponent<PlayerBoat>();
            if (boat == null) boat = root.AddComponent<PlayerBoat>();
            var inv = new SerializedObject(root.GetComponent<PlayerInventory>());
            Object config = inv.FindProperty("_config").objectReferenceValue;
            var so = new SerializedObject(boat);
            so.FindProperty("_config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            bool ok = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab).GetComponent<PlayerBoat>() != null;
            Debug.Log($"RESULT: PlayerBoat on Player.prefab {ok}, config {(config != null ? config.name : "none")}");
        }
    }
}