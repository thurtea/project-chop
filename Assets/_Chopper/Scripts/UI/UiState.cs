using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Chopper.UI
{
    // Which full screen menus are open. While any is open the cursor is free and gameplay input (movement, chopping,
    // hotbar) is ignored, but the world keeps running so other players are not paused.
    public static class UiState
    {
        // How long gameplay keys stay ignored after a menu closes, so the button that closed it (B, A, Esc or
        // interact) does not also roll, jump or interact in the world.
        private const float CloseGraceSeconds = 0.25f;
        private static float _closedAt = float.NegativeInfinity;
        private static bool _pause, _inventory, _map, _howTo, _dev, _console, _workbench, _chest, _build;

        public static bool PauseOpen { get => _pause; set => Set(ref _pause, value); }
        public static bool InventoryOpen { get => _inventory; set => Set(ref _inventory, value); }
        public static bool MapOpen { get => _map; set => Set(ref _map, value); }
        public static bool HowToPlayOpen { get => _howTo; set => Set(ref _howTo, value); }
        public static bool DevOpen { get => _dev; set => Set(ref _dev, value); }
        public static bool ConsoleOpen { get => _console; set => Set(ref _console, value); }
        public static bool WorkbenchOpen { get => _workbench; set => Set(ref _workbench, value); }
        public static bool ChestOpen { get => _chest; set => Set(ref _chest, value); }
        // The building piece picker (hammer out, Tab or the inventory button).
        public static bool BuildMenuOpen { get => _build; set => Set(ref _build, value); }

        public static bool InputBlocked => PauseOpen || InventoryOpen || MapOpen || HowToPlayOpen || DevOpen || ConsoleOpen
                                           || WorkbenchOpen || ChestOpen || BuildMenuOpen;

        // For gameplay actions (move, jump, roll, attack, interact, hotbar): blocked while a menu is open and for a
        // moment after it closes.
        public static bool GameplayBlocked => InputBlocked || Time.unscaledTime - _closedAt < CloseGraceSeconds;

        private static void Set(ref bool field, bool value)
        {
            if (field && !value) _closedAt = Time.unscaledTime;
            field = value;
        }

        // Raised when something in the world (the guide signpost, taking the axe) wants the How to play screen shown.
        public static event System.Action HowToPlayRequested;

        public static void RequestHowToPlay() => HowToPlayRequested?.Invoke();

        // Raised when the player reads a sign: its title and text are shown in the reading panel.
        public static event System.Action<string, string> SignRequested;

        public static void RequestReadSign(string title, string body) => SignRequested?.Invoke(title, body);

        // Raised when the player uses a workbench or the storage chest: the matching menu opens.
        public static event System.Action WorkbenchRequested;
        public static event System.Action ChestRequested;

        public static void RequestWorkbench() => WorkbenchRequested?.Invoke();

        public static void RequestChest() => ChestRequested?.Invoke();

        public static void CloseAll()
        {
            PauseOpen = false;
            InventoryOpen = false;
            MapOpen = false;
            HowToPlayOpen = false;
            DevOpen = false;
            ConsoleOpen = false;
            WorkbenchOpen = false;
            ChestOpen = false;
            BuildMenuOpen = false;
        }
    }

    // Player preferences saved between sessions.
    public static class UserSettings
    {
        private const string VolumeKey = "Chopper.Volume";
        private const string EffectsVolumeKey = "Chopper.EffectsVolume";
        private const string MusicVolumeKey = "Chopper.MusicVolume";
        private const string SensitivityKey = "Chopper.Sensitivity";
        private const string PadSensitivityKey = "Chopper.PadSensitivity";
        private const string InvertPadKey = "Chopper.InvertPadY";
        private const string FovKey = "Chopper.Fov";
        private const string FullscreenKey = "Chopper.Fullscreen";
        private const string PixelKey = "Chopper.PixelLook";
        private const string AmbientKey = "Chopper.AmbientParticles";
        private const string RenderScaleKey = "Chopper.RenderScale";
        private const string AntiAliasingKey = "Chopper.AntiAliasing";
        private const string ShadowQualityKey = "Chopper.ShadowQuality";
        private const string VSyncKey = "Chopper.VSync";
        private const string MenuOpacityKey = "Chopper.MenuOpacity";
        private const float PixelRenderScale = 0.42f;
        private const float WindowedScreenFraction = 0.8f;

        // Tuned so a new player does not need to turn the game down: master 70 percent, effects 100, ambience 80.
        public const float DefaultVolume = 0.7f;
        public const float DefaultEffectsVolume = 1f;
        // Music sits under the world: a quarter of full by default (it was a half and covered the effects).
        public const float DefaultMusicVolume = 0.25f;
        private const float OldDefaultMusicVolume = 0.5f;
        private const string MusicMigratedKey = "Chopper.MusicDefault25";
        private static bool _musicMigrated;
        public const float DefaultSensitivity = 1f;
        public const float DefaultFov = 80f;
        // The old default. A saved field of view still at it was never chosen, so it moves to the new default once.
        private const float OldDefaultFov = 87f;
        private const string FovMigratedKey = "Chopper.FovDefault80";
        public const float MinFov = 50f;
        public const float MaxFov = 100f;
        public const float MinRenderScale = 0.5f;
        public const float MaxRenderScale = 1.5f;
        public const int DefaultAntiAliasing = 5;
        public const int DefaultShadowQuality = 2;
        public const float DefaultMenuOpacity = 1f;
        public const float MinMenuOpacity = 0.3f;
        public const float MinSensitivity = 0.25f;
        public const float MaxSensitivity = 3f;

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
            set { PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); AudioListener.volume = Mathf.Clamp01(value); }
        }

        // One shot sounds: axe, footsteps, hits, hurt, growls, stingers. Multiplies the master volume.
        public static float EffectsVolume
        {
            get => PlayerPrefs.GetFloat(EffectsVolumeKey, DefaultEffectsVolume);
            set => PlayerPrefs.SetFloat(EffectsVolumeKey, Mathf.Clamp01(value));
        }

        // Music, when music files are present. Multiplies the master volume.
        public static float MusicVolume
        {
            get
            {
                // A saved volume still at the old default (one half) was never chosen: it moves to the new default once.
                if (!_musicMigrated)
                {
                    _musicMigrated = true;
                    if (!PlayerPrefs.HasKey(MusicMigratedKey))
                    {
                        if (PlayerPrefs.HasKey(MusicVolumeKey) && Mathf.Abs(PlayerPrefs.GetFloat(MusicVolumeKey) - OldDefaultMusicVolume) < 0.001f)
                            PlayerPrefs.DeleteKey(MusicVolumeKey);
                        PlayerPrefs.SetInt(MusicMigratedKey, 1);
                    }
                }
                return PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume);
            }
            set => PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
        }

        // The wind, crickets, surf and other ambience beds. Multiplies the master volume.
        // Multiplies the mouse look speed from GameConfig.
        public static float Sensitivity
        {
            get => PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);
            set => PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, MinSensitivity, MaxSensitivity));
        }

        // Multiplies the right stick look speed from GameConfig. Separate from the mouse because the two feel different.
        public static float PadSensitivity
        {
            get => PlayerPrefs.GetFloat(PadSensitivityKey, DefaultSensitivity);
            set => PlayerPrefs.SetFloat(PadSensitivityKey, Mathf.Clamp(value, MinSensitivity, MaxSensitivity));
        }

        // Pushing the right stick up looks down, as some players prefer.
        public static bool InvertPadY
        {
            get => PlayerPrefs.GetInt(InvertPadKey, 0) == 1;
            set => PlayerPrefs.SetInt(InvertPadKey, value ? 1 : 0);
        }

        public static float FieldOfView
        {
            get
            {
                if (!_fovMigrated)
                {
                    _fovMigrated = true;
                    if (!PlayerPrefs.HasKey(FovMigratedKey))
                    {
                        if (PlayerPrefs.HasKey(FovKey) && Mathf.Abs(PlayerPrefs.GetFloat(FovKey) - OldDefaultFov) < 0.01f) PlayerPrefs.DeleteKey(FovKey);
                        PlayerPrefs.SetInt(FovMigratedKey, 1);
                    }
                }
                return PlayerPrefs.GetFloat(FovKey, DefaultFov);
            }
            set => PlayerPrefs.SetFloat(FovKey, Mathf.Clamp(value, MinFov, MaxFov));
        }

        private static bool _fovMigrated;

        // Borderless fullscreen at the desktop resolution (the default), or a window 80% of the desktop size.
        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
                ApplyDisplay();
            }
        }

        // Drifting zone particles (embers, leaves, rain, mist). Off by default: they are a matter of taste and can distract.
        // How solid menu windows are. Fully opaque by default so the text is easy to read over the world.
        public static event System.Action MenuOpacityChanged;
        public static float MenuOpacity
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(MenuOpacityKey, DefaultMenuOpacity), MinMenuOpacity, 1f);
            set
            {
                PlayerPrefs.SetFloat(MenuOpacityKey, Mathf.Clamp(value, MinMenuOpacity, 1f));
                MenuOpacityChanged?.Invoke();
            }
        }

        public static bool AmbientParticles
        {
            get => PlayerPrefs.GetInt(AmbientKey, 0) == 1;
            set => PlayerPrefs.SetInt(AmbientKey, value ? 1 : 0);
        }

        // ---------- Graphics ----------

        public static readonly string[] AntiAliasingNames = { "Off", "SMAA", "MSAA 2x", "MSAA 4x", "MSAA 8x", "MSAA 4x + SMAA" };
        public static readonly string[] ShadowQualityNames = { "Low", "Medium", "High", "Ultra" };
        private static readonly int[] MsaaSamples = { 1, 1, 2, 4, 8, 4 };
        private static readonly bool[] UsesSmaa = { false, true, false, false, false, true };
        private static readonly int[] ShadowResolutions = { 1024, 2048, 4096, 8192 };
        private static readonly float[] ShadowDistances = { 70f, 100f, 140f, 180f };

        // 1 is the screen's own resolution. Above 1 the world is drawn larger than the screen and scaled down
        // (supersampling), which is the crispest option and the most expensive.
        public static float RenderScale
        {
            get => PlayerPrefs.GetFloat(RenderScaleKey, 1f);
            set { PlayerPrefs.SetFloat(RenderScaleKey, Mathf.Clamp(value, MinRenderScale, MaxRenderScale)); ApplyGraphics(); }
        }

        // An index into AntiAliasingNames. The default pairs 4x MSAA (clean geometry edges) with SMAA (clean alpha
        // cutout leaves and shader edges).
        public static int AntiAliasing
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(AntiAliasingKey, DefaultAntiAliasing), 0, AntiAliasingNames.Length - 1);
            set { PlayerPrefs.SetInt(AntiAliasingKey, Mathf.Clamp(value, 0, AntiAliasingNames.Length - 1)); ApplyGraphics(); }
        }

        // An index into ShadowQualityNames: shadow map size and how far shadows reach.
        public static int ShadowQuality
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(ShadowQualityKey, DefaultShadowQuality), 0, ShadowQualityNames.Length - 1);
            set { PlayerPrefs.SetInt(ShadowQualityKey, Mathf.Clamp(value, 0, ShadowQualityNames.Length - 1)); ApplyGraphics(); }
        }

        // A cap on frames per second, kept even with vertical sync on (a 144 Hz screen recorded at 60 fps judders
        // otherwise, and an uncapped game leaves the recorder no room). Index into FrameCapNames; 0 is no cap.
        public static readonly string[] FrameCapNames = { "None", "30", "60", "120", "144" };
        private static readonly int[] FrameCaps = { -1, 30, 60, 120, 144 };
        private const string FrameCapKey = "Chopper.FrameCap";

        public static int FrameCap
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(FrameCapKey, 0), 0, FrameCapNames.Length - 1);
            set { PlayerPrefs.SetInt(FrameCapKey, Mathf.Clamp(value, 0, FrameCapNames.Length - 1)); ApplyGraphics(); }
        }

        public static bool VSync
        {
            get => PlayerPrefs.GetInt(VSyncKey, 1) == 1;
            set { PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0); ApplyGraphics(); }
        }

        // The retro option: render at a low resolution and scale it up with hard pixel edges. Off by default, the
        // pixel feel of the world comes from its point filtered textures at full resolution.
        public static bool PixelLook
        {
            get => PlayerPrefs.GetInt(PixelKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(PixelKey, value ? 1 : 0);
                ApplyGraphics();
            }
        }

        // Pushes every graphics preference into the pipeline asset (players only: in the editor the asset on disk
        // would be changed) and the cameras.
        public static void ApplyGraphics()
        {
            // Vertical sync wins over a target frame rate in Unity, so a cap switches it off and paces frames itself.
            int cap = FrameCaps[FrameCap];
            QualitySettings.vSyncCount = VSync && cap < 0 ? 1 : 0;
            Application.targetFrameRate = cap;
#if !UNITY_EDITOR
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline != null)
            {
                pipeline.renderScale = PixelLook ? PixelRenderScale : RenderScale;
                pipeline.upscalingFilter = PixelLook ? UpscalingFilterSelection.Point : UpscalingFilterSelection.Auto;
                pipeline.msaaSampleCount = MsaaSamples[AntiAliasing];
                pipeline.mainLightShadowmapResolution = ShadowResolutions[ShadowQuality];
                pipeline.shadowDistance = ShadowDistances[ShadowQuality];
            }
#endif
            foreach (Camera camera in Camera.allCameras) ApplyToCamera(camera);
        }

        public static void ApplyToCamera(Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.Game) return;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            if (data != null) { data.antialiasing = UsesSmaa[AntiAliasing] ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None; data.antialiasingQuality = AntialiasingQuality.High; }
        }

        public static void Apply()
        {
            ApplyGraphics();
            AudioListener.volume = Volume;
            // Only override the display once the player has chosen, so command line sizes (tests, screenshots) still work.
            if (PlayerPrefs.HasKey(FullscreenKey)) ApplyDisplay();
        }

        private static void ApplyDisplay()
        {
#if !UNITY_EDITOR
            DisplayInfo display = Screen.mainWindowDisplayInfo;
            if (Fullscreen)
            {
                Screen.SetResolution(display.width, display.height, FullScreenMode.FullScreenWindow);
            }
            else
            {
                Screen.SetResolution(Mathf.RoundToInt(display.width * WindowedScreenFraction), Mathf.RoundToInt(display.height * WindowedScreenFraction), FullScreenMode.Windowed);
            }
#endif
        }
    }
}
