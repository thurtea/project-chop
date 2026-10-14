using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Chopper.Editor
{
    // Player builds. Batch examples:
    //   -executeMethod Chopper.Editor.BuildTools.BuildDevPlayer      -buildOutput <folder>   Windows development player (smoke tests)
    //   -executeMethod Chopper.Editor.BuildTools.BuildPlaytestPlayer -buildOutput <folder>   Windows release player
    //   -executeMethod Chopper.Editor.BuildTools.BuildPlaytestLinux  -buildOutput <folder>   Linux release player
    //   -executeMethod Chopper.Editor.BuildTools.BuildPlaytestMac    -buildOutput <folder>   macOS release app (Intel and Apple Silicon)
    //   -executeMethod Chopper.Editor.BuildTools.BuildDevMac         -buildOutput <folder>   macOS development app (smoke tests on a Mac)
    //   -executeMethod Chopper.Editor.BuildTools.BuildAllPlaytest    -buildOutput <folder>   every platform whose build support is installed
    //   -executeMethod Chopper.Editor.BuildTools.CiBuild                                      used by the GitHub Actions workflow
    // Release builds are borderless fullscreen by default and have no development overlay.
    public static class BuildTools
    {
        private const string OutputArg = "-buildOutput";
        private const string DefaultOutput = "Builds/Dev";
        private const string ReleaseOutput = "Builds/Release";
        private const string ProductName = "Chopper";
        // Valve's public test app (Spacewar) until Chopper has its own Steam app id.
        private const string SteamAppId = "480";
        private const string Version = "0.2.9";
        private static readonly string[] Scenes = { "Assets/_Chopper/Scenes/Boot.unity", "Assets/_Chopper/Scenes/World.unity" };

        private struct Platform
        {
            public string Folder;
            public BuildTarget Target;
            public NamedBuildTarget Named;
            public string File;
        }

        private static readonly Platform Windows = new Platform { Folder = "windows", Target = BuildTarget.StandaloneWindows64, Named = NamedBuildTarget.Standalone, File = "Chopper.exe" };
        private static readonly Platform Linux = new Platform { Folder = "linux", Target = BuildTarget.StandaloneLinux64, Named = NamedBuildTarget.Standalone, File = "Chopper.x86_64" };
        private static readonly Platform Mac = new Platform { Folder = "macos", Target = BuildTarget.StandaloneOSX, Named = NamedBuildTarget.Standalone, File = "Chopper.app" };

        [MenuItem("Chopper/Build/Windows Playtest Player")]
        public static void BuildPlaytestPlayer() => BatchBuild(Windows, false, OutputFolder(DefaultOutput));

        [MenuItem("Chopper/Build/Linux Playtest Player")]
        public static void BuildPlaytestLinux() => BatchBuild(Linux, false, OutputFolder(DefaultOutput));

        [MenuItem("Chopper/Build/macOS Playtest App")]
        public static void BuildPlaytestMac() => BatchBuild(Mac, false, OutputFolder(DefaultOutput));

        [MenuItem("Chopper/Build/Windows Development Player")]
        public static void BuildDevPlayer() => BatchBuild(Windows, true, OutputFolder(DefaultOutput));

        [MenuItem("Chopper/Build/macOS Development App")]
        public static void BuildDevMac() => BatchBuild(Mac, true, OutputFolder(DefaultOutput));

        [MenuItem("Chopper/Build/Linux Development Player")]
        public static void BuildDevLinux() => BatchBuild(Linux, true, OutputFolder(DefaultOutput));

        // Builds every platform whose Unity build support module is installed into <folder>/<platform>/ and reports
        // which ones were skipped. Zip the folders with Tools/package_release.py for GitHub releases.
        [MenuItem("Chopper/Build/All Platforms (Release)")]
        public static void BuildAllPlaytest()
        {
            string root = OutputFolder(ReleaseOutput);
            bool ok = true;
            foreach (Platform platform in new[] { Windows, Linux, Mac })
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, platform.Target))
                {
                    Debug.LogWarning($"[Chopper] Skipped {platform.Folder}: its Unity build support module is not installed.");
                    continue;
                }
                ok &= Build(platform, false, Path.Combine(root, platform.Folder));
            }
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        // Entry point for the GitHub Actions workflow (GameCI passes -buildTarget and -customBuildPath).
        public static void CiBuild()
        {
            string[] args = Environment.GetCommandLineArgs();
            string target = ArgValue(args, "-buildTarget");
            string path = ArgValue(args, "-customBuildPath");
            Platform platform = target == "StandaloneLinux64" ? Linux : (target == "StandaloneOSX" ? Mac : Windows);
            string folder = string.IsNullOrEmpty(path) ? Path.Combine(ReleaseOutput, platform.Folder) : path;
            // GameCI gives a file path; the player file goes inside its folder.
            if (Path.HasExtension(folder) && !folder.EndsWith(".app")) folder = Path.GetDirectoryName(folder);
            bool ok = Build(platform, false, folder);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static void BatchBuild(Platform platform, bool development, string folder)
        {
            bool ok = Build(platform, development, folder);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Build(Platform platform, bool development, string folder)
        {
            Directory.CreateDirectory(folder);
            LightingSetup.EnsureForestShafts();

            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Chopper/Art/Branding/icon.png");
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Application);
            if (platform.Target == BuildTarget.StandaloneOSX) UseUniversalMacArchitecture();

            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = Path.Combine(folder, platform.File),
                target = platform.Target,
                options = development ? BuildOptions.Development : BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Chopper] {platform.Folder} build {report.summary.result} in {report.summary.totalTime}, {report.summary.totalSize / (1024 * 1024)} MB.");
            // Steam needs the app id next to the game when it is not started from the Steam library.
            if (report.summary.result == BuildResult.Succeeded) File.WriteAllText(Path.Combine(folder, "steam_appid.txt"), SteamAppId);
            return report.summary.result == BuildResult.Succeeded;
        }

        // One macOS app for both Intel and Apple Silicon Macs.
        private static void UseUniversalMacArchitecture()
        {
            try
            {
                EditorUserBuildSettings.SetPlatformSettings("Standalone", "OSXUniversal", "Architecture", "x64arm64");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Chopper] Could not select the universal macOS architecture: " + e.Message);
            }
        }

        private static string OutputFolder(string fallback)
        {
            string value = ArgValue(Environment.GetCommandLineArgs(), OutputArg);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static string ArgValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return null;
        }
    }
}
