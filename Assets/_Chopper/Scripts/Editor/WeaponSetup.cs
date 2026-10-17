using Chopper.Core;
using Chopper.Player;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    public static class WeaponSetup
    {
        const string PlayerPrefab = "Assets/_Chopper/Prefabs/Player/Player.prefab";

        public static void AddToPlayer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            PlayerWeapons weapons = root.GetComponent<PlayerWeapons>();
            if (weapons == null) weapons = root.AddComponent<PlayerWeapons>();
            var inv = new SerializedObject(root.GetComponent<PlayerInventory>());
            Object config = inv.FindProperty("_config").objectReferenceValue;
            var so = new SerializedObject(weapons);
            so.FindProperty("_config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            bool ok = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab).GetComponent<PlayerWeapons>() != null;
            int models = 0;
            foreach (string n in new[] { "sword", "mace", "spear" })
                if (Resources.Load<GameObject>("Weapons/chopper_" + n) != null) models++;
            Debug.Log($"RESULT: PlayerWeapons on Player.prefab {ok}, config {(config != null ? config.name : "none")}, weapon models {models}/3");
        }
    }
}
