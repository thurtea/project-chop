#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chopper.Enemies;
using Chopper.Player;
using Chopper.Trees;
using Chopper.UI;
using Chopper.World;
using Chopper.World.Building;
using Chopper.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Chopper.Networking
{
    // Development only. With -chopperShot <folder> (and -chopperHost) the game takes screenshots of the HUD and menus
    // in a few states, then quits. Needs a window (do not use -batchmode or -nographics).
    public static class ShotCapture
    {
        private const string ShotArg = "-chopperShot";
        private const float WaitTimeout = 40f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-chopperPadTest") >= 0)
            {
                var padHost = new GameObject("PadTest");
                UnityEngine.Object.DontDestroyOnLoad(padHost);
                padHost.AddComponent<Runner>().StartCoroutine(PadNavTest());
                return;
            }

            int titleIndex = Array.IndexOf(args, "-chopperTitle");
            if (titleIndex >= 0 && titleIndex + 1 < args.Length)
            {
                var titleHost = new GameObject("ShotCapture");
                UnityEngine.Object.DontDestroyOnLoad(titleHost);
                bool loadTour = Array.IndexOf(args, "-chopperLoadTour") >= 0;
                titleHost.AddComponent<Runner>().StartCoroutine(loadTour ? LoadTour(args[titleIndex + 1]) : TitleShot(args[titleIndex + 1]));
                return;
            }

            int index = Array.IndexOf(args, ShotArg);
            if (index < 0 || index + 1 >= args.Length) return;

            var host = new GameObject("ShotCapture");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>().StartCoroutine(Run(args[index + 1]));
        }

        private class Runner : MonoBehaviour { }

        // Logs frame times over a stretch: average, the slowest 5 percent, the worst frame, and garbage collections.
        private static IEnumerator MeasureFrames(string label, float seconds)
        {
            var frames = new System.Collections.Generic.List<float>();
            using var allocated = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame");
            using var batches = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Batches Count");
            using var setPass = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "SetPass Calls Count");
            using var triangles = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Triangles Count");
            long allocatedSum = 0;
            int gcBefore = GC.CollectionCount(0);
            long memoryBefore = GC.GetTotalMemory(false);
            // Main thread and GPU time per frame (Frame Timing Stats), to tell which side holds the frame back.
            var timings = new FrameTiming[1];
            double cpuSum = 0, gpuSum = 0;
            int timed = 0;
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000f);
                allocatedSum += allocated.LastValue;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
                {
                    cpuSum += timings[0].cpuMainThreadFrameTime;
                    gpuSum += timings[0].gpuFrameTime;
                    timed++;
                }
            }
            frames.Sort();
            float sum = 0f;
            foreach (float f in frames) sum += f;
            float p95 = frames[Mathf.Clamp(Mathf.FloorToInt(frames.Count * 0.95f), 0, frames.Count - 1)];
            int hitches = 0;
            foreach (float f in frames) if (f > 25f) hitches++;
            Debug.Log($"[Perf] {label}: {frames.Count} frames, avg {sum / frames.Count:F1} ms ({frames.Count / seconds:F0} fps), p95 {p95:F1} ms, " +
                      $"worst {frames[frames.Count - 1]:F1} ms, gc {GC.CollectionCount(0) - gcBefore}, allocated {allocatedSum / Mathf.Max(1, frames.Count) / 1024f:F1} KB per frame" +
                      (timed > 0 ? $", cpu main {cpuSum / timed:F1} ms, gpu {gpuSum / timed:F1} ms" : string.Empty) +
                      $", batches {batches.LastValue}, setpass {setPass.LastValue}, tris {triangles.LastValue / 1000}k, over 25 ms {hitches}");
        }

        // The start screen, its settings, then the New Game world step and the Join step, then quit.
        private static void Click(string name)
        {
            foreach (Button button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude))
            {
                if (button.name == name && button.gameObject.activeInHierarchy)
                {
                    button.onClick.Invoke();
                    return;
                }
            }
            Debug.Log("[Shot] no button " + name);
        }

        // Saved worlds on the start screen (-chopperTitle <folder> -chopperLoadTour): a temporary save makes Continue and
        // Load Game appear, the load list and the loading screen are captured, then the world it loads. The save is
        // deleted again (the dev player shares its save folder with the real game).
        private static IEnumerator LoadTour(string folder)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(Chopper.World.Saving.SaveFiles.Folder, "Tour_world_delete_me" + Chopper.World.Saving.SaveFiles.Extension);
            var save = new Chopper.World.Saving.WorldSave { Name = "Tour world", Seed = 1337, Day = 4, SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
            save.Hut = new Chopper.World.Saving.HutRecord { Repaired = true, Level = 1, Health = 400, WoodStore = 55 };
            Chopper.World.Saving.SaveFiles.Write(save, path);
            yield return new WaitForSeconds(3f);
            // Going to Settings and back rebuilds the main menu, which now finds the save.
            Click("Button_Settings");
            yield return new WaitForSeconds(0.3f);
            Click("Button_Back");
            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "load_1_menu");
            Click("Button_Load Game");
            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "load_2_list");
            HoldButton deleteHold = UnityEngine.Object.FindAnyObjectByType<HoldButton>();
            if (deleteHold != null)
            {
                deleteHold.DebugShowProgress(0.6f);
                yield return new WaitForSeconds(0.2f);
                yield return Shot(folder, "load_2b_deleting");
                Click("Button_Back");
                yield return new WaitForSeconds(0.3f);
                Click("Button_Load Game");
                yield return new WaitForSeconds(0.5f);
            }
            Click("Button_Load");
            yield return new WaitForSeconds(0.15f);
            yield return Shot(folder, "load_3_loading");
            float deadline = Time.unscaledTime + 40f;
            while (Time.unscaledTime < deadline && PlayerWood.Local == null) yield return null;
            yield return new WaitForSeconds(3f);
            yield return Shot(folder, "load_4_in_world");
            Chopper.World.Saving.SaveSystem.Clear();
            Chopper.World.Saving.SaveFiles.Delete(path);
            Debug.Log("[Shot] done");
            Application.Quit(0);
        }

        private static IEnumerator TitleShot(string folder)
        {
            Directory.CreateDirectory(folder);
            yield return new WaitForSeconds(3f);
            yield return Shot(folder, "title");
            Click("Button_Settings");
            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "title_settings");
            Click("Tab_Keyboard and mouse");
            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "title_keyboard");
            Click("Tab_Gamepad");
            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "title_gamepad");
            Click("Button_Back");
            yield return new WaitForSeconds(0.5f);
            Click("Button_New Game");
            yield return new WaitForSeconds(0.6f);
            yield return Shot(folder, "title_world");
            Click("Button_Back");
            yield return new WaitForSeconds(0.3f);
            Click("Button_Join Game");
            yield return new WaitForSeconds(0.6f);
            yield return Shot(folder, "title_join");
            Click("Button_Back");
            yield return new WaitForSeconds(0.3f);
            Click("Button_Credits");
            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "title_credits");
            Application.Quit(0);
        }

        // ---------- Gamepad navigation test (-chopperPadTest) ----------

        private static int _padFailures;

        private static void PadCheck(bool ok, string what)
        {
            Debug.Log("[PadTest] " + (ok ? "ok   " : "FAIL ") + what);
            if (!ok) _padFailures++;
        }

        // A tap on a pad button: held long enough to be seen by a frame, then released. Used for the bumpers and B,
        // which the game polls itself.
        private static IEnumerator PadPress(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return new WaitForSecondsRealtime(0.16f);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return new WaitForSecondsRealtime(0.3f);
        }

        // The D-pad and A are Unity's own UI navigation: the module turns them into these events for the selected
        // control. Sending the events directly tests the real navigation graph, focus and sliders without depending
        // on how fast a virtual device is polled.
        private static void Move(MoveDirection direction)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null) return;
            ExecuteEvents.Execute(selected, new AxisEventData(EventSystem.current) { moveDir = direction, moveVector = direction == MoveDirection.Left ? Vector2.left : direction == MoveDirection.Right ? Vector2.right : direction == MoveDirection.Up ? Vector2.up : Vector2.down }, ExecuteEvents.moveHandler);
        }

        private static void Submit()
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null) ExecuteEvents.Execute(selected, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }

        private static string Selected()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null ? selected.name : "none";
        }

        private static bool Active(string name)
        {
            foreach (RectTransform rect in UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include))
            {
                if (rect.name == name) return rect.gameObject.activeInHierarchy;
            }
            return false;
        }

        // Walks the title screen the way a controller would: moves, selects, switches tabs with the bumpers, changes a
        // slider, and goes back with B.
        private static IEnumerator PadNavTest()
        {
            yield return new WaitForSeconds(3f);
            string[] launchArgs = Environment.GetCommandLineArgs();
            int fovArg = Array.IndexOf(launchArgs, "-chopperSetFov");
            if (fovArg >= 0 && fovArg + 1 < launchArgs.Length && float.TryParse(launchArgs[fovArg + 1], out float fov))
            {
                // A one-off repair of the saved field of view: set it and quit, without running the test.
                UserSettings.FieldOfView = fov;
                PlayerPrefs.Save();
                Debug.Log($"[PadTest] saved field of view set to {fov}");
                Application.Quit(0);
                yield break;
            }
            Debug.Log($"[PadTest] saved settings at start: fov {UserSettings.FieldOfView}, volume {UserSettings.Volume}");
            // A test run is usually not the foreground window, and the Input System would drop the virtual pad's events.
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            yield return new WaitForSeconds(0.5f);

            // With saved worlds the menu starts on Continue, with Load Game under New Game.
            bool saves = Active("Button_Continue");
            PadCheck(Selected() == (saves ? "Button_Continue" : "Button_New Game"), "the menu starts on its first entry (" + Selected() + ")");
            if (saves) Move(MoveDirection.Down);
            Move(MoveDirection.Down);
            if (saves) Move(MoveDirection.Down);
            PadCheck(Selected() == "Button_Join Game", "down moves to Join Game (" + Selected() + ")");
            Move(MoveDirection.Down);
            PadCheck(Selected() == "Button_Settings", "two moves reach Settings (" + Selected() + ")");
            Move(MoveDirection.Up);
            PadCheck(Selected() == "Button_Join Game", "up goes back to Join Game (" + Selected() + ")");
            Move(MoveDirection.Down);

            Submit();
            yield return null;
            PadCheck(Active("Settings") && !Active("Menu"), "submitting Settings opens the settings panel");
            PadCheck(Selected().StartsWith("Tab_"), "the selection lands inside the settings (" + Selected() + ")");

            yield return PadPress(pad, GamepadButton.RightShoulder);
            PadCheck(Active("Page_Graphics") && !Active("Page_General"), "RB switches to the Graphics tab");
            yield return PadPress(pad, GamepadButton.RightShoulder);
            PadCheck(Active("Page_Keyboard and mouse"), "RB again reaches Keyboard and mouse");
            yield return PadPress(pad, GamepadButton.RightShoulder);
            PadCheck(Active("Page_Gamepad"), "RB again reaches Gamepad");
            yield return PadPress(pad, GamepadButton.RightShoulder);
            PadCheck(Active("Page_General"), "RB wraps back to General");
            yield return PadPress(pad, GamepadButton.LeftShoulder);
            PadCheck(Active("Page_Gamepad"), "LB wraps back to Gamepad");
            PadCheck(Selected().StartsWith("Tab_"), "the selection stays on a tab while paging (" + Selected() + ")");
            yield return PadPress(pad, GamepadButton.RightShoulder);

            // Down from the tab onto the first slider, then left and right change it.
            Move(MoveDirection.Down);
            PadCheck(Selected() == "Slider", "down from the tab reaches the first slider (" + Selected() + ")");
            float before = UserSettings.Volume;
            // Whatever slider this is, left and right must change it (it may already sit at one end), and the test puts
            // it back, because a dev player shares its saved settings with the real game.
            Slider selectedSlider = EventSystem.current.currentSelectedGameObject != null ? EventSystem.current.currentSelectedGameObject.GetComponent<Slider>() : null;
            PadCheck(selectedSlider != null, "the selected control is a slider");
            float startValue = selectedSlider != null ? selectedSlider.value : 0f;
            for (int i = 0; i < 3; i++) Move(MoveDirection.Right);
            float afterRight = selectedSlider != null ? selectedSlider.value : 0f;
            for (int i = 0; i < 3; i++) Move(MoveDirection.Left);
            float afterLeft = selectedSlider != null ? selectedSlider.value : 0f;
            PadCheck(selectedSlider != null && (Mathf.Abs(afterRight - startValue) > 0.0001f || Mathf.Abs(afterLeft - afterRight) > 0.0001f),
                "left and right change the slider (" + startValue.ToString("0.00") + ", " + afterRight.ToString("0.00") + ", " + afterLeft.ToString("0.00") + ")");
            PadCheck(Selected() == "Slider", "the selection stays on the slider while it is adjusted (" + Selected() + ")");
            if (selectedSlider != null) selectedSlider.value = startValue;

            // A toggle button can be reached, and every page has a reachable control.
            int steps = 0;
            while (!Selected().StartsWith("Button_") && steps++ < 12) Move(MoveDirection.Down);
            PadCheck(Selected().StartsWith("Button_"), "down reaches a toggle button on General (" + Selected() + ")");

            // B goes back to the main menu with a selection.
            yield return PadPress(pad, GamepadButton.East);
            PadCheck(Active("Menu") && !Active("Settings"), "B returns to the main menu");
            yield return null;
            PadCheck(Selected().StartsWith("Button_"), "the main menu has a selection again (" + Selected() + ")");

            // The main menu is New Game, Join Game, Settings, Exit: the controls live in Settings now.
            PadCheck(!Active("Button_Controls") && !Active("Button_Character"), "the main menu has no Controls or Character entry");

            // New Game goes straight to the world seed (everyone plays Chopper, there is no character step).
            // New Game sits under Continue when worlds are saved: go to the top, then down to it.
            for (int i = 0; i < 6; i++) Move(MoveDirection.Up);
            if (Selected() != "Button_New Game") Move(MoveDirection.Down);
            Submit();
            yield return null;
            PadCheck(Active("New World") && !Active("Menu"), "New Game opens the world seed (" + Selected() + ")");
            PadCheck(Selected() == "Button_Start", "the world screen starts on Start (" + Selected() + ")");
            yield return PadPress(pad, GamepadButton.East);
            PadCheck(Active("Menu") && !Active("New World"), "B returns to the main menu from the world screen");

            // Join Game goes straight to the friends list.
            yield return null;
            for (int i = 0; i < 6 && Selected() != "Button_Join Game"; i++) Move(MoveDirection.Down);
            Submit();
            yield return null;
            PadCheck(Active("Join Game") && Selected() == "Button_Refresh", "Join Game opens the friends list (" + Selected() + ")");
            yield return PadPress(pad, GamepadButton.East);
            PadCheck(Active("Menu"), "B returns to the main menu from the friends list");

            Debug.Log(_padFailures == 0 ? "[PadTest] PASS" : "[PadTest] FAIL: " + _padFailures + " checks failed");
            Application.Quit(_padFailures == 0 ? 0 : 1);
        }

        private static IEnumerator Run(string folder)
        {
            Directory.CreateDirectory(folder);
            float deadline = Time.unscaledTime + WaitTimeout;
            while (Time.unscaledTime < deadline && (PlayerWood.Local == null || EnemyDirector.Instance == null || DayNightCycle.Instance == null)) yield return null;
            if (PlayerWood.Local == null)
            {
                Debug.Log("[Shot] world never appeared");
                Application.Quit(1);
                yield break;
            }
            yield return new WaitForSeconds(2f);

            PlayerWood wood = PlayerWood.Local;
            PlayerInventory inventory = wood.GetComponent<PlayerInventory>();
            GameUi ui = UnityEngine.Object.FindAnyObjectByType<GameUi>();

            Debug.Log($"[Shot] player starts at {wood.transform.position}");
            yield return Shot(folder, "1_day_hud");

            // Fly tour: the developer console, free flight high over the land, a crypt by teleport, landing again,
            // fabric bushes and herbs up close, and the bandage at the workbench.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperFly") >= 0)
            {
                yield return FlyTour(folder, wood, ui);
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            // Combo tour: the three axe swings in empty air, then the fists, one shot into each.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperCombo") >= 0)
            {
                Hut.Instance.ServerSetRepaired(true);
                ui.DebugShow(0);
                // A spot with no tree in front, so every swing is a combo swing.
                Vector2 clear = new Vector2(0f, 40f);
                for (int tries = 0; tries < 60; tries++)
                {
                    Vector2 candidate = new Vector2(-30f + tries * 1.7f, 30f + (tries % 7) * 3f);
                    if (!TreeManager.Instance.ServerFindTree(new Vector3(candidate.x, 0f, candidate.y), Vector3.forward, out _, out _)) { clear = candidate; break; }
                }
                yield return TeleportLocalPlayer(clear, 0f);
                PlayerAxe comboAxe = wood.GetComponent<PlayerAxe>();
                PlayerCamera comboCamera = wood.GetComponent<PlayerCamera>();
                comboAxe.SwingPlayed += st => Debug.Log("[Combo] SwingPlayed step=" + st);
                for (int pass = 0; pass < 2; pass++)
                {
                    comboAxe.ServerSetHasAxe(pass == 0);
                    yield return new WaitForSeconds(1f);
                    for (int step = 0; step < 3; step++)
                    {
                        bool swung = comboAxe.TrySwing();
                        yield return new WaitForSeconds(0.28f);
                        Animator comboAnimator = wood.GetComponentInChildren<Animator>();
                        string[] names = { "Empty", "Swing", "Swing2", "Swing3", "Punch", "Punch2", "Guard" };
                        string active = "?";
                        foreach (string n in names) if (comboAnimator.GetCurrentAnimatorStateInfo(1).IsName(n)) active = n;
                        Debug.Log($"[Combo] pass {pass} step {step}: swung={swung} chop layer state={active} t={comboAnimator.GetCurrentAnimatorStateInfo(1).normalizedTime:F2}");
                        comboCamera.SnapYaw(-60f);
                        ui.DebugShow(0); yield return Shot(folder, $"combo_{(pass == 0 ? "axe" : "fist")}_{step + 1}");
                        comboCamera.SnapYaw(0f);
                        yield return new WaitForSeconds(0.12f);
                    }
                }
                Application.Quit(0);
                yield break;
            }

            // Ground probe (-chopperGroundProbe): logs every renderer at the player's feet and the block colour there,
            // then takes the same view with the block chunks, the water and the terrain toggled, to find what draws the
            // dark ground.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperGroundProbe") >= 0)
            {
                Hut.Instance.ServerSetRepaired(true);
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.4f);
                WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
                yield return new WaitForSeconds(4f);
                GameUi.HudHidden = true;
                yield return TeleportLocalPlayer(new Vector2(0f, 9f), 0f);
                yield return new WaitForSeconds(1.5f);
                ui.DebugShow(0);
                Vector3 feet = wood.transform.position;
                Debug.Log($"[Probe] feet {feet.x:F2},{feet.y:F2},{feet.z:F2} pipeline {UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name}");
                Chopper.World.Voxel.BlockPalette.Ensure();
                Debug.Log($"[Probe] palette from file {Chopper.World.Voxel.BlockPalette.LoadedFromFile} grass top {Chopper.World.Voxel.BlockPalette.Of(Chopper.World.Voxel.BlockType.Grass).Top}");
                var probe = new Bounds(feet, new Vector3(0.5f, 1.5f, 0.5f));
                foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy || !r.bounds.Intersects(probe)) continue;
                    if (r.transform.IsChildOf(wood.transform)) continue;
                    Material m = r.sharedMaterial;
                    Debug.Log($"[Probe] renderer {r.name} parent {(r.transform.parent ? r.transform.parent.name : "-")} material {(m ? m.name : "none")} shader {(m && m.shader ? m.shader.name : "none")} bounds y {r.bounds.min.y:F2}..{r.bounds.max.y:F2}");
                    if (r is MeshRenderer && r.name.StartsWith("Chunk_"))
                    {
                        Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
                        Vector3[] v = mesh.vertices;
                        Color[] c = mesh.colors;
                        int best = -1;
                        float bestD = float.MaxValue;
                        for (int i = 0; i < v.Length; i++)
                        {
                            float d = (r.transform.TransformPoint(v[i]) - feet).sqrMagnitude;
                            if (d < bestD) { bestD = d; best = i; }
                        }
                        if (best >= 0) Debug.Log($"[Probe] chunk vertex {r.transform.TransformPoint(v[best])} colour {(c.Length > best ? c[best].ToString() : "none")} colours {c.Length} verts {v.Length}");
                    }
                }
                Terrain probeTerrain = Terrain.activeTerrain;
                Debug.Log($"[Probe] terrain {(probeTerrain ? probeTerrain.name : "none")} drawHeightmap {(probeTerrain ? probeTerrain.drawHeightmap : false)} height {(probeTerrain ? probeTerrain.SampleHeight(feet) + probeTerrain.transform.position.y : 0f):F2}");

                PlayerCamera probeCam = wood.GetComponent<PlayerCamera>();
                probeCam.DebugSetDistance(5f);
                probeCam.DebugSetPitch(30f);
                yield return new WaitForSeconds(1f);
                yield return Shot(folder, "probe_0_all");
                GameObject voxelRoot = GameObject.Find("VoxelWorld");
                GameObject waterRoot = GameObject.Find("Water");
                if (voxelRoot) voxelRoot.SetActive(false);
                yield return new WaitForSeconds(0.3f);
                yield return Shot(folder, "probe_1_no_blocks");
                if (voxelRoot) voxelRoot.SetActive(true);
                if (waterRoot) waterRoot.SetActive(false);
                yield return new WaitForSeconds(0.3f);
                yield return Shot(folder, "probe_2_no_water");
                if (waterRoot) waterRoot.SetActive(true);
                if (probeTerrain) probeTerrain.drawHeightmap = true;
                yield return new WaitForSeconds(0.3f);
                yield return Shot(folder, "probe_3_terrain_paint_on");
                Debug.Log("[Probe] done");
                Application.Quit(0);
                yield break;
            }

            // Lighting tour: the same few places at several times of day, for judging light, shadow and exposure.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperLight") >= 0)
            {
                Hut.Instance.ServerSetRepaired(true);
                wood.GetComponent<PlayerAxe>().ServerSetHasAxe(true);
                ui.DebugShow(0);
                // Place, camera yaw (0 looks north), and a short name. The yard shot looks at the hut from the path.
                (Vector2 where, float yaw, string label)[] places =
                {
                    (new Vector2(0f, 9f), 180f, "yard"),
                    (new Vector2(0f, 45f), 0f, "hub"),
                    (new Vector2(150f, 0f), 90f, "earth"),
                    (new Vector2(0f, -150f), 180f, "fire"),
                    (new Vector2(-150f, -170f), 225f, "shore")
                };
                (string label, DayPhase phase, float fraction, int placeCount)[] times =
                {
                    ("morning", DayPhase.Day, 0.04f, 2),
                    ("noon", DayPhase.Day, 0.5f, 5),
                    ("late", DayPhase.Day, 0.92f, 2),
                    ("dusk", DayPhase.Dusk, 0.6f, 2),
                    ("night", DayPhase.Night, 0.3f, 2)
                };
                foreach (var time in times)
                {
                    DayNightCycle.Instance.ServerDebugSetClock(time.phase, time.fraction);
                    for (int i = 0; i < time.placeCount; i++)
                    {
                        yield return TeleportLocalPlayer(places[i].where, places[i].yaw);
                        yield return new WaitForSeconds(1.2f);
                        ui.DebugShow(0);
                        yield return Shot(folder, $"light_{time.label}_{places[i].label}");
                    }
                }
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            // The practice yard (-chopperTutorial -chopperTutorialTour): checks the yard is set up, then pictures the
            // welcome sign, a sign read open, the training tree and the fishing sign. Logs "[Tutorial] ok" or "FAIL".
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperTutorialTour") >= 0)
            {
                yield return new WaitForSeconds(6f);
                ui.DebugShow(0);
                bool all = true;
                void Expect(bool condition, string what)
                {
                    all &= condition;
                    Debug.Log($"[Tutorial] {(condition ? "ok  " : "FAIL")} {what}");
                }
                TutorialYard yard = TutorialYard.Instance;
                DayNightCycle cycle = DayNightCycle.Instance;
                PlayerInventory kit = wood.GetComponent<PlayerInventory>();
                PlayerFishing rod = wood.GetComponent<PlayerFishing>();
                Expect(TutorialSession.Active && yard != null, "the practice yard is up");
                Expect(!Chopper.World.Saving.SaveSystem.Saving, "the practice yard is never saved");
                Expect(yard != null && yard.SignCount == TutorialStations.Count, $"a sign at every station ({(yard != null ? yard.SignCount : 0)})");
                Expect(Hut.Instance.IsRepaired, "the hut is repaired");
                Expect(wood.GetComponent<PlayerAxe>().HasAxe && rod.Rod > 0, "the player has the axe and a rod");
                Expect(kit.Torches > 0 && kit.Salves > 0 && kit.Herbs > 0 && kit.Fabric > 0 && kit.Iron > 0 && kit.Shards > 0 && wood.Carried > 0,
                    "the player carries torches, bandages, herbs, fiber, iron, shards and wood");
                float clockBefore = cycle.PhaseElapsed;
                yield return new WaitForSeconds(3f);
                Expect(cycle.Phase == DayPhase.Day && Mathf.Abs(cycle.PhaseElapsed - clockBefore) < 0.01f, "the clock holds at day");
                Treant dummy = yard != null ? yard.Dummy : null;
                Expect(dummy != null && dummy.IsActive, "a training tree stands at the fighting station");

                Chopper.World.Generation.WorldGen tutorialGen = Chopper.World.Generation.WorldSession.Gen;
                foreach (TutorialStations.Station station in new[] { TutorialStations.Station.Welcome, TutorialStations.Station.Fighting, TutorialStations.Station.Fishing })
                {
                    Vector2 sign = TutorialStations.Position(station, tutorialGen.PondCenter, Chopper.World.Generation.WorldGen.PondOuterRadius);
                    Vector2 stand = sign * 0.88f;
                    Vector2 look = sign - stand;
                    yield return TeleportLocalPlayer(stand, Mathf.Atan2(look.x, look.y) * Mathf.Rad2Deg);
                    yield return new WaitForSeconds(1.5f);
                    ui.DebugShow(0);
                    yield return Shot(folder, "tutorial_" + station.ToString().ToLowerInvariant());
                    if (station == TutorialStations.Station.Welcome)
                    {
                        Sign near = Sign.Nearest(wood.transform.position);
                        Expect(near != null && near.Title == TutorialStations.Title(station), "the welcome sign can be read from beside it");
                        if (near != null)
                        {
                            UiState.RequestReadSign(near.Title, near.Body);
                            yield return new WaitForSeconds(0.6f);
                            yield return Shot(folder, "tutorial_welcome_read");
                            ui.DebugShow(0);
                            yield return new WaitForSeconds(0.3f);
                        }
                    }
                }

                // Fell the training tree: it comes back.
                if (dummy != null)
                {
                    dummy.ServerHit(100f, null, wood);
                    yield return new WaitForSeconds(6f);
                    Treant again = yard.Dummy;
                    Expect(again != null && again != dummy && again.IsActive, "a felled training tree comes back");
                }
                Debug.Log($"[Tutorial] RESULT {(all ? "PASS" : "FAIL")}");
                Application.Quit();
                yield break;
            }

            // Smoothness check (-chopperMoveTest): a virtual gamepad sprints the player through the hub, up the slopes and
            // along the foot of the mountains, then into the pond. Every frame the player's step is measured: a jump of
            // more than a meter is a teleport, and standing still for a quarter second while the stick is held is a
            // freeze. Also counts the server's speed check snap backs. Needs a window.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperMoveTest") >= 0)
            {
                // As a joining client (-chopperJoin) the test starts where the player spawned: the server side
                // set up (repair, endless stamina, teleport) only exists on the host.
                bool hosting = wood.IsServerInitialized;
                if (hosting)
                {
                    Hut.Instance.ServerSetRepaired(true);
                    yield return TeleportLocalPlayer(new Vector2(0f, 30f), 0f);
                    wood.GetComponent<PlayerCombat>().ServerSetGod(true);
                }
                yield return new WaitForSeconds(6f);
                ui.DebugShow(0);
                PlayerCamera moveCamera = wood.GetComponent<PlayerCamera>();
                Gamepad pad = InputSystem.AddDevice<Gamepad>();
                int snapBacks = 0;
                void CountSnaps(string condition, string trace, LogType type)
                {
                    if (condition.StartsWith("[MovementCheck]")) snapBacks++;
                }
                Application.logMessageReceived += CountSnaps;
                Chopper.World.Generation.WorldGen moveGen = Chopper.World.Generation.WorldSession.Gen;
                Vector2 toPond = moveGen.PondCenter - new Vector2(0f, 30f);
                (string label, float yaw, float seconds)[] legs =
                {
                    ("north up the path", 0f, 6f), ("northeast into the ridge", 45f, 7f), ("east along the foot", 100f, 6f),
                    ("south across the hub", 190f, 7f), ("west into the hills", 275f, 7f), ("to the pond", Mathf.Atan2(toPond.x, toPond.y) * Mathf.Rad2Deg, 8f),
                };
                int teleports = 0, freezes = 0;
                float worstStep = 0f;
                foreach (var leg in legs)
                {
                    float start = Time.time;
                    Vector3 last = wood.transform.position;
                    float stillFor = 0f;
                    while (Time.time - start < leg.seconds)
                    {
                        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.up }.WithButton(GamepadButton.LeftStick));
                        moveCamera.SnapYaw(leg.yaw);
                        yield return null;
                        Vector3 now = wood.transform.position;
                        Vector3 step = now - last;
                        step.y = 0f;
                        float d = step.magnitude;
                        worstStep = Mathf.Max(worstStep, d);
                        if (d > 1f)
                        {
                            teleports++;
                            Debug.Log($"[Move] TELEPORT {leg.label}: {d:F2} m in one frame at {now:F1}");
                        }
                        stillFor = d < 0.005f ? stillFor + Time.deltaTime : 0f;
                        if (stillFor >= 0.25f && stillFor - Time.deltaTime < 0.25f)
                        {
                            freezes++;
                            Debug.Log($"[Move] FREEZE {leg.label}: stopped at {now:F1} with the stick held");
                        }
                        last = now;
                    }
                    Debug.Log($"[Move] {leg.label}: ended at {wood.transform.position:F1}");
                }
                InputSystem.QueueStateEvent(pad, new GamepadState());
                InputSystem.RemoveDevice(pad);
                Application.logMessageReceived -= CountSnaps;
                Debug.Log($"[Move] RESULT {(hosting ? "host" : "client")} teleports {teleports}, freezes {freezes}, speed check snap backs {snapBacks}, biggest step {worstStep:F2} m");
                Application.Quit();
                yield break;
            }

            // Guard check (-chopperGuardTest): a virtual gamepad holds the block trigger and walks sideways, backward,
            // forward and in a circle. Logs the body yaw, the model's own yaw, the shield's local rotation and the hip
            // height every quarter second, so a spin or a drift shows up as numbers, and takes a few pictures.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperGuardTest") >= 0)
            {
                Hut.Instance.ServerSetRepaired(true);
                wood.GetComponent<PlayerAxe>().ServerSetHasAxe(true);
                yield return TeleportLocalPlayer(new Vector2(0f, 40f), 0f);
                yield return new WaitForSeconds(6f);
                ui.DebugShow(0);
                Gamepad pad = InputSystem.AddDevice<Gamepad>();
                Debug.Log($"[Guard] gamepads connected: {Gamepad.all.Count}");
                // Endless stamina so the guard never drops mid test, and the camera pinned north each frame so stray
                // mouse or stick input on this PC cannot turn it.
                wood.GetComponent<PlayerCombat>().ServerSetGod(true);
                PlayerCamera guardCamera = wood.GetComponent<PlayerCamera>();
                GuardPose guard = wood.GetComponentInChildren<GuardPose>(true);
                Transform pelvis = null;
                foreach (Transform t in guard.GetComponentsInChildren<Transform>(true)) if (t.name == "pelvis") pelvis = t;
                Transform holder = null;
                foreach (Transform t in guard.GetComponentsInChildren<Transform>(true)) if (t.name == "AxeGrip") holder = t;
                Transform shoulderL = null, shoulderR = null;
                foreach (Transform t in guard.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "upperarm_l") shoulderL = t;
                    if (t.name == "upperarm_r") shoulderR = t;
                }
                // Where the model really faces: perpendicular to the shoulder line (left to right shoulder, up).
                float ModelFacing()
                {
                    if (shoulderL == null || shoulderR == null) return float.NaN;
                    Vector3 facing = Vector3.Cross(shoulderR.position - shoulderL.position, Vector3.up);
                    return Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
                }
                (string label, Vector2 stick)[] moves =
                {
                    ("still", Vector2.zero), ("right", Vector2.right), ("left", Vector2.left), ("back", Vector2.down),
                    ("forward", Vector2.up), ("back_right", new Vector2(0.7f, -0.7f)), ("circle", Vector2.zero), ("released", Vector2.zero)
                };
                float maxSpin = 0f;
                float axeWobble = 0f;
                float facingMin = float.MaxValue, facingMax = float.MinValue;
                foreach (var move in moves)
                {
                    float start = Time.time;
                    float lastYaw = wood.transform.eulerAngles.y;
                    float turned = 0f;
                    Quaternion lastAxe = holder != null ? holder.rotation : Quaternion.identity;
                    while (Time.time - start < 2.5f)
                    {
                        Vector2 stick = move.label == "circle"
                            ? new Vector2(Mathf.Sin((Time.time - start) * 2.5f), Mathf.Cos((Time.time - start) * 2.5f))
                            : move.stick;
                        float trigger = move.label == "released" ? 0f : 1f;
                        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = stick, leftTrigger = trigger });
                        float until = Time.time + 0.25f;
                        while (Time.time < until)
                        {
                            guardCamera.SnapYaw(0f);
                            yield return null;
                        }
                        float yaw = wood.transform.eulerAngles.y;
                        turned += Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));
                        lastYaw = yaw;
                        // Standing still with the guard up, the axe must hold still too (after the raise settles).
                        if (holder != null && move.label == "still" && Time.time - start > 0.6f)
                            axeWobble = Mathf.Max(axeWobble, Quaternion.Angle(lastAxe, holder.rotation));
                        if (holder != null) lastAxe = holder.rotation;
                        // Standing still with the guard up, the upper body must hold its stance, not drift or spin.
                        if (move.label == "still" && Time.time - start > 0.6f)
                        {
                            float off = Mathf.DeltaAngle(yaw, ModelFacing());
                            facingMin = Mathf.Min(facingMin, off);
                            facingMax = Mathf.Max(facingMax, off);
                        }
                        Debug.Log($"[Guard] {move.label} t {Time.time - start:F2} body {yaw:F0} faces {Mathf.Repeat(ModelFacing(), 360f):F0} cam {guardCamera.Yaw % 360f:F0} model {guard.transform.localEulerAngles.y:F0} " +
                                  $"weight {guard.Weight:F2} axe {(holder != null ? holder.eulerAngles.ToString("F0") : "none")} pelvisY {(pelvis != null ? pelvis.localPosition.y.ToString("F3") : "none")}");
                    }
                    if (move.label != "circle") maxSpin = Mathf.Max(maxSpin, turned);
                    Debug.Log($"[Guard] {move.label} body turned {turned:F0} degrees in total");
                    ui.DebugShow(0);
                    yield return Shot(folder, "guard_" + move.label);
                }
                InputSystem.QueueStateEvent(pad, new GamepadState());
                InputSystem.RemoveDevice(pad);
                float drift = facingMax - facingMin;
                bool pass = maxSpin < 120f && axeWobble < 8f && drift < 6f;
                Debug.Log($"[Guard] RESULT {(pass ? "PASS no spin" : "FAIL spin")} (largest body turn in one straight move {maxSpin:F0} degrees, axe wobble standing still {axeWobble:F1} degrees, upper body drift standing still {drift:F1} degrees)");
                Application.Quit(pass ? 0 : 1);
                yield break;
            }

            // Concept 2.0 cabin (-chopperCabin): places a floor, log walls, a doorway, a window, a gable and two
            // roofs through the real build rules, away from the hub. Logs "[Cabin] RESULT PASS" or "FAIL".
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperCabin") >= 0)
            {
                yield return CabinTour(folder, wood, ui);
                Application.Quit(0);
                yield break;
            }

            // Foliage walk (-chopperFoliageWalk): the player stands in a patch of grass carpet with decorative bushes, then a
            // virtual stick walks him straight through it. Pictures standing still, mid-walk, and a second after passing,
            // with the bushes' lean measured at each. Logs "[Foliage] RESULT PASS" or "FAIL".
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperFoliageWalk") >= 0)
            {
                yield return FoliageWalk(folder, wood, ui);
                Application.Quit();
                yield break;
            }

            // World audit (-chopperWorldAudit): wide pictures of the world for comparing against the concept art, from
            // the gameplay camera and from free vantage points, plus frame times. Logs "[Audit] RESULT".
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperWorldAudit") >= 0)
            {
                yield return WorldAudit(folder, wood, ui);
                Application.Quit(0);
                yield break;
            }

            // Gear look (-chopperGearLook): the held Chopper axe and the shield on the back at third person distance,
            // then one chop into a tree. Logs where the axe and shield sit against the body.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperGearLook") >= 0)
            {
                yield return GearLook(folder, wood, ui);
                Application.Quit();
                yield break;
            }

            // Grass check: does it lean away from a player standing in it? (-chopperGrassTest)
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperGrassTest") >= 0)
            {
                ui.DebugShow(0);
                yield return TeleportLocalPlayer(new Vector2(-45f, -45f), 45f);
                yield return new WaitForSeconds(1.5f);
                GameObject grassRoot = GameObject.Find("Grass");
                Vector3 at = wood.transform.position;
                float TiltNear(float radius)
                {
                    float max = 0f; int n = 0;
                    foreach (Transform child in grassRoot.transform)
                    {
                        Vector3 d = child.position - at; d.y = 0f;
                        if (d.magnitude > radius) continue;
                        n++;
                        max = Mathf.Max(max, Vector3.Angle(child.up, Vector3.up));
                    }
                    Debug.Log($"[Grass] {n} plants within {radius} m, max tilt {max:F1} degrees");
                    return max;
                }
                Debug.Log($"[Grass] total plants {(grassRoot != null ? grassRoot.transform.childCount : 0)} pushers {Chopper.World.GrassPushers.All.Count}");
                float near = TiltNear(2.5f);
                float far = TiltNear(40f) ;
                Debug.Log($"[Grass] RESULT {(near > 10f ? "PASS leaning" : "FAIL not leaning")}");
                Application.Quit(near > 10f ? 0 : 1);
                yield break;
            }

            // Website tour: the three pictures the public page uses (Website/images: character, night, fishing), under
            // those names as PNGs; Tools turns them into the page's 1600 by 900 JPGs.
            // The pad hotbar with a virtual pad: D-pad left and right move the cursor (wrapping), D-pad up uses the slot,
            // D-pad down zooms. Logs [PadHotbar] ok or FAIL lines and quits with 1 on any failure.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperPadHotbar") >= 0)
            {
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                Hut.Instance.ServerSetRepaired(true);
                PlayerAxe padAxe = wood.GetComponent<PlayerAxe>();
                padAxe.ServerSetHasAxe(true);
                inventory.ServerGrant(3, 2);
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.4f);
                yield return new WaitForSeconds(5f);
                ui.DebugShow(0);
                yield return new WaitForSeconds(0.5f);
                Gamepad pad = InputSystem.AddDevice<Gamepad>();
                yield return new WaitForSeconds(0.5f);
                int failures = 0;
                void Check(bool ok, string what)
                {
                    Debug.Log("[PadHotbar] " + (ok ? "ok   " : "FAIL ") + what);
                    if (!ok) failures++;
                }

                int start = PadHotbar.Selected;
                yield return PadPress(pad, GamepadButton.DpadRight);
                Check(PadHotbar.Selected == PadHotbar.Step(start, 1), $"D-pad right moves the cursor ({start} to {PadHotbar.Selected})");
                while (PadHotbar.Selected != 1) yield return PadPress(pad, GamepadButton.DpadRight);
                yield return Shot(folder, "padhotbar_1_cursor_on_torch");
                bool litBefore = inventory.TorchLit;
                yield return PadPress(pad, GamepadButton.DpadUp);
                yield return new WaitForSeconds(0.4f);
                Check(inventory.TorchLit != litBefore, $"D-pad up on the torch slot lights it ({litBefore} to {inventory.TorchLit})");
                yield return Shot(folder, "padhotbar_2_torch_lit");
                yield return PadPress(pad, GamepadButton.DpadUp);
                yield return new WaitForSeconds(0.4f);
                Check(inventory.TorchLit == litBefore, "D-pad up again puts the torch out");

                yield return PadPress(pad, GamepadButton.DpadLeft);
                Check(PadHotbar.Selected == 0, $"D-pad left moves back to the axe ({PadHotbar.Selected})");
                bool drawnBefore = padAxe.IsDrawn;
                yield return PadPress(pad, GamepadButton.DpadUp);
                yield return new WaitForSeconds(0.6f);
                Check(padAxe.IsDrawn != drawnBefore, $"D-pad up on the axe slot draws or puts it away ({drawnBefore} to {padAxe.IsDrawn})");

                yield return PadPress(pad, GamepadButton.DpadLeft);
                Check(PadHotbar.Selected == PadHotbar.Slots - 1, $"D-pad left from the first slot wraps to the hammer ({PadHotbar.Selected})");
                yield return PadPress(pad, GamepadButton.DpadUp);
                yield return new WaitForSeconds(0.4f);
                Check(PlayerBuilder.LocalBuilding, "D-pad up on the hammer slot takes the hammer out");
                yield return Shot(folder, "padhotbar_3_hammer");
                yield return PadPress(pad, GamepadButton.DpadUp);
                yield return new WaitForSeconds(0.4f);
                Check(!PlayerBuilder.LocalBuilding, "D-pad up again puts the hammer away");

                Transform padCam = Camera.main.transform;
                float near = Vector3.Distance(padCam.position, wood.transform.position);
                yield return PadPress(pad, GamepadButton.DpadDown);
                yield return new WaitForSeconds(1.5f);
                float far = Vector3.Distance(padCam.position, wood.transform.position);
                Check(Mathf.Abs(far - near) > 0.2f, $"D-pad down zooms ({near:0.00} m to {far:0.00} m)");
                PlayerMotor padMotor = wood.GetComponent<PlayerMotor>();
                yield return PadPress(pad, GamepadButton.LeftShoulder);
                Check(padMotor.IsCrouching && PadHotbar.Selected == PadHotbar.Slots - 1 && !PlayerBuilder.LocalBuilding, "LB crouches and leaves the hotbar alone");
                yield return PadPress(pad, GamepadButton.LeftShoulder);
                Check(!padMotor.IsCrouching, "LB again stands up");

                // R3 puts away whatever is in the hands; pressed again with empty hands, the axe comes back.
                if (!padAxe.IsDrawn) { padAxe.RequestToggleDrawn(); yield return new WaitForSeconds(0.6f); }
                yield return PadPress(pad, GamepadButton.RightStick);
                yield return new WaitForSeconds(0.4f);
                Check(!padAxe.IsDrawn, "R3 puts the axe on the back");
                yield return PadPress(pad, GamepadButton.RightStick);
                yield return new WaitForSeconds(0.4f);
                Check(padAxe.IsDrawn, "R3 again takes the axe back out");
                if (!inventory.TorchLit) { inventory.RequestToggleTorch(); yield return new WaitForSeconds(0.5f); }
                bool drawnWithTorch = padAxe.IsDrawn;
                yield return PadPress(pad, GamepadButton.RightStick);
                yield return new WaitForSeconds(0.5f);
                Check(!inventory.TorchLit && !padAxe.IsDrawn, $"R3 puts out the torch and puts the axe away together (axe was out {drawnWithTorch})");
                wood.GetComponent<PlayerBuilder>().Select(1);
                yield return new WaitForSeconds(0.3f);
                yield return PadPress(pad, GamepadButton.RightStick);
                yield return new WaitForSeconds(0.3f);
                Check(!PlayerBuilder.LocalBuilding, "R3 puts the hammer away");
                Check(!padMotor.IsCrouching, "R3 no longer crouches");

                // A menu owns the D-pad: the cursor stays put.
                ui.DebugShow(2);
                yield return new WaitForSeconds(0.3f);
                int held = PadHotbar.Selected;
                yield return PadPress(pad, GamepadButton.DpadRight);
                Check(PadHotbar.Selected == held, "the cursor does not move while the bag is open");
                ui.DebugShow(0);

                InputSystem.RemoveDevice(pad);
                Debug.Log($"[PadHotbar] done, {failures} failures");
                Application.Quit(failures == 0 ? 0 : 1);
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperSite") >= 0)
            {
                Hut.Instance.ServerSetRepaired(true);
                wood.GetComponent<PlayerAxe>().ServerSetHasAxe(true);
                PlayerFishing siteFishing = wood.GetComponent<PlayerFishing>();
                siteFishing.ServerGrantGear();
                // Handed out before the wait, so no pickup card is left on screen in any picture.
                inventory.ServerGrant(2, 0);
                ui.DebugShow(0);
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.4f);
                WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
                yield return new WaitForSeconds(7f);
                ui.DebugShow(0);
                // Clean pictures: no hotbar, bars, minimap or prompts.
                GameUi.HudHidden = true;
                PlayerCamera siteCam = wood.GetComponent<PlayerCamera>();

                // Chopper from the front with the axe in hand, the crag and the red pines of the Fire forest behind him (the
                // page caption says so). The follow camera, swung round in front of him: it keeps clear of trees, which a
                // free camera here did not (it sat inside a canopy).
                yield return TeleportLocalPlayer(new Vector2(6f, -150f), 195f);
                siteCam.DebugSetDistance(3.1f);
                siteCam.SnapYaw(35f);
                siteCam.DebugSetPitch(6f);
                yield return new WaitForSeconds(2f);
                ui.DebugShow(0);
                yield return Shot(folder, "character");

                // Fishing from the hub pond's bank, the float out on the water.
                Chopper.World.Generation.WorldGen siteGen = Chopper.World.Generation.WorldSession.Gen;
                Vector2 siteBank = siteGen.PondCenter + Vector2.right * (Chopper.World.Generation.WorldGen.PondOuterRadius + 1.2f);
                // Facing the middle of the pond (west), from a little higher, so the float reads as out on the water
                // and not hanging from the rod.
                yield return TeleportLocalPlayer(siteBank, 270f);
                siteCam.DebugSetDistance(4.6f);
                siteCam.SnapYaw(278f);
                siteCam.DebugSetPitch(17f);
                yield return new WaitForSeconds(1f);
                siteFishing.TryToggle();
                yield return new WaitForSeconds(1.5f);
                siteFishing.DebugCast(0.9f);
                yield return new WaitForSeconds(2.2f);
                ui.DebugShow(0);
                yield return Shot(folder, "fishing");
                siteFishing.TryToggle();
                yield return new WaitForSeconds(0.5f);

                // The path north through the mountain gorge, the blue pines of the Water forest beyond.
                float siteHub = TreeManager.Instance.Config.HubRadius;
                yield return TeleportLocalPlayer(new Vector2(0f, 118f), 0f);
                siteCam.DebugSetDistance(4.2f);
                siteCam.SnapYaw(0f);
                siteCam.DebugSetPitch(6f);
                yield return new WaitForSeconds(2f);
                ui.DebugShow(0);
                yield return Shot(folder, "pass");

                // A pond deep in the Water forest: the nearest marsh pool there, seen past Chopper.
                Chopper.World.Generation.WaterBody pondBody = default;
                float pondBest = float.MaxValue;
                foreach (Chopper.World.Generation.WaterBody body in siteGen.WaterBodies)
                {
                    // The Water forest is a marsh of pools (WorldGen); the nearest one.
                    if (!body.Marsh || ZoneMap.ElementAt(body.Center.x, body.Center.y, siteHub) != Element.Water) continue;
                    float off = body.Center.magnitude;
                    if (off < pondBest) { pondBest = off; pondBody = body; }
                }
                if (pondBest < float.MaxValue)
                {
                    float edge = Chopper.World.Generation.WorldGen.ShoreEdge(pondBody);
                    Vector2 shore = pondBody.Center + Vector2.down * (edge + 2.5f);
                    yield return TeleportLocalPlayer(shore, 0f);
                    siteCam.DebugSetDistance(4.5f);
                    siteCam.SnapYaw(0f);
                    siteCam.DebugSetPitch(10f);
                    yield return new WaitForSeconds(2f);
                    ui.DebugShow(0);
                    yield return Shot(folder, "forest-pond");
                }
                else Debug.Log("[Shot] no Water forest pond found");

                // The concept 2.0 cabin, door and gable on +Z. A three-quarter from the front so the doorway, window and roof read.
                // The camera stays well inside the yard torch ring (WorldGen.WallRadius, 14 m): at 13 m a torch post filled the frame.
                Vector3 siteHome = Hut.Instance.transform.position;
                yield return TeleportLocalPlayer(new Vector2(siteHome.x + 2.0f, siteHome.z + 5.0f), 205f);
                siteCam.DebugSetDistance(4.6f);
                siteCam.SnapYaw(205f);
                siteCam.DebugSetPitch(6f);
                yield return new WaitForSeconds(1.5f);
                ui.DebugShow(0);
                yield return Shot(folder, "hut");

                // Night outside the cabin with a lit torch, the hut and the yard torches glowing behind.
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Night, 0.35f);
                yield return new WaitForSeconds(0.5f);
                inventory.ServerToggleTorch();
                yield return TeleportLocalPlayer(new Vector2(siteHome.x - 1.6f, siteHome.z + 5.5f), 155f);
                siteCam.DebugSetDistance(4.2f);
                siteCam.SnapYaw(165f);
                siteCam.DebugSetPitch(8f);
                yield return new WaitForSeconds(2f);
                ui.DebugShow(0);
                yield return Shot(folder, "night");
                inventory.ServerToggleTorch();

                // With the HUD on, for the icons: the hotbar (axe, torches, bandages, rod, fish, hammer), the Fed badge,
                // the bag and the build list.
                GameUi.HudHidden = false;
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.42f);
                inventory.ServerGrant(1, 3);
                inventory.ServerAddHerbs(4);
                inventory.ServerAddFabric(3);
                inventory.ServerAddRelics(3, 2);
                wood.ServerAddWood(24);
                siteFishing.ServerChangeFish(FishKind.Perch, 3);
                siteFishing.ServerChangeFish(FishKind.Pike, 1);
                siteFishing.ServerEat(FishKind.Perch);
                yield return TeleportLocalPlayer(new Vector2(6f, -150f), 195f);
                siteCam.DebugSetDistance(3.2f);
                siteCam.SnapYaw(35f);
                siteCam.DebugSetPitch(6f);
                yield return new WaitForSeconds(4f);
                ui.DebugShow(0);
                yield return Shot(folder, "hud");
                ui.DebugShow(2);
                ui.DebugInventoryPage(1);
                yield return new WaitForSeconds(0.8f);
                yield return Shot(folder, "bag");
                ui.DebugShow(0);
                PlayerBuilder siteHammer = wood.GetComponent<PlayerBuilder>();
                siteHammer.Select(1);
                yield return new WaitForSeconds(0.8f);
                ui.DebugShow(10);
                yield return new WaitForSeconds(0.8f);
                yield return Shot(folder, "build");
                ui.DebugShow(0);
                siteHammer.SetActive(false);
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            // The walk-in hut: from the yard, inside toward the bed and the fireplace, the workbench, and the room at night.
            // House tour: the workbench, chest and bed prompts and menus, worn tools on the hotbar, a small hut built from
            // pieces next to a built workbench, the hammer's ghost and its piece list. Logs [House] lines.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperHouse") >= 0)
            {
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.35f);
                WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
                Hut.Instance.ServerSetRepaired(true);
                yield return new WaitForSeconds(7f);
                ui.DebugShow(0);
                PlayerCamera houseCam = wood.GetComponent<PlayerCamera>();
                PlayerAxe houseAxe = wood.GetComponent<PlayerAxe>();
                PlayerFishing houseRod = wood.GetComponent<PlayerFishing>();
                wood.ServerAddWood(30);
                inventory.ServerAddHerbs(4);
                inventory.ServerAddFabric(4);
                inventory.ServerAddRelics(3, 2);
                houseAxe.ServerLoadWear(Mathf.RoundToInt(houseAxe.MaxDurability * 0.55f));
                houseRod.ServerLoadRodWear(houseRod.RodMaxDurability);
                houseRod.ServerChangeFish(FishKind.Perch, 3);
                houseRod.ServerEat(FishKind.Perch);

                Vector3 benchAt = Workbench.All[0].transform.position;
                yield return TeleportLocalPlayer(new Vector2(benchAt.x - 1.7f, benchAt.z), 90f);
                houseCam.DebugSetPitch(12f);
                houseCam.DebugSetDistance(2.8f);
                yield return new WaitForSeconds(1.5f);
                yield return Shot(folder, "house_1_workbench_prompt");
                ui.DebugShow(8);
                for (int page = 0; page < 4; page++)
                {
                    ui.DebugInventoryPage(page);
                    yield return new WaitForSeconds(0.5f);
                    yield return Shot(folder, "house_2_bench_page" + page);
                }
                ui.DebugShow(0);
                wood.GetComponent<PlayerUpgrades>().ServerRepair(ToolKind.Rod);
                yield return new WaitForSeconds(0.5f);
                ui.DebugShow(2);
                ui.DebugInventoryPage(1);
                yield return new WaitForSeconds(0.5f);
                yield return Shot(folder, "house_3_tab_bag");
                ui.DebugInventoryPage(2);
                yield return new WaitForSeconds(0.5f);
                yield return Shot(folder, "house_3b_tab_craft");
                ui.DebugShow(0);

                StorageChest storage = UnityEngine.Object.FindAnyObjectByType<StorageChest>();
                SharedChest.Instance.ServerMove(wood.gameObject, (int)ItemKind.Wood, 8, true);
                SharedChest.Instance.ServerMove(wood.gameObject, (int)ItemKind.Herb, 2, true);
                SharedChest.Instance.ServerMove(wood.gameObject, ItemRules.Fish(FishKind.Perch), 1, true);
                Vector3 chestAt = storage.transform.position;
                yield return TeleportLocalPlayer(new Vector2(chestAt.x + 1.6f, chestAt.z), 270f);
                houseCam.DebugSetPitch(14f);
                houseCam.DebugSetDistance(2.6f);
                yield return new WaitForSeconds(1.2f);
                yield return Shot(folder, "house_4_chest_prompt");
                ui.DebugShow(9);
                yield return new WaitForSeconds(0.6f);
                yield return Shot(folder, "house_4b_chest_menu");
                ui.DebugShow(0);

                Bed houseBed = UnityEngine.Object.FindAnyObjectByType<Bed>();
                Vector3 bedAt = houseBed.transform.position;
                yield return TeleportLocalPlayer(new Vector2(bedAt.x + 1.5f, bedAt.z + 0.5f), 240f);
                yield return new WaitForSeconds(1f);
                yield return Shot(folder, "house_5_bed_by_day");
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Night, 0.3f);
                yield return new WaitForSeconds(1.5f);
                yield return Shot(folder, "house_5b_bed_at_night");
                bool lay = wood.GetComponent<PlayerRest>().ServerLieDown();
                yield return new WaitForSeconds(1f);
                Debug.Log($"[House] lay down {lay}, phase now {DayNightCycle.Instance.Phase}");
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.3f);
                ClearForTour();

                // A little hut from pieces, on flat ground near the hub, with a built workbench beside it.
                var build = Chopper.World.Building.BuildManager.Instance;
                Vector3 site = FlatSpot();
                Debug.Log($"[House] building site {site}");
                Vector3 cell = BuildRules.Snap(site, PieceSnap.Cell, 0f);
                float floorY = Chopper.World.Building.BuildManager.GroundAt(cell, out _) + 0.02f;
                yield return TeleportLocalPlayer(new Vector2(cell.x + 1f, cell.z - 5f), 0f);
                yield return new WaitForSeconds(0.5f);
                wood.ServerAddWood(400);
                inventory.ServerAddFabric(10);
                void Place(int kind, Vector3 at, float yaw)
                {
                    TeleportNear(at);
                    wood.ServerAddWood(20);
                    var result = build.ServerPlace(wood.gameObject, kind, at, yaw);
                    Debug.Log($"[House] place {build.Catalog.Get(kind).DisplayName} at {at} yaw {yaw}: {result}");
                }
                // The hut pieces are aimed sloppily (as a player would: up to half a meter off and a little high or
                // low) and go through the hammer's own snapping, which must put each exactly where it belongs.
                var sloppy = new System.Random(4);
                float worstMiss = 0f;
                void PlaceAimed(int kind, Vector3 ideal, float yaw)
                {
                    Vector3 aim = ideal + new Vector3((float)sloppy.NextDouble() - 0.5f, ((float)sloppy.NextDouble() - 0.5f) * 0.3f, (float)sloppy.NextDouble() - 0.5f);
                    Vector3 grid = BuildRules.Snap(aim, build.Catalog.Get(kind).Snap, yaw);
                    Vector3 at = build.SnapToPieces(wood.gameObject, kind, aim, yaw, grid);
                    float miss = Vector3.Distance(at, ideal);
                    worstMiss = Mathf.Max(worstMiss, miss);
                    Debug.Log($"[House] snapped {build.Catalog.Get(kind).DisplayName}: aimed {Vector3.Distance(aim, ideal):F2} m off, placed {miss:F3} m from where it belongs");
                    Place(kind, at, yaw);
                }
                Vector3 benchSpot = new Vector3(cell.x + 5f, 0f, cell.z - 2f);
                benchSpot.y = Chopper.World.Building.BuildManager.GroundAt(benchSpot, out _);
                Place(5, BuildRules.Snap(benchSpot, PieceSnap.Free, 90f), 90f);
                Place(0, new Vector3(cell.x, floorY, cell.z), 0f);
                PlaceAimed(0, new Vector3(cell.x + 2f, floorY, cell.z), 0f);
                PlaceAimed(0, new Vector3(cell.x, floorY, cell.z + 2f), 0f);
                PlaceAimed(0, new Vector3(cell.x + 2f, floorY, cell.z + 2f), 0f);
                float wallY = floorY + 0.2f;
                float x0 = cell.x, z0 = cell.z;
                PlaceAimed(2, new Vector3(x0, wallY, z0 - 1f), 0f);
                PlaceAimed(1, new Vector3(x0 + 2f, wallY, z0 - 1f), 0f);
                PlaceAimed(1, new Vector3(x0, wallY, z0 + 3f), 0f);
                PlaceAimed(1, new Vector3(x0 + 2f, wallY, z0 + 3f), 0f);
                PlaceAimed(1, new Vector3(x0 - 1f, wallY, z0), 90f);
                PlaceAimed(1, new Vector3(x0 - 1f, wallY, z0 + 2f), 90f);
                PlaceAimed(1, new Vector3(x0 + 3f, wallY, z0), 90f);
                PlaceAimed(1, new Vector3(x0 + 3f, wallY, z0 + 2f), 90f);
                float roofY = wallY + 2f;
                PlaceAimed(3, new Vector3(x0, roofY, z0), 0f);
                PlaceAimed(3, new Vector3(x0 + 2f, roofY, z0), 0f);
                PlaceAimed(3, new Vector3(x0, roofY, z0 + 2f), 180f);
                PlaceAimed(3, new Vector3(x0 + 2f, roofY, z0 + 2f), 180f);
                Debug.Log($"[House] snapping: the worst piece landed {worstMiss:F3} m from where it belongs");
                Vector3 torchAt = new Vector3(x0 - 1.5f, 0f, z0 - 2f);
                torchAt.y = Chopper.World.Building.BuildManager.GroundAt(torchAt, out _);
                Place(6, BuildRules.Snap(torchAt, PieceSnap.Free, 0f), 0f);
                Vector3 fireAt = new Vector3(x0 + 1f, 0f, z0 - 4.5f);
                fireAt.y = Chopper.World.Building.BuildManager.GroundAt(fireAt, out _);
                Place(7, BuildRules.Snap(fireAt, PieceSnap.Free, 0f), 0f);
                Debug.Log($"[House] pieces standing {build.Count}");
                yield return new WaitForSeconds(1f);
                yield return TeleportLocalPlayer(new Vector2(x0 + 6f, z0 - 7f), 320f);
                houseCam.SnapYaw(320f);
                houseCam.DebugSetPitch(14f);
                houseCam.DebugSetDistance(5.5f);
                yield return new WaitForSeconds(1.5f);
                yield return Shot(folder, "house_7_built");
                yield return TeleportLocalPlayer(new Vector2(x0 + 1f, z0 - 3.2f), 0f);
                houseCam.SnapYaw(0f);
                houseCam.DebugSetPitch(4f);
                houseCam.DebugSetDistance(3f);
                yield return new WaitForSeconds(1.2f);
                yield return Shot(folder, "house_7b_doorway");
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Night, 0.3f);
                yield return TeleportLocalPlayer(new Vector2(x0 + 6f, z0 - 7f), 320f);
                houseCam.SnapYaw(320f);
                yield return new WaitForSeconds(2f);
                yield return Shot(folder, "house_7c_built_night");
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.35f);

                // The hammer: a ghost wall over open ground, then the piece list.
                yield return TeleportLocalPlayer(new Vector2(x0 + 6f, z0 + 1f), 90f);
                houseCam.SnapYaw(90f);
                houseCam.DebugSetPitch(22f);
                houseCam.DebugSetDistance(3.5f);
                PlayerBuilder hammer = wood.GetComponent<PlayerBuilder>();
                wood.ServerAddWood(20);
                hammer.Select(1);
                yield return new WaitForSeconds(1.2f);
                Debug.Log($"[House] ghost {hammer.Kind} valid {hammer.AimValid} ({hammer.AimResult})");
                yield return Shot(folder, "house_8_ghost");
                ui.DebugShow(10);
                yield return new WaitForSeconds(0.6f);
                yield return Shot(folder, "house_9_build_menu");
                ui.DebugShow(0);
                hammer.SetActive(false);
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperHut") >= 0)
            {
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.4f);
                WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
                yield return new WaitForSeconds(7f);
                ui.DebugShow(0);
                GameUi.HudHidden = true;
                PlayerCamera hutCam = wood.GetComponent<PlayerCamera>();
                // Broken: a ruin. Then the repair swaps in the whole house.
                yield return TeleportLocalPlayer(new Vector2(4f, 22f), 195f);
                hutCam.SnapYaw(195f);
                hutCam.DebugSetPitch(16f);
                hutCam.DebugSetDistance(7f);
                yield return new WaitForSeconds(2.5f);
                hutCam.SnapYaw(195f);
                yield return null;
                LogWalls("before repair");
                yield return Shot(folder, "hut_0_broken");
                yield return TeleportLocalPlayer(new Vector2(10f, -6f), 300f);
                hutCam.SnapYaw(300f);
                hutCam.DebugSetPitch(14f);
                hutCam.DebugSetDistance(5f);
                yield return new WaitForSeconds(1.5f);
                yield return Shot(folder, "hut_0a_ruin_close");
                yield return TeleportLocalPlayer(new Vector2(4f, 22f), 195f);
                hutCam.SnapYaw(195f);
                hutCam.DebugSetPitch(16f);
                hutCam.DebugSetDistance(7f);
                yield return new WaitForSeconds(1.5f);
                Hut.Instance.ServerSetRepaired(true);
                yield return new WaitForSeconds(1.2f);
                LogWalls("1.2 s after repair");
                yield return Shot(folder, "hut_0b_repaired");
                (string name, Vector2 at, float yaw, float pitch, float distance)[] hutViews =
                {
                    ("hut_1_yard", new Vector2(4f, 12.5f), 200f, 12f, 6f),
                    ("hut_2_doorway", new Vector2(0f, 8f), 180f, 6f, 3.2f),
                    ("hut_3_inside_bed", new Vector2(1.5f, 1.5f), 215f, 10f, 2.6f),
                    ("hut_4_workbench", new Vector2(1f, 0.6f), 90f, 14f, 2.4f),
                    ("hut_5_from_back", new Vector2(-1f, -2f), 20f, 8f, 2.6f),
                };
                foreach (var view in hutViews)
                {
                    yield return TeleportLocalPlayer(view.at, view.yaw);
                    hutCam.SnapYaw(view.yaw);
                    hutCam.DebugSetPitch(view.pitch);
                    hutCam.DebugSetDistance(view.distance);
                    yield return new WaitForSeconds(1.5f);
                    // A stray Tab from the desktop (Alt+Tab) once left the Tab screen over these shots.
                    ui.DebugShow(0);
                    yield return Shot(folder, view.name);
                }
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Night, 0.4f);
                yield return TeleportLocalPlayer(new Vector2(1.5f, 1.5f), 215f);
                hutCam.SnapYaw(215f);
                yield return new WaitForSeconds(2f);
                ui.DebugShow(0);
                yield return Shot(folder, "hut_6_inside_night");
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            // Performance probe: frame times in a few places by day, then at night with enemies awake.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperPerf") >= 0)
            {
                Hut.Instance.ServerSetRepaired(true);
                // Uncapped, so the numbers show the real cost and not the 60 fps vsync limit. Saved settings are untouched.
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                ui.DebugShow(0);
                yield return new WaitForSeconds(8f);
                ui.DebugShow(0);
                Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
                (string name, Vector2 where, float yaw)[] spots =
                {
                    ("hut", new Vector2(0f, 12f), 180f),
                    ("pond", gen.PondCenter + Vector2.right * 26f, 270f),
                    ("earth forest", new Vector2(170f, 0f), 90f),
                    ("water pass", new Vector2(0f, 118f), 0f),
                };
                foreach (var spot in spots)
                {
                    yield return TeleportLocalPlayer(spot.where, spot.yaw);
                    yield return new WaitForSeconds(1.5f);
                    yield return MeasureFrames(spot.name, 5f);
                }

                // A run through ground nobody has seen yet, the way a player explores once the world is built: new grass
                // cells, trees and plants come into view every frame. Hitches here are what a player feels.
                yield return TeleportLocalPlayer(new Vector2(0f, -30f), 180f);
                yield return new WaitForSeconds(1.5f);
                PlayerWood.Local.StartCoroutine(RunAcross(new Vector2(0f, -30f), new Vector2(0f, -285f), 9f));
                yield return MeasureFrames("running to the far forest", 30f);

                // Who allocates: switch the suspects off one at a time at the hut and measure again.
                yield return TeleportLocalPlayer(new Vector2(0f, 12f), 180f);
                yield return new WaitForSeconds(1.5f);
                yield return MeasureFrames("hut again", 3f);

                // What the GPU spends at the hut: each graphics option dropped one at a time, then put back. Only the
                // live pipeline asset and camera change; the saved settings are not touched.
                var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                Camera view = Camera.main;
                var viewData = view != null ? view.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
                if (pipeline != null)
                {
                    int msaa = pipeline.msaaSampleCount;
                    float scale = pipeline.renderScale;
                    float shadowDistance = pipeline.shadowDistance;
                    int shadowResolution = pipeline.mainLightShadowmapResolution;
                    int cascades = pipeline.shadowCascadeCount;
                    var smaa = viewData != null ? viewData.antialiasing : UnityEngine.Rendering.Universal.AntialiasingMode.None;
                    bool postFx = viewData != null && viewData.renderPostProcessing;
                    Debug.Log($"[Perf] settings: msaa {msaa}, render scale {scale:F2}, shadows {shadowResolution} px to {shadowDistance:F0} m in {cascades} cascades, aa {smaa}, post {postFx}, screen {Screen.width}x{Screen.height}");

                    pipeline.msaaSampleCount = 1;
                    yield return new WaitForSeconds(0.5f);
                    yield return MeasureFrames("no msaa", 3f);
                    pipeline.msaaSampleCount = msaa;

                    if (viewData != null)
                    {
                        viewData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
                        yield return new WaitForSeconds(0.5f);
                        yield return MeasureFrames("no smaa", 3f);
                        viewData.antialiasing = smaa;
                        viewData.renderPostProcessing = false;
                        yield return new WaitForSeconds(0.5f);
                        yield return MeasureFrames("no post processing", 3f);
                        viewData.renderPostProcessing = postFx;
                    }

                    pipeline.mainLightShadowmapResolution = 2048;
                    pipeline.shadowDistance = 100f;
                    yield return new WaitForSeconds(0.5f);
                    yield return MeasureFrames("medium shadows", 3f);
                    pipeline.shadowDistance = 0f;
                    yield return new WaitForSeconds(0.5f);
                    yield return MeasureFrames("no shadows", 3f);
                    pipeline.mainLightShadowmapResolution = shadowResolution;
                    pipeline.shadowDistance = shadowDistance;

                    pipeline.renderScale = 0.75f;
                    yield return new WaitForSeconds(0.5f);
                    yield return MeasureFrames("render scale 0.75", 3f);
                    pipeline.renderScale = scale;

                    if (view != null)
                    {
                        float far = view.farClipPlane;
                        Debug.Log($"[Perf] camera far clip {far:F0} m");
                        view.farClipPlane = 220f;
                        yield return new WaitForSeconds(0.5f);
                        yield return MeasureFrames("far clip 220 m", 3f);
                        view.farClipPlane = far;
                    }
                    Terrain land = Terrain.activeTerrain;
                    if (land != null)
                    {
                        float error = land.heightmapPixelError;
                        land.heightmapPixelError = 6f;
                        yield return new WaitForSeconds(0.5f);
                        yield return MeasureFrames("terrain pixel error 6 (was " + error.ToString("F0") + ")", 3f);
                        land.heightmapPixelError = error;
                    }

                    // Shadow casters: how many renderers draw into the shadow map, by kind.
                    int casters = 0, grassCasters = 0;
                    foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                    {
                        if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off) continue;
                        casters++;
                        if (r.name.ToLowerInvariant().Contains("grass")) grassCasters++;
                    }
                    Debug.Log($"[Perf] shadow casting renderers: {casters} (grass {grassCasters})");

                    // Where the triangles are: every visible mesh renderer, summed by mesh name, biggest first.
                    var byMesh = new System.Collections.Generic.Dictionary<string, (long tris, int count)>();
                    foreach (MeshFilter filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude))
                    {
                        Renderer r = filter.GetComponent<Renderer>();
                        Mesh m = filter.sharedMesh;
                        if (r == null || m == null || !r.enabled || !r.isVisible) continue;
                        long tris = 0;
                        for (int sub = 0; sub < m.subMeshCount; sub++) tris += m.GetIndexCount(sub) / 3;
                        byMesh.TryGetValue(m.name, out var entry);
                        byMesh[m.name] = (entry.tris + tris, entry.count + 1);
                    }
                    foreach (SkinnedMeshRenderer skin in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Exclude))
                    {
                        if (skin.sharedMesh == null || !skin.isVisible) continue;
                        long tris = 0;
                        for (int sub = 0; sub < skin.sharedMesh.subMeshCount; sub++) tris += skin.sharedMesh.GetIndexCount(sub) / 3;
                        byMesh.TryGetValue("skinned " + skin.sharedMesh.name, out var entry);
                        byMesh["skinned " + skin.sharedMesh.name] = (entry.tris + tris, entry.count + 1);
                    }
                    var ranked = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, (long tris, int count)>>(byMesh);
                    ranked.Sort((x, y) => y.Value.tris.CompareTo(x.Value.tris));
                    long total = 0;
                    foreach (var pair in ranked) total += pair.Value.tris;
                    var top = new System.Text.StringBuilder();
                    for (int i = 0; i < Mathf.Min(20, ranked.Count); i++)
                        top.Append($"{ranked[i].Key} x{ranked[i].Value.count} = {ranked[i].Value.tris / 1000}k; ");
                    Debug.Log($"[Perf] visible mesh triangles {total / 1000}k: {top}");
                    yield return new WaitForSeconds(0.5f);
                }
                (string name, System.Type type)[] suspects =
                {
                    ("hud", typeof(GameUi)), ("grass carpet", typeof(GrassCarpet)), ("grass field", typeof(GrassField)),
                    ("zone atmosphere", typeof(ZoneAtmosphere)), ("zone tracker", typeof(ZoneTracker)), ("water", typeof(WaterSurface)),
                    ("day night", typeof(DayNightCycle)), ("herbs", typeof(HerbManager)), ("branches", typeof(BranchManager)),
                };
                foreach (var suspect in suspects)
                {
                    var found = UnityEngine.Object.FindObjectsByType(suspect.type, FindObjectsInactive.Exclude);
                    foreach (UnityEngine.Object o in found) ((Behaviour)o).enabled = false;
                    yield return MeasureFrames("without " + suspect.name + " (" + found.Length + ")", 3f);
                    foreach (UnityEngine.Object o in found) ((Behaviour)o).enabled = true;
                }
                yield return TeleportLocalPlayer(new Vector2(0f, 20f), 0f);
                for (int i = 0; i < 16; i++)
                {
                    float angle = i / 16f * Mathf.PI * 2f;
                    Vector3 at = new Vector3(Mathf.Cos(angle) * 18f, 0f, 20f + Mathf.Sin(angle) * 18f);
                    Terrain ground = Terrain.activeTerrain;
                    at.y = ground.SampleHeight(at) + ground.transform.position.y;
                    EnemyDirector.Instance.ServerSpawn((Element)(i % 5), (TreeSize)(i % 3), false, at);
                }
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Night, 0.2f);
                wood.GetComponent<PlayerCombat>().ServerSetGod(true);
                yield return new WaitForSeconds(6f);
                yield return MeasureFrames("night, " + EnemyDirector.CountActive() + " enemies awake", 6f);
                WeatherSystem.Instance.ServerSet(WeatherKind.Rain);
                yield return new WaitForSeconds(4f);
                yield return MeasureFrames("night in rain", 5f);
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.35f);
                yield return new WaitForSeconds(3f);
                yield return MeasureFrames("day in rain", 5f);
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            // Inventory tour: every page of the Tab screen with a bag of things, next to the hut.
            // Mine tour (-chopperMine): every mine portal from outside, then down the first mine: the chambers, a vein
            // glowing, strikes timed to the glow (one picture mid swing), the find, the Mining page, and back out.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperMine") >= 0)
            {
                Hut.Instance.ServerSetRepaired(true);
                wood.GetComponent<PlayerAxe>().ServerSetHasAxe(true);
                wood.GetComponent<PlayerCombat>().ServerSetGod(true);
                yield return new WaitForSeconds(5f);
                ui.DebugShow(0);
                GameUi.HudHidden = false;
                var mines = Chopper.World.Generation.MineWorld.Plans;
                Debug.Log($"[Mine] mines {mines.Count}, veins {Chopper.World.Generation.MineWorld.Veins.Count}");
                PlayerCamera mineCam = wood.GetComponent<PlayerCamera>();
                for (int m = 0; m < mines.Count; m++)
                {
                    var plan = mines[m];
                    Vector3 front = plan.DoorSpot + Quaternion.Euler(0f, plan.Site.Yaw, 0f) * Vector3.forward * 5f;
                    Debug.Log($"[Mine] {plan.Name} at {plan.Site.Position} yaw {plan.Site.Yaw:F0}, {plan.Rooms.Count} chambers, {plan.Veins.Count} veins");
                    yield return TeleportLocalPlayer(new Vector2(front.x, front.z), plan.Site.Yaw + 180f);
                    mineCam.SnapYaw(plan.Site.Yaw + 180f);
                    mineCam.DebugSetDistance(4.5f);
                    yield return new WaitForSeconds(1.5f);
                    ui.DebugShow(0); yield return Shot(folder, $"mine_{m}_portal");
                }

                // Down the first mine through its portal, as a player would.
                var first = mines[0];
                yield return TeleportLocalPlayer(new Vector2(first.DoorSpot.x, first.DoorSpot.z), first.Site.Yaw + 180f);
                yield return new WaitForSeconds(0.6f);
                ui.DebugShow(0); yield return Shot(folder, "mine_door_prompt");
                PlayerMining miner = wood.GetComponent<PlayerMining>();
                miner.TryInteract();
                yield return new WaitForSeconds(2f);
                Debug.Log($"[Mine] after the portal: at {wood.transform.position:F1}, in mine {Chopper.World.Generation.MineWorld.MineAt(wood.transform.position)}");
                mineCam.DebugSetDistance(3.2f);
                ui.DebugShow(0); yield return Shot(folder, "mine_inside_arrival");
                for (int room = 1; room < first.Rooms.Count; room++)
                {
                    Vector3 c = first.RoomCenter(room);
                    wood.transform.position = c + Vector3.up * 0.1f;
                    var nt = wood.GetComponent<FishNet.Component.Transforming.NetworkTransform>();
                    if (nt != null) nt.Teleport();
                    mineCam.SnapYaw(room * 90f);
                    yield return new WaitForSeconds(0.8f);
                    ui.DebugShow(0); yield return Shot(folder, $"mine_chamber_{room}");
                }

                // The first vein of this mine: stand in front of it, face it, and strike on the glow.
                int vein = -1;
                for (int i = 0; i < Chopper.World.Generation.MineWorld.Veins.Count && vein < 0; i++)
                    if (Chopper.World.Generation.MineWorld.Veins[i].Mine == first.Index) vein = i;
                var v = Chopper.World.Generation.MineWorld.Veins[vein].Vein;
                Vector3 stand = v.Position + v.Normal * 1.3f;
                stand.y = Chopper.World.Generation.MinePlan.FloorY + 0.1f;
                float faceYaw = Mathf.Atan2(-v.Normal.x, -v.Normal.z) * Mathf.Rad2Deg;
                wood.transform.SetPositionAndRotation(stand, Quaternion.Euler(0f, faceYaw, 0f));
                var nt2 = wood.GetComponent<FishNet.Component.Transforming.NetworkTransform>();
                if (nt2 != null) nt2.Teleport();
                mineCam.SnapYaw(faceYaw - 35f);
                mineCam.DebugSetDistance(2.6f);
                yield return new WaitForSeconds(1f);
                // Wait for the glow's peak, picture it, then strike on peaks until the find comes out.
                while (MiningRules.Glint(MiningClock.Now, vein) < 0.95f) yield return null;
                ui.DebugShow(0); yield return QuickShot(folder, "mine_vein_glow");
                Vector3 aim = Quaternion.Euler(0f, faceYaw, 0f) * Vector3.forward;
                int strikes = 0;
                for (int tries = 0; tries < 12 && !Chopper.World.Generation.MineWorld.SpentHere(vein); tries++)
                {
                    float wait = 0f;
                    while (MiningRules.Glint(MiningClock.Now, vein) < 0.9f && wait < 3f) { wait += Time.deltaTime; yield return null; }
                    yield return new WaitForSeconds(0.05f);
                    bool used = miner.TryStrike(aim);
                    strikes++;
                    Debug.Log($"[Mine] strike {strikes}: used {used}, word '{miner.CueWord}'");
                    if (strikes == 1)
                    {
                        yield return new WaitForSeconds(0.22f);
                        ui.DebugShow(0); yield return QuickShot(folder, "mine_swing");
                    }
                    yield return new WaitForSeconds(0.8f);
                }
                yield return new WaitForSeconds(0.3f);
                Debug.Log($"[Mine] vein spent here {Chopper.World.Generation.MineWorld.SpentHere(vein)}, cue '{miner.CueWord}'");
                ui.DebugShow(0); yield return Shot(folder, "mine_found");
                int carried = 0;
                for (int k = 0; k < MiningRules.Kinds; k++) carried += miner.FindCount((MineFindKind)k);
                Debug.Log($"[Mine] finds carried {carried}");

                ui.DebugShow(2);
                ui.DebugInventoryPage(4);
                yield return new WaitForSeconds(0.6f);
                yield return Shot(folder, "mine_page");
                ui.DebugInventoryPage(1);
                yield return new WaitForSeconds(0.5f);
                yield return Shot(folder, "mine_bag");
                ui.DebugShow(0);

                // Out by the ladder.
                wood.transform.position = first.ExitSpot + Vector3.up * 0.1f;
                var nt3 = wood.GetComponent<FishNet.Component.Transforming.NetworkTransform>();
                if (nt3 != null) nt3.Teleport();
                yield return new WaitForSeconds(0.6f);
                ui.DebugShow(0); yield return Shot(folder, "mine_ladder_prompt");
                miner.TryInteract();
                yield return new WaitForSeconds(2f);
                Debug.Log($"[Mine] after the ladder: at {wood.transform.position:F1}, distance to the portal {Vector3.Distance(wood.transform.position, first.DoorSpot):F1}");
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperInventory") >= 0)
            {
                Hut.Instance.ServerSetRepaired(true);
                wood.GetComponent<PlayerAxe>().ServerSetHasAxe(true);
                ui.DebugShow(0);
                yield return new WaitForSeconds(7f);
                ui.DebugShow(0);
                wood.ServerAddWood(18);
                inventory.ServerGrant(2, 2);
                inventory.ServerAddHerbs(4);
                inventory.ServerAddFabric(3);
                inventory.ServerAddRelics(5, 4);
                if (Chopper.World.Generation.CryptWorld.Plans.Count > 0) inventory.ServerAddEssence(Chopper.World.Generation.CryptWorld.Plans[0].Site.Zone, 3);
                PlayerFishing fishing = wood.GetComponent<PlayerFishing>();
                int bag = FishingRules.Add(FishingRules.Add(0, FishKind.Perch, 3), FishKind.Pike, 1);
                fishing.ServerLoad(1, 1, bag, 22);
                yield return TeleportLocalPlayer(new Vector2(0f, 9f), 180f);
                yield return new WaitForSeconds(1f);
                yield return Shot(folder, "inv_0_hud_hut");
                ui.DebugShow(2);
                for (int page = 0; page < 6; page++)
                {
                    ui.DebugInventoryPage(page);
                    yield return new WaitForSeconds(0.5f);
                    yield return Shot(folder, "inv_" + (page + 1) + "_page");
                }
                ui.DebugShow(0);
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            // Weather tour: the hub in sun, cloud and rain, and how the frame time holds up in the rain.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperWeather") >= 0)
            {
                Hut.Instance.ServerSetRepaired(true);
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.4f);
                ui.DebugShow(0);
                yield return new WaitForSeconds(7f);
                ui.DebugShow(0);
                PlayerMotor weatherMotor = wood.GetComponent<PlayerMotor>();
                PlayerCamera weatherCam = wood.GetComponent<PlayerCamera>();
                int weatherFailures = 0;
                void WeatherCheck(bool ok, string what)
                {
                    Debug.Log("[Weather] " + (ok ? "ok   " : "FAIL ") + what);
                    if (!ok) weatherFailures++;
                }
                ParticleSystem RainSystem()
                {
                    GameObject rainObject = GameObject.Find("Rain");
                    return rainObject != null ? rainObject.GetComponent<ParticleSystem>() : null;
                }

                // Sun, cloud and rain in the open meadow: the look, the wind and the rain itself.
                yield return TeleportLocalPlayer(new Vector2(-30f, -30f), 45f);
                float lastWind = 0f;
                foreach (WeatherKind kind in new[] { WeatherKind.Sunny, WeatherKind.Cloudy, WeatherKind.Rain })
                {
                    WeatherSystem.Instance.ServerSet(kind);
                    yield return new WaitForSeconds(0.3f);
                    WeatherSystem.Instance.DebugSnapLook();
                    yield return new WaitForSeconds(2.5f);
                    float wind = Shader.GetGlobalFloat("_ChopperWind");
                    ParticleSystem rain = RainSystem();
                    int drops = rain != null ? rain.particleCount : 0;
                    float sun = RenderSettings.sun != null ? RenderSettings.sun.intensity : -1f;
                    Debug.Log($"[Weather] {WeatherRules.Name(kind)}: wind {wind:0.00}, rain drops {drops}, fog {RenderSettings.fogDensity:0.0000}, sun {sun:0.00}");
                    WeatherCheck(wind > lastWind, $"{WeatherRules.Name(kind)} blows harder than the weather before ({lastWind:0.00} to {wind:0.00})");
                    WeatherCheck(kind == WeatherKind.Rain ? drops > 500 : drops == 0, $"{WeatherRules.Name(kind)}: {drops} rain drops");
                    lastWind = wind;
                    yield return Shot(folder, "weather_" + WeatherRules.Name(kind).ToLowerInvariant());
                }

                // The rain slants along the wind.
                ParticleSystem slant = RainSystem();
                if (slant != null)
                {
                    var drops = new ParticleSystem.Particle[slant.particleCount];
                    int alive = slant.GetParticles(drops);
                    Vector3 sum = Vector3.zero;
                    for (int i = 0; i < alive; i++) sum += drops[i].totalVelocity;
                    Vector2 flat = new Vector2(sum.x, sum.z).normalized;
                    WeatherCheck(Vector2.Dot(flat, WeatherRules.WindDirection.normalized) > 0.9f, $"the rain slants with the wind ({flat.x:0.00}, {flat.y:0.00})");
                }

                // Standing in the open in the rain soaks you.
                weatherMotor.DebugSetWet(0f);
                yield return new WaitForSeconds(1.5f);
                WeatherCheck(weatherMotor.IsWet, $"rain in the open soaks the player ({weatherMotor.WetSeconds:0} s)");

                // Inside the hut: a roof overhead, no drops in the room, and no soaking.
                yield return TeleportLocalPlayer(new Vector2(1.5f, 1.5f), 215f);
                weatherCam.SnapYaw(215f);
                weatherCam.DebugSetPitch(10f);
                weatherCam.DebugSetDistance(2.6f);
                weatherMotor.DebugSetWet(0f);
                yield return new WaitForSeconds(3f);
                Vector3 inside = wood.transform.position;
                bool warm = Campfire.IsWarmAt(inside);
                WeatherCheck(WeatherSystem.Sheltered(inside, wood.transform), "the roof of the hut shelters the player");
                WeatherCheck(!weatherMotor.IsWet, $"no soaking under the roof (wet {weatherMotor.WetSeconds:0} s, by the fire {warm})");
                ParticleSystem roofRain = RainSystem();
                int indoorDrops = 0;
                if (roofRain != null)
                {
                    var drops = new ParticleSystem.Particle[roofRain.particleCount];
                    int alive = roofRain.GetParticles(drops);
                    for (int i = 0; i < alive; i++)
                    {
                        Vector3 p = drops[i].position;
                        if (Mathf.Abs(p.x - inside.x) < 2.5f && Mathf.Abs(p.z - inside.z) < 2.5f && p.y < inside.y + 2.4f) indoorDrops++;
                    }
                }
                WeatherCheck(indoorDrops == 0, $"no rain falls inside the hut ({indoorDrops} drops in the room)");
                ui.DebugShow(0);
                yield return Shot(folder, "weather_rain_inside_hut");
                WeatherCheck(!WeatherSystem.Sheltered(new Vector3(-30f, inside.y, -30f), wood.transform), "the open meadow has no roof");
                yield return TeleportLocalPlayer(new Vector2(-30f, -30f), 45f);
                yield return new WaitForSeconds(1.5f);
                yield return MeasureFrames("rain", 4f);

                // The air of each forest (the ambient particles setting): Water drizzle and mist, Wind streaks and
                // leaves, Fire embers and ash, Earth spores. The setting is put back afterwards.
                WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
                WeatherSystem.Instance.DebugSnapLook();
                bool ambientBefore = UserSettings.AmbientParticles;
                UserSettings.AmbientParticles = true;
                float hubRadius = TreeManager.Instance.Config.HubRadius;
                (string name, Vector2 at, float yaw)[] airStops =
                {
                    ("air_water", new Vector2(0f, hubRadius + 60f), 0f),
                    ("air_earth", new Vector2(hubRadius + 60f, 0f), 90f),
                    ("air_fire", new Vector2(0f, -(hubRadius + 60f)), 180f),
                    ("air_wind", new Vector2(-(hubRadius + 60f), 0f), 270f),
                };
                ZoneAtmosphere atmosphere = UnityEngine.Object.FindAnyObjectByType<ZoneAtmosphere>();
                foreach (var stop in airStops)
                {
                    yield return TeleportLocalPlayer(stop.at, stop.yaw);
                    weatherCam.SnapYaw(stop.yaw);
                    weatherCam.DebugSetPitch(6f);
                    weatherCam.DebugSetDistance(4f);
                    yield return new WaitForSeconds(4f);
                    int motes = 0;
                    if (atmosphere != null) foreach (ParticleSystem layer in atmosphere.GetComponentsInChildren<ParticleSystem>()) motes += layer.particleCount;
                    WeatherCheck(motes > 20, $"{stop.name}: the air of the zone is moving ({motes} particles)");
                    ui.DebugShow(0);
                    yield return Shot(folder, stop.name);
                }
                UserSettings.AmbientParticles = ambientBefore;
                Debug.Log($"[Weather] done, {weatherFailures} failures");
                Debug.Log("[Shot] done");
                Application.Quit(weatherFailures == 0 ? 0 : 1);
                yield break;
            }

            // Fishing tour: the rod out on the pond bank, a cast, the bite, and a fish on the line.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperFishing") >= 0)
            {
                PlayerFishing fishing = wood.GetComponent<PlayerFishing>();
                Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
                fishing.ServerGrantGear();
                Hut.Instance.ServerSetRepaired(true);
                ui.DebugShow(0);
                yield return new WaitForSeconds(7f);
                ui.DebugShow(0);
                Vector2 bank = gen.PondCenter + Vector2.right * (Chopper.World.Generation.WorldGen.PondOuterRadius + 1.2f);
                yield return TeleportLocalPlayer(bank, 270f);
                yield return new WaitForSeconds(1.5f);
                ui.DebugShow(0);
                yield return Shot(folder, "fish_1_bank");
                // Chopper's hair and beard from the front and the side.
                PlayerCamera cam = wood.GetComponent<PlayerCamera>();
                cam.DebugSetDistance(2.2f);
                cam.SnapYaw(90f);
                yield return new WaitForSeconds(0.6f);
                yield return Shot(folder, "fish_0_head_front");
                cam.SnapYaw(0f);
                yield return new WaitForSeconds(0.6f);
                yield return Shot(folder, "fish_0_head_side");
                cam.DebugSetDistance(4f);
                cam.SnapYaw(270f);
                yield return new WaitForSeconds(0.6f);
                fishing.TryToggle();
                yield return new WaitForSeconds(0.8f);
                yield return Shot(folder, "fish_2_rod_out");
                fishing.ServerForceKind((int)FishKind.Pike);
                fishing.DebugCast(0.7f);
                yield return new WaitForSeconds(1.5f);
                yield return Shot(folder, "fish_3_waiting");
                // Working the lure: gentle twitches draw the school after it.
                for (int i = 0; i < 3; i++)
                {
                    fishing.DebugTwitch();
                    yield return new WaitForSeconds(0.9f);
                }
                yield return Shot(folder, "fish_3b_following");
                fishing.DebugBiteNow();
                yield return new WaitForSeconds(0.2f);
                yield return Shot(folder, "fish_4_bite");
                fishing.DebugHook();
                yield return new WaitForSeconds(0.4f);
                fishing.DebugSetTension(0.7f, 0.5f);
                yield return null;
                yield return Shot(folder, "fish_5_reeling");
                FishSpecies pike = fishing.SpeciesOf(FishKind.Pike);
                fishing.DebugSetTension(0.4f, pike.CalmSeconds + pike.PullSeconds + 0.2f);
                yield return new WaitForSeconds(0.25f);
                yield return Shot(folder, "fish_6_thrashing");
                fishing.DebugSnap();
                yield return new WaitForSeconds(0.3f);
                yield return Shot(folder, "fish_7_snapped");
                // A hotspot, and the cast aim ring.
                FishingHotspots hotspots = FishingHotspots.Instance;
                if (hotspots != null)
                {
                    Vector3 spot = hotspots.Position(0);
                    Vector2 toBank = (new Vector2(spot.x, spot.z) - gen.PondCenter).normalized;
                    if (toBank == Vector2.zero) toBank = Vector2.right;
                    Vector2 standAt = gen.PondCenter + toBank * (Chopper.World.Generation.WorldGen.PondOuterRadius + 1.2f);
                    fishing.TryToggle();
                    yield return TeleportLocalPlayer(standAt, Mathf.Atan2(-toBank.x, -toBank.y) * Mathf.Rad2Deg);
                    yield return new WaitForSeconds(1.2f);
                    yield return Shot(folder, "fish_8_hotspot");
                }
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            // Land tour: wide views with the HUD hidden, for judging the terrain (cliffs, ridges, texture and grass edges).
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperLand") >= 0)
            {
                GameUi.HudHidden = true;
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.35f);
                (string name, Vector2 at, float yaw, float pitch, float distance)[] views =
                {
                    ("land_1_hub_to_ridge", new Vector2(0f, -30f), 45f, 6f, 8f),
                    ("land_2_gorge", new Vector2(0f, 95f), 0f, 4f, 6f),
                    ("land_3_mountain_foot", new Vector2(70f, 70f), 45f, 2f, 5f),
                    ("land_4_path_edges", new Vector2(0f, 40f), 180f, 30f, 10f),
                    ("land_5_forest", new Vector2(150f, 0f), 90f, 8f, 8f),
                };
                foreach (var view in views)
                {
                    yield return TeleportLocalPlayer(view.at, view.yaw);
                    PlayerCamera.Local.SnapYaw(view.yaw);
                    PlayerCamera.Local.DebugSetPitch(view.pitch);
                    PlayerCamera.Local.DebugSetDistance(view.distance);
                    yield return new WaitForSeconds(1.5f);
                    yield return Shot(folder, view.name);
                }
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            // Pickup tour: the prompt beside the crosshair, then a herb knocked loose onto the ground and taken by walking
            // over it.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperPickup") >= 0)
            {
                HerbManager herbs = HerbManager.Instance;
                Vector3 herb = herbs.PositionOf(0);
                ui.DebugShow(0);
                yield return TeleportLocalPlayer(new Vector2(herb.x + 1.6f, herb.z), 270f);
                PlayerCamera.Local.DebugSetDistance(2.6f);
                yield return new WaitForSeconds(1.2f);
                ui.DebugShow(0); yield return Shot(folder, "pickup_1_prompt");
                wood.ServerPickHerb();
                yield return new WaitForSeconds(0.12f);
                ui.DebugShow(0); yield return Shot(folder, "pickup_2_popping");
                yield return new WaitForSeconds(0.1f);
                Debug.Log($"[Pickup] items on the ground {GroundItem.All.Count}, herbs {inventory.Herbs}");
                ui.DebugShow(0); yield return Shot(folder, "pickup_3_on_ground");
                yield return new WaitForSeconds(1f);
                Debug.Log($"[Pickup] after a second: items on the ground {GroundItem.All.Count}, herbs {inventory.Herbs}");
                ui.DebugShow(0); yield return Shot(folder, "pickup_4_taken");
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            // Hold tour (-chopperHold): the axe in the hand, put on the back and drawn again in slow motion, on the back
            // from every side, then one chop into a tree in slow motion. For judging grips, the holster and the chop.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperHold") >= 0)
            {
                Hut.Instance.ServerSetRepaired(true);
                PlayerAxe holdAxe = wood.GetComponent<PlayerAxe>();
                holdAxe.ServerSetHasAxe(true);
                PlayerCamera holdCamera = wood.GetComponent<PlayerCamera>();
                yield return TeleportLocalPlayer(new Vector2(0f, 40f), 0f);
                yield return new WaitForSeconds(4f);
                ui.DebugShow(0);
                (string name, float yaw, float distance)[] handViews = { ("hand_right", 270f, 1.7f), ("hand_front", 180f, 1.9f), ("hand_back_right", 315f, 1.9f) };
                foreach (var view in handViews)
                {
                    holdCamera.SnapYaw(view.yaw);
                    holdCamera.DebugSetDistance(view.distance);
                    yield return new WaitForSeconds(0.7f);
                    ui.DebugShow(0); yield return Shot(folder, $"hold_{view.name}");
                }
                // Close ups of the right hand from four sides, standing, then while running (a virtual stick). The hand is
                // the one holding the axe grip (a name search found another hand_r and framed the hips).
                Transform holdGrip = null;
                foreach (Transform t in wood.GetComponentsInChildren<Transform>(true)) if (t.name == "AxeGrip") holdGrip = t;
                Transform holdHand = holdGrip.parent;
                var closeCamera = new GameObject("CloseCamera").AddComponent<Camera>();
                closeCamera.depth = 50f;
                closeCamera.fieldOfView = 40f;
                closeCamera.nearClipPlane = 0.02f;
                closeCamera.enabled = false;
                (string name, Vector3 from)[] closeViews = { ("out", Vector3.right), ("front", Vector3.forward), ("back", Vector3.back), ("in", new Vector3(-0.4f, 0.2f, 0.9f)) };
                IEnumerator CloseUps(string label)
                {
                    GripReport(wood.transform, holdGrip, label);
                    foreach (var view in closeViews)
                    {
                        Vector3 from = wood.transform.TransformDirection(view.from.normalized);
                        closeCamera.transform.position = holdGrip.position + from * 0.6f + Vector3.up * 0.05f;
                        closeCamera.transform.LookAt(holdGrip.position);
                        closeCamera.enabled = true;
                        yield return QuickShot(folder, $"hold_close_{label}_{view.name}");
                    }
                    closeCamera.enabled = false;
                }
                yield return CloseUps("stand");
                Gamepad holdPad = InputSystem.AddDevice<Gamepad>();
                for (float run = 0f; run < 1.2f; run += Time.deltaTime)
                {
                    InputSystem.QueueStateEvent(holdPad, new GamepadState { leftStick = Vector2.up });
                    yield return null;
                }
                Time.timeScale = 0.05f;
                yield return CloseUps("run");
                Time.timeScale = 1f;
                InputSystem.QueueStateEvent(holdPad, new GamepadState());
                InputSystem.RemoveDevice(holdPad);
                yield return TeleportLocalPlayer(new Vector2(0f, 40f), 0f);
                yield return new WaitForSeconds(1f);

                for (int pass = 0; pass < 2; pass++)
                {
                    string label = pass == 0 ? "holster" : "draw";
                    holdCamera.SnapYaw(300f);
                    holdCamera.DebugSetDistance(2.1f);
                    yield return new WaitForSeconds(0.6f);
                    Time.timeScale = 0.1f;
                    holdAxe.RequestToggleDrawn();
                    for (int i = 0; i < 7; i++)
                    {
                        yield return new WaitForSeconds(0.09f);
                        ui.DebugShow(0); yield return QuickShot(folder, $"hold_{label}_{i}");
                    }
                    Time.timeScale = 1f;
                    yield return new WaitForSeconds(0.8f);
                    Debug.Log($"[Hold] after {label}: drawn {holdAxe.IsDrawn}");
                    if (pass == 1) break;
                    (string name, float yaw, float distance)[] backViews = { ("back", 0f, 1.8f), ("back_left", 45f, 1.6f), ("back_right", 315f, 1.6f), ("side_right", 270f, 1.7f), ("side_left", 90f, 1.7f), ("front", 180f, 2f) };
                    foreach (var view in backViews)
                    {
                        holdCamera.SnapYaw(view.yaw);
                        holdCamera.DebugSetDistance(view.distance);
                        yield return new WaitForSeconds(0.7f);
                        ui.DebugShow(0); yield return Shot(folder, $"hold_onback_{view.name}");
                    }
                }
                // A spot facing a tree, for a chop.
                Vector2 chopSpot = Vector2.zero;
                float chopYaw = 0f;
                bool found = false;
                for (int tries = 0; tries < 400 && !found; tries++)
                {
                    Vector2 candidate = new Vector2(-40f + (tries % 20) * 4f, 20f + (tries / 20) * 4f);
                    for (int turn = 0; turn < 8 && !found; turn++)
                    {
                        Vector3 aim = Quaternion.Euler(0f, turn * 45f, 0f) * Vector3.forward;
                        if (!TreeManager.Instance.ServerFindTree(new Vector3(candidate.x, 0f, candidate.y), aim, out _, out _)) continue;
                        chopSpot = candidate;
                        chopYaw = turn * 45f;
                        found = true;
                    }
                }
                Debug.Log($"[Hold] tree found {found} at {chopSpot} facing {chopYaw}");
                yield return TeleportLocalPlayer(chopSpot, chopYaw);
                holdCamera.SnapYaw(chopYaw);
                yield return new WaitForSeconds(1.5f);
                holdCamera.SnapYaw(chopYaw);
                Time.timeScale = 0.1f;
                bool chopped = holdAxe.TrySwing();
                for (int i = 0; i < 4; i++)
                {
                    yield return new WaitForSeconds(0.06f);
                    Vector3 side = wood.transform.TransformDirection(new Vector3(1f, 0.3f, 0.2f).normalized);
                    closeCamera.transform.position = holdHand.position + side * 1.3f;
                    closeCamera.transform.LookAt(holdHand.position + wood.transform.forward * 0.3f);
                    closeCamera.enabled = true;
                    yield return QuickShot(folder, $"hold_close_chop_{i}");
                }
                closeCamera.enabled = false;
                Time.timeScale = 1f;
                yield return new WaitForSeconds(1.2f);
                Time.timeScale = 0.1f;
                chopped = holdAxe.TrySwing();
                Debug.Log($"[Hold] chop swung {chopped}");
                holdCamera.SnapYaw(chopYaw + 95f);
                holdCamera.DebugSetDistance(3.2f);
                for (int i = 0; i < 8; i++)
                {
                    yield return new WaitForSeconds(0.1f);
                    ui.DebugShow(0); yield return QuickShot(folder, $"hold_chop_{i}");
                }
                Time.timeScale = 1f;
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            // Motion tour (-chopperMotion): every gait filmed from the right side in slow motion, eight frames over about one
            // cycle, and how fast the planted foot slides over the ground at full speed (0 is a clean step; the player's own
            // speed means the feet do not move at all).
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperMotion") >= 0)
            {
                Hut.Instance.ServerSetRepaired(true);
                wood.GetComponent<PlayerCombat>().ServerSetGod(true);
                PlayerAxe motionAxe = wood.GetComponent<PlayerAxe>();
                PlayerMotor motionMotor = wood.GetComponent<PlayerMotor>();
                motionAxe.ServerSetHasAxe(true);
                Animator motionAnimator = wood.GetComponentInChildren<Animator>();
                Transform toesL = motionAnimator.GetBoneTransform(HumanBodyBones.LeftToes);
                Transform toesR = motionAnimator.GetBoneTransform(HumanBodyBones.RightToes);
                if (toesL == null) toesL = motionAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
                if (toesR == null) toesR = motionAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
                var sideCamera = new GameObject("SideCamera").AddComponent<Camera>();
                sideCamera.depth = 50f;
                sideCamera.fieldOfView = 35f;
                sideCamera.enabled = false;
                void FrameSide()
                {
                    Vector3 body = wood.transform.position + Vector3.up * 0.85f;
                    sideCamera.transform.position = body + wood.transform.right * 4.2f + Vector3.up * 0.15f;
                    sideCamera.transform.LookAt(body);
                }
                Gamepad motionPad = InputSystem.AddDevice<Gamepad>();
                (string label, bool axe, float stick, bool sprint, bool crouch, bool jump)[] gaits =
                {
                    ("idle_axe", true, 0f, false, false, false), ("walk_axe", true, 0.3f, false, false, false),
                    ("run_axe", true, 1f, false, false, false), ("sprint_axe", true, 1f, true, false, false),
                    ("idle_bare", false, 0f, false, false, false), ("run_bare", false, 1f, false, false, false),
                    ("crouch_idle", true, 0f, false, true, false), ("crouch_walk", true, 1f, false, true, false),
                    ("jump", true, 1f, false, false, true),
                };
                foreach (var gait in gaits)
                {
                    yield return TeleportLocalPlayer(new Vector2(0f, 30f), 0f);
                    motionMotor.DebugSetCrouch(gait.crouch);
                    if (motionAxe.IsDrawn != gait.axe) motionAxe.RequestToggleDrawn();
                    InputSystem.QueueStateEvent(motionPad, new GamepadState());
                    yield return new WaitForSeconds(1.2f);
                    ui.DebugShow(0);
                    bool sprintPressed = false;
                    float speedSum = 0f, clipSpeedSum = 0f;
                    int speedFrames = 0;
                    // Each foot's height over the player's feet and how fast it moves over the ground; afterwards only the
                    // lower foot of each frame counts, when within 1.5 cm of its lowest (the foot is down), median.
                    var footSamples = new List<(bool left, float height, float speed, float back)>();
                    Vector3 lastLocalL = wood.transform.InverseTransformPoint(toesL.position), lastLocalR = wood.transform.InverseTransformPoint(toesR.position);
                    float cycleStart = -1f, cycleStartTime = 0f;
                    Vector3 lastL = toesL.position, lastR = toesR.position, lastBody = wood.transform.position;
                    for (float t = 0f; t < 2.6f; t += Time.deltaTime)
                    {
                        var state = new GamepadState { leftStick = Vector2.up * gait.stick };
                        // Sprint is a toggle: one press.
                        if (gait.sprint && !sprintPressed && t > 0.1f) { state = state.WithButton(GamepadButton.LeftStick); sprintPressed = true; }
                        InputSystem.QueueStateEvent(motionPad, state);
                        PlayerCamera.Local.SnapYaw(0f);
                        // After the Animator has posed this frame.
                        yield return new WaitForEndOfFrame();
                        Vector3 moved = wood.transform.position - lastBody;
                        moved.y = 0f;
                        if (t > 1.2f && cycleStart < 0f) { cycleStart = motionAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime; cycleStartTime = t; }
                        if (t > 1.2f && Time.deltaTime > 0f)
                        {
                            speedSum += moved.magnitude / Time.deltaTime;
                            speedFrames++;
                            Vector3 clipTravel = motionAnimator.velocity;
                            clipTravel.y = 0f;
                            clipSpeedSum += clipTravel.magnitude;
                            Vector3 stepL = toesL.position - lastL, stepR = toesR.position - lastR;
                            stepL.y = 0f; stepR.y = 0f;
                            Vector3 localL = wood.transform.InverseTransformPoint(toesL.position), localR = wood.transform.InverseTransformPoint(toesR.position);
                            footSamples.Add((true, toesL.position.y - wood.transform.position.y, stepL.magnitude / Time.deltaTime, (lastLocalL.z - localL.z) / Time.deltaTime));
                            footSamples.Add((false, toesR.position.y - wood.transform.position.y, stepR.magnitude / Time.deltaTime, (lastLocalR.z - localR.z) / Time.deltaTime));
                        }
                        lastL = toesL.position; lastR = toesR.position; lastBody = wood.transform.position;
                        lastLocalL = wood.transform.InverseTransformPoint(toesL.position); lastLocalR = wood.transform.InverseTransformPoint(toesR.position);
                    }
                    float lowL = footSamples.Where(f => f.left).Select(f => f.height).DefaultIfEmpty(0f).Min();
                    float lowR = footSamples.Where(f => !f.left).Select(f => f.height).DefaultIfEmpty(0f).Min();
                    // Samples come in pairs (left, right): keep the lower foot of each frame, when it is down.
                    var down = new List<(float height, float speed, float back)>();
                    for (int k = 0; k + 1 < footSamples.Count; k += 2)
                    {
                        var l = footSamples[k];
                        var r = footSamples[k + 1];
                        bool leftDown = l.height - lowL < r.height - lowR;
                        var f = leftDown ? l : r;
                        float above = f.height - (leftDown ? lowL : lowR);
                        if (above <= 0.015f) down.Add((above, f.speed, f.back));
                    }
                    List<float> slides = down.Select(f => f.speed).OrderBy(v => v).ToList();
                    float slide = slides.Count > 0 ? slides[slides.Count / 2] : 0f;
                    List<float> backs = down.Select(f => f.back).OrderBy(v => v).ToList();
                    float footBack = backs.Count > 0 ? backs[backs.Count / 2] : 0f;
                    AnimatorStateInfo loop = motionAnimator.GetCurrentAnimatorStateInfo(0);
                    float cycles = (loop.normalizedTime - cycleStart) / Mathf.Max(0.01f, 2.6f - cycleStartTime);
                    Debug.Log($"[Motion] {gait.label}: speed {speedSum / Mathf.Max(1, speedFrames):F2} m/s, foot on the ground slides {slide:F2} m/s (moves back under the body at {footBack:F2}), Speed param {motionAnimator.GetFloat("Speed"):F2}, "
                              + $"{cycles:F2} cycles/s, root motion {clipSpeedSum / Mathf.Max(1, speedFrames):F2} m/s, state length {loop.length:F2} s, speed x{loop.speed * loop.speedMultiplier:F2}");
                    if (gait.jump)
                    {
                        var state = new GamepadState { leftStick = Vector2.up }.WithButton(GamepadButton.South);
                        InputSystem.QueueStateEvent(motionPad, state);
                        yield return null;
                    }
                    Time.timeScale = 0.05f;
                    for (int i = 0; i < 8; i++)
                    {
                        InputSystem.QueueStateEvent(motionPad, new GamepadState { leftStick = Vector2.up * gait.stick });
                        FrameSide();
                        sideCamera.enabled = true;
                        yield return QuickShot(folder, $"motion_{gait.label}_{i}");
                        yield return new WaitForSeconds(gait.jump ? 0.11f : 0.09f);
                    }
                    sideCamera.enabled = false;
                    Time.timeScale = 1f;
                    InputSystem.QueueStateEvent(motionPad, new GamepadState());
                    if (gait.sprint) { InputSystem.QueueStateEvent(motionPad, new GamepadState().WithButton(GamepadButton.LeftStick)); yield return null; }
                    yield return null;
                }
                motionMotor.DebugSetCrouch(false);
                InputSystem.RemoveDevice(motionPad);
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            // Axe run check (-chopperAxeRun): the axe drawn, a virtual gamepad walks then sprints straight ahead. Every frame
            // the axe's turn in the body's own frame is measured; the run cycle swings it a few degrees a frame, so a turn
            // of more than 25 degrees in one frame is a flip (playtesters saw the axe flip flop while running).
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperAxeRun") >= 0)
            {
                Hut.Instance.ServerSetRepaired(true);
                wood.GetComponent<PlayerCombat>().ServerSetGod(true);
                yield return TeleportLocalPlayer(new Vector2(0f, 30f), 0f);
                PlayerAxe runAxe = wood.GetComponent<PlayerAxe>();
                runAxe.ServerSetHasAxe(true);
                if (!runAxe.IsDrawn) runAxe.RequestToggleDrawn();
                yield return new WaitForSeconds(5f);
                ui.DebugShow(0);
                Transform axeGrip = null;
                foreach (Transform t in wood.GetComponentsInChildren<Transform>(true)) if (t.name == "AxeGrip") axeGrip = t;
                PlayerCamera runCamera = wood.GetComponent<PlayerCamera>();
                Gamepad pad = InputSystem.AddDevice<Gamepad>();
                (string label, bool sprint, float seconds)[] legs = { ("walk", false, 4f), ("sprint", true, 5f) };
                int shot = 0;
                foreach (var leg in legs)
                {
                    float start = Time.time;
                    Quaternion last = Quaternion.Inverse(wood.transform.rotation) * axeGrip.rotation;
                    Quaternion lastArm = Quaternion.Inverse(wood.transform.rotation) * axeGrip.parent.parent.rotation;
                    int flips = 0, frames = 0;
                    float worst = 0f, sum = 0f, nextShot = start + 1.5f;
                    // Where the head points in the body's frame (z forward): the average and the extremes over the stride.
                    HeldItemFrame runFrame = axeGrip.GetComponent<HeldItemFrame>();
                    Vector3 headSum = Vector3.zero;
                    float headMinZ = 1f, headMaxZ = -1f;
                    while (Time.time - start < leg.seconds)
                    {
                        var state = new GamepadState { leftStick = Vector2.up };
                        InputSystem.QueueStateEvent(pad, leg.sprint ? state.WithButton(GamepadButton.LeftStick) : state);
                        runCamera.SnapYaw(0f);
                        yield return null;
                        Quaternion now = Quaternion.Inverse(wood.transform.rotation) * axeGrip.rotation;
                        float turn = Quaternion.Angle(last, now);
                        last = now;
                        Quaternion armNow = Quaternion.Inverse(wood.transform.rotation) * axeGrip.parent.parent.rotation;
                        float armTurn = Quaternion.Angle(lastArm, armNow);
                        lastArm = armNow;
                        if (Time.time - start < 0.5f) continue;
                        frames++;
                        sum += turn;
                        Vector3 headBody = wood.transform.InverseTransformDirection(axeGrip.rotation * runFrame.Head);
                        headSum += headBody;
                        headMinZ = Mathf.Min(headMinZ, headBody.z);
                        headMaxZ = Mathf.Max(headMaxZ, headBody.z);
                        worst = Mathf.Max(worst, turn);
                        // A flip is the axe turning in the hand, not with it: the run swings the forearm fast, so the axe
                        // turning with the forearm is only the arm's swing.
                        if (turn > armTurn + 15f)
                        {
                            flips++;
                            Animator runAnimator = wood.GetComponentInChildren<Animator>();
                            string states = "";
                            for (int layer = 0; layer < runAnimator.layerCount; layer++)
                                states += $" L{layer} {runAnimator.GetCurrentAnimatorStateInfo(layer).shortNameHash} w{runAnimator.GetLayerWeight(layer):F2} trans {runAnimator.IsInTransition(layer)}"
                                          + $" next {runAnimator.GetNextAnimatorStateInfo(layer).shortNameHash};";
                            foreach (AnimatorControllerParameter parameter in runAnimator.parameters)
                                if (parameter.type == AnimatorControllerParameterType.Bool) states += $" {parameter.name}={runAnimator.GetBool(parameter.name)}";
                                else if (parameter.type == AnimatorControllerParameterType.Float) states += $" {parameter.name}={runAnimator.GetFloat(parameter.name):F2}";
                            Debug.Log($"[AxeRun] flip {leg.label} at {Time.time - start:F2} s: {turn:F1} deg (forearm {armTurn:F1}), frame {Time.deltaTime * 1000f:F0} ms,{states}");
                        }
                        if (Time.time >= nextShot && shot < 8)
                        {
                            nextShot += 0.37f;
                            yield return Shot(folder, $"axerun_{shot++}_{leg.label}");
                            // A picture takes a few frames; measure again from here.
                            last = Quaternion.Inverse(wood.transform.rotation) * axeGrip.rotation;
                            lastArm = Quaternion.Inverse(wood.transform.rotation) * axeGrip.parent.parent.rotation;
                        }
                    }
                    Debug.Log($"[AxeRun] {leg.label}: frames {frames}, flips {flips}, biggest turn {worst:F1} deg, mean {sum / Mathf.Max(1, frames):F1} deg, head average {(headSum / Mathf.Max(1, frames)).normalized:F2}, head forward from {headMinZ:F2} to {headMaxZ:F2}");
                }
                InputSystem.QueueStateEvent(pad, new GamepadState());
                InputSystem.RemoveDevice(pad);
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            // Look tour: Chopper close up from every side, to check the outfit has no see-through gaps.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperLook") >= 0)
            {
                ui.DebugShow(0);
                yield return TeleportLocalPlayer(new Vector2(0f, 40f), 0f);
                yield return new WaitForSeconds(1.5f);
                (string name, float yaw, float distance)[] views =
                {
                    ("front", 180f, 2.2f), ("front_left", 135f, 1.8f), ("side", 90f, 2f), ("back", 0f, 2f), ("back_right", 315f, 1.8f),
                };
                foreach (var view in views)
                {
                    PlayerCamera.Local.SnapYaw(view.yaw);
                    PlayerCamera.Local.DebugSetDistance(view.distance);
                    yield return new WaitForSeconds(0.8f);
                    ui.DebugShow(0); yield return Shot(folder, $"look_{view.name}");
                }

                // Close ups of the collar, neck, shoulders, cuffs and waist from every side: nothing may show through.
                Transform lookNeck = null, lookPelvis = null, lookHandL = null;
                foreach (Transform t in wood.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "neck_01") lookNeck = t;
                    if (t.name == "pelvis") lookPelvis = t;
                    if (t.name == "hand_l") lookHandL = t;
                }
                var lookCamera = new GameObject("LookCloseCamera").AddComponent<Camera>();
                lookCamera.depth = 50f;
                lookCamera.fieldOfView = 40f;
                lookCamera.nearClipPlane = 0.02f;
                (string name, Transform target, Vector3 from, float distance)[] closeLooks =
                {
                    ("collar_front", lookNeck, new Vector3(0f, -0.1f, 1f), 0.55f), ("collar_back", lookNeck, new Vector3(0f, 0.2f, -1f), 0.55f),
                    ("collar_left", lookNeck, new Vector3(-1f, 0.1f, 0.2f), 0.55f), ("collar_right", lookNeck, new Vector3(1f, 0.1f, 0.2f), 0.55f),
                    ("collar_above", lookNeck, new Vector3(0.1f, 1f, 0.5f), 0.6f), ("cuff_left", lookHandL, new Vector3(-0.3f, 0.2f, 1f), 0.45f),
                    ("waist_front", lookPelvis, new Vector3(0f, 0f, 1f), 0.8f), ("waist_back", lookPelvis, new Vector3(0f, 0.1f, -1f), 0.8f)
                };
                foreach (var look in closeLooks)
                {
                    if (look.target == null) continue;
                    Vector3 from = wood.transform.TransformDirection(look.from.normalized);
                    lookCamera.transform.position = look.target.position + from * look.distance;
                    lookCamera.transform.LookAt(look.target.position);
                    lookCamera.enabled = true;
                    yield return new WaitForSeconds(0.3f);
                    yield return Shot(folder, $"look_close_{look.name}");
                }
                UnityEngine.Object.Destroy(lookCamera.gameObject);

                // The axe in the hand, then on the back; then at night with the torch lit, from behind and close from the left.
                PlayerAxe lookAxe = wood.GetComponent<PlayerAxe>();
                lookAxe.ServerSetHasAxe(true);
                yield return new WaitForSeconds(0.5f);
                PlayerCamera.Local.SnapYaw(150f);
                yield return new WaitForSeconds(0.6f);
                ui.DebugShow(0); yield return Shot(folder, "look_axe_drawn");
                // From the front, close: the angle where a handle beside the fist shows most.
                PlayerCamera.Local.SnapYaw(180f);
                PlayerCamera.Local.DebugSetDistance(1.8f);
                yield return new WaitForSeconds(0.8f);
                ui.DebugShow(0); yield return Shot(folder, "look_axe_front");
                // From his right, close: the handle must run through the fist.
                PlayerCamera.Local.SnapYaw(270f);
                PlayerCamera.Local.DebugSetDistance(1.7f);
                yield return new WaitForSeconds(0.8f);
                ui.DebugShow(0); yield return Shot(folder, "look_axe_right");
                PlayerCamera.Local.SnapYaw(300f);
                yield return new WaitForSeconds(0.6f);
                ui.DebugShow(0); yield return Shot(folder, "look_axe_right_front");
                PlayerCamera.Local.DebugSetDistance(3f);
                lookAxe.RequestToggleDrawn();
                yield return new WaitForSeconds(0.6f);
                Debug.Log($"[Look] axe drawn {lookAxe.IsDrawn}");
                PlayerCamera.Local.SnapYaw(0f);
                yield return new WaitForSeconds(0.6f);
                ui.DebugShow(0); yield return Shot(folder, "look_axe_on_back");
                PlayerCamera.Local.SnapYaw(120f);
                yield return new WaitForSeconds(0.6f);
                ui.DebugShow(0); yield return Shot(folder, "look_axe_on_back_side");
                PlayerInventory lookInventory = wood.GetComponent<PlayerInventory>();
                if (lookInventory.Torches == 0) wood.GetComponent<PlayerDevTools>().Execute(DevCommand.GiveTorches, 2);
                yield return new WaitForSeconds(0.3f);
                lookInventory.ServerToggleTorch();
                // By day first, close from the torch side, to see the handle in the fist.
                PlayerCamera.Local.SnapYaw(250f);
                PlayerCamera.Local.DebugSetDistance(1.6f);
                yield return new WaitForSeconds(1f);
                ui.DebugShow(0); yield return Shot(folder, "look_torch_day_close");
                PlayerCamera.Local.SnapYaw(200f);
                yield return new WaitForSeconds(0.6f);
                ui.DebugShow(0); yield return Shot(folder, "look_torch_day_front");
                PlayerCamera.Local.DebugSetDistance(3f);
                for (int i = 0; i < 6 && !DayNightCycle.Instance.IsNight; i++) DayNightCycle.Instance.ServerSkipPhase();
                PlayerCamera.Local.SnapYaw(160f);
                yield return new WaitForSeconds(1.5f);
                Debug.Log($"[Look] torch lit {lookInventory.TorchLit}");
                ui.DebugShow(0); yield return Shot(folder, "look_torch_night");
                PlayerCamera.Local.SnapYaw(250f);
                PlayerCamera.Local.DebugSetDistance(1.6f);
                yield return new WaitForSeconds(0.8f);
                ui.DebugShow(0); yield return Shot(folder, "look_torch_close");
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            // Swim tour: drop into the hub pond and the sea, then log where the body and the camera sit against the
            // surface while floating still and swimming forward.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperSwim") >= 0)
            {
                yield return SwimTour(folder, wood, ui);
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            // Combat tour: a guard, a roll, a swim and an enemy taking a hit, for checking the new moves.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperCombat") >= 0)
            {
                GameConfig cfg = TreeManager.Instance.Config;
                Hut.Instance.ServerSetRepaired(true);
                wood.GetComponent<PlayerAxe>().ServerSetHasAxe(true);
                ui.DebugShow(0);
                yield return TeleportLocalPlayer(new Vector2(0f, 40f), 0f);
                yield return new WaitForSeconds(1f);
                // Taking the axe opens the How to play screen a moment later, which blocks input and the guard.
                ui.DebugShow(0);
                wood.GetComponent<PlayerCombat>().DebugSetBlocking(true);
                yield return new WaitForSeconds(1.2f);
                ui.DebugShow(0); yield return Shot(folder, "combat_1_block");
                // From the front, to see the shield: swing the camera round before the body turns after it.
                wood.GetComponent<PlayerCamera>().SnapYaw(200f);
                ui.DebugShow(0); yield return Shot(folder, "combat_1b_block_front");
                wood.GetComponent<PlayerCamera>().SnapYaw(0f);
                wood.GetComponent<PlayerCombat>().DebugSetBlocking(false);
                Vector3 OnGround(float x, float z) => new Vector3(x, WorldGround.HeightAt(x, z), z);
                // Saplings (they are not removed by day) in each element, so the model of every element can be checked.
                Element[] looks = { Element.Water, Element.Fire, Element.Earth, Element.Wind };
                for (int i = 0; i < looks.Length; i++) EnemyDirector.Instance.ServerSpawn(looks[i], TreeSize.Medium, false, OnGround(-6f + i * 4f, 50f));
                Treant foe = EnemyDirector.Instance.ServerSpawn(Element.Fire, TreeSize.Medium, false, OnGround(0f, 46f));
                yield return new WaitForSeconds(0.3f);
                foe.ServerActivate();
                yield return new WaitForSeconds(2.2f);
                ui.DebugShow(0); yield return Shot(folder, "combat_2_enemy_windup");
                // Close, then a step to the side: the creature that stands up where the pine was, mid punch.
                wood.GetComponent<PlayerCamera>().DebugSetDistance(4.2f);
                yield return new WaitForSeconds(0.4f);
                ui.DebugShow(0); yield return Shot(folder, "combat_2b_golem");
                wood.GetComponent<PlayerCamera>().SnapYaw(55f);
                wood.GetComponent<PlayerCamera>().DebugSetDistance(5.5f);
                yield return new WaitForSeconds(0.5f);
                ui.DebugShow(0); yield return Shot(folder, "combat_2c_golem_side");
                wood.GetComponent<PlayerCamera>().SnapYaw(0f);
                wood.GetComponent<PlayerCamera>().DebugSetDistance(8f);
                if (foe != null) foe.ServerHit(0.6f, null, wood, wood.transform.position);
                yield return new WaitForSeconds(0.1f);
                ui.DebugShow(0); yield return Shot(folder, "combat_3_enemy_hit");
                yield return new WaitForSeconds(0.4f);
                if (foe != null) foe.ServerHit(1000f, null, wood, wood.transform.position);
                yield return new WaitForSeconds(0.45f);
                ui.DebugShow(0); yield return Shot(folder, "combat_3b_enemy_falls");
                wood.GetComponent<PlayerCombat>().DebugRoll();
                yield return new WaitForSeconds(0.25f);
                ui.DebugShow(0); yield return Shot(folder, "combat_4_roll");
                yield return TeleportLocalPlayer(new Vector2(60f, -45f), 90f);
                yield return new WaitForSeconds(2f);
                ui.DebugShow(0); yield return Shot(folder, "combat_5_swim");

                // A group: they should spread round the player and take turns, not pile in at once.
                yield return TeleportLocalPlayer(new Vector2(0f, -45f), 0f);
                yield return new WaitForSeconds(1f);
                wood.GetComponent<PlayerCombat>().ServerSetGod(true);
                for (int i = 0; i < 4; i++)
                {
                    Treant member = EnemyDirector.Instance.ServerSpawn(Element.Neutral, TreeSize.Small, false, OnGround((i - 1.5f) * 3f, -33f));
                    yield return null;
                    member.ServerActivate();
                }
                wood.GetComponent<PlayerCamera>().DebugSetDistance(11f);
                for (int t = 0; t < 12; t++)
                {
                    yield return new WaitForSeconds(1f);
                    var line = new System.Text.StringBuilder($"[GroupAI] t={t + 1}s");
                    foreach (Treant member in Treant.All)
                    {
                        if (member == null || !member.IsActive) continue;
                        Vector3 d = member.transform.position - wood.transform.position;
                        d.y = 0f;
                        float bearing = Vector3.SignedAngle(wood.transform.forward, d, Vector3.up);
                        line.Append($" | {d.magnitude:F1} m at {bearing:F0} deg {member.GetComponent<TreantAI>().ModeName}");
                    }
                    Debug.Log(line.ToString());
                    if (t == 4) { ui.DebugShow(0); yield return Shot(folder, "combat_6_group"); }
                }
                foreach (Treant member in new System.Collections.Generic.List<Treant>(Treant.All)) if (member != null) member.ServerDespawn();
                yield return new WaitForSeconds(0.5f);

                // The forest bosses: no separate creature, the night enemy made big (the grove boss and a grown boss
                // tree), each with its element's crown.
                GameConfig bossConfig = TreeManager.Instance.Config;
                Treant warden = EnemyDirector.Instance.ServerWakeWarden(OnGround(0f, -28f), Element.Earth);
                yield return new WaitForSeconds(2.5f);
                wood.GetComponent<PlayerCamera>().SnapYaw(0f);
                wood.GetComponent<PlayerCamera>().DebugSetDistance(9f);
                wood.GetComponent<PlayerCamera>().DebugSetPitch(6f);
                yield return new WaitForSeconds(0.6f);
                Debug.Log($"[Boss] grove boss: active {warden != null && warden.IsActive}, golem {warden != null && warden.ShowsGolem}, height {(warden != null ? warden.CurrentHeight() : 0f):0.0} m, phase {(warden != null ? warden.WardenPhase : 0)}");
                ui.DebugShow(0); yield return Shot(folder, "combat_7_grove_boss");
                if (warden != null) warden.ServerDespawn();
                yield return new WaitForSeconds(0.5f);
                Treant grown = EnemyDirector.Instance.ServerSpawn(Element.Fire, TreeSize.Large, true, OnGround(0f, -30f), bornOffsetDays: bossConfig.BossGrowDays + 1f);
                yield return null;
                if (grown != null) grown.ServerActivate();
                yield return new WaitForSeconds(2.5f);
                Debug.Log($"[Boss] boss tree: active {grown != null && grown.IsActive}, golem {grown != null && grown.ShowsGolem}, height {(grown != null ? grown.CurrentHeight() : 0f):0.0} m");
                ui.DebugShow(0); yield return Shot(folder, "combat_8_boss_tree");
                wood.GetComponent<PlayerCombat>().ServerSetGod(false);
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            // Yard tour (-chopperYard, further down): the start of the game, the ruin, the workbench and the notice board.
            // Crypt tour: an entrance, the rooms, a chest, the workbench rows, the map below ground, and a crushing.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperCrypt") >= 0)
            {
                PlayerHealth health = wood.GetComponent<PlayerHealth>();
                PlayerCrypt traveller = wood.GetComponent<PlayerCrypt>();
                Chopper.World.Generation.CryptPlan plan = Chopper.World.Generation.CryptWorld.Plans[0];
                Vector3 door = plan.ReturnSpot;
                Vector3 outward = Quaternion.Euler(0f, plan.ReturnYaw, 0f) * Vector3.forward;
                yield return TeleportLocalPlayer(new Vector2(door.x + outward.x * 5f, door.z + outward.z * 5f), plan.ReturnYaw + 180f);
                yield return new WaitForSeconds(1.5f);
                yield return Shot(folder, "crypt_1_entrance");
                yield return TeleportExact(plan.DoorSpot + Vector3.up * 0.2f, plan.ReturnYaw + 180f);
                yield return new WaitForSeconds(0.6f);
                yield return Shot(folder, "crypt_2_door_prompt");

                CryptManager.Instance.ServerTryEnter(traveller);
                yield return new WaitForSeconds(2.5f);
                health.ServerHealFull();
                yield return Shot(folder, "crypt_3_first_room");
                PlayerCamera.Local.SnapYaw(plan.ArrivalYaw + 180f);
                yield return new WaitForSeconds(0.8f);
                yield return Shot(folder, "crypt_4_stairs_up");

                for (int id = 0; id < Chopper.World.Generation.CryptWorld.Chests.Count; id++)
                {
                    Chopper.World.Generation.CryptWorld.ChestRef chest = Chopper.World.Generation.CryptWorld.Chests[id];
                    if (chest.Crypt != 0 || !chest.Chest.Grand) continue;
                    Vector3 facing = Quaternion.Euler(0f, chest.Chest.Yaw, 0f) * Vector3.forward;
                    yield return TeleportExact(chest.Chest.Position + facing * 4f + Vector3.up * 0.2f, chest.Chest.Yaw + 180f);
                    yield return new WaitForSeconds(1f);
                    health.ServerHealFull();
                    yield return Shot(folder, "crypt_5_vault");
                    yield return TeleportExact(chest.Chest.Position + facing * 1.4f + Vector3.up * 0.2f, chest.Chest.Yaw + 180f);
                    yield return new WaitForSeconds(0.5f);
                    CryptManager.Instance.ServerTryOpenChest(traveller);
                    yield return new WaitForSeconds(0.8f);
                    health.ServerHealFull();
                    yield return Shot(folder, "crypt_6_loot");
                }

                // The arena under the vault: the archway, a wave, the boss, the reward.
                yield return TeleportExact(plan.ArenaDoorSpot + Vector3.up * 0.2f, plan.VaultReturnYaw + 180f);
                yield return new WaitForSeconds(0.6f);
                yield return Shot(folder, "crypt_6b_arena_door");
                ArenaManager.Instance.ServerTryEnter(traveller);
                yield return new WaitForSeconds(3.5f);
                health.ServerHealFull();
                yield return Shot(folder, "crypt_6c_arena_wave");
                float arenaDeadline = Time.time + 40f;
                while (Time.time < arenaDeadline && !ArenaManager.Instance.BossUp(0))
                {
                    health.ServerHealFull();
                    foreach (Treant t in new System.Collections.Generic.List<Treant>(Treant.All)) if (t != null && t.Arena == 0 && t.IsActive) t.ServerHit(1000f, null, wood);
                    yield return new WaitForSeconds(0.4f);
                }
                yield return new WaitForSeconds(2f);
                health.ServerHealFull();
                PlayerCamera.Local.SnapYaw(0f);
                yield return Shot(folder, "crypt_6d_arena_boss");
                foreach (Treant t in new System.Collections.Generic.List<Treant>(Treant.All)) if (t != null && t.Arena == 0) t.ServerHit(10000f, null, wood);
                yield return new WaitForSeconds(1.2f);
                yield return Shot(folder, "crypt_6e_arena_cleared");

                ui.DebugShow(2);
                yield return new WaitForSeconds(0.5f);
                yield return Shot(folder, "crypt_7_inventory");
                ui.DebugShow(3);
                yield return new WaitForSeconds(0.5f);
                yield return Shot(folder, "crypt_8_map_below");
                ui.DebugShow(0);

                yield return TeleportExact(plan.ExitSpot + Vector3.up * 0.2f, plan.ArrivalYaw + 180f);
                yield return new WaitForSeconds(0.5f);
                health.ServerHealFull();
                yield return Shot(folder, "crypt_9_exit_prompt");
                CryptManager.Instance.ServerTryExit(traveller);
                yield return new WaitForSeconds(2.5f);
                yield return Shot(folder, "crypt_10_back_up");

                health.ServerCrush();
                yield return new WaitForSeconds(1.5f);
                yield return Shot(folder, "crypt_11_crushed");
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperYard") >= 0)
            {
                yield return TeleportLocalPlayer(new Vector2(0f, 11.2f), 180f);
                yield return new WaitForSeconds(1.2f);
                yield return Shot(folder, "yard_1_broken_gate");

                Hut.Instance.ServerSetRepaired(true);
                Vector3 stand = Workbench.All[0].transform.position;
                yield return TeleportLocalPlayer(new Vector2(stand.x - 1.8f, stand.z), 90f);
                yield return new WaitForSeconds(1.2f);
                yield return Shot(folder, "yard_2_workbench");

                ui.DebugShow(0);
                Vector3 board = Sign.Guide.transform.position;
                yield return TeleportLocalPlayer(new Vector2(board.x, board.z + 3.2f), 180f);
                yield return new WaitForSeconds(1.2f);
                yield return Shot(folder, "yard_4_notice_board");

                yield return TeleportLocalPlayer(new Vector2(0f, 9.5f), 180f);
                yield return new WaitForSeconds(1.2f);
                yield return Shot(folder, "yard_5_house_front");
                yield return TeleportLocalPlayer(new Vector2(7.5f, 6.5f), 225f);
                yield return new WaitForSeconds(1.2f);
                yield return Shot(folder, "yard_6_house_corner");
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            // Zone tour: stand on each forest path and look around, to judge how every zone reads.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperZones") >= 0)
            {
                GameConfig config = TreeManager.Instance.Config;
                float hub = config.HubRadius;
                var stops = new (string name, Vector2 position, float yaw)[]
                {
                    ("zone_hub", new Vector2(40f, 40f), 210f),
                    ("zone_hub_edge", new Vector2(0f, hub - 10f), 0f),
                    ("zone_water", new Vector2(0f, hub + 45f), 0f),
                    ("zone_earth", new Vector2(hub + 45f, 0f), 90f),
                    ("zone_fire", new Vector2(0f, -(hub + 45f)), 180f),
                    ("zone_wind", new Vector2(-(hub + 45f), 0f), 270f),
                    ("zone_water_deep", new Vector2(10f, hub + 120f), 0f),
                    ("zone_earth_deep", new Vector2(hub + 120f, 10f), 90f),
                    ("zone_fire_deep", new Vector2(10f, -(hub + 120f)), 180f),
                    ("zone_wind_deep", new Vector2(-(hub + 120f), 10f), 270f),
                };
                // Plant some saplings in front of the camera so the sapling counter and the sprouts can be checked.
                if (EnemyDirector.Instance != null)
                {
                    for (int i = 0; i < 3; i++) EnemyDirector.Instance.ServerOnTreeHit(new Vector3(48f + i * 3f, 0f, 44f), Element.Neutral, TreeSize.Medium, wood);
                }
                foreach (var stop in stops)
                {
                    yield return TeleportLocalPlayer(stop.position, stop.yaw);
                    yield return new WaitForSeconds(1.2f);
                    yield return Shot(folder, stop.name);
                }
                Debug.Log("[Shot] done");
                Application.Quit(0);
                yield break;
            }

            // A busy night: enemies around, a lit torch, charged axe, some damage.
            wood.ServerAddWood(14);
            inventory.ServerGrant(2, 2);
            inventory.ServerToggleTorch();
            wood.GetComponent<PlayerBuffs>().ServerAwardKill(Element.Fire);
            wood.GetComponent<PlayerHealth>().ServerDamage(35);
            Hut.Instance.ServerDamage(120);
            EnemyDirector dir = EnemyDirector.Instance;
            for (int i = 0; i < 6; i++)
            {
                Treant t = dir.ServerSpawn((Element)(1 + i % 4), (TreeSize)(i % 3), false, new Vector3(-30f + i * 12f, 0f, -55f));
            }
            DayNightCycle.Instance.ServerSkipPhase();
            DayNightCycle.Instance.ServerSkipPhase();
            yield return new WaitForSeconds(6f);
            yield return Shot(folder, "2_night_hud");
            inventory.ServerToggleTorch();
            yield return new WaitForSeconds(1f);
            yield return Shot(folder, "2b_night_torch_off");
            inventory.ServerToggleTorch();
            ui.DebugShow(1);
            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "3_pause");
            ui.DebugShow(4);
            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "3a_settings_general");
            ui.DebugShow(5);
            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "3b_settings_graphics");
            ui.DebugShow(6);
            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "3c_settings_keyboard");
            ui.DebugShow(7);
            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "3d_settings_gamepad");
            ui.DebugShow(2);
            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "4_inventory");
            ui.DebugShow(3);
            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "5_map");
            GameUi.Map.RevealAll();
            yield return new WaitForSeconds(0.3f);
            yield return Shot(folder, "5b_map_revealed");
            ui.DebugShow(0);

            Debug.Log("[Shot] done");
            Application.Quit(0);
        }

        // -chopperSwim: the hub pond and the sea. Logs "[Swim]" lines with the feet, the surface and the camera.
        private static IEnumerator SwimTour(string folder, PlayerWood wood, GameUi ui)
        {
            Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            (string label, Vector2 at, float yaw)[] waters =
            {
                ("pond", gen.PondCenter, 0f),
                ("sea", new Vector2(0f, Chopper.World.Generation.WorldGen.SeaInnerRadius + 25f), 0f),
            };
            foreach (var water in waters)
            {
                ui.DebugShow(0);
                yield return TeleportLocalPlayer(water.at, water.yaw);
                for (int i = 0; i < 6; i++)
                {
                    yield return new WaitForSeconds(0.5f);
                    LogSwim(water.label, $"still {i * 0.5f + 0.5f:F1}s", wood);
                }
                ui.DebugShow(0); yield return Shot(folder, $"swim_{water.label}_1_still");
                float start = Time.time;
                Vector3 from = wood.transform.position;
                while (Time.time - start < 3f)
                {
                    InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.up });
                    PlayerCamera.Local.SnapYaw(water.yaw);
                    yield return null;
                }
                LogSwim(water.label, "after swimming 3 s", wood);
                Debug.Log($"[Swim] {water.label} speed {SpeedCheck.HorizontalDistance(from, wood.transform.position) / (Time.time - start):F2} m/s, " +
                          $"stamina {PlayerStamina.Local.Current:F0}");
                // Looking up while floating: the camera must stay above the surface.
                PlayerCamera.Local.DebugSetPitch(-25f);
                yield return new WaitForSeconds(0.6f);
                LogSwim(water.label, "looking up", wood);
                ui.DebugShow(0); yield return Shot(folder, $"swim_{water.label}_3_looking_up");
                PlayerCamera.Local.DebugSetPitch(10f);
                ui.DebugShow(0); yield return Shot(folder, $"swim_{water.label}_2_moving");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return new WaitForSeconds(0.5f);
            }
            InputSystem.RemoveDevice(pad);

            // Out of the water, wet; then dried by the yard campfire.
            PlayerMotor motor = wood.GetComponent<PlayerMotor>();
            yield return TeleportLocalPlayer(new Vector2(0f, 30f), 0f);
            yield return new WaitForSeconds(2f);
            Debug.Log($"[Swim] on land after the sea: wet {motor.IsWet} ({motor.WetSeconds:F0} s left)");
            float a = 120f * Mathf.Deg2Rad;
            Vector2 fire = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 6.8f;
            yield return TeleportLocalPlayer(fire + new Vector2(0f, 2.4f), 180f);
            PlayerCamera.Local.DebugSetDistance(4f);
            yield return new WaitForSeconds(6f);
            Debug.Log($"[Swim] after 6 s by the campfire: wet {motor.IsWet} ({motor.WetSeconds:F0} s left), campfires {Campfire.All.Count}");
            ui.DebugShow(0); yield return Shot(folder, "swim_campfire_yard");
            yield return new WaitForSeconds(5f);
            Debug.Log($"[Swim] after 11 s by the campfire: wet {motor.IsWet} ({motor.WetSeconds:F0} s left)");
            if (Campfire.All.Count > 2)
            {
                Vector3 pondFire = Campfire.All[2].transform.position;
                yield return TeleportLocalPlayer(new Vector2(pondFire.x, pondFire.z + 3.5f), 180f);
                yield return new WaitForSeconds(1.5f);
                ui.DebugShow(0); yield return Shot(folder, "swim_campfire_pond");
            }
        }

        private static void LogSwim(string water, string when, PlayerWood wood)
        {
            Vector3 feet = wood.transform.position;
            float depth = WaterSystem.DepthAt(feet, out float surface);
            Camera cam = Camera.main;
            float camY = cam != null ? cam.transform.position.y : float.NaN;
            Debug.Log($"[Swim] {water} {when}: feet {feet.y:F2}, surface {surface:F2}, depth under feet {depth:F2}, " +
                      $"feet below surface {surface - feet.y:F2}, camera {camY:F2} ({camY - surface:F2} above surface), swimming {WaterSystem.IsSwimming(feet)}");
        }

        private static IEnumerator FlyTour(string folder, PlayerWood wood, GameUi ui)
        {
            PlayerDevTools tools = wood.GetComponent<PlayerDevTools>();
            PlayerMotor motor = wood.GetComponent<PlayerMotor>();
            ui.DebugConsole("help");
            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "fly_1_console_help");
            ui.DebugShow(0);
            // Chopper from the front: hat, beard, red shirt, blue trousers, boots.
            yield return TeleportLocalPlayer(new Vector2(wood.transform.position.x, wood.transform.position.z), 0f);
            PlayerCamera.Local.SnapYaw(180f);
            PlayerCamera.Local.DebugSetDistance(2.2f);
            yield return new WaitForSeconds(1.2f);
            yield return Shot(folder, "fly_0_chopper_front");
            PlayerCamera.Local.DebugSetDistance(3f);
            ui.DebugConsole("crypts");
            ui.DebugConsole("pos");
            ui.DebugConsole("fly");
            ui.DebugConsole("god");
            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "fly_2_console_commands");
            ui.DebugShow(0);
            Debug.Log($"[Fly] flying {tools.Flying}");

            // Free flight: no gravity, and a big jump is not snapped back by the server speed check.
            Vector3 start = wood.transform.position;
            Vector3 high = new Vector3(start.x, start.y + 45f, start.z - 30f);
            wood.transform.position = high;
            yield return new WaitForSeconds(1.5f);
            float drift = Vector3.Distance(wood.transform.position, high);
            Debug.Log($"[Fly] {(tools.Flying && drift < 0.5f ? "PASS" : "FAIL")} hovering after a 55 m jump (moved {drift:F2} m since)");
            PlayerCamera.Local.SnapYaw(0f);
            PlayerCamera.Local.DebugSetDistance(40f);
            yield return new WaitForSeconds(2f);
            yield return Shot(folder, "fly_3_high_over_the_hut");

            Vector3 below = high;
            bool hasGround = Chopper.World.WorldGround.Exists;
            below.y = Chopper.World.WorldGround.HeightAt(below) - 3f;
            wood.transform.position = below;
            yield return new WaitForSeconds(0.3f);
            ui.DebugConsole("fly");
            ui.DebugShow(0);
            yield return new WaitForSeconds(2f);
            float ground = Chopper.World.WorldGround.HeightAt(wood.transform.position);
            float height = wood.transform.position.y - ground;
            Debug.Log($"[Fly] {(!tools.Flying && !motor.IsFlying && Mathf.Abs(height) < 0.6f ? "PASS" : "FAIL")} landing from inside the hill puts the player on the ground ({height:F2} m above it)");

            // Night at the hut: the torch posts are lit and the elemental enemies glow in their colors.
            ui.DebugConsole("tp hut");
            ui.DebugConsole("night");
            ui.DebugConsole("spawn 6");
            ui.DebugShow(0);
            yield return new WaitForSeconds(2.5f);
            PlayerCamera.Local.DebugSetDistance(9f);
            yield return new WaitForSeconds(1f);
            yield return Shot(folder, "fly_3b_night_hut");
            // Then out in the open, facing the nearest enemy, to see its glow.
            foreach (Treant t in new System.Collections.Generic.List<Treant>(Treant.All)) t.ServerDespawn();
            ui.DebugConsole("tp 0 45");
            ui.DebugConsole("spawn 8");
            ui.DebugShow(0);
            yield return new WaitForSeconds(1.5f);
            Treant nearest = null;
            foreach (Treant t in Treant.All)
            {
                if (nearest == null || (t.transform.position - wood.transform.position).sqrMagnitude < (nearest.transform.position - wood.transform.position).sqrMagnitude) nearest = t;
            }
            if (nearest != null)
            {
                Vector3 toEnemy = nearest.transform.position - wood.transform.position;
                PlayerCamera.Local.SnapYaw(Mathf.Atan2(toEnemy.x, toEnemy.z) * Mathf.Rad2Deg);
                PlayerCamera.Local.DebugSetDistance(4f);
                yield return new WaitForSeconds(0.8f);
                yield return Shot(folder, "fly_3c_night_enemy");
            }
            ui.DebugConsole("day");
            ui.DebugShow(0);
            PlayerCamera.Local.DebugSetDistance(3f);
            foreach (Treant t in new System.Collections.Generic.List<Treant>(Treant.All)) t.ServerDespawn();

            ui.DebugConsole("tp crypt 1");
            yield return new WaitForSeconds(2f);
            ui.DebugShow(0);
            yield return Shot(folder, "fly_4_tp_crypt");
            Vector3 door = Chopper.World.Generation.CryptWorld.Plans[0].ReturnSpot;
            Debug.Log($"[Fly] {(SpeedCheck.HorizontalDistance(wood.transform.position, door) < 2f ? "PASS" : "FAIL")} tp crypt 1 lands at its door");

            HerbManager plants = HerbManager.Instance;
            int bush = plants.FirstFabricBush;
            Vector3 plant = plants.PositionOf(bush);
            // Off to one side, so the plant is not hidden behind the player.
            yield return TeleportLocalPlayer(new Vector2(plant.x + 1.4f, plant.z - 1.6f), -40f);
            PlayerCamera.Local.SnapYaw(-40f);
            PlayerCamera.Local.DebugSetDistance(3f);
            yield return new WaitForSeconds(1.5f);
            yield return Shot(folder, "fly_5_fabric_bush");
            Vector3 herb = plants.PositionOf(0);
            yield return TeleportLocalPlayer(new Vector2(herb.x + 1.4f, herb.z - 1.6f), -40f);
            PlayerCamera.Local.SnapYaw(-40f);
            yield return new WaitForSeconds(1.5f);
            yield return Shot(folder, "fly_6_herb");

            ui.DebugConsole("herbs 4");
            ui.DebugConsole("fiber 3");
            ui.DebugConsole("wood 12");
            ui.DebugShow(0);
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[Fly] pickup popups showing: {PickupFeed.Entries.Count}");
            yield return Shot(folder, "fly_6b_pickups");
            // A bandage on a hurt player: the heal gesture and the green motes (god mode off, or nothing hurts).
            ui.DebugConsole("god");
            ui.DebugShow(0);
            yield return new WaitForSeconds(0.3f);
            wood.GetComponent<PlayerHealth>().ServerDamage(30);
            yield return new WaitForSeconds(0.3f);
            PlayerHealth hurt = wood.GetComponent<PlayerHealth>();
            Debug.Log($"[Fly] before the bandage: health {hurt.Health}/{hurt.MaxHealth}, god {wood.GetComponent<PlayerCombat>().God}");
            bool used = wood.GetComponent<PlayerInventory>().ServerUseSalve();
            yield return new WaitForSeconds(0.45f);
            Animator body = wood.GetComponentInChildren<Animator>();
            bool healing = body != null && body.layerCount > 1 && body.GetCurrentAnimatorStateInfo(1).IsName("Heal");
            Debug.Log($"[Fly] bandage used {used}, health now {hurt.Health}, heal animation playing {healing}");
            yield return Shot(folder, "fly_6c_heal");
            ui.DebugConsole("tp hut");
            yield return new WaitForSeconds(1.5f);
            ui.DebugShow(2);
            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "fly_7_workbench_bandage");
            ui.DebugShow(0);
        }

        private static void LogWalls(string when)
        {
            Debug.Log($"[Hut] {when}: repaired {Hut.Instance.IsRepaired}, health {Hut.Instance.Health} of {Hut.Instance.MaxHealth}");
        }

        // Slides the local player along the ground from one point to another at a running speed, for the perf tour.
        private static IEnumerator RunAcross(Vector2 from, Vector2 to, float speed)
        {
            PlayerWood wood = PlayerWood.Local;
            var controller = wood.GetComponent<CharacterController>();
            var validator = wood.GetComponent<PlayerMovementValidator>();
            bool hasGround = Chopper.World.WorldGround.Exists;
            Vector2 direction = (to - from).normalized;
            float yaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            float length = Vector2.Distance(from, to);
            for (float d = 0f; d < length; d += speed * Time.deltaTime)
            {
                Vector2 p = from + direction * d;
                float y = Chopper.World.WorldGround.HeightAt(new Vector3(p.x, 0f, p.y)) + 0.3f;
                validator.ServerNotifyTeleport(new Vector3(p.x, y, p.y));
                controller.enabled = false;
                wood.transform.SetPositionAndRotation(new Vector3(p.x, y, p.y), Quaternion.Euler(0f, yaw, 0f));
                controller.enabled = true;
                PlayerCamera.Local.SnapYaw(yaw);
                yield return null;
            }
        }

        // Flat, dry ground near the hut (within reach of its workbench is not needed: the tour builds its own).
        private static Vector3 FlatSpot()
        {
            bool hasGround = Chopper.World.WorldGround.Exists;
            for (float radius = 26f; radius < 60f; radius += 3f)
            {
                for (float angle = 0f; angle < 360f; angle += 20f)
                {
                    Vector3 center = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad)) * radius;
                    float low = float.MaxValue, high = float.MinValue;
                    for (int x = -4; x <= 6; x += 2)
                    for (int z = -6; z <= 5; z += 2)
                    {
                        float h = Chopper.World.WorldGround.HeightAt(center + new Vector3(x, 0f, z));
                        low = Mathf.Min(low, h);
                        high = Mathf.Max(high, h);
                    }
                    center.y = low;
                    if (high - low > 0.35f || WaterSystem.DepthAt(center + Vector3.up * 0.1f, out _) > 0f) continue;
                    if (Physics.CheckBox(center + new Vector3(1f, 2f, -0.5f), new Vector3(6f, 1.6f, 7f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                        && HasSolid(center)) continue;
                    return center;
                }
            }
            return new Vector3(30f, Chopper.World.WorldGround.HeightAt(new Vector3(30f, 0f, 0f)), 0f);
        }

        private static bool HasSolid(Vector3 center)
        {
            foreach (Collider c in Physics.OverlapBox(center + new Vector3(1f, 2f, -0.5f), new Vector3(6f, 1.6f, 7f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if (!(c is TerrainCollider) && !(c is CharacterController)) return true;
            return false;
        }

        // The server only lets a player build within reach: the tour stands beside each spot first.
        private static void TeleportNear(Vector3 at)
        {
            PlayerWood wood = PlayerWood.Local;
            var controller = wood.GetComponent<CharacterController>();
            Vector3 spot = at + new Vector3(0f, 0.1f, -3.5f);
            spot.y = Mathf.Max(spot.y, Chopper.World.Building.BuildManager.GroundAt(spot, out _) + 0.1f);
            wood.GetComponent<PlayerMovementValidator>().ServerNotifyTeleport(spot);
            controller.enabled = false;
            wood.transform.position = spot;
            controller.enabled = true;
        }

        private static void ClearForTour()
        {
            foreach (Treant t in new System.Collections.Generic.List<Treant>(Treant.All)) t.ServerDespawn();
        }

        // Counts errors and exceptions while a tour runs, and those that name the foliage code.
        private sealed class ErrorWatch
        {
            public int Errors;
            public int Foliage;

            public void OnLog(string condition, string trace, LogType type)
            {
                if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
                Errors++;
                string all = condition + "\n" + trace;
                if (all.Contains("GrassField") || all.Contains("HerbManager") || all.Contains("Grass")) Foliage++;
            }
        }

        private static float PrivateFloat(object target, string field)
        {
            if (target == null) return float.NaN;
            var info = target.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return info != null && info.GetValue(target) is float value ? value : float.NaN;
        }

        private static float _foliageMidLean;

        private static IEnumerator FoliageWalk(string folder, PlayerWood wood, GameUi ui)
        {
            var watch = new ErrorWatch();
            Application.logMessageReceived += watch.OnLog;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Hut.Instance.ServerSetRepaired(true);
            wood.GetComponent<PlayerCombat>().ServerSetGod(true);
            DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.4f);
            if (WeatherSystem.Instance != null) WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
            GameUi.HudHidden = true;
            ui.DebugShow(0);

            GrassField field = UnityEngine.Object.FindAnyObjectByType<GrassField>();
            for (float waited = 0f; field == null && waited < 20f; waited += Time.deltaTime)
            {
                yield return null;
                field = UnityEngine.Object.FindAnyObjectByType<GrassField>();
            }
            if (field == null)
            {
                Debug.Log("[Foliage] RESULT FAIL: no GrassField in the world");
                Application.logMessageReceived -= watch.OnLog;
                yield break;
            }
            var bushes = new List<Transform>();
            foreach (Transform child in field.transform) if (child.name.StartsWith("Bush_")) bushes.Add(child);
            GrassCarpet carpet = GrassCarpet.Instance;
            float pushStrength = float.NaN;
            if (carpet != null)
            {
                var materialField = typeof(GrassCarpet).GetField("_material", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (materialField?.GetValue(carpet) is Material carpetMaterial && carpetMaterial.HasProperty("_PushStrength"))
                    pushStrength = carpetMaterial.GetFloat("_PushStrength");
            }
            float playerRadius = float.NaN;
            foreach (GrassPushers.Pusher pusher in GrassPushers.All) if (pusher.Transform == wood.transform) playerRadius = pusher.Radius;
            Debug.Log($"[Foliage] GrassField plants {field.transform.childCount}, decorative bushes {bushes.Count}, pushers {GrassPushers.All.Count}, " +
                      $"grass carpet {(carpet != null ? "on" : "missing")} _PushStrength {pushStrength:F2}, player push radius {playerRadius:F2} m " +
                      $"(carpet reach {playerRadius + 0.5f:F2} m, GrassField reach {playerRadius + PrivateFloat(field, "_pushRadiusPadding"):F2} m), " +
                      $"max bend {PrivateFloat(field, "_maxBendDegrees"):F0} deg, bend speed {PrivateFloat(field, "_bendSpeed"):F1}/s, recover speed {PrivateFloat(field, "_recoverSpeed"):F1}/s");

            // The densest knot of bushes standing in thick grass, in the open: no tree within 9 m (trunks and
            // canopies block the camera and the walk) and flat ground along the walk.
            var treePositions = new List<Vector3>();
            TreeDatabase treeDb = TreeManager.Instance != null ? TreeManager.Instance.Database : null;
            if (treeDb != null) for (int i = 0; i < treeDb.Count; i++) treePositions.Add(treeDb.Get(i).Position);
            bool Open(Vector3 p)
            {
                foreach (Vector3 tp in treePositions)
                {
                    float dx = tp.x - p.x, dz = tp.z - p.z;
                    if (dx * dx + dz * dz < 9f * 9f) return false;
                }
                if (WorldGround.Exists)
                {
                    float h = WorldGround.HeightAt(p);
                    for (int k = 0; k < 8; k++)
                    {
                        Vector3 q = p + Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward * 6f;
                        if (Mathf.Abs(WorldGround.HeightAt(q) - h) > 1.2f) return false;
                    }
                }
                return true;
            }
            Transform best = null;
            int bestCount = -1;
            foreach (Transform b in bushes)
            {
                if (carpet != null && carpet.DensityAt(b.position.x, b.position.z) < 0.5f) continue;
                if (!Open(b.position)) continue;
                int n = 0;
                foreach (Transform o in bushes)
                {
                    Vector3 d = o.position - b.position;
                    d.y = 0f;
                    if (d.sqrMagnitude < 2.5f * 2.5f) n++;
                }
                if (n > bestCount) { bestCount = n; best = b; }
            }
            if (best == null)
            {
                Debug.Log("[Foliage] RESULT FAIL: no bush stands in grass");
                Application.logMessageReceived -= watch.OnLog;
                yield break;
            }
            Vector3 center = Vector3.zero;
            int centerCount = 0;
            foreach (Transform o in bushes)
            {
                Vector3 d = o.position - best.position;
                d.y = 0f;
                if (d.sqrMagnitude < 2.5f * 2.5f) { center += o.position; centerCount++; }
            }
            center /= Mathf.Max(1, centerCount);
            // The walking line through the knot that passes closest to the most bushes.
            float walkYaw = 0f;
            int bestLine = -1;
            for (int turn = 0; turn < 8; turn++)
            {
                Vector3 dir = Quaternion.Euler(0f, turn * 45f, 0f) * Vector3.forward;
                int n = 0;
                foreach (Transform o in bushes)
                {
                    Vector3 d = o.position - center;
                    d.y = 0f;
                    float along = Vector3.Dot(d, dir);
                    if (Mathf.Abs(along) > 4f) continue;
                    if ((d - dir * along).magnitude < 1f) n++;
                }
                if (n > bestLine) { bestLine = n; walkYaw = turn * 45f; }
            }
            Vector3 walkDir = Quaternion.Euler(0f, walkYaw, 0f) * Vector3.forward;
            Debug.Log($"[Foliage] patch at {center:F1}: {centerCount} bushes within 2.5 m, grass density {(carpet != null ? carpet.DensityAt(center.x, center.z) : -1f):F2}, " +
                      $"walking yaw {walkYaw:F0} past {bestLine} bushes within 1 m of the line");

            (int count, float maxLean) Lean(IEnumerable<Transform> set, Vector3 at, float radius)
            {
                int count = 0;
                float max = 0f;
                foreach (Transform t in set)
                {
                    Vector3 d = t.position - at;
                    d.y = 0f;
                    if (d.magnitude > radius) continue;
                    count++;
                    max = Mathf.Max(max, Vector3.Angle(t.up, Vector3.up));
                }
                return (count, max);
            }
            float MaxLeanOf(List<Transform> set)
            {
                float max = 0f;
                foreach (Transform t in set) max = Mathf.Max(max, Vector3.Angle(t.up, Vector3.up));
                return max;
            }
            var plants = new List<Transform>();
            foreach (Transform child in field.transform) plants.Add(child);
            void Report(string label)
            {
                Vector3 at = wood.transform.position;
                var near = Lean(bushes, at, 1.25f);
                var nearAll = Lean(plants, at, 1.25f);
                var patch = Lean(bushes, center, 3f);
                Debug.Log($"[Foliage] {label}: player at {at:F2} (along the line {Vector3.Dot(at - center, walkDir):F2} m), " +
                          $"bushes within 1.25 m {near.count} max lean {near.maxLean:F1} deg, all GrassField plants within 1.25 m {nearAll.count} max lean {nearAll.maxLean:F1} deg, " +
                          $"patch bushes within 3 m of its center {patch.count} max lean {patch.maxLean:F1} deg");
            }

            PlayerCamera camera = wood.GetComponent<PlayerCamera>();
            float cameraYaw = walkYaw + 60f;
            void Frame()
            {
                camera.SnapYaw(cameraYaw);
                camera.DebugSetDistance(4.5f);
                camera.DebugSetPitch(35f);
            }

            // (a) Standing still in the patch.
            Vector3 still = center - walkDir * 0.3f;
            yield return TeleportLocalPlayer(new Vector2(still.x, still.z), walkYaw);
            Frame();
            yield return new WaitForSeconds(2.5f);
            Frame();
            yield return new WaitForSeconds(0.3f);
            Report("a_still");
            ui.DebugShow(0);
            yield return Shot(folder, "foliage_a_still");

            // Back to the start of the line; the patch springs back.
            Vector3 start = center - walkDir * 4.5f;
            yield return TeleportLocalPlayer(new Vector2(start.x, start.z), walkYaw);
            Frame();
            yield return new WaitForSeconds(2f);
            Report("before_walk");

            // (b) Walking through: the stick points along the line in the camera's frame.
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            float rel = (walkYaw - cameraYaw) * Mathf.Deg2Rad;
            Vector2 stick = new Vector2(Mathf.Sin(rel), Mathf.Cos(rel));
            var frameTimes = new List<float>();
            List<Transform> passed = null;
            float walkStart = Time.time;
            while (Time.time - walkStart < 12f)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = stick });
                camera.SnapYaw(cameraYaw);
                yield return null;
                frameTimes.Add(Time.unscaledDeltaTime * 1000f);
                float along = Vector3.Dot(wood.transform.position - center, walkDir);
                if (passed == null && along >= -0.1f)
                {
                    Report("b_mid_walk");
                    passed = new List<Transform>();
                    foreach (Transform b in bushes)
                    {
                        Vector3 d = b.position - wood.transform.position;
                        d.y = 0f;
                        if (d.magnitude <= 1.25f + PrivateFloat(field, "_pushRadiusPadding")) passed.Add(b);
                    }
                    ui.DebugShow(0);
                    yield return QuickShot(folder, "foliage_b_mid_walk");
                    _foliageMidLean = MaxLeanOf(passed);
                    Debug.Log($"[Foliage] b_mid_walk picture taken, {passed.Count} bushes inside the lean reach, their max lean {_foliageMidLean:F1} deg");
                }
                if (along >= 3.5f) break;
            }
            InputSystem.QueueStateEvent(pad, new GamepadState());
            float walked = Time.time - walkStart;
            Vector3 flat = wood.transform.position - start;
            flat.y = 0f;
            Debug.Log($"[Foliage] walked {flat.magnitude:F2} m in {walked:F2} s ({flat.magnitude / Mathf.Max(0.01f, walked):F2} m/s), heading off the line {Vector3.Angle(flat, walkDir):F1} deg");
            if (passed == null)
            {
                passed = new List<Transform>();
                Debug.Log("[Foliage] the player never reached the patch center");
            }

            // (c) Spring back: the bushes passed through, measured every quarter second.
            float released = Time.time;
            while (Time.time - released < 1.25f)
            {
                camera.SnapYaw(cameraYaw);
                yield return new WaitForSeconds(0.25f);
                Debug.Log($"[Foliage] {Time.time - released:F2} s after passing: passed bushes max lean {MaxLeanOf(passed):F1} deg");
            }
            Report("c_after");
            float afterLean = MaxLeanOf(passed);
            Debug.Log($"[Foliage] c_after: the {passed.Count} bushes passed through, max lean {afterLean:F1} deg");
            ui.DebugShow(0);
            yield return Shot(folder, "foliage_c_after");
            InputSystem.RemoveDevice(pad);

            frameTimes.Sort();
            float sum = 0f;
            foreach (float f in frameTimes) sum += f;
            float avg = sum / Mathf.Max(1, frameTimes.Count);
            float p95 = frameTimes.Count > 0 ? frameTimes[Mathf.Clamp(Mathf.FloorToInt(frameTimes.Count * 0.95f), 0, frameTimes.Count - 1)] : 0f;
            Debug.Log($"[Foliage] walk frames {frameTimes.Count}, average {avg:F1} ms ({1000f / Mathf.Max(0.01f, avg):F0} fps), p95 {p95:F1} ms");

            Application.logMessageReceived -= watch.OnLog;
            bool pass = passed.Count > 0 && _foliageMidLean > 1f && afterLean < 1f && watch.Foliage == 0;
            Debug.Log($"[Foliage] errors during the tour {watch.Errors}, naming grass or herbs {watch.Foliage}");
            Debug.Log($"[Foliage] RESULT {(pass ? "PASS" : "FAIL")} (bushes in reach mid-walk {passed.Count}, max lean after passing {afterLean:F1} deg, max lean mid-walk {_foliageMidLean:F1} deg, foliage errors {watch.Foliage})");
        }

        private static IEnumerator GearLook(string folder, PlayerWood wood, GameUi ui)
        {
            var watch = new ErrorWatch();
            Application.logMessageReceived += watch.OnLog;
            Application.runInBackground = true;
            Hut.Instance.ServerSetRepaired(true);
            wood.GetComponent<PlayerCombat>().ServerSetGod(true);
            DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.4f);
            if (WeatherSystem.Instance != null) WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
            GameUi.HudHidden = true;
            PlayerAxe axe = wood.GetComponent<PlayerAxe>();
            axe.ServerSetHasAxe(true);
            yield return TeleportLocalPlayer(new Vector2(0f, 40f), 0f);
            yield return new WaitForSeconds(3f);
            if (!axe.IsDrawn)
            {
                axe.RequestToggleDrawn();
                yield return new WaitForSeconds(1.2f);
            }
            ui.DebugShow(0);

            Transform Find(string name)
            {
                foreach (Transform t in wood.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
                return null;
            }
            Transform grip = Find("AxeGrip"), axeMesh = Find("chopper_axe"), shield = Find("chopper_shield"), socket = Find("Weapon_R");
            Transform oldAxe = Find("Axe");
            PlayerCamera camera = wood.GetComponent<PlayerCamera>();
            Camera eye = Camera.main;
            void GearReport(string label)
            {
                Transform body = wood.transform;
                float height = 0f;
                foreach (SkinnedMeshRenderer skin in wood.GetComponentsInChildren<SkinnedMeshRenderer>())
                    if (skin.enabled && skin.gameObject.activeInHierarchy) height = Mathf.Max(height, skin.bounds.max.y - body.position.y);
                string axeText = "no chopper_axe";
                if (axeMesh != null && grip != null)
                {
                    Renderer r = axeMesh.GetComponent<Renderer>();
                    MeshFilter mf = axeMesh.GetComponent<MeshFilter>();
                    Vector3 size = mf != null && mf.sharedMesh != null ? Vector3.Scale(mf.sharedMesh.bounds.size, axeMesh.lossyScale) : Vector3.zero;
                    HeldItemFrame frame = grip.GetComponent<HeldItemFrame>();
                    Vector3 head = frame != null ? body.InverseTransformDirection(grip.rotation * frame.Head) : Vector3.zero;
                    Vector3 edge = frame != null ? body.InverseTransformDirection(grip.rotation * frame.Edge) : Vector3.zero;
                    axeText = $"axe parent {grip.parent.name}/{grip.name}/{axeMesh.name}, enabled {r != null && r.enabled && axeMesh.gameObject.activeInHierarchy}, " +
                              $"mesh size {size:F2} m, grip at hand {(grip.position - grip.parent.position).magnitude * 100f:F1} cm, " +
                              $"grip to Weapon_R {(socket != null ? (grip.position - socket.position).magnitude * 100f : -1f):F1} cm, angle to Weapon_R {(socket != null ? Quaternion.Angle(grip.rotation, socket.rotation) : -1f):F1} deg, " +
                              $"head dir in body {head:F2}, blade dir in body {edge:F2}, axe center in body {body.InverseTransformPoint(r.bounds.center):F2}";
                }
                string shieldText = "no chopper_shield";
                if (shield != null)
                {
                    Renderer r = shield.GetComponent<Renderer>();
                    Bounds b = r.bounds;
                    shieldText = $"shield parent {shield.parent.name}, enabled {r.enabled && shield.gameObject.activeInHierarchy}, center in body {body.InverseTransformPoint(b.center):F2}, size {b.size:F2}, " +
                                 $"camera to shield bounds {Mathf.Sqrt(b.SqrDistance(eye.transform.position)):F2} m (inside {b.Contains(eye.transform.position)}), near clip {eye.nearClipPlane:F2}";
                }
                bool oldVisible = oldAxe != null && oldAxe.gameObject.activeInHierarchy && oldAxe.GetComponentInChildren<Renderer>() != null && oldAxe.GetComponentInChildren<Renderer>().enabled;
                Debug.Log($"[Gear] {label}: body height {height:F2} m, camera distance {(eye.transform.position - body.position - Vector3.up * 1.5f).magnitude:F2} m, drawn {axe.IsDrawn}; {axeText}; {shieldText}; old woodsman axe visible {oldVisible}");
            }

            (string name, float yaw)[] views = { ("gear_back_right", 315f), ("gear_right", 270f), ("gear_front_right", 220f), ("gear_back", 0f) };
            foreach (var view in views)
            {
                camera.SnapYaw(view.yaw);
                yield return new WaitForSeconds(0.9f);
                GearReport(view.name);
                ui.DebugShow(0);
                yield return Shot(folder, view.name);
            }
            // Close from the right, to judge the grip.
            camera.SnapYaw(250f);
            camera.DebugSetDistance(1.8f);
            yield return new WaitForSeconds(0.9f);
            GearReport("gear_close_right");
            ui.DebugShow(0);
            yield return Shot(folder, "gear_close_right");

            // A tree standing alone (so nothing blocks the camera), the player in reach of it, then chops seen from
            // two sides at play distance.
            Vector2 chopSpot = Vector2.zero;
            float chopYaw = 0f;
            bool found = false;
            TreeDatabase gearTrees = TreeManager.Instance.Database;
            var order = new List<(float clear, int id)>();
            for (int i = 0; i < gearTrees.Count; i++)
            {
                Vector3 p = gearTrees.Get(i).Position;
                if (p.magnitude > 150f) continue;
                float nearest = float.MaxValue;
                for (int j = 0; j < gearTrees.Count; j++)
                {
                    if (j == i) continue;
                    Vector3 q = gearTrees.Get(j).Position;
                    nearest = Mathf.Min(nearest, new Vector2(q.x - p.x, q.z - p.z).magnitude);
                }
                order.Add((nearest, i));
            }
            order.Sort((x, y) => y.clear.CompareTo(x.clear));
            for (int k = 0; k < order.Count && k < 40 && !found; k++)
            {
                if (TreeManager.Instance.IsFelledOnClient(order[k].id)) continue;
                Vector3 p = gearTrees.Get(order[k].id).Position;
                for (int turn = 0; turn < 8 && !found; turn++)
                {
                    Vector3 aim = Quaternion.Euler(0f, turn * 45f, 0f) * Vector3.forward;
                    foreach (float back in new[] { 1.8f, 2.2f, 1.5f })
                    {
                        Vector3 stand = p - aim * back;
                        if (!TreeManager.Instance.ServerFindTree(new Vector3(stand.x, p.y, stand.z), aim, out int hit, out _) || hit != order[k].id) continue;
                        chopSpot = new Vector2(stand.x, stand.z);
                        chopYaw = turn * 45f;
                        found = true;
                        Debug.Log($"[Gear] lone tree {order[k].id} at {p:F1}, nearest other tree {order[k].clear:F1} m");
                        break;
                    }
                }
            }
            Debug.Log($"[Gear] tree found {found} at {chopSpot} facing {chopYaw}");
            yield return TeleportLocalPlayer(chopSpot, chopYaw);
            float[] chopViews = { chopYaw - 90f, chopYaw + 90f };
            for (int v = 0; v < chopViews.Length; v++)
            {
                camera.DebugSetDistance(3.4f);
                camera.SnapYaw(chopViews[v]);
                yield return new WaitForSeconds(1.5f);
                camera.SnapYaw(chopViews[v]);
                Time.timeScale = 0.1f;
                bool swung = axe.TrySwing();
                Debug.Log($"[Gear] chop {v} swung {swung}, camera yaw {chopViews[v]:F0}");
                for (int i = 0; i < 6; i++)
                {
                    yield return new WaitForSeconds(0.05f);
                    if (i == 3) GearReport($"gear_chop{v}_3");
                    ui.DebugShow(0);
                    yield return QuickShot(folder, $"gear_chop{v}_{i}");
                }
                Time.timeScale = 1f;
                yield return new WaitForSeconds(1.2f);
            }
            Time.timeScale = 1f;
            yield return new WaitForSeconds(1f);
            Application.logMessageReceived -= watch.OnLog;
            Debug.Log($"[Gear] errors during the tour {watch.Errors}");
            Debug.Log("[Gear] done");
        }

        // The world audit: the gameplay camera at a few typical places, free cameras from higher up, dusk and night, the
        // ground and the sky, with frame times measured uncapped at three of the places.
        private static IEnumerator WorldAudit(string folder, PlayerWood wood, GameUi ui)
        {
            var watch = new ErrorWatch();
            Application.logMessageReceived += watch.OnLog;
            Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
            Hut.Instance.ServerSetRepaired(true);
            wood.GetComponent<PlayerAxe>().ServerSetHasAxe(true);
            wood.GetComponent<PlayerCombat>().ServerSetGod(true);
            if (WeatherSystem.Instance != null) WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
            DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.35f);
            foreach (Treant t in new List<Treant>(Treant.All)) t.ServerDespawn();
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            PlayerCamera cam = PlayerCamera.Local;
            float Ground(float x, float z) => WorldGround.HeightAt(x, z);
            Vector3 hut = Hut.Instance.transform.position;
            Debug.Log($"[Audit] hut at {hut}, hut yaw {Hut.Instance.transform.eulerAngles.y:F0}, pond at {gen.PondCenter}, camera far {Camera.main.farClipPlane:F0} m, fog {RenderSettings.fog} {RenderSettings.fogMode} density {RenderSettings.fogDensity:F4} start {RenderSettings.fogStartDistance:F0} end {RenderSettings.fogEndDistance:F0}, skybox {(RenderSettings.skybox != null ? RenderSettings.skybox.name + " / " + RenderSettings.skybox.shader.name : "none")}, sun {(RenderSettings.sun != null ? RenderSettings.sun.name : "none")}");
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (pipeline != null) Debug.Log($"[Audit] pipeline {pipeline.name}: msaa {pipeline.msaaSampleCount}, render scale {pipeline.renderScale:F2}, shadows {pipeline.mainLightShadowmapResolution} px to {pipeline.shadowDistance:F0} m, hdr {pipeline.supportsHDR}, screen {Screen.width}x{Screen.height}");
            foreach (var volume in UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Exclude))
            {
                string parts = "";
                if (volume.sharedProfile != null) foreach (var c in volume.sharedProfile.components) parts += (c.active ? "" : "(off)") + c.GetType().Name + " ";
                Debug.Log($"[Audit] volume {volume.name} global {volume.isGlobal} weight {volume.weight:F2} profile {(volume.sharedProfile != null ? volume.sharedProfile.name : "none")}: {parts}");
            }
            yield return new WaitForSeconds(4f);

            // Gameplay camera stops: position, yaw, pitch, camera distance.
            Vector2 pondView = gen.PondCenter + (gen.PondCenter.sqrMagnitude > 1f ? gen.PondCenter.normalized : Vector2.right) * 22f;
            float pondYaw = Mathf.Atan2(gen.PondCenter.x - pondView.x, gen.PondCenter.y - pondView.y) * Mathf.Rad2Deg;
            (string name, Vector2 at, float yaw, float pitch, float distance)[] stops =
            {
                ("01_concept_framing_hut", new Vector2(hut.x - 9f, hut.z + 16f), 150f, 8f, 4.5f),
                ("02_meadow_pines", new Vector2(70f, -45f), 120f, 6f, 5f),
                ("03_near_water_pond", pondView, pondYaw, 12f, 5f),
                ("04_toward_mountains", new Vector2(0f, 70f), 0f, -8f, 5f),
                ("05_terrain_edge_beach", new Vector2(0f, 232f), 0f, 6f, 5f),
                ("06_deep_forest_earth", new Vector2(175f, 10f), 90f, 6f, 5f),
                ("07_ground_closeup", new Vector2(30f, 30f), 45f, 55f, 2.5f),
                ("08_sky_up", new Vector2(30f, 30f), 135f, -20f, 3f),
            };
            foreach (var stop in stops)
            {
                yield return TeleportLocalPlayer(stop.at, stop.yaw);
                cam.DebugSetDistance(stop.distance);
                cam.DebugSetPitch(stop.pitch);
                cam.SnapYaw(stop.yaw);
                ui.DebugShow(0);
                yield return new WaitForSeconds(2f);
                yield return Shot(folder, stop.name);
                if (stop.name.StartsWith("01") || stop.name.StartsWith("02") || stop.name.StartsWith("05"))
                    yield return MeasureFrames("audit " + stop.name, 4f);
            }

            // Props up close: the hub tree nearest a meadow spot standing, falling and as a stump, then a yard campfire.
            TreeView standing = null;
            Vector3 meadow = new Vector3(40f, 0f, 40f);
            foreach (TreeView v in UnityEngine.Object.FindObjectsByType<TreeView>(FindObjectsInactive.Exclude))
                if (standing == null || (v.transform.position - meadow).sqrMagnitude < (standing.transform.position - meadow).sqrMagnitude) standing = v;
            if (standing != null)
            {
                int treeId = standing.TreeId;
                Vector3 trunk = standing.transform.position;
                Renderer[] parts = standing.GetComponentsInChildren<Renderer>();
                Bounds tree = parts[0].bounds;
                foreach (Renderer r in parts) tree.Encapsulate(r.bounds);
                Debug.Log($"[Audit] tree {treeId} at {trunk:F1}: {tree.size.x:F2} x {tree.size.y:F2} x {tree.size.z:F2} m, bottom {tree.min.y - trunk.y:F2} m from its base, scale {standing.transform.localScale.x:F2}, root yaw {standing.transform.eulerAngles:F0}");
                Vector2 stand = new Vector2(trunk.x, trunk.z - 7f);
                yield return TeleportLocalPlayer(stand, 0f);
                cam.DebugSetDistance(4f);
                cam.DebugSetPitch(4f);
                cam.SnapYaw(0f);
                ui.DebugShow(0);
                yield return new WaitForSeconds(1.5f);
                yield return Shot(folder, "15_tree_upright_close");
                // Chop it down: hits until it falls, a picture mid fall, then the stump and the fallen trunk.
                bool down = false;
                for (int hit = 0; hit < 40 && !down; hit++)
                {
                    TreeManager.Instance.ServerHitTree(treeId, wood, Vector3.forward, true, 4f);
                    yield return new WaitForSeconds(0.15f);
                    down = true;
                    foreach (TreeView v in UnityEngine.Object.FindObjectsByType<TreeView>(FindObjectsInactive.Exclude)) if (v.TreeId == treeId) down = false;
                }
                Debug.Log($"[Audit] tree {treeId} felled {down}");
                yield return new WaitForSeconds(0.9f);
                yield return QuickShot(folder, "16_tree_falling");
                yield return new WaitForSeconds(3f);
                yield return Shot(folder, "17_stump_and_fallen");
                foreach (Transform t in UnityEngine.Object.FindAnyObjectByType<TreeManager>().GetComponentsInChildren<Transform>())
                {
                    if (t.name != "Stump" || (t.position - trunk).sqrMagnitude > 1f) continue;
                    Renderer[] stumpParts = t.GetComponentsInChildren<Renderer>();
                    if (stumpParts.Length == 0) continue;
                    Bounds sb = stumpParts[0].bounds;
                    foreach (Renderer r in stumpParts) sb.Encapsulate(r.bounds);
                    var capsule = t.GetComponent<CapsuleCollider>();
                    Debug.Log($"[Audit] stump {sb.size.x:F2} x {sb.size.y:F2} x {sb.size.z:F2} m, bottom {sb.min.y - trunk.y:F2} m, collider {(capsule != null ? $"world height axis {(capsule.direction == 1 ? t.up : capsule.direction == 0 ? t.right : t.forward):F2}" : "none")}");
                }
            }
            if (Campfire.All.Count > 0)
            {
                // The pond fire stands in the open (the yard fires sit against the hut); viewed from the hut's side.
                Vector3 fire = Campfire.All[Campfire.All.Count > 2 ? 2 : 0].transform.position;
                Vector2 toHut = new Vector2(-fire.x, -fire.z).normalized;
                Vector2 standAt = new Vector2(fire.x, fire.z) + toHut * 2.6f;
                float fireYaw = Mathf.Atan2(-toHut.x, -toHut.y) * Mathf.Rad2Deg;
                yield return TeleportLocalPlayer(standAt, fireYaw);
                cam.DebugSetDistance(2.4f);
                cam.DebugSetPitch(38f);
                cam.SnapYaw(fireYaw + 30f);
                ui.DebugShow(0);
                yield return new WaitForSeconds(1.5f);
                yield return Shot(folder, "18_campfire_close");
            }

            // Free cameras from higher up: the player stands below (the grass and trees follow the camera and player).
            (string name, Vector2 feet, Vector3 eye, Vector3 look)[] high =
            {
                ("09_high_hub_overview", new Vector2(0f, -40f), new Vector3(0f, 0f, -70f) + Vector3.up * 38f, new Vector3(0f, 0f, 40f)),
                ("10_high_ring_and_forest", new Vector2(90f, 90f), new Vector3(80f, 0f, 80f) + Vector3.up * 30f, new Vector3(200f, 10f, 200f)),
                ("11_high_shore_to_sea", new Vector2(0f, 200f), new Vector3(0f, 0f, 190f) + Vector3.up * 25f, new Vector3(0f, -10f, 320f)),
                ("12_eye_level_valley", new Vector2(-60f, 20f), new Vector3(-55f, 0f, 20f) + Vector3.up * 3f, new Vector3(-160f, 15f, 20f)),
            };
            foreach (var view in high)
            {
                yield return TeleportLocalPlayer(view.feet, 0f);
                Vector3 eye = view.eye;
                eye.y += Ground(eye.x, eye.z);
                Vector3 look = view.look;
                look.y += Ground(look.x, look.z);
                yield return FreeShot(folder, view.name, eye, look);
            }

            // Dusk and night at the hut, gameplay camera.
            (string name, DayPhase phase, float fraction)[] times =
            {
                ("13_dusk_hut", DayPhase.Dusk, 0.5f),
                ("14_night_hut", DayPhase.Night, 0.3f),
            };
            foreach (var time in times)
            {
                DayNightCycle.Instance.ServerDebugSetClock(time.phase, time.fraction);
                yield return TeleportLocalPlayer(new Vector2(hut.x - 9f, hut.z + 16f), 150f);
                cam.DebugSetDistance(4.5f);
                cam.DebugSetPitch(8f);
                foreach (Treant t in new List<Treant>(Treant.All)) t.ServerDespawn();
                ui.DebugShow(0);
                yield return new WaitForSeconds(2.5f);
                yield return Shot(folder, time.name);
            }
            yield return MeasureFrames("audit night at hut", 4f);
            DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.35f);
            Application.logMessageReceived -= watch.OnLog;
            Debug.Log($"[Audit] RESULT done, errors during the tour {watch.Errors}");
        }

        // A picture from a free camera: the player's camera rig is paused, the camera placed and aimed, then restored.
        private static IEnumerator FreeShot(string folder, string name, Vector3 eye, Vector3 look)
        {
            PlayerCamera rig = PlayerCamera.Local;
            Camera view = Camera.main;
            rig.enabled = false;
            view.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye));
            yield return new WaitForSeconds(2f);
            view.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye));
            yield return Shot(folder, name);
            Debug.Log($"[Audit] {name} eye {eye:F0} look {look:F0}");
            rig.enabled = true;
        }

        // A 2 m by 4 m log cabin: doorway and gable on the south end, window on the east wall, two roofs
        // whose high edges meet on the ridge (yaw 0 and 180). Placed with ServerPlace so overlap is real.
        private static IEnumerator CabinTour(string folder, PlayerWood wood, GameUi ui)
        {
            if (ui != null) ui.DebugShow(0);
            wood.ServerAddWood(80);
            yield return null;

            BuildManager build = BuildManager.Instance;
            PieceCatalog catalog = build != null ? build.Catalog : null;
            int floor = CabinKind(catalog, "Wood floor");
            int wall = CabinKind(catalog, "Wood wall");
            int doorway = CabinKind(catalog, "Doorway");
            int window = CabinKind(catalog, "Log window");
            int gable = CabinKind(catalog, "Log gable");
            int roof = CabinKind(catalog, "Roof");
            int bench = CabinKind(catalog, "Workbench");
            if (build == null || floor < 0 || wall < 0 || doorway < 0 || window < 0 || gable < 0 || roof < 0 || bench < 0)
            {
                Debug.Log("[Cabin] RESULT FAIL missing catalog piece");
                yield break;
            }

            float x0 = 0f, z0 = 0f, gy = 0f;
            bool found = false;
            var probe = new Collider[16];
            for (int i = 0; i < 48 && !found; i++)
            {
                x0 = Mathf.Floor((28f + (i % 8) * 8f) / 2f) * 2f;
                z0 = Mathf.Floor((-48f + (i / 8) * 8f) / 2f) * 2f;
                Vector3 mid = new Vector3(x0 + 1f, 0f, z0 + 2f);
                gy = WorldGround.HeightAt(mid);
                if (gy < Chopper.World.Generation.WorldGen.WaterHeight + 1f) continue;
                if (BuildRules.TooSteep(WorldGround.NormalAt(mid).y)) continue;
                int hits = Physics.OverlapBoxNonAlloc(mid + Vector3.up * 2f, new Vector3(2.2f, 2.5f, 3.2f), probe, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                for (int h = 0; h < hits; h++)
                {
                    if (probe[h] == null || probe[h] is TerrainCollider) continue;
                    blocked = true;
                    break;
                }
                if (!blocked) found = true;
            }
            if (!found)
            {
                Debug.Log("[Cabin] RESULT FAIL no clear ground");
                yield break;
            }

            yield return TeleportLocalPlayer(new Vector2(x0 + 1f, z0 - 3.2f), 0f);
            yield return new WaitForSeconds(0.4f);

            var fails = new List<string>();
            void Place(string label, int kind, Vector3 pos, float yaw)
            {
                PlaceResult result = build.ServerPlace(wood.gameObject, kind, pos, yaw);
                Debug.Log($"[Cabin] {label} {result} at {pos.x:F2},{pos.y:F2},{pos.z:F2} yaw {yaw:F0}");
                if (result != PlaceResult.Ok) fails.Add(label + " " + result);
            }

            Place("bench", bench, new Vector3(x0 + 4.5f, gy, z0 + 1f), 0f);
            Place("floor south", floor, new Vector3(x0 + 1f, gy, z0 + 1f), 0f);
            Place("floor north", floor, new Vector3(x0 + 1f, gy, z0 + 3f), 0f);
            Place("doorway", doorway, new Vector3(x0 + 1f, gy, z0), 0f);
            Place("wall north", wall, new Vector3(x0 + 1f, gy, z0 + 4f), 0f);
            Place("wall west south", wall, new Vector3(x0, gy, z0 + 1f), 90f);
            Place("wall west north", wall, new Vector3(x0, gy, z0 + 3f), 90f);
            Place("window", window, new Vector3(x0 + 2f, gy, z0 + 1f), 90f);
            Place("wall east", wall, new Vector3(x0 + 2f, gy, z0 + 3f), 90f);
            wood.ServerAddWood(40);
            Place("gable", gable, new Vector3(x0 + 1f, gy + 2f, z0), 0f);
            Place("roof south", roof, new Vector3(x0 + 1f, gy + 2f, z0 + 1f), 0f);
            Place("roof north", roof, new Vector3(x0 + 1f, gy + 2f, z0 + 3f), 180f);
            yield return new WaitForSeconds(0.8f);

            float ridgeZ = z0 + 2f;
            int roofCount = 0;
            bool roofsOk = true;
            foreach (PieceState state in build.Pieces)
            {
                if (state.Kind != roof) continue;
                roofCount++;
                Vector3 high = BuildRules.WorldPoint(state.Position, state.Yaw, new Vector3(0f, 2f, 1f));
                Vector3 low = BuildRules.WorldPoint(state.Position, state.Yaw, new Vector3(0f, 0f, -1f));
                bool toward = high.y > low.y + 0.5f && Mathf.Abs(high.z - ridgeZ) + 0.2f < Mathf.Abs(low.z - ridgeZ);
                Debug.Log($"[Cabin] roof high {high.x:F2},{high.y:F2},{high.z:F2} low {low.x:F2},{low.y:F2},{low.z:F2} toward ridge {toward}");
                if (!toward) roofsOk = false;
            }

            Vector3 door = new Vector3(x0 + 1f, gy, z0);
            Vector3 doorTop = BuildRules.WorldPoint(door, 0f, new Vector3(-1f, 2f, 0f));
            Vector3 gableFoot = BuildRules.WorldPoint(door + Vector3.up * 2f, 0f, new Vector3(-1f, 0f, 0f));
            float gableGap = (doorTop - gableFoot).magnitude;
            bool gableOk = gableGap < 0.05f;
            Debug.Log($"[Cabin] gable on wall top {gableOk} gap {gableGap:F3} m");

            int doorHits = CabinSolid(door + Vector3.up * 0.9f, new Vector3(0.2f, 0.3f, 0.06f), Quaternion.identity, probe);
            bool doorOpen = doorHits == 0;
            Debug.Log($"[Cabin] doorway opening colliders {doorHits}");

            Quaternion windowYaw = Quaternion.Euler(0f, 90f, 0f);
            Vector3 windowHole = new Vector3(x0 + 2f, gy, z0 + 1f) + windowYaw * new Vector3(0f, 1.25f, 0f);
            int windowHits = CabinSolid(windowHole, new Vector3(0.12f, 0.12f, 0.06f), windowYaw, probe);
            bool windowOpen = windowHits == 0;
            Debug.Log($"[Cabin] window opening colliders {windowHits}");

            yield return TeleportLocalPlayer(new Vector2(x0 + 1f, z0 - 4.5f), 0f);
            yield return new WaitForSeconds(0.6f);
            yield return Shot(folder, "cabin_front");
            yield return TeleportLocalPlayer(new Vector2(x0 + 5.5f, z0 - 3.5f), 230f);
            yield return new WaitForSeconds(0.4f);
            yield return Shot(folder, "cabin_three_quarter");

            Hut hut = Hut.Instance;
            bool cabinHome = false, longhouse = false, bed = false, chest = false, fire = false;
            if (hut != null)
            {
                foreach (Transform child in hut.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "Cabin") cabinHome = true;
                    if (child.name == "Longhouse") longhouse = true;
                    if (child.name == "Bed") bed = true;
                    if (child.name == "StorageChest") chest = true;
                    if (child.name == "Fireplace") fire = true;
                }
                Vector3 home = hut.transform.position;
                yield return TeleportLocalPlayer(new Vector2(home.x, home.z + 5.5f), 180f);
                yield return new WaitForSeconds(0.5f);
                yield return Shot(folder, "hub_front");
                yield return TeleportLocalPlayer(new Vector2(home.x + 6f, home.z + 6f), 225f);
                yield return new WaitForSeconds(0.3f);
                yield return Shot(folder, "hub_three_quarter");
            }
            bool hubSizeOk = false;
            if (hut != null)
            {
                Transform built = null;
                foreach (Transform child in hut.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Cabin") { built = child; break; }
                if (built != null)
                {
                    Renderer[] renderers = built.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        Bounds bounds = renderers[0].bounds;
                        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                        hubSizeOk = bounds.size.x < 4f && bounds.size.z < 6f && bounds.size.y > 2f && bounds.size.y < 6f;
                        Debug.Log($"[Cabin] hub mesh {renderers.Length} renderers size {bounds.size.x:F2} x {bounds.size.y:F2} x {bounds.size.z:F2}");
                    }
                }
            }
            bool hubOk = cabinHome && !longhouse && bed && chest && fire && hubSizeOk;
            Debug.Log($"[Cabin] hub cabin {cabinHome} longhouse {longhouse} bed {bed} chest {chest} fireplace {fire} size {hubSizeOk} repair wood {(hut != null ? hut.RepairWoodNeeded : -1)}");

            bool pass = fails.Count == 0 && roofsOk && roofCount == 2 && gableOk && doorOpen && windowOpen && hubOk;
            string why = fails.Count == 0 ? "all pieces placed" : string.Join(", ", fails);
            Debug.Log($"[Cabin] RESULT {(pass ? "PASS" : "FAIL")} ({why}; roofs {roofCount} toward ridge {roofsOk}; gable {gableOk}; door open {doorOpen}; window open {windowOpen}; hub {hubOk})");
        }

        private static int CabinKind(PieceCatalog catalog, string displayName)
        {
            if (catalog == null) return -1;
            for (int i = 0; i < catalog.Count; i++)
            {
                PieceDefinition piece = catalog.Get(i);
                if (piece != null && piece.DisplayName == displayName) return i;
            }
            return -1;
        }

        // Colliders in a box, ignoring the terrain and the player. Returns how many solid ones sit there.
        private static int CabinSolid(Vector3 center, Vector3 halfExtents, Quaternion rotation, Collider[] probe)
        {
            int hits = Physics.OverlapBoxNonAlloc(center, halfExtents, probe, rotation, ~0, QueryTriggerInteraction.Ignore);
            int solid = 0;
            for (int i = 0; i < hits; i++)
            {
                Collider hit = probe[i];
                if (hit == null || hit is TerrainCollider || hit is CharacterController) continue;
                solid++;
            }
            return solid;
        }

        private static IEnumerator TeleportLocalPlayer(Vector2 position, float yaw)
        {
            PlayerWood wood = PlayerWood.Local;
            bool hasGround = Chopper.World.WorldGround.Exists;
            float y = Chopper.World.WorldGround.HeightAt(new Vector3(position.x, 0f, position.y)) + 0.3f;
            var controller = wood.GetComponent<CharacterController>();
            // The server speed check would snap a sudden jump like this back, so it is told about the teleport first.
            wood.GetComponent<PlayerMovementValidator>().ServerNotifyTeleport(new Vector3(position.x, y, position.y));
            controller.enabled = false;
            wood.transform.SetPositionAndRotation(new Vector3(position.x, y, position.y), Quaternion.Euler(0f, yaw, 0f));
            controller.enabled = true;
            var networkTransform = wood.GetComponent<FishNet.Component.Transforming.NetworkTransform>();
            if (networkTransform != null) networkTransform.Teleport();
            PlayerCamera.Local.SnapYaw(yaw);
            yield return null;
        }

        // Moves the local player to an exact position (crypt floors are not on the terrain).
        private static IEnumerator TeleportExact(Vector3 position, float yaw)
        {
            PlayerWood wood = PlayerWood.Local;
            var controller = wood.GetComponent<CharacterController>();
            wood.GetComponent<PlayerMovementValidator>().ServerNotifyTeleport(position);
            controller.enabled = false;
            wood.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            controller.enabled = true;
            var networkTransform = wood.GetComponent<FishNet.Component.Transforming.NetworkTransform>();
            if (networkTransform != null) networkTransform.Teleport();
            PlayerCamera.Local.SnapYaw(yaw);
            yield return null;
        }

        // A picture without the wait after it, for frames of a slow motion sequence.
        // The held axe in numbers: where the head and the edge point in the body's frame (x right, y up, z forward),
        // the handle's angle to the line across the fist (0 = straight through it), how far the fist's hollow and each
        // fingertip are from the handle's center line, and the fist layer's weight.
        private static void GripReport(Transform body, Transform grip, string label)
        {
            // Which way the animated body faces, in the player's frame: from the heel to the toes, and the pelvis's
            // forward (at right angles to the hips, left to right). Both should be about (0, 0, 1).
            Transform footL = null, ballL = null, thighL = null, thighR = null, modelRoot = body.GetComponentInChildren<Animator>().transform;
            foreach (Transform t in body.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "foot_l") footL = t;
                if (t.name == "ball_l") ballL = t;
                if (t.name == "thigh_l") thighL = t;
                if (t.name == "thigh_r") thighR = t;
            }
            Vector3 toes = footL != null && ballL != null ? body.InverseTransformDirection(Vector3.ProjectOnPlane(ballL.position - footL.position, body.up).normalized) : Vector3.zero;
            Vector3 hips = thighL != null && thighR != null ? body.InverseTransformDirection(Vector3.Cross(thighR.position - thighL.position, body.up).normalized) : Vector3.zero;
            Debug.Log($"[Facing] {label}: toes {toes:F2}, hips {hips:F2}, model local yaw {modelRoot.localEulerAngles.y:F0}, root yaw {body.eulerAngles.y:F0}");
            var frame = grip.GetComponent<HeldItemFrame>();
            Transform hand = grip.parent;
            Vector3 head = (grip.rotation * frame.Head).normalized;
            Vector3 edge = (grip.rotation * frame.Edge).normalized;
            float Off(Vector3 p) => Vector3.Cross(p - grip.position, head).magnitude;
            var fist = new HandGrip(hand, "r");
            float across = Vector3.Angle(fist.Across, head);
            across = Mathf.Min(across, 180f - across);
            string tips = "";
            foreach (string finger in new[] { "thumb_03_r", "index_03_r", "middle_03_r", "ring_03_r", "pinky_03_r" })
            {
                Transform tip = null;
                foreach (Transform t in hand.GetComponentsInChildren<Transform>(true)) if (t.name == finger) tip = t;
                tips += tip != null ? $" {finger.Substring(0, finger.IndexOf('_'))} {Off(tip.position) * 100f:F1}cm" : $" {finger} missing";
            }
            Animator animator = body.GetComponentInChildren<Animator>();
            int layer = animator.GetLayerIndex("Grip");
            float thumbSide = Vector3.Dot(head, fist.Across);
            Debug.Log($"[Grip] {label} at {Time.time:F2} s, hand in body {(Quaternion.Inverse(body.rotation) * hand.rotation).eulerAngles:F0}: head {body.InverseTransformDirection(head):F2} edge {body.InverseTransformDirection(edge):F2}"
                      + $" handle to fist line {across:F0} deg, thumb side {thumbSide:F2}, hollow {Off(fist.Center) * 100f:F1}cm, grip to hollow {(fist.Center - grip.position).magnitude * 100f:F1}cm,"
                      + $" tips{tips}, fist layer {(layer >= 0 ? animator.GetLayerWeight(layer) : -1f):F2}");
        }

        private static IEnumerator QuickShot(string folder, string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return null;
        }

        private static IEnumerator Shot(string folder, string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return new WaitForSeconds(0.6f);
            Debug.Log("[Shot] wrote " + name);
        }
    }
}
#endif
