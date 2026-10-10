using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Chopper.Editor
{
    // Makes testing obvious: pressing Play always starts the Boot scene, whichever scene is open, and by default
    // the start screen appears. Turn Auto Host on to skip it and host straight away.
    // Menu: Chopper > Play.
    [InitializeOnLoad]
    public static class PlayFlow
    {
        public const string AutoHostKey = "Chopper.AutoHost";
        private const string BootPath = "Assets/_Chopper/Scenes/Boot.unity";
        private const string AutoHostMenu = "Chopper/Play/Auto Host On Play";
        private const string StartMenu = "Chopper/Play/Start Game";

        static PlayFlow()
        {
            EditorApplication.delayCall += ApplyStartScene;
        }

        private static void ApplyStartScene()
        {
            var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootPath);
            if (boot != null) EditorSceneManager.playModeStartScene = boot;
        }

        [MenuItem(StartMenu)]
        public static void StartGame()
        {
            ApplyStartScene();
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(AutoHostMenu)]
        private static void ToggleAutoHost()
        {
            EditorPrefs.SetBool(AutoHostKey, !EditorPrefs.GetBool(AutoHostKey, false));
        }

        [MenuItem(AutoHostMenu, true)]
        private static bool ToggleAutoHostValidate()
        {
            Menu.SetChecked(AutoHostMenu, EditorPrefs.GetBool(AutoHostKey, false));
            return true;
        }
    }
}
