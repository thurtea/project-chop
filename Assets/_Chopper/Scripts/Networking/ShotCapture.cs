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
            // Where the player first appeared, before the arrival drop (W9-01) has run its course.
            Vector3 firstSeen = PlayerWood.Local.transform.position;
            yield return new WaitForSeconds(2f);

            PlayerWood wood = PlayerWood.Local;
            PlayerInventory inventory = wood.GetComponent<PlayerInventory>();
            GameUi ui = UnityEngine.Object.FindAnyObjectByType<GameUi>();

            Debug.Log($"[Shot] player starts at {wood.transform.position} (first seen at {firstSeen:F1})");
            bool arrivalTour = Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperArrival") >= 0;
            // Every other tour tests something else: give the outfit and the axe that a player picks up on the shore.
            if (!arrivalTour && !TutorialSession.Active && wood.IsServerInitialized)
            {
                wood.GetComponent<PlayerUpgrades>().ServerSetDressed(true);
                PlayerAxe startAxe = wood.GetComponent<PlayerAxe>();
                if (!startAxe.HasAxe) startAxe.ServerResetTool();
            }
            // The tours build in the hub to test building itself (W9-03 keeps players from building there).
            BuildManager.DebugAllowHubBuilding = true;
            yield return Shot(folder, "1_day_hud");

            // Arrival (-chopperArrival, W9-01): the player falls into the hub pond with no outfit and no axe, swims to
            // the bank, picks both up on the shore; then falls in a fight and leaves both where they fell, and arrives
            // again in the pond empty-handed. Logs "[Arrival] RESULT".
            if (arrivalTour)
            {
                yield return ArrivalTour(folder, wood, ui, firstSeen);
                Application.Quit(0);
                yield break;
            }

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
                int comboStrikes = 0;
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
                        int chopLayer = comboAnimator.GetLayerIndex("Chop");
                        AnimatorStateInfo chopState = comboAnimator.GetCurrentAnimatorStateInfo(chopLayer);
                        AnimatorStateInfo chopNext = comboAnimator.GetNextAnimatorStateInfo(chopLayer);
                        string active = "?";
                        foreach (string n in names) if (chopState.IsName(n) || chopNext.IsName(n)) active = n;
                        if (swung && active != "Empty" && active != "?") comboStrikes++;
                        Debug.Log($"[Combo] pass {pass} step {step}: swung={swung} chop layer state={active} t={chopState.normalizedTime:F2} weight={comboAnimator.GetLayerWeight(chopLayer):F2}");
                        comboCamera.SnapYaw(-60f);
                        ui.DebugShow(0); yield return Shot(folder, $"combo_{(pass == 0 ? "axe" : "fist")}_{step + 1}");
                        comboCamera.SnapYaw(0f);
                        yield return new WaitForSeconds(0.12f);
                    }
                }
                Debug.Log($"[Combo] RESULT {(comboStrikes == 6 ? "PASS" : "FAIL")} ({comboStrikes} of 6 swings playing a strike on the Chop layer)");
                Application.Quit(0);
                yield break;
            }

            // Arm motion (-chopperAnimProbe): on flat yard ground, how far each upper arm swings out to the side
            // (abduction from hanging down) and forward and back, standing and walking, with the animator's grounded flag.
            // Logs "[Anim]" maxima and pictures side and front mid-walk.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperAnimProbe") >= 0)
            {
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.4f);
                yield return new WaitForSeconds(4f);
                ui.DebugShow(0);
                GameUi.HudHidden = true;
                Vector3 yard = Hut.Instance.transform.position;
                yield return TeleportLocalPlayer(new Vector2(yard.x - 10f, yard.z - 2f), 180f);
                PlayerCamera animCam = wood.GetComponent<PlayerCamera>();
                animCam.DebugSetDistance(3.4f);
                animCam.DebugSetPitch(5f);
                yield return new WaitForSeconds(1f);
                Animator anim = wood.GetComponentInChildren<Animator>();
                string[] armNames = { "left", "right" };
                Transform[] upper = { anim.GetBoneTransform(HumanBodyBones.LeftUpperArm), anim.GetBoneTransform(HumanBodyBones.RightUpperArm) };
                Transform[] lower = { anim.GetBoneTransform(HumanBodyBones.LeftLowerArm), anim.GetBoneTransform(HumanBodyBones.RightLowerArm) };
                var maxOut = new float[2];
                var minSwing = new float[2];
                var maxSwing = new float[2];
                int airborne = 0, frames = 0;
                IEnumerator Watch(float seconds)
                {
                    for (int k = 0; k < 2; k++) { maxOut[k] = 0f; minSwing[k] = 0f; maxSwing[k] = 0f; }
                    airborne = 0;
                    frames = 0;
                    for (float t = 0f; t < seconds; t += Time.deltaTime)
                    {
                        for (int k = 0; k < 2; k++)
                        {
                            Vector3 v = wood.transform.InverseTransformDirection(lower[k].position - upper[k].position).normalized;
                            maxOut[k] = Mathf.Max(maxOut[k], Mathf.Atan2(Mathf.Abs(v.x), -v.y) * Mathf.Rad2Deg);
                            float swing = Mathf.Atan2(v.z, -v.y) * Mathf.Rad2Deg;
                            minSwing[k] = Mathf.Min(minSwing[k], swing);
                            maxSwing[k] = Mathf.Max(maxSwing[k], swing);
                        }
                        if (!anim.GetBool("Grounded")) airborne++;
                        frames++;
                        yield return null;
                    }
                }
                string Report() => $"{armNames[0]} out {maxOut[0]:F0}, swing {minSwing[0]:F0}..{maxSwing[0]:F0}; {armNames[1]} out {maxOut[1]:F0}, swing {minSwing[1]:F0}..{maxSwing[1]:F0}; airborne {airborne}/{frames} frames";
                yield return Watch(2f);
                Debug.Log("[Anim] idle: " + Report());
                Gamepad animPad = InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(animPad, new GamepadState { leftStick = new Vector2(0f, 1f) });
                yield return new WaitForSeconds(0.6f);
                yield return Watch(2f);
                Debug.Log("[Anim] walk: " + Report());
                // Frame strips (one picture every 0.1 s) to see the motion: walking sideways across the view (stick left,
                // so the camera sees him from the side), then a chop.
                InputSystem.QueueStateEvent(animPad, new GamepadState { leftStick = new Vector2(-0.5f, 0f) });
                yield return new WaitForSeconds(0.8f);
                for (int f = 0; f < 8; f++)
                {
                    yield return Shot(folder, "anim_walk_" + f);
                    yield return new WaitForSeconds(0.1f);
                }
                InputSystem.QueueStateEvent(animPad, new GamepadState());
                InputSystem.RemoveDevice(animPad);
                yield return new WaitForSeconds(1f);
                PlayerAxe probeAxe = wood.GetComponent<PlayerAxe>();
                probeAxe.ServerSetHasAxe(true);
                yield return new WaitForSeconds(0.5f);
                // The three-swing combo away from trees, filmed from his right side.
                for (int swingIndex = 0; swingIndex < 3; swingIndex++)
                {
                    float wait = 0f;
                    bool swung = false;
                    while (!swung && wait < 2f)
                    {
                        swung = probeAxe.TrySwing();
                        if (!swung) { wait += Time.deltaTime; yield return null; }
                    }
                    animCam.SnapYaw(wood.transform.eulerAngles.y - 90f);
                    // How high the axe hand rises over the shoulder during the swing, and what the chop layer plays.
                    Transform hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                    Transform shoulder = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    float topRise = -9f;
                    string states = string.Empty;
                    int chopLayer = anim.GetLayerIndex("Chop");
                    for (float t = 0f; t < 0.7f; t += Time.deltaTime)
                    {
                        topRise = Mathf.Max(topRise, hand.position.y - shoulder.position.y);
                        if (chopLayer >= 0)
                        {
                            AnimatorClipInfo[] clips = anim.GetCurrentAnimatorClipInfo(chopLayer);
                            string playing = clips.Length > 0 ? clips[0].clip.name : "-";
                            if (!states.EndsWith(playing)) states += " > " + playing;
                        }
                        yield return null;
                    }
                    Debug.Log($"[Anim] swing {swingIndex} played {swung}: chop layer {chopLayer} weight {(chopLayer >= 0 ? anim.GetLayerWeight(chopLayer) : 0f):F2}, clips{states}, hand rose up to {topRise:F2} m over the shoulder");
                    yield return new WaitForSeconds(0.1f);
                }
                Debug.Log("[Anim] done");
                Application.Quit(0);
                yield break;
            }

            // Hub house (-chopperHouse): counts the beds and workbenches in the house, walks a virtual pad up the outside
            // steps, through the door and up the inside stairs to the top floor, and up from the basement to the main
            // floor, logging heights. Pictures outside and in, and "[House] RESULT".
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperHouse") >= 0)
            {
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                Hut.Instance.ServerSetRepaired(true);
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.4f);
                WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
                yield return new WaitForSeconds(4f);
                ui.DebugShow(0);
                GameUi.HudHidden = true;
                Vector3 home = Hut.Instance.transform.position;
                PlayerCamera houseCam = wood.GetComponent<PlayerCamera>();
                int beds = 0, benches = 0;
                foreach (Bed bed in UnityEngine.Object.FindObjectsByType<Bed>(FindObjectsSortMode.None))
                    if (Hut.Instance.DistanceToWalls(bed.transform.position) < 0.01f) beds++;
                foreach (Workbench bench in Workbench.All)
                    if (Hut.Instance.DistanceToWalls(bench.transform.position) < 0.01f) benches++;
                Transform cabin = Hut.Instance.transform.Find("Cabin");
                Debug.Log($"[House] pieces {(cabin != null ? cabin.childCount : 0)}, beds inside {beds}, workbenches inside {benches}");

                float hs = Hut.HouseScale, storey = Hut.StoreyHeight;
                float YawTo(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
                IEnumerator LookAtHouse(Vector2 from, float distance, float pitch, string name)
                {
                    float yaw = YawTo(new Vector3(from.x, 0f, from.y), home);
                    yield return TeleportLocalPlayer(from, yaw);
                    houseCam.DebugSetDistance(distance);
                    houseCam.SnapYaw(yaw);
                    houseCam.DebugSetPitch(pitch);
                    yield return new WaitForSeconds(1.5f);
                    yield return Shot(folder, name);
                }
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.85f);
                yield return new WaitForSeconds(2f);
                {
                    var hidden = new System.Collections.Generic.List<Renderer>();
                    foreach (Renderer r in PlayerWood.Local.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hidden.Add(r); }
                    yield return LookAtHouse(new Vector2(home.x + 22f, home.z + 30f), 3.5f, 2f, "house_concept_view");
                    foreach (Renderer r in hidden) r.enabled = true;
                }
                {
                    Vector2 pondAt = Chopper.World.Generation.WorldSession.Gen.PondCenter;
                    Vector2 hut2 = new Vector2(home.x, home.z);
                    Vector2 toHut = (hut2 - pondAt).normalized;
                    Vector2 side = new Vector2(-toHut.y, toHut.x);
                    Vector2 stand = pondAt + toHut * (Chopper.World.Generation.WorldGen.PondOuterRadius + 3f) + side * 9f;
                    if (Vector2.Distance(stand, hut2) > 45f) stand = hut2 + (stand - hut2).normalized * 45f;
                    yield return LookAtHouse(stand, 3.2f, 1f, "house_concept_pond");
                }
                yield return LookAtHouse(new Vector2(home.x + 12f, home.z + 24f), 6f, 10f, "house_out_front");
                yield return LookAtHouse(new Vector2(home.x - 24f, home.z - 8f), 6f, 10f, "house_out_side");
                yield return LookAtHouse(new Vector2(home.x + 1.5f * hs, home.z + 11f * hs), 4.2f, 16f, "house_out_steps");

                Gamepad walkPad = InputSystem.AddDevice<Gamepad>();
                // Walks to a point in piece-metres of the house (x, z), steering every frame: the stick is read relative
                // to the camera, so the direction is turned by its yaw.
                IEnumerator WalkTo(float x, float z, float seconds)
                {
                    Vector3 target = home + new Vector3(x, 0f, z) * hs;
                    for (float t = 0f; t < seconds; t += Time.deltaTime)
                    {
                        Vector3 to = target - wood.transform.position;
                        to.y = 0f;
                        if (to.magnitude < 0.3f) break;
                        Vector3 stick = Quaternion.Euler(0f, -houseCam.Yaw, 0f) * to.normalized;
                        InputSystem.QueueStateEvent(walkPad, new GamepadState { leftStick = new Vector2(stick.x, stick.z) });
                        yield return null;
                    }
                    InputSystem.QueueStateEvent(walkPad, new GamepadState());
                    yield return new WaitForSeconds(0.4f);
                }
                float Rise() => wood.transform.position.y - home.y;

                // The outside steps to the door, across the main floor, and a look round every wall and up at the roof.
                yield return TeleportLocalPlayer(new Vector2(home.x + 1f * hs, home.z + 6.2f * hs), 180f);
                houseCam.DebugSetDistance(2.6f);
                houseCam.SnapYaw(180f);
                houseCam.DebugSetPitch(-12f);
                yield return new WaitForSeconds(0.8f);
                yield return Shot(folder, "house_porch_behind");
                yield return TeleportLocalPlayer(new Vector2(home.x + 1f * hs, home.z + 7f * hs), 180f);
                houseCam.DebugSetDistance(3f);
                yield return new WaitForSeconds(0.8f);
                float startRise = Rise();
                yield return WalkTo(1f, 4.3f, 4f);
                houseCam.DebugSetPitch(10f);
                yield return new WaitForSeconds(0.2f);
                yield return Shot(folder, "house_door_walkthrough");
                yield return WalkTo(1f, 2.8f, 4f);
                float atDoor = Rise();
                Vector3 doorAt = wood.transform.position - home;
                Debug.Log($"[House] outside steps: from {startRise:F2} m to {atDoor:F2} m, at {doorAt.x:F2},{doorAt.z:F2}");
                houseCam.DebugSetPitch(20f);
                yield return new WaitForSeconds(0.5f);
                yield return Shot(folder, "house_in_main");
                yield return WalkTo(-1f, 0f, 4f);
                float atTop = Rise();
                Vector3 topAt = wood.transform.position - home;
                Debug.Log($"[House] one floor: main floor at {atTop:F2} m at {topAt.x:F2},{topAt.z:F2}");
                houseCam.DebugSetDistance(2.2f);
                foreach ((float yaw, float pitch, string label) in new[] { (0f, 8f, "house_in_back"), (90f, 8f, "house_in_west"), (180f, 8f, "house_in_front"), (270f, 8f, "house_in_east"), (0f, 55f, "house_in_roof") })
                {
                    houseCam.SnapYaw(yaw);
                    houseCam.DebugSetPitch(pitch);
                    yield return new WaitForSeconds(0.6f);
                    yield return Shot(folder, label);
                }
                float upstairs = Rise();

                ui.DebugShow(0);
                // The basement, up its stairs at the -X end to the main floor.
                yield return TeleportLocalPlayer(new Vector2(home.x - 3f * hs, home.z + 2f * hs), 180f);
                houseCam.SnapYaw(0f);
                houseCam.DebugSetPitch(15f);
                yield return new WaitForSeconds(0.8f);
                float basement = Rise();
                yield return Shot(folder, "house_in_basement");
                yield return WalkTo(-5f, 2.6f, 5f);
                houseCam.SnapYaw(0f);
                houseCam.DebugSetPitch(20f);
                houseCam.DebugSetDistance(3f);
                yield return Shot(folder, "house_stairs_bottom");
                yield return WalkTo(-5f, 0f, 4f);
                Debug.Log($"[House] stairs mid rise {Rise():F2} m at {(wood.transform.position - home).x / hs:F2},{(wood.transform.position - home).z / hs:F2}");
                yield return Shot(folder, "house_stairs_mid");
                yield return WalkTo(-5f, -3f, 5f);
                float fromBasement = Rise();
                Debug.Log($"[House] upstairs across the floor at {upstairs:F2} m; basement at {basement:F2} m, up its stairs to {fromBasement:F2} m");
                InputSystem.RemoveDevice(walkPad);

                // Each floor to the front yard. PathComplete means the doorway and the stairs are on the NavMesh.
                bool pathsOut = true;
                {
                    Vector3 yard = home + new Vector3(1f, 0f, 12f) * hs;
                    yard.y = WorldGround.HeightAt(yard);
                    bool yardOnMesh = UnityEngine.AI.NavMesh.SamplePosition(yard, out UnityEngine.AI.NavMeshHit yardHit, 3f, UnityEngine.AI.NavMesh.AllAreas);
                    foreach ((string floor, Vector3 local) in new[] { ("basement", new Vector3(-3f, 0f, 2f)), ("main floor", new Vector3(-2f, 2f, 0f)) })
                    {
                        Vector3 from = home + local * hs + Vector3.down * Chopper.World.Generation.WorldGen.HutSink;
                        if (!yardOnMesh || !UnityEngine.AI.NavMesh.SamplePosition(from, out UnityEngine.AI.NavMeshHit fromHit, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            pathsOut = false;
                            Debug.Log($"[House] nav {floor}: {(yardOnMesh ? "no NavMesh on this floor" : "no NavMesh in the yard")}");
                            continue;
                        }
                        var path = new UnityEngine.AI.NavMeshPath();
                        UnityEngine.AI.NavMesh.CalculatePath(fromHit.position, yardHit.position, UnityEngine.AI.NavMesh.AllAreas, path);
                        if (path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete) pathsOut = false;
                        Vector3 end = path.corners.Length > 0 ? path.corners[path.corners.Length - 1] - home : Vector3.zero;
                        Debug.Log($"[House] nav {floor}: {path.status}, {path.corners.Length} corners, ends at {end.x / hs:F1},{end.y / hs:F1},{end.z / hs:F1} piece-m from the hut (yard at 1,?,12)");
                    }
                }

                Debug.Log($"[House] check paths {pathsOut} beds {beds} benches {benches} door {atDoor - basement:F2} doorZ {doorAt.z:F2} top {atTop - basement:F2} up {upstairs - basement:F2} basement {basement:F2} fromBasement {fromBasement:F2} storey {storey:F2}");
                bool houseOk = pathsOut && beds == 2 && benches == 2 && atDoor - basement > storey - 0.2f && doorAt.z < Hut.HouseHalfSize.y
                               && Mathf.Abs(atTop - basement - storey) < 0.35f && Mathf.Abs(upstairs - basement - storey) < 0.35f && basement < 0.6f && fromBasement - basement > storey - 0.2f;
                Debug.Log("[House] RESULT " + (houseOk ? "PASS" : "FAIL"));
                Application.Quit(houseOk ? 0 : 1);
                yield break;
            }

            // Armour kit (-chopperArmour): the player starts unarmoured, a felled boss tree (EnemyDirector.ServerOnBossKilled)
            // gives a kit, and using it from the bag shows the helmet and shield and hides the hair (chainmail and bracers come with the outfit).
            // Pictures before and after, and "[Armour] RESULT".
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperArmour") >= 0)
            {
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.4f);
                WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
                yield return new WaitForSeconds(4f);
                ui.DebugShow(0);
                GameUi.HudHidden = true;
                yield return TeleportLocalPlayer(new Vector2(6f, 15f), 0f);
                PlayerCamera armourCam = wood.GetComponent<PlayerCamera>();
                armourCam.DebugSetDistance(3.2f);
                armourCam.SnapYaw(180f);
                armourCam.DebugSetPitch(8f);
                yield return new WaitForSeconds(1.5f);
                PlayerUpgrades kitUpgrades = wood.GetComponent<PlayerUpgrades>();
                PlayerOutfit outfit = wood.GetComponent<PlayerOutfit>();
                bool Shown(string part)
                {
                    foreach (Renderer r in wood.GetComponentsInChildren<Renderer>(true))
                    {
                        for (Transform t = r.transform; t != null && t != wood.transform; t = t.parent)
                            if (t.name == part) return r.enabled && r.gameObject.activeInHierarchy;
                    }
                    return false;
                }
                string Look() => $"helmet {Shown("chopper_helmet")}, chainmail {Shown("chopper_chainmail")}, bracers {Shown("chopper_bracers")}, shield {Shown("chopper_shield")}, hair {Shown("chopper_hair")}, beard {Shown("chopper_beard")}, vest {Shown("chopper_vest")}";
                float reductionBefore = kitUpgrades.ArmorReduction;
                bool startOk = outfit != null && !kitUpgrades.Armoured && kitUpgrades.ArmourKits == 0
                               && !Shown("chopper_helmet") && Shown("chopper_chainmail") && Shown("chopper_bracers") && !Shown("chopper_shield")
                               && Shown("chopper_hair") && Shown("chopper_vest");
                Debug.Log($"[Armour] start: outfit view {outfit != null}, armoured {kitUpgrades.Armoured}, kits {kitUpgrades.ArmourKits}; {Look()}");
                yield return Shot(folder, "armour_0_start");

                Chopper.Enemies.EnemyDirector.Instance.ServerOnBossKilled();
                yield return new WaitForSeconds(0.5f);
                bool kitOk = kitUpgrades.ArmourKits == 1 && !kitUpgrades.Armoured;
                Debug.Log($"[Armour] after the boss: kits {kitUpgrades.ArmourKits}, armoured {kitUpgrades.Armoured}");

                kitUpgrades.RequestUseArmourKit();
                yield return new WaitForSeconds(1f);
                bool wornOk = kitUpgrades.Armoured && kitUpgrades.ArmourKits == 0
                              && Shown("chopper_helmet") && Shown("chopper_chainmail") && Shown("chopper_bracers") && Shown("chopper_shield")
                              && !Shown("chopper_hair") && Shown("chopper_beard");
                bool reductionOk = Mathf.Abs(kitUpgrades.ArmorReduction - reductionBefore - PlayerUpgrades.ArmouredReduction) < 0.001f;
                Debug.Log($"[Armour] used: armoured {kitUpgrades.Armoured}, kits {kitUpgrades.ArmourKits}, reduction {reductionBefore:F2} -> {kitUpgrades.ArmorReduction:F2}; {Look()}");
                yield return Shot(folder, "armour_1_worn");
                armourCam.SnapYaw(0f);
                yield return new WaitForSeconds(0.6f);
                yield return Shot(folder, "armour_2_worn_back");

                // A second kit cannot be used once the armour is on.
                Chopper.Enemies.EnemyDirector.Instance.ServerOnBossKilled();
                yield return new WaitForSeconds(0.3f);
                bool secondRefused = !kitUpgrades.ServerUseArmourKit() && kitUpgrades.ArmourKits == 1;
                Debug.Log($"[Armour] start ok {startOk}, kit ok {kitOk}, worn ok {wornOk}, reduction ok {reductionOk}, second kit refused {secondRefused}");
                bool armourPass = startOk && kitOk && wornOk && reductionOk && secondRefused;
                Debug.Log("[Armour] RESULT " + (armourPass ? "PASS" : "FAIL"));
                Application.Quit(armourPass ? 0 : 1);
                yield break;
            }

            // Double jump (-chopperDoubleJump): a virtual pad's A makes one jump, then a jump and a second jump in the air,
            // then a third press in the air that must do nothing. Logs the peak heights and "[DoubleJump] RESULT".
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperDoubleJump") >= 0)
            {
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.4f);
                yield return new WaitForSeconds(4f);
                ui.DebugShow(0);
                // Open sky, clear of the hut porch and of trees.
                Vector2 open = new Vector2(0f, 40f);
                for (int tries = 0; tries < 60; tries++)
                {
                    Vector2 candidate = new Vector2(-30f + tries * 1.7f, 30f + (tries % 7) * 3f);
                    if (!TreeManager.Instance.ServerFindTree(new Vector3(candidate.x, 0f, candidate.y), Vector3.forward, out _, out _)) { open = candidate; break; }
                }
                yield return TeleportLocalPlayer(open, 0f);
                yield return new WaitForSeconds(1.5f);
                PlayerMotor jumper = wood.GetComponent<PlayerMotor>();
                PlayerStamina jumpStamina = wood.GetComponent<PlayerStamina>();
                Gamepad jumpPad = InputSystem.AddDevice<Gamepad>();
                yield return new WaitForSeconds(0.3f);
                float baseY = wood.transform.position.y;
                float peak = baseY;
                IEnumerator Press()
                {
                    InputSystem.QueueStateEvent(jumpPad, new GamepadState().WithButton(GamepadButton.South));
                    for (float t = 0f; t < 0.12f; t += Time.deltaTime) { peak = Mathf.Max(peak, wood.transform.position.y); yield return null; }
                    InputSystem.QueueStateEvent(jumpPad, new GamepadState());
                    for (float t = 0f; t < 0.3f; t += Time.deltaTime) { peak = Mathf.Max(peak, wood.transform.position.y); yield return null; }
                }
                IEnumerator Land()
                {
                    for (float t = 0f; t < 3f; t += Time.deltaTime)
                    {
                        peak = Mathf.Max(peak, wood.transform.position.y);
                        if (t > 0.3f && wood.GetComponent<CharacterController>().isGrounded) break;
                        yield return null;
                    }
                    yield return new WaitForSeconds(0.5f);
                }

                yield return Press();
                yield return Land();
                float single = peak - baseY;
                int afterSingle = jumper.AirJumpCount;

                baseY = wood.transform.position.y;
                peak = baseY;
                yield return Press();
                yield return Press();
                int afterDouble = jumper.AirJumpCount;
                yield return Press();
                int afterThird = jumper.AirJumpCount;
                yield return Land();
                float twice = peak - baseY;

                baseY = wood.transform.position.y;
                peak = baseY;
                yield return Press();
                InputSystem.QueueStateEvent(jumpPad, new GamepadState().WithButton(GamepadButton.South));
                for (float t = 0f; t < 0.2f; t += Time.deltaTime) { peak = Mathf.Max(peak, wood.transform.position.y); yield return null; }
                InputSystem.QueueStateEvent(jumpPad, new GamepadState());
                bool flipping = wood.GetComponent<PlayerAnimator>().IsFlipping;
                yield return Shot(folder, "doublejump_flip");
                yield return Land();
                float again = peak - baseY;
                int afterAgain = jumper.AirJumpCount;
                InputSystem.RemoveDevice(jumpPad);

                Debug.Log($"[DoubleJump] single peak {single:F2} m, air jumps {afterSingle}");
                Debug.Log($"[DoubleJump] double peak {twice:F2} m, air jumps {afterDouble}, after a third press {afterThird}");
                Debug.Log($"[DoubleJump] flip playing 0.2 s after the second jump: {flipping}");
                Debug.Log($"[DoubleJump] after landing, double peak {again:F2} m, air jumps {afterAgain}, stamina {(jumpStamina != null ? jumpStamina.Current.ToString("F0") : "-")}");
                bool jumpOk = single > 0.9f && single < 1.5f && afterSingle == 0
                              && afterDouble == 1 && afterThird == 1 && twice > single + 0.7f
                              && afterAgain == 2 && again > single + 0.7f && flipping;
                Debug.Log("[DoubleJump] RESULT " + (jumpOk ? "PASS" : "FAIL"));
                Application.Quit(jumpOk ? 0 : 1);
                yield break;
            }

            // Concept 2.0 comparison (-chopperConcept): the same framings as chopper-3D-concept-2.0.jpg, armoured Chopper in
            // front of the hut in late afternoon light, the hut on its own, the ground and grass low down, Chopper close.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperConcept") >= 0)
            {
                Hut.Instance.ServerSetRepaired(true);
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.75f);
                WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
                wood.GetComponent<PlayerAxe>().ServerSetHasAxe(true);
                wood.GetComponent<PlayerUpgrades>().ServerLoadOutfit(0, true);
                yield return new WaitForSeconds(4f);
                GameUi.HudHidden = true;
                ui.DebugShow(0);
                Debug.developerConsoleVisible = false;
                Debug.Log($"[Concept] hut at {Hut.Instance.transform.position} rotation {Hut.Instance.transform.eulerAngles.y:F0}");
                // (player position, body yaw, camera yaw, distance, pitch, name)
                (Vector2 at, float body, float camYaw, float dist, float pitch, string name)[] views =
                {
                    (new Vector2(6f, 15f), 20f, 200f, 3.6f, 4f, "concept_view"),
                    (new Vector2(-4f, 30f), 180f, 170f, 9f, 8f, "hut_front"),
                    (new Vector2(14f, 12f), 230f, 230f, 2.2f, 14f, "ground_grass"),
                    (new Vector2(6f, 15f), 0f, 180f, 2.0f, 2f, "character_front"),
                };
                foreach (var v in views)
                {
                    yield return TeleportLocalPlayer(v.at, v.body);
                    PlayerCamera cam = PlayerCamera.Local;
                    cam.DebugSetDistance(v.dist);
                    cam.DebugSetPitch(v.pitch);
                    cam.SnapYaw(v.camYaw);
                    yield return new WaitForSeconds(1.5f);
                    Debug.developerConsoleVisible = false;
                    yield return Shot(folder, v.name);
                }
                {
                    PlayerAxe holster = wood.GetComponent<PlayerAxe>();
                    holster.ServerPickUp(holster.MaxDurability);
                    yield return new WaitForSeconds(0.4f);
                    yield return TeleportLocalPlayer(new Vector2(6f, 15f), 0f);
                    PlayerCamera cam = PlayerCamera.Local;
                    Vector3 at = wood.transform.position;
                    Vector3 watch = at - wood.transform.forward * 3.0f + Vector3.up * 1.55f;
                    cam.DebugHold(watch, Quaternion.LookRotation(at + Vector3.up * 1.1f - watch));
                    yield return new WaitForSeconds(0.4f);
                    yield return Shot(folder, "character_back");
                    cam.DebugRelease();
                }
                wood.GetComponent<PlayerUpgrades>().ServerLoadOutfit(0, false);
                wood.GetComponent<PlayerAxe>().ServerSetHasAxe(true);
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.45f);
                (float body, float camYaw, string name)[] sheetViews =
                {
                    (0f, 180f, "sheet_front"),
                    (0f, 215f, "sheet_three_quarter"),
                };
                foreach (var v in sheetViews)
                {
                    yield return TeleportLocalPlayer(new Vector2(6f, 15f), v.body);
                    PlayerCamera cam = PlayerCamera.Local;
                    cam.DebugSetDistance(2.2f);
                    cam.DebugSetPitch(2f);
                    cam.SnapYaw(v.camYaw);
                    yield return new WaitForSeconds(1.5f);
                    Debug.developerConsoleVisible = false;
                    yield return Shot(folder, v.name);
                }
                {
                    PlayerAxe holster = wood.GetComponent<PlayerAxe>();
                    holster.ServerPickUp(holster.MaxDurability);
                    yield return new WaitForSeconds(0.4f);
                    yield return TeleportLocalPlayer(new Vector2(6f, 15f), 0f);
                    PlayerCamera cam = PlayerCamera.Local;
                    Vector3 at = wood.transform.position;
                    Vector3 back = at - wood.transform.forward * 3.0f + Vector3.up * 1.55f;
                    cam.DebugHold(back, Quaternion.LookRotation(at + Vector3.up * 1.1f - back));
                    yield return new WaitForSeconds(0.4f);
                    yield return Shot(folder, "sheet_back");
                    Vector3 above = at + Vector3.up * 2.7f - wood.transform.forward * 0.35f;
                    cam.DebugHold(above, Quaternion.LookRotation(at + Vector3.up * 1.05f - above));
                    yield return new WaitForSeconds(0.3f);
                    yield return Shot(folder, "sheet_above");
                    Vector3 hut = Hut.Instance.transform.position;
                    Vector3 toHut = hut - at;
                    toHut.y = 0f;
                    if (toHut.sqrMagnitude < 0.01f) toHut = Vector3.forward;
                    toHut.Normalize();
                    wood.transform.rotation = Quaternion.LookRotation(toHut);
                    Vector3 meadow = at - toHut * 8.5f + Vector3.up * 2.4f;
                    cam.DebugHold(meadow, Quaternion.LookRotation(at + Vector3.up * 1.0f - meadow));
                    yield return new WaitForSeconds(0.3f);
                    yield return Shot(folder, "meadow_behind");
                    Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
                    if (gen != null)
                    {
                        Vector2 bank = gen.PondCenter + Vector2.right * (Chopper.World.Generation.WorldGen.PondOuterRadius + 1.2f);
                        yield return TeleportLocalPlayer(bank, 270f);
                        at = wood.transform.position;
                        Vector3 pondCam = at - wood.transform.forward * 3.4f + Vector3.up * 1.65f;
                        cam.DebugHold(pondCam, Quaternion.LookRotation(at + Vector3.up * 1.1f - pondCam));
                        yield return new WaitForSeconds(0.4f);
                        var shown = new System.Collections.Generic.List<string>();
                        foreach (Renderer r in wood.GetComponentsInChildren<Renderer>(true))
                        {
                            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                            shown.Add(r.gameObject.name);
                        }
                        Debug.Log("[Concept] pond_behind renderers " + string.Join(", ", shown));
                        yield return Shot(folder, "pond_behind");
                    }
                    cam.DebugRelease();
                }
                Debug.Log("[Concept] done");
                Application.Quit(0);
                yield break;
            }

            // Flicker in motion (-chopperFlicker): the camera orbits the standing player slowly at a fixed frame step
            // and every frame is saved, once per setting (as is, sun shadows off, anti-aliasing off, character atlas
            // mip bias +1, ground block detail off). Tools/flicker_measure.py scores the shimmer between frames.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperFlicker") >= 0)
            {
                yield return FlickerTour(folder, wood, ui);
                Debug.Log("[Flicker] done");
                Application.Quit(0);
                yield break;
            }

            // Block ground (-chopperBlockGround, W5-08 step 5): the chunk colliders are the ground and the Terrain
            // collider is off; the player stands on the block top at several places (feet within 12 cm, the skin, of the column
            // top); every tree entry sits on its column top; pictures of the steps. Logs "[BlockGround] RESULT".
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperBlockGround") >= 0)
            {
                Hut.Instance.ServerSetRepaired(true);
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.4f);
                WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
                yield return new WaitForSeconds(4f);
                GameUi.HudHidden = true;
                ui.DebugShow(0);
                var blockColumns = Chopper.World.Voxel.VoxelColumns.Current;
                Terrain smoothLand = Terrain.activeTerrain;
                TerrainCollider smoothCollider = smoothLand != null ? smoothLand.GetComponent<TerrainCollider>() : null;
                GameObject chunkRoot = GameObject.Find("VoxelWorld");
                int chunkColliders = chunkRoot != null ? chunkRoot.GetComponentsInChildren<MeshCollider>().Length : 0;
                bool terrainOff = smoothCollider == null || !smoothCollider.enabled;
                Debug.Log($"[BlockGround] columns {(blockColumns != null ? blockColumns.Crc.ToString("X8") : "none")}, chunk colliders {chunkColliders}, terrain collider {(terrainOff ? "off" : "ON")}");

                Vector2[] spots = { new Vector2(0f, 9f), new Vector2(0f, 30f), new Vector2(40f, 40f), new Vector2(-60f, 20f), new Vector2(25f, -70f), new Vector2(-90f, -90f) };
                float worstFeet = 0f;
                int feetChecked = 0;
                foreach (Vector2 at in spots)
                {
                    yield return TeleportLocalPlayer(at, 0f);
                    yield return new WaitForSeconds(1.2f);
                    Vector3 p = wood.transform.position;
                    if (blockColumns == null || !blockColumns.TryTopAt(p.x, p.z, out float top)) continue;
                    // Only judge spots where the feet are on the blocks (the hub spot stands on the house floor).
                    if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out RaycastHit under, 2f, ~0, QueryTriggerInteraction.Ignore)
                        || !Chopper.World.Voxel.VoxelWorld.IsGround(under.collider))
                    {
                        Debug.Log($"[BlockGround] at {at}: standing on {(under.collider != null ? under.collider.name : "nothing")}, not the blocks; skipped");
                        continue;
                    }
                    // On a step edge the controller may rest on the higher neighbour: take the nearest top of the 3 x 3.
                    float best = float.MaxValue;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                            if (blockColumns.TryTopAt(p.x + dx * 0.35f, p.z + dz * 0.35f, out float t)) best = Mathf.Min(best, Mathf.Abs(p.y - t));
                    feetChecked++;
                    worstFeet = Mathf.Max(worstFeet, best);
                    Debug.Log($"[BlockGround] at {at}: feet y {p.y:F3}, block top {top:F2}, nearest top off by {best:F3} m");
                }

                int treesOff = 0, treesChecked = 0;
                var treeList = Chopper.World.Generation.WorldSession.Trees;
                for (int i = 0; treeList != null && blockColumns != null && i < treeList.Count; i++)
                {
                    Vector3 tp = treeList.Get(i).Position;
                    if (!blockColumns.TryTopAt(tp.x, tp.z, out float top)) continue;
                    treesChecked++;
                    if (Mathf.Abs(tp.y - top) > 0.001f) treesOff++;
                }
                Debug.Log($"[BlockGround] trees on their block top: {treesChecked - treesOff} of {treesChecked}");

                yield return TeleportLocalPlayer(new Vector2(-60f, 20f), 120f);
                PlayerCamera groundCam = wood.GetComponent<PlayerCamera>();
                groundCam.DebugSetDistance(6f);
                groundCam.DebugSetPitch(22f);
                groundCam.SnapYaw(120f);
                yield return new WaitForSeconds(1.5f);
                yield return Shot(folder, "blocks_hills");
                yield return TeleportLocalPlayer(new Vector2(40f, 40f), 210f);
                groundCam.SnapYaw(210f);
                yield return new WaitForSeconds(1.5f);
                yield return Shot(folder, "blocks_meadow");

                // Building on blocks (W5-08 step 10): a floor on a flat cell sits exactly on the block top, a floor over a
                // half-block step stands on the higher top, and a footprint over more than half a block is too steep.
                bool buildOk;
                {
                    BuildManager build = BuildManager.Instance;
                    PieceCatalog catalog = build != null ? build.Catalog : null;
                    int floorKind = CabinKind(catalog, "Wood floor");
                    int benchKind = CabinKind(catalog, "Workbench");
                    PieceDefinition floorDef = catalog != null && floorKind >= 0 ? catalog.Get(floorKind) : null;
                    string flat = "none", half = "none", steep = "none";
                    bool flatOk = false, halfOk = true, steepOk = true;
                    int steepSeen = 0, halfSeen = 0;
                    PlaceResult benchResult = PlaceResult.BadKind;
                    bool hubRefused = false;
                    bool forestOpen = false;
                    // The meadow for the flat and half-block cells, then hillier ground until a steeper cell turns up.
                    foreach (Vector2 buildSpot in new[] { new Vector2(40f, 40f), new Vector2(-60f, 20f), new Vector2(60f, 92f), new Vector2(-92f, 60f), new Vector2(92f, -60f) })
                    {
                    if (buildSpot.x != 40f && steepSeen > 0) break;
                    yield return TeleportLocalPlayer(buildSpot, 0f);
                    yield return new WaitForSeconds(0.8f);
                    wood.ServerAddWood(200);
                    Vector3 me = wood.transform.position;
                    Vector3 benchAt = me + Vector3.right * 2.5f;
                    benchAt.y = build != null ? build.OnBlockTop(benchKind, benchAt, 0f) : benchAt.y;
                    if (buildSpot.x == 40f && build != null)
                    {
                        // W9-03: (40, 40) is inside the 110 m hub. With the rule on, that workbench is refused,
                        // and the same check just past the earth pass is not.
                        BuildManager.DebugAllowHubBuilding = false;
                        hubRefused = build.Check(wood.gameObject, benchKind, benchAt, 0f) == PlaceResult.HubProtected;
                        Debug.Log($"[BlockGround] building in the hub refused: {hubRefused}");
                        Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
                        if (gen != null)
                        {
                            Vector2 gate = gen.ZoneGatePosition(Element.Earth);
                            Vector2 outward = gate.sqrMagnitude > 1f ? gate.normalized : Vector2.right;
                            Vector2 forest = gate + outward * 36f;
                            yield return TeleportLocalPlayer(forest, 0f);
                            yield return new WaitForSeconds(0.4f);
                            Vector3 forestAt = wood.transform.position + Vector3.right * 2f;
                            forestAt.y = build.OnBlockTop(benchKind, forestAt, 0f);
                            PlaceResult forestResult = build.Check(wood.gameObject, benchKind, forestAt, 0f);
                            forestOpen = forestResult != PlaceResult.HubProtected
                                && ZoneMap.ElementAt(forestAt, gen.HubRadius) != Element.Neutral;
                            Debug.Log($"[BlockGround] building in a forest allowed: {forestOpen} ({forestResult})");
                        }
                        BuildManager.DebugAllowHubBuilding = true;
                        yield return TeleportLocalPlayer(buildSpot, 0f);
                        yield return new WaitForSeconds(0.4f);
                    }
                    PlaceResult benchHere = build != null ? build.ServerPlace(wood.gameObject, benchKind, benchAt, 0f) : PlaceResult.BadKind;
                    if (buildSpot.x == 40f) benchResult = benchHere;
                    for (int dz = -2; dz <= 2 && floorDef != null; dz++)
                    {
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            float cx = BuildRules.CellCenter(me.x + dx * BuildRules.Cell), cz = BuildRules.CellCenter(me.z + dz * BuildRules.Cell);
                            var probeAt = new Vector3(cx, 0f, cz);
                            Bounds box = BuildRules.BoundsOf(probeAt, floorDef.Size, 0f);
                            if (box.Intersects(BuildRules.BoundsOf(benchAt, catalog.Get(benchKind).Size, 0f))) continue;
                            if (!WorldGround.FootprintTops(box, out float lo, out float hi)) continue;
                            probeAt.y = build.OnBlockTop(floorKind, probeAt, 0f);
                            float step = hi - lo;
                            if (step < 0.01f && flat == "none")
                            {
                                PlaceResult placed = build.ServerPlace(wood.gameObject, floorKind, probeAt, 0f);
                                bool flush = false;
                                for (int i = 0; i < build.Pieces.Count; i++)
                                    if (build.Pieces[i].Kind == floorKind && (build.Pieces[i].Position - probeAt).sqrMagnitude < 0.01f)
                                        flush = Mathf.Abs(build.Pieces[i].Position.y - hi) < 0.001f;
                                flatOk = placed == PlaceResult.Ok && flush;
                                flat = $"{placed} at y {probeAt.y:F2} on top {hi:F2}, flush {flush}";
                            }
                            else if (step > 0.4f && step < 0.6f)
                            {
                                PlaceResult r = build.Check(wood.gameObject, floorKind, probeAt, 0f);
                                halfSeen++;
                                if (r != PlaceResult.Ok && r != PlaceResult.Overlaps)
                                {
                                    halfOk = false;
                                    Debug.Log($"[BlockGround] half-block cell at {probeAt:F1} over {lo:F1}..{hi:F1} gave {r}");
                                }
                                if (half == "none") half = $"{r} over {lo:F1}..{hi:F1}";
                            }
                            else if (step > 0.6f)
                            {
                                PlaceResult r = build.Check(wood.gameObject, floorKind, probeAt, 0f);
                                steepSeen++;
                                // Any refusal will do (high up a riser it is also out of reach); it must never be Ok.
                                if (r == PlaceResult.Ok)
                                {
                                    steepOk = false;
                                    Debug.Log($"[BlockGround] steeper cell at {probeAt:F1} over {lo:F1}..{hi:F1} gave {r}");
                                }
                                if (steep == "none") steep = $"{r} over {lo:F1}..{hi:F1}";
                            }
                        }
                    }
                    }
                    buildOk = benchResult == PlaceResult.Ok && flatOk && halfOk && steepOk && steepSeen > 0 && hubRefused && forestOpen;
                    Debug.Log($"[BlockGround] building: workbench {benchResult}; flat cell {flat}; half-block cells {halfSeen} ({half}); steeper cells {steepSeen} ({steep})");
                    yield return Shot(folder, "blocks_build");
                }

                // Climb out of the hub pond: swim from its middle toward the hut side and pull up onto the bank blocks.
                Chopper.World.Generation.WorldGen pondGen = Chopper.World.Generation.WorldSession.Gen;
                Vector2 pond = pondGen.PondCenter;
                Vector2 toHub = pond.sqrMagnitude > 1f ? -pond.normalized : Vector2.up;
                float outYaw = Mathf.Atan2(toHub.x, toHub.y) * Mathf.Rad2Deg;
                yield return TeleportLocalPlayer(pond, outYaw);
                yield return new WaitForSeconds(1.5f);
                PlayerMotor swimMotor = wood.GetComponent<PlayerMotor>();
                bool startedSwimming = swimMotor.IsSwimming;
                Gamepad swimPad = InputSystem.AddDevice<Gamepad>();
                float swimStart = Time.time;
                while (Time.time - swimStart < 20f && (swimMotor.IsSwimming || Time.time - swimStart < 1f))
                {
                    InputSystem.QueueStateEvent(swimPad, new GamepadState { leftStick = Vector2.up });
                    groundCam.SnapYaw(outYaw);
                    yield return null;
                }
                for (float t = 0f; t < 1.5f; t += Time.deltaTime)
                {
                    InputSystem.QueueStateEvent(swimPad, new GamepadState { leftStick = Vector2.up });
                    yield return null;
                }
                InputSystem.QueueStateEvent(swimPad, new GamepadState());
                InputSystem.RemoveDevice(swimPad);
                yield return new WaitForSeconds(0.5f);
                Vector3 outAt = wood.transform.position;
                bool climbedOut = startedSwimming && !swimMotor.IsSwimming
                                  && outAt.y > Chopper.World.Generation.WorldGen.WaterHeight - 0.2f;
                Debug.Log($"[BlockGround] pond climb-out: started swimming {startedSwimming}, after {Time.time - swimStart:F1} s at {outAt:F2}, swimming {swimMotor.IsSwimming}, ground {Chopper.World.WorldGround.HeightAt(outAt):F2}");
                yield return Shot(folder, "blocks_pond_out");

                bool groundPass = blockColumns != null && chunkColliders > 0 && terrainOff && feetChecked >= 4 && worstFeet < 0.12f
                                  && treesChecked > 0 && treesOff == 0 && climbedOut && buildOk;
                Debug.Log($"[BlockGround] RESULT {(groundPass ? "PASS" : "FAIL")} (feet checked {feetChecked}, worst {worstFeet:F3} m; trees off {treesOff}; climbed out of the pond {climbedOut}; building {buildOk})");
                Application.Quit(groundPass ? 0 : 1);
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
                Debug.Log("[AxeHand] " + wood.GetComponent<PlayerAxe>().HandReport());
                if (hosting)
                {
                    Vector3 row = wood.transform.position + Vector3.forward * 4f;
                    for (int e = 0; e < 5; e++)
                    {
                        Vector3 at = row + Vector3.right * (e - 2) * 1.6f;
                        at.y = Chopper.World.WorldGround.HeightAt(at);
                        EnemyDirector.Instance.ServerSpawn((Element)e, TreeSize.Small, false, at);
                    }
                    yield return new WaitForSeconds(1.5f);
                    PlayerCamera rowCamera = wood.GetComponent<PlayerCamera>();
                    rowCamera.SnapYaw(0f);
                    yield return Shot(folder, "sapling_row");
                    int voxelSaplings = 0, saplings = 0;
                    foreach (Treant t in Treant.All)
                    {
                        if (t == null || t.IsActive) continue;
                        saplings++;
                        foreach (Transform c in t.GetComponentsInChildren<Transform>()) if (c.name.StartsWith("prop_sapling_")) { voxelSaplings++; break; }
                    }
                    Debug.Log($"[Sapling] {saplings} dormant, {voxelSaplings} with the voxel look");
                }
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
                            Debug.Log($"[Move] FREEZE {leg.label}: stopped at {now:F1} with the stick held; ui blocked {UiState.GameplayBlocked}, fishing {wood.GetComponent<PlayerFishing>().Busy}, swimming {wood.GetComponent<PlayerMotor>().IsSwimming}, down {wood.GetComponent<PlayerHealth>().IsDown}, controller {wood.GetComponent<CharacterController>().enabled}");
                            var freezeColumns = Chopper.World.Voxel.VoxelColumns.Current;
                            if (freezeColumns != null)
                            {
                                Vector3 ahead = Quaternion.Euler(0f, leg.yaw, 0f) * Vector3.forward;
                                string tops = "";
                                for (int k = 0; k <= 3; k++)
                                    tops += freezeColumns.TryTopAt(now.x + ahead.x * k, now.z + ahead.z * k, out float ft) ? $" {ft:F1}" : " -";
                                Debug.Log($"[Move] block tops ahead (here, 1, 2, 3 m):{tops}; water {Chopper.World.Generation.WorldGen.WaterHeight:F2}");
                                string blockers = "";
                                foreach (Collider c in Physics.OverlapSphere(now + ahead * 0.8f + Vector3.up * 0.9f, 0.9f, ~0, QueryTriggerInteraction.Ignore))
                                    if (!(c is CharacterController)) blockers += $" {c.name}({c.GetType().Name}, parent {(c.transform.parent ? c.transform.parent.name : "-")})";
                                Debug.Log($"[Move] colliders ahead:{blockers}");
                            }
                        }
                        last = now;
                    }
                    Debug.Log($"[Move] {leg.label}: ended at {wood.transform.position:F1}");
                    yield return GripStop(pad, wood, leg.label, folder);
                }
                if (hosting)
                {
                    var gripColumns = Chopper.World.Voxel.VoxelColumns.Current;
                    Vector2 snowSpot = Vector2.zero;
                    float best = float.MaxValue;
                    for (int gx = 2; gripColumns != null && gx < Chopper.World.Voxel.VoxelColumns.Resolution - 2; gx += 2)
                        for (int gz = 2; gz < Chopper.World.Voxel.VoxelColumns.Resolution - 2; gz += 2)
                        {
                            bool flat = true;
                            for (int ox = -2; ox <= 2 && flat; ox++)
                                for (int oz = -2; oz <= 2 && flat; oz++)
                                    flat = gripColumns.TypeAt(gx + ox, gz + oz) == Chopper.World.Voxel.BlockType.Snow && gripColumns.TopUnits(gx + ox, gz + oz) == gripColumns.TopUnits(gx, gz);
                            if (!flat) continue;
                            Vector2 w = new Vector2(Chopper.World.Voxel.VoxelColumns.WorldX(gx) + 0.5f, Chopper.World.Voxel.VoxelColumns.WorldZ(gz) + 0.5f);
                            if (w.sqrMagnitude < best) { best = w.sqrMagnitude; snowSpot = w; }
                        }
                    if (best < float.MaxValue)
                    {
                        yield return TeleportLocalPlayer(snowSpot - new Vector2(0f, 1.6f), 0f);
                        yield return new WaitForSeconds(2f);
                        float runStart = Time.time;
                        while (Time.time - runStart < 1.5f)
                        {
                            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.up }.WithButton(GamepadButton.LeftStick));
                            moveCamera.SnapYaw(0f);
                            yield return null;
                        }
                        yield return GripStop(pad, wood, "snow patch", folder);
                    }
                    else Debug.Log("[Grip] no flat snow patch found");
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
                Animator guardAnimator = guard.GetComponentInParent<Animator>();
                if (guardAnimator != null && guardAnimator.isHuman)
                {
                    if (pelvis == null) pelvis = guardAnimator.GetBoneTransform(HumanBodyBones.Hips);
                    if (shoulderL == null) shoulderL = guardAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    if (shoulderR == null) shoulderR = guardAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
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

            // Feet and body against the world (-chopperFeet): close side shots of the player standing on flat ground, a
            // slope, the hut steps, inside the hut against a wall and in a mine chamber, plus an enemy on the slope. Each
            // stop measures every FootGrounder sole against the ground under it and probes the chest, head and hands for
            // being inside solid geometry. Logs "[Feet] RESULT PASS" when no sole is more than 4 cm in and no bone is buried.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperFeet") >= 0)
            {
                yield return FeetTour(folder, wood, ui);
                Application.Quit(0);
                yield break;
            }

            // Plants and the held torch (-chopperPlants): one healing herb, the lit torch in the hand, and a placed campfire piece.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperPlants") >= 0)
            {
                yield return PlantLook(folder, wood, ui);
                Application.Quit(0);
                yield break;
            }

            // Sky and water (-chopperSky): the hub pond, then a look straight at the block sun and at one cloud.
            // Logs water ripple settings and "[Sky] RESULT".
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperSky") >= 0)
            {
                yield return SkyLook(folder, wood, ui);
                Application.Quit(0);
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
                // The ring stays in the yard in front of the house: around (0, 20) at 18 m it reached into the hall
                // (z 2..6), and enemies woken inside it had no way out (W5-08 step 6 NavCheck).
                yield return TeleportLocalPlayer(new Vector2(0f, 32f), 0f);
                for (int i = 0; i < 16; i++)
                {
                    float angle = i / 16f * Mathf.PI * 2f;
                    Vector3 at = new Vector3(Mathf.Cos(angle) * 14f, 0f, 32f + Mathf.Sin(angle) * 14f);
                    at.y = WorldGround.HeightAt(at);
                    EnemyDirector.Instance.ServerSpawn((Element)(i % 5), (TreeSize)(i % 3), false, at);
                }
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Night, 0.2f);
                wood.GetComponent<PlayerCombat>().ServerSetGod(true);
                yield return new WaitForSeconds(6f);
                yield return MeasureFrames("night, " + EnemyDirector.CountActive() + " enemies awake", 6f);
                // W5-08 step 6: the NavMesh is baked from the block chunk colliders. Every awake enemy stands on it, and
                // they close in on the player (the group fight keeps them on a ring a few metres out).
                {
                    Vector3 me = wood.transform.position;
                    int active = 0, onMesh = 0, close = 0;
                    float far = 0f;
                    foreach (Treant t in Treant.All)
                    {
                        if (t == null || !t.IsActive) continue;
                        active++;
                        if (t.Agent != null && t.Agent.enabled && t.Agent.isOnNavMesh) onMesh++;
                        float d = Vector3.Distance(new Vector3(t.transform.position.x, 0f, t.transform.position.z), new Vector3(me.x, 0f, me.z));
                        if (d < 7f) close++;
                        else if (t.Agent != null && t.Agent.isOnNavMesh)
                        {
                            var probePath = new UnityEngine.AI.NavMeshPath();
                            bool found = UnityEngine.AI.NavMesh.CalculatePath(t.transform.position, me, UnityEngine.AI.NavMesh.AllAreas, probePath);
                            Debug.Log($"[NavCheck] far {t.name} at {t.transform.position:F1} ({d:F1} m): path to the player {(found ? probePath.status.ToString() : "none")}, agent path {t.Agent.pathStatus}, has path {t.Agent.hasPath}, speed {t.Agent.velocity.magnitude:F2}, stopped {t.Agent.isStopped}, boss {t.IsBoss}, size {t.Size}");
                        }
                        far = Mathf.Max(far, d);
                    }
                    bool navPass = active >= 16 && onMesh == active && close == active;
                    Debug.Log($"[NavCheck] RESULT {(navPass ? "PASS" : "FAIL")}: {active} awake, {onMesh} on the NavMesh, {close} within 7 m of the player (farthest {far:F1} m)");
                }
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

            // Logging tour (-chopperLogging): stand at one hub tree, chop until it falls, wood arrives in the arms.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperLogging") >= 0)
            {
                yield return LoggingTour(folder, wood, ui);
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
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.45f);
                WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
                yield return new WaitForSeconds(4f);
                Debug.Log($"[Mine] portal light: phase {DayNightCycle.Instance.Phase}, darkness {DayNightCycle.Instance.Darkness:F2}");
                for (int m = 0; m < mines.Count; m++)
                {
                    var plan = mines[m];
                    Vector3 front = plan.DoorSpot + Quaternion.Euler(0f, plan.Site.Yaw, 0f) * Vector3.forward * 7.5f;
                    Debug.Log($"[Mine] {plan.Name} at {plan.Site.Position} yaw {plan.Site.Yaw:F0}, {plan.Rooms.Count} chambers, {plan.Veins.Count} veins");
                    yield return TeleportLocalPlayer(new Vector2(front.x, front.z), plan.Site.Yaw + 180f);
                    mineCam.SnapYaw(plan.Site.Yaw + 180f);
                    mineCam.DebugSetDistance(4.5f);
                    yield return new WaitForSeconds(1.5f);
                    ui.DebugShow(0);
                    var hiddenP = new System.Collections.Generic.List<Renderer>();
                    if (PlayerWood.Local != null) foreach (Renderer r in PlayerWood.Local.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hiddenP.Add(r); }
                    yield return Shot(folder, $"mine_{m}_portal");
                    foreach (Renderer r in hiddenP) if (r != null) r.enabled = true;
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
                    // TeleportExact: a bare transform move was snapped back by the controller and the server speed check.
                    yield return TeleportExact(c + Vector3.up * 0.1f, room * 90f);
                    yield return new WaitForSeconds(0.8f);
                    ui.DebugShow(0); yield return Shot(folder, $"mine_chamber_{room}");
                }

                // The first vein of this mine: stand in front of it, face it, and strike on the glow.
                int vein = -1;
                for (int i = 0; i < Chopper.World.Generation.MineWorld.Veins.Count && vein < 0; i++)
                    if (Chopper.World.Generation.MineWorld.Veins[i].Mine == first.Index) vein = i;
                var v = Chopper.World.Generation.MineWorld.Veins[vein].Vein;
                // A bare transform write leaves the controller where it was, so the strike never sees the vein.
                Vector3 stand = v.Position + v.Normal * 1.6f;
                stand.y = Chopper.World.Generation.MinePlan.FloorY + 0.1f;
                float faceYaw = Mathf.Atan2(-v.Normal.x, -v.Normal.z) * Mathf.Rad2Deg;
                yield return TeleportExact(stand, faceYaw);
                mineCam.SnapYaw(faceYaw - 35f);
                mineCam.DebugSetDistance(2.6f);
                yield return new WaitForSeconds(0.4f);
                Vector3 aim = Quaternion.Euler(0f, faceYaw, 0f) * Vector3.forward;
                int reach = Chopper.World.Generation.MineWorld.VeinInReach(wood.transform.position, aim);
                Debug.Log($"[Mine] stood {Vector3.Distance(wood.transform.position, v.Position):F2} m from vein {vein}, reach {reach}");
                miner.TryStrike(aim);
                Debug.Log($"[Mine] engaged {miner.Engaged} on vein {miner.EngagedVein} (wanted {vein})");
                while (Time.time < miner.RhythmStart + 0.02f) yield return null;
                ui.DebugShow(0); yield return QuickShot(folder, "mine_vein_glow");
                int strikes = 0;
                int[] slots = { 0, 1, 3 };
                for (int bar = 1; bar < 8 && !Chopper.World.Generation.MineWorld.SpentHere(vein); bar++)
                {
                    foreach (int slot in slots)
                    {
                        float target = miner.RhythmStart + (bar * RhythmRules.BarBeats + slot) * RhythmRules.Beat;
                        while (Time.time < target - 0.02f) yield return null;
                        bool used = miner.TryStrike(aim);
                        strikes++;
                        Debug.Log($"[Mine] strike {strikes} (bar {bar}, beat {slot + 1}): used {used}, {RhythmRules.Judge(Time.time - miner.RhythmStart)}");
                        if (strikes == 1)
                        {
                            yield return new WaitForSeconds(0.12f);
                            ui.DebugShow(0); yield return QuickShot(folder, "mine_swing");
                        }
                        if (Chopper.World.Generation.MineWorld.SpentHere(vein)) break;
                    }
                }
                yield return new WaitForSeconds(0.3f);
                bool worked = Chopper.World.Generation.MineWorld.SpentHere(vein);
                Debug.Log($"[Mine] vein spent here {worked} after {strikes} strikes, engaged after {miner.Engaged}");
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

                // Out by the ladder. TeleportExact, not a bare transform move: that was snapped back to the vein just
                // struck (the speed check), so the prompt showed the vein and the ladder press did nothing.
                yield return TeleportExact(first.ExitSpot + Vector3.up * 0.1f, first.LadderYaw + 180f);   // facing the ladder
                yield return new WaitForSeconds(0.6f);
                ui.DebugShow(0); yield return Shot(folder, "mine_ladder_prompt");
                miner.TryInteract();
                yield return new WaitForSeconds(2f);
                Debug.Log($"[Mine] after the ladder: at {wood.transform.position:F1}, distance to the portal {Vector3.Distance(wood.transform.position, first.DoorSpot):F1}");
                bool backUp = Chopper.World.Generation.MineWorld.MineAt(wood.transform.position) < 0 && wood.transform.position.y > -50f;
                Debug.Log("[Mine] RESULT " + (backUp && worked && carried > 0 ? $"PASS (vein worked in rhythm, {carried} find, out by the ladder)"
                    : $"FAIL (worked {worked}, finds {carried}, out by the ladder {backUp})"));
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

                WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
                yield return new WaitForSeconds(0.2f);
                WeatherSystem.Instance.DebugSnapLook();
                yield return new WaitForSeconds(0.8f);
                float sunnySun = RenderSettings.sun != null ? RenderSettings.sun.intensity : -1f;
                Color sunnyAmbient = RenderSettings.ambientSkyColor;
                Material sunnySky = RenderSettings.skybox;
                WeatherSystem.Instance.ServerSet(WeatherKind.Snow);
                yield return new WaitForSeconds(0.2f);
                WeatherSystem.Instance.DebugSnapLook();
                yield return new WaitForSeconds(2.6f);
                GameObject snowObject = GameObject.Find("Snow");
                ParticleSystem snow = snowObject != null ? snowObject.GetComponent<ParticleSystem>() : null;
                int flakes = snow != null ? snow.particleCount : 0;
                float snowSun = RenderSettings.sun != null ? RenderSettings.sun.intensity : -1f;
                Color snowAmbient = RenderSettings.ambientSkyColor;
                Material snowSky = RenderSettings.skybox;
                Debug.Log($"[Weather] Snow: flakes {flakes}, sun {snowSun:0.00} (sunny {sunnySun:0.00}), ambient {snowAmbient} (sunny {sunnyAmbient})");
                WeatherCheck(flakes > 40, $"Snow: {flakes} flakes on the camera");
                WeatherCheck(Mathf.Abs(snowSun - sunnySun) < 0.05f, $"Snow leaves the sun alone ({sunnySun:0.00} to {snowSun:0.00})");
                bool ambientSame = Mathf.Abs(snowAmbient.r - sunnyAmbient.r) < 0.04f && Mathf.Abs(snowAmbient.g - sunnyAmbient.g) < 0.04f && Mathf.Abs(snowAmbient.b - sunnyAmbient.b) < 0.04f;
                WeatherCheck(ambientSame, "Snow leaves ambient alone");
                WeatherCheck(snowSky == sunnySky, "Snow leaves the skybox alone");
                ui.DebugShow(0);
                yield return Shot(folder, "weather_snow");
                yield return MeasureFrames("snow", 4f);

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
                bool rodOut = false;
                float bankR = Chopper.World.Generation.WorldGen.PondOuterRadius;
                for (float r = Chopper.World.Generation.WorldGen.PondOuterRadius + 1.2f; r > 2f; r -= 0.5f)
                {
                    Vector2 probe = gen.PondCenter + Vector2.right * r;
                    Vector3 probe3 = new Vector3(probe.x, Chopper.World.WorldGround.HeightAt(new Vector3(probe.x, 0f, probe.y)), probe.y);
                    if (!PlayerFishing.AtWater(probe3)) continue;
                    Debug.Log($"[Fishing] first fishing bank at r {r:F1}");
                    bankR = r;
                    yield return TeleportLocalPlayer(probe, 270f);
                    cam.SnapYaw(270f);
                    yield return new WaitForSeconds(0.8f);
                    break;
                }
                for (int step = 0; step < 6 && !rodOut; step++)
                {
                    rodOut = fishing.TryToggle();
                    Debug.Log($"[Fishing] rod toggle {rodOut} at {wood.transform.position:F1}, can fish {fishing.CanFish}, at water {PlayerFishing.AtWater(wood.transform.position)}, owner {fishing.IsOwner}, busy {fishing.Busy}, pond {gen.PondCenter:F1} r {Chopper.World.Generation.WorldGen.PondOuterRadius:F1}, swim {WaterSystem.IsSwimming(wood.transform.position)}, under {Chopper.World.Generation.CryptWorld.IsUnderground(wood.transform.position)}");
                    if (rodOut) break;
                    Vector2 closer = gen.PondCenter + Vector2.right * (bankR - step * 0.5f);
                    yield return TeleportLocalPlayer(closer, 270f);
                    cam.SnapYaw(270f);
                    yield return new WaitForSeconds(0.8f);
                }
                yield return new WaitForSeconds(1.2f);
                float water = Chopper.World.Generation.WorldGen.WaterHeight;
                float ClearOfWater(float extra) => Mathf.Max(water + 1.85f, wood.transform.position.y + 1.45f) + extra;
                Vector3 PondSide(Vector3 target, float along, float extra)
                {
                    Vector3 pond = new Vector3(gen.PondCenter.x, 0f, gen.PondCenter.y);
                    Vector3 dir = pond - target;
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                    dir.Normalize();
                    Vector3 at = target + dir * along;
                    at.y = ClearOfWater(extra);
                    return at;
                }
                Vector3 stand = wood.transform.position;
                Vector3 toPond = new Vector3(gen.PondCenter.x, 0f, gen.PondCenter.y) - stand;
                toPond.y = 0f;
                if (toPond.sqrMagnitude < 0.01f) toPond = Vector3.left;
                toPond.Normalize();
                Vector3 rodCam = stand - toPond * 2.2f + Vector3.Cross(toPond, Vector3.up) * 1.8f;
                rodCam.y = ClearOfWater(0.15f);
                cam.DebugHold(rodCam, Quaternion.LookRotation(stand + toPond * 0.6f + Vector3.up * 1.05f - rodCam));
                Debug.Log($"[Fishing] rod cam y {rodCam.y:F2} water {water:F2} above {rodCam.y - water:F2}");
                yield return Shot(folder, "fish_2_rod_out");
                fishing.ServerForceKind((int)FishKind.Pike);
                fishing.DebugCast(0.7f);
                yield return new WaitForSeconds(1.5f);
                yield return Shot(folder, "fish_3_waiting");
                for (int i = 0; i < 3; i++)
                {
                    fishing.DebugTwitch();
                    yield return new WaitForSeconds(0.9f);
                }
                Vector3 lure = fishing.BobberWorld;
                fishing.DebugPoseSchool(lure);
                Debug.Log($"[Fishing] school {fishing.DebugSchoolCount} bobber {lure.y:F2} water {water:F2}");
                Vector3 playerAt = wood.transform.position;
                Vector3 bankLook = Vector3.Lerp(playerAt + Vector3.up * 1.15f, lure + Vector3.up * 0.25f, 0.48f);
                Vector3 across = Vector3.Cross(lure - playerAt, Vector3.up);
                if (across.sqrMagnitude < 0.01f) across = Vector3.right;
                across.Normalize();
                Vector3 bankCam = playerAt - toPond * 2.6f + across * 2.3f;
                bankCam.y = ClearOfWater(0.35f);
                cam.DebugHold(bankCam, Quaternion.LookRotation(bankLook - bankCam));
                Debug.Log($"[Fishing] bank cam y {bankCam.y:F2} water {water:F2} above {bankCam.y - water:F2} lure {lure.y:F2}");
                yield return new WaitForSeconds(0.35f);
                ui.DebugShow(0);
                yield return Shot(folder, "fish_3b_following");
                Vector3 watch = PondSide(lure, 1.55f, 0.75f);
                cam.DebugHold(watch, Quaternion.LookRotation((lure + Vector3.up * 0.45f) - watch));
                yield return new WaitForSeconds(0.42f);
                ui.DebugShow(0);
                yield return Shot(folder, "fish_3d_jump");
                fishing.DebugSinkSchool(0.32f);
                Vector3 under = PondSide(lure, 0.55f, 1.2f);
                cam.DebugHold(under, Quaternion.LookRotation((lure + Vector3.down * 0.1f) - under));
                yield return new WaitForSeconds(0.4f);
                ui.DebugShow(0);
                yield return Shot(folder, "fish_3c_under");
                cam.DebugRelease();
                cam.DebugSetPitch(8f);
                cam.DebugSetDistance(4f);
                cam.SnapYaw(270f);
                yield return null;
                fishing.DebugBiteNow();
                yield return new WaitForSeconds(0.2f);
                yield return Shot(folder, "fish_4_bite");
                fishing.DebugHook();
                yield return new WaitForSeconds(0.4f);
                for (int retry = 0; retry < 3 && fishing.CurrentStage != PlayerFishing.Stage.Fighting; retry++)
                {
                    if (fishing.CurrentStage == PlayerFishing.Stage.Off) fishing.TryToggle();
                    yield return new WaitForSeconds(0.6f);
                    fishing.DebugCast(0.7f);
                    yield return new WaitForSeconds(1.6f);
                    fishing.DebugBiteNow();
                    yield return new WaitForSeconds(0.25f);
                    fishing.DebugHook();
                    yield return new WaitForSeconds(0.4f);
                }
                Debug.Log($"[Fishing] fight stage {fishing.CurrentStage}");
                fishing.DebugSetTension(0.7f, 0.5f);
                ui.DebugShow(0);
                lure = fishing.BobberWorld;
                Vector3 fightFrom = wood.transform.position;
                Vector3 fightSide = Vector3.Cross(lure - fightFrom, Vector3.up);
                fightSide = fightSide.sqrMagnitude < 0.01f ? Vector3.right : fightSide.normalized;
                Vector3 fightCam = Vector3.Lerp(fightFrom, lure, 0.3f) + fightSide * 1.8f;
                fightCam.y = Mathf.Max(Chopper.World.WorldGround.HeightAt(fightCam), Chopper.World.Generation.WorldGen.WaterHeight) + 2.4f;
                cam.DebugHold(fightCam, Quaternion.LookRotation((lure + Vector3.up * 0.5f) - fightCam));
                yield return new WaitForSeconds(0.35f);
                yield return Shot(folder, "fish_5_reeling");
                FishSpecies pike = fishing.SpeciesOf(FishKind.Pike);
                fishing.DebugSetTension(0.4f, pike.CalmSeconds + pike.PullSeconds + 0.2f);
                yield return new WaitForSeconds(0.25f);
                yield return Shot(folder, "fish_6_thrashing");
                cam.DebugRelease();
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
                bool foeEngaged = foe != null && foe.IsActive && foe.GetComponent<TreantAI>().ModeName != "Approach";
                Debug.Log($"[Combat] foe after 2.2 s: active {foe != null && foe.IsActive}, mode {(foe != null ? foe.GetComponent<TreantAI>().ModeName : "none")}, {(foe != null ? Vector3.Distance(foe.transform.position, wood.transform.position) : 0f):F1} m away");
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
                Vector3 foeBefore = foe != null ? foe.transform.position : Vector3.zero;
                if (foe != null) foe.ServerHit(0.6f, null, wood, wood.transform.position);
                yield return new WaitForSeconds(0.1f);
                bool foeStaggered = foe != null && foe.IsStaggered;
                Debug.Log($"[Combat] hit: staggered {foeStaggered}, knocked {(foe != null ? Vector3.Distance(foe.transform.position, foeBefore) : 0f):F2} m");
                ui.DebugShow(0); yield return Shot(folder, "combat_3_enemy_hit");
                yield return new WaitForSeconds(0.4f);
                if (foe != null) foe.ServerHit(1000f, null, wood, wood.transform.position);
                yield return new WaitForSeconds(0.45f);
                bool foeDown = foe == null || !foe.IsActive;
                Debug.Log($"[Combat] killing blow: down {foeDown}");
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
                int mostAttacking = 0;
                var closest = new System.Collections.Generic.Dictionary<Treant, float>();
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
                        closest[member] = Mathf.Min(closest.TryGetValue(member, out float c) ? c : float.MaxValue, d.magnitude);
                    }
                    int attacking = 0;
                    foreach (Treant member in Treant.All) if (member != null && member.IsActive && member.GetComponent<TreantAI>().ModeName == "Attack") attacking++;
                    mostAttacking = Mathf.Max(mostAttacking, attacking);
                    Debug.Log(line.ToString());
                    if (t == 4) { ui.DebugShow(0); yield return Shot(folder, "combat_6_group"); }
                }
                int reached = 0;
                foreach (float c in closest.Values) if (c <= 2.5f) reached++;
                bool groupOk = closest.Count == 4 && reached == 4 && mostAttacking <= CombatCircle.AttackersPerPlayer;
                Debug.Log($"[Combat] group: {reached} of {closest.Count} came within 2.5 m, most attacking at once {mostAttacking} (limit {CombatCircle.AttackersPerPlayer})");
                Debug.Log($"[Combat] RESULT {(foeEngaged && foeStaggered && foeDown && groupOk ? "PASS" : "FAIL")} (engaged {foeEngaged}, staggered {foeStaggered}, down {foeDown}, group {groupOk})");
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
                yield return TeleportLocalPlayer(new Vector2(door.x + outward.x * 12f, door.z + outward.z * 12f), plan.ReturnYaw + 180f);
                if (PlayerCamera.Local != null) PlayerCamera.Local.DebugSetPitch(-12f);
                yield return new WaitForSeconds(1.5f);
                yield return Shot(folder, "crypt_1_entrance");
                if (PlayerCamera.Local != null) PlayerCamera.Local.DebugSetPitch(0f);
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
                bool cryptBackUp = !Chopper.World.Generation.CryptWorld.IsUnderground(wood.transform.position);
                Debug.Log($"[Crypt] back up {cryptBackUp} at {wood.transform.position:F1}; health {health.Health}");
                Debug.Log("[Crypt] RESULT " + (cryptBackUp ? "PASS" : "FAIL") + " (crypt_11_crushed is the deliberate crush test, not a door death)");

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

            // Shaft tour (W11-57 graphics): a forest with the sun up, so the light shafts read as rays, and the same
            // forest with the sun down, where they must be gone. Logs "[Shafts] RESULT".
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chopperShafts") >= 0)
            {
                GameConfig shaftConfig = TreeManager.Instance.Config;
                float shaftHub = shaftConfig.HubRadius;
                WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
                GameUi.HudHidden = true;
                ui.DebugShow(0);
                yield return TeleportLocalPlayer(new Vector2(shaftHub + 55f, 0f), 90f);
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.5f);
                yield return new WaitForSeconds(1f);
                Light shaftSun = RenderSettings.sun;
                Vector3 toSun = shaftSun != null ? -shaftSun.transform.forward : Vector3.forward;
                float sunYaw = Mathf.Atan2(toSun.x, toSun.z) * Mathf.Rad2Deg;
                yield return TeleportLocalPlayer(new Vector2(shaftHub + 55f, 0f), sunYaw);
                PlayerCamera.Local.SnapYaw(sunYaw);
                PlayerCamera.Local.DebugSetPitch(20f);
                yield return new WaitForSeconds(1.4f);
                yield return Shot(folder, "shafts_forest_sun_up");
                float dayStrength = DayNightCycle.LastShaftStrength;
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Night, 0.35f);
                yield return new WaitForSeconds(1.6f);
                yield return Shot(folder, "shafts_forest_sun_down");
                float nightStrength = DayNightCycle.LastShaftStrength;
                bool shaftsOk = dayStrength > 0.01f && nightStrength <= 0.0001f;
                Debug.Log($"[Shafts] forest shaft strength: sun up {dayStrength:0.000}, sun down {nightStrength:0.000}");
                Debug.Log("[Shafts] RESULT " + (shaftsOk ? "PASS" : "FAIL"));
                Debug.Log("[Shot] done");
                Application.Quit(shaftsOk ? 0 : 1);
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
                    ("zone_water_deep", new Vector2(10f, hub + 120f), 180f),
                    ("zone_earth_deep", new Vector2(hub + 120f, 10f), 270f),
                    ("zone_fire_deep", new Vector2(10f, -(hub + 120f)), 0f),
                    ("zone_wind_deep", new Vector2(-(hub + 120f), 10f), 90f),
                };
                // Plant some saplings in front of the camera so the sapling counter and the sprouts can be checked.
                if (EnemyDirector.Instance != null)
                {
                    for (int i = 0; i < 3; i++) EnemyDirector.Instance.ServerOnTreeHit(new Vector3(48f + i * 3f, 0f, 44f), Element.Neutral, TreeSize.Medium, wood);
                }
                foreach (var stop in stops)
                {
                    yield return TeleportLocalPlayer(stop.position, stop.yaw);
                    if (stop.name != "zone_hub" && stop.name != "zone_hub_edge") PlayerCamera.Local.DebugSetPitch(-16f);
                    else PlayerCamera.Local.DebugSetPitch(8f);
                    yield return new WaitForSeconds(1.2f);
                    yield return Shot(folder, stop.name);
                }
                DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Night, 0.35f);
                yield return new WaitForSeconds(0.6f);
                foreach (var stop in new[] { stops[6], stops[7] })
                {
                    yield return TeleportLocalPlayer(stop.position, stop.yaw);
                    PlayerCamera.Local.DebugSetPitch(-16f);
                    yield return new WaitForSeconds(2.4f);
                    yield return Shot(folder, stop.name + "_night");
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

            ui.DebugConsole("tp hut");
            ui.DebugConsole("night");
            ui.DebugConsole("spawn 6");
            ui.DebugShow(0);
            yield return new WaitForSeconds(2.5f);
            PlayerCamera.Local.SnapYaw(180f);
            PlayerCamera.Local.DebugSetDistance(6f);
            yield return new WaitForSeconds(1f);
            yield return Shot(folder, "fly_3b_night_hut");
            foreach (Treant t in new List<Treant>(Treant.All)) t.ServerDespawn();
            Element[] lineup = { Element.Neutral, Element.Fire, Element.Water, Element.Wind };
            string[] names = { "bark", "fire", "water", "wind" };
            var bodies = new Treant[4];
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * 3.6f;
                bodies[i] = EnemyDirector.Instance.ServerSpawn(lineup[i], TreeSize.Medium, false,
                    new Vector3(x, WorldGround.HeightAt(x, 48f), 48f));
                if (bodies[i] != null) bodies[i].ServerActivate();
            }
            yield return new WaitForSeconds(0.5f);
            yield return TeleportLocalPlayer(new Vector2(0f, 39.2f), 0f);
            PlayerCamera.Local.SnapYaw(0f);
            PlayerCamera.Local.DebugSetPitch(8f);
            PlayerCamera.Local.DebugSetDistance(7.2f);
            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "fly_elem_lineup");
            ui.DebugConsole("day");
            ui.DebugConsole("god");
            ui.DebugShow(0);
            yield return new WaitForSeconds(1.0f);
            foreach (Treant t in new List<Treant>(Treant.All)) t.ServerDespawn();
            for (int i = 0; i < 4; i++)
            {
                foreach (Treant t in new List<Treant>(Treant.All)) t.ServerDespawn();
                float ex = 0f;
                float ez = 48f;
                Treant one = EnemyDirector.Instance.ServerSpawn(lineup[i], TreeSize.Medium, false,
                    new Vector3(ex, WorldGround.HeightAt(ex, ez), ez));
                if (one == null)
                {
                    Debug.Log("[Fly] elem spawn miss " + names[i]);
                    continue;
                }
                one.ServerActivate();
                yield return new WaitForSeconds(0.5f);
                Debug.Log($"[Fly] elem {names[i]} at {one.transform.position}");
                yield return TeleportLocalPlayer(new Vector2(ex - 2.3f, ez - 0.4f), 85f);
                PlayerCamera.Local.SnapYaw(85f);
                PlayerCamera.Local.DebugSetPitch(6f);
                PlayerCamera.Local.DebugSetDistance(2.1f);
                yield return new WaitForSeconds(0.45f);
                yield return Shot(folder, "fly_elem_" + names[i]);
                yield return TeleportLocalPlayer(new Vector2(ex + 0.2f, ez + 2.3f), 180f);
                PlayerCamera.Local.SnapYaw(180f);
                PlayerCamera.Local.DebugSetPitch(6f);
                PlayerCamera.Local.DebugSetDistance(2.1f);
                yield return new WaitForSeconds(0.12f);
                yield return Shot(folder, "fly_elem_" + names[i] + "_back");
                string[] attacks = { "Elemental_Chop", "Elemental_Thrust", "Elemental_Smash", "Elemental_Stab" };
                float[] strike = { 0.55f, 0.55f, 0.72f, 0.58f };
                float ay = WorldGround.HeightAt(ex + 0.7f, ez);
                one.transform.position = new Vector3(ex + 0.7f, ay, ez);
                UnityEngine.AI.NavMeshAgent agent = one.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                yield return TeleportLocalPlayer(new Vector2(ex - 0.4f, ez - 2.7f), 12f);
                Vector3 face = wood.transform.position - one.transform.position;
                face.y = 0f;
                if (face.sqrMagnitude > 0.01f) one.transform.rotation = Quaternion.LookRotation(face);
                PlayerCamera.Local.SnapYaw(12f);
                PlayerCamera.Local.DebugSetPitch(4f);
                PlayerCamera.Local.DebugSetDistance(3.1f);
                Renderer[] hidden = wood.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in hidden) r.enabled = false;
                Animator anim = one.BodyAnimator;
                if (anim != null)
                {
                    anim.SetInteger("Weapon", i);
                    anim.speed = 0f;
                    anim.Play(attacks[i], 0, strike[i]);
                    yield return null;
                    anim.Play(attacks[i], 0, strike[i]);
                    anim.speed = 0f;
                    AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
                    Debug.Log($"[Fly] elem {names[i]} attack {attacks[i]} playing {info.IsName(attacks[i])} t {info.normalizedTime:F2}");
                }
                yield return null;
                yield return Shot(folder, "fly_elem_" + names[i] + "_atk");
                foreach (Renderer r in hidden) if (r != null) r.enabled = true;
                if (anim != null) anim.speed = 1f;
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
            bool healing = body != null && body.GetLayerIndex("Chop") >= 0 && body.GetCurrentAnimatorStateInfo(body.GetLayerIndex("Chop")).IsName("Heal");
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
                if (!Chopper.World.Voxel.VoxelWorld.IsGround(c) && !(c is CharacterController)) return true;
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

        // One hub tree, chopped from in front until it falls. Wood must land in the arms.
        private static bool TryFindChopStand(TreeDatabase trees, TreeSize want, out int id, out Vector2 stand, out float yaw)
        {
            id = -1;
            stand = Vector2.zero;
            yaw = 0f;
            float hub = TreeManager.Instance.Config.HubRadius;
            float best = 999f;
            for (int i = 0; i < trees.Count; i++)
            {
                TreeEntry entry = trees.Get(i);
                if (entry.Size != want) continue;
                Vector3 p = entry.Position;
                float ring = p.magnitude;
                if (ZoneMap.ElementAt(p, hub) != Element.Earth) continue;
                if (ring < hub + 16f || ring > hub + 50f) continue;
                if (TreeManager.Instance.IsFelledOnClient(i)) continue;
                for (int turn = 0; turn < 8; turn++)
                {
                    Vector3 aim = Quaternion.Euler(0f, turn * 45f, 0f) * Vector3.forward;
                    Vector3 at = p - aim * 1.8f;
                    if (!TreeManager.Instance.ServerFindTree(new Vector3(at.x, p.y, at.z), aim, out int hit, out _) || hit != i) continue;
                    float standY = WorldGround.HeightAt(at);
                    if (Mathf.Abs(standY - p.y) > 0.6f) continue;
                    Vector3 right = Vector3.Cross(Vector3.up, aim).normalized;
                    float leftY = WorldGround.HeightAt(at - right * 1.6f);
                    float sideY = WorldGround.HeightAt(at + right * 1.6f);
                    float openY = Mathf.Min(leftY, sideY);
                    if (openY > p.y + 1.1f) continue;
                    float score = ring + Mathf.Abs(standY - p.y) * 8f + (openY - p.y) * 4f;
                    if (score >= best) break;
                    id = i;
                    stand = new Vector2(at.x, at.z);
                    yaw = turn * 45f;
                    best = score;
                    break;
                }
            }
            return id >= 0;
        }

        private static void FrameChop(float bodyYaw, Vector3 treePos, Vector3 stand)
        {
            PlayerCamera cam = PlayerCamera.Local;
            if (cam == null) return;
            Vector3 aim = Quaternion.Euler(0f, bodyYaw, 0f) * Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, aim).normalized;
            Vector3 look = treePos + Vector3.up * 1.25f;
            float standY = WorldGround.HeightAt(stand);
            Vector3 pick = stand + Vector3.up * 1.55f + right * 1.65f - aim * 0.35f;
            bool found = false;
            float[] sides = { 1f, -1f };
            for (int i = 0; i < sides.Length; i++)
            {
                Vector3 raw = stand + right * (1.65f * sides[i]) - aim * 0.35f;
                float ground = WorldGround.HeightAt(raw);
                if (ground - standY > 0.85f) continue;
                pick = new Vector3(raw.x, ground + 1.52f, raw.z);
                found = true;
                break;
            }
            if (!found)
            {
                Vector3 raw = stand + right * 0.95f - aim * 1.2f;
                pick = new Vector3(raw.x, WorldGround.HeightAt(raw) + 1.55f, raw.z);
            }
            cam.DebugHold(pick, Quaternion.LookRotation(look - pick));
        }

        private static IEnumerator ChopUntilFelled(PlayerAxe axe, PlayerWood wood, GameUi ui, string folder, string tag, int id, Vector2 stand, float yaw)
        {
            Vector3 treePos = TreeManager.Instance.Database.Get(id).Position;
            Vector3 at = new Vector3(stand.x, WorldGround.HeightAt(stand.x, stand.y), stand.y);
            yield return TeleportExact(at, yaw);
            FrameChop(yaw, treePos, at);
            yield return new WaitForSeconds(0.4f);
            Vector3 aim = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            bool reach = TreeManager.Instance.ServerFindTree(wood.transform.position, aim, out int hit, out _) && hit == id;
            Debug.Log($"[Log] {tag} reach {reach} hit {hit} want {id} stand {at} now {wood.transform.position}");
            ui.DebugShow(0);
            yield return Shot(folder, tag + "_tree");
            int swings = 0;
            float start = Time.time;
            float strikeWait = 0.22f;
            while (Time.time - start < 40f && swings < 40 && !TreeManager.Instance.IsFelledOnClient(id))
            {
                yield return TeleportExact(at, yaw);
                FrameChop(yaw, treePos, at);
                yield return null;
                if (axe.TrySwing()) swings++;
                if (swings == 2)
                {
                    yield return new WaitForSeconds(strikeWait);
                    FrameChop(yaw, treePos, at);
                    ui.DebugShow(0);
                    yield return Shot(folder, tag + "_chop");
                }
                yield return new WaitForSeconds(0.85f);
            }
            if (PlayerCamera.Local != null) PlayerCamera.Local.DebugRelease();
            bool felled = TreeManager.Instance.IsFelledOnClient(id);
            if (felled) yield return TeleportLocalPlayer(new Vector2(8f, 12f), 180f);
            yield return new WaitForSeconds(1.6f);
            ui.DebugShow(0);
            yield return Shot(folder, tag + "_after");
            Debug.Log($"[Log] {tag} tree {id} swings {swings}, felled {felled}, wood {wood.Carried}");
            _lastChopSwings = swings;
            _lastChopFelled = felled;
        }

        private static int _lastChopSwings;
        private static bool _lastChopFelled;

        private static IEnumerator LoggingTour(string folder, PlayerWood wood, GameUi ui)
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Hut.Instance.ServerSetRepaired(true);
            wood.GetComponent<PlayerCombat>().ServerSetGod(true);
            PlayerAxe axe = wood.GetComponent<PlayerAxe>();
            PlayerUpgrades upgrades = wood.GetComponent<PlayerUpgrades>();
            axe.ServerSetHasAxe(true);
            DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.4f);
            yield return new WaitForSeconds(4f);
            if (!axe.IsDrawn)
            {
                axe.RequestToggleDrawn();
                yield return new WaitForSeconds(1.1f);
            }
            ui.DebugShow(0);
            TreeDatabase trees = TreeManager.Instance.Database;
            GameConfig config = TreeManager.Instance.Config;
            int largeHits = config.GetHitsToFell(TreeSize.Large);
            int plainLarge = ChopRules.SwingsToFell(largeHits, 1f);

            axe.ServerLoadLogSkill(SkillRules.MaxSkill);
            if (upgrades != null) upgrades.ServerLoad(3, upgrades.ArmorLevel, upgrades.SturdyLevel, upgrades.RuneLevel);
            float gear100 = upgrades != null ? upgrades.AxeDamageMultiplier : 2f;
            float power100 = SkillRules.TreeChopPower(gear100, SkillRules.MaxSkill);
            int expect100 = ChopRules.SwingsToFell(largeHits, power100);
            if (!TryFindChopStand(trees, TreeSize.Large, out int id100, out Vector2 stand100, out float yaw100))
            {
                Debug.Log("[Log] RESULT FAIL (no large tree for skill 100)");
                yield break;
            }
            Debug.Log($"[Log] skill 100 + top gear {gear100:F2} power {power100:F2} tree {id100} expect {expect100} stand {stand100} facing {yaw100}");
            yield return ChopUntilFelled(axe, wood, ui, folder, "log_skill100", id100, stand100, yaw100);
            int swings100 = _lastChopSwings;
            bool felled100 = _lastChopFelled;
            TreeSize size100 = trees.Get(id100).Size;
            yield return new WaitForSeconds(2f);

            axe.ServerLoadLogSkill(0);
            if (upgrades != null) upgrades.ServerLoad(0, upgrades.ArmorLevel, upgrades.SturdyLevel, upgrades.RuneLevel);
            if (!TryFindChopStand(trees, TreeSize.Large, out int id0, out Vector2 stand0, out float yaw0))
            {
                Debug.Log("[Log] RESULT FAIL (no large tree in reach at skill 0)");
                yield break;
            }
            TreeSize size0 = trees.Get(id0).Size;
            int hits0 = config.GetHitsToFell(size0);
            float power0 = SkillRules.TreeChopPower(1f, 0);
            int expect0 = ChopRules.SwingsToFell(hits0, power0);
            Debug.Log($"[Log] skill 0 tree {id0} {size0} hits {hits0} expect {expect0} (plain {ChopRules.SwingsToFell(hits0, 1f)}) stand {stand0} facing {yaw0}");
            yield return ChopUntilFelled(axe, wood, ui, folder, "log_skill0", id0, stand0, yaw0);
            int swings0 = _lastChopSwings;
            bool felled0 = _lastChopFelled;

            bool mineStruck = false;
            var mines = Chopper.World.Generation.MineWorld.Plans;
            if (mines.Count > 0 && Chopper.World.Generation.MineWorld.Veins.Count > 0)
            {
                var first = mines[0];
                int vein = -1;
                for (int i = 0; i < Chopper.World.Generation.MineWorld.Veins.Count && vein < 0; i++)
                    if (Chopper.World.Generation.MineWorld.Veins[i].Mine == first.Index) vein = i;
                if (vein >= 0)
                {
                    var v = Chopper.World.Generation.MineWorld.Veins[vein].Vein;
                    Vector3 stand = v.Position + v.Normal * 1.6f;
                    stand.y = Chopper.World.Generation.MinePlan.FloorY + 0.1f;
                    float faceYaw = Mathf.Atan2(-v.Normal.x, -v.Normal.z) * Mathf.Rad2Deg;
                    PlayerMining miner = wood.GetComponent<PlayerMining>();
                    yield return TeleportLocalPlayer(new Vector2(first.DoorSpot.x, first.DoorSpot.z), first.Site.Yaw + 180f);
                    yield return new WaitForSeconds(0.6f);
                    miner.TryInteract();
                    yield return new WaitForSeconds(1.6f);
                    yield return TeleportExact(stand, faceYaw);
                    if (PlayerCamera.Local != null)
                    {
                        PlayerCamera.Local.SnapYaw(faceYaw - 35f);
                        PlayerCamera.Local.DebugSetDistance(2.6f);
                    }
                    yield return new WaitForSeconds(0.4f);
                    Vector3 aim = Quaternion.Euler(0f, faceYaw, 0f) * Vector3.forward;
                    bool engaged = miner.TryStrike(aim);
                    while (Time.time < miner.RhythmStart + 0.02f) yield return null;
                    float target = miner.RhythmStart + RhythmRules.Beat;
                    while (Time.time < target - 0.02f) yield return null;
                    bool struck = miner.TryStrike(aim);
                    mineStruck = engaged && (struck || miner.Engaged);
                    Debug.Log($"[Log] mine strike engaged {engaged} used {struck} on vein {miner.EngagedVein} (wanted {vein})");
                    yield return new WaitForSeconds(0.15f);
                    ui.DebugShow(0);
                    yield return Shot(folder, "log_mine_strike");
                    yield return TeleportExact(first.ExitSpot + Vector3.up * 0.1f, first.LadderYaw + 180f);
                    yield return new WaitForSeconds(0.5f);
                    miner.TryInteract();
                    yield return new WaitForSeconds(1.6f);
                }
            }

            bool fishCast = false;
            PlayerFishing fishing = wood.GetComponent<PlayerFishing>();
            if (fishing != null)
            {
                fishing.ServerSetGear(1, 1);
                Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
                Vector2 bank = gen.PondCenter + Vector2.right * (Chopper.World.Generation.WorldGen.PondOuterRadius + 2.4f);
                bool foundBank = false;
                for (int dir = 0; dir < 16 && !foundBank; dir++)
                {
                    float rad = dir * 22.5f * Mathf.Deg2Rad;
                    Vector2 n = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
                    for (float r = 10f; r <= Chopper.World.Generation.WorldGen.PondOuterRadius + 8f; r += 0.6f)
                    {
                        Vector2 probe = gen.PondCenter + n * r;
                        Vector3 at = new Vector3(probe.x, Chopper.World.WorldGround.HeightAt(new Vector3(probe.x, 0f, probe.y)) + 0.3f, probe.y);
                        if (PlayerFishing.AtWater(at) && !Chopper.World.WaterSystem.IsSwimming(at))
                        {
                            bank = probe;
                            foundBank = true;
                            break;
                        }
                    }
                }
                yield return TeleportLocalPlayer(bank, 270f);
                if (PlayerCamera.Local != null)
                {
                    PlayerCamera.Local.SnapYaw(270f);
                    PlayerCamera.Local.DebugSetDistance(4f);
                }
                yield return new WaitForSeconds(1.6f);
                bool rod = fishing.TryToggle();
                yield return new WaitForSeconds(0.8f);
                fishing.DebugCast(0.7f);
                yield return new WaitForSeconds(1.4f);
                fishCast = fishing.Busy;
                Debug.Log($"[Log] fish toggle {rod} cast busy {fishCast} atWater {PlayerFishing.AtWater(wood.transform.position)}");
                ui.DebugShow(0);
                yield return Shot(folder, "log_fish_cast");
            }

            int stumpVoxel = 0, stumpOld = 0;
            if (EnemyDirector.Instance != null)
            {
                Vector3 stumpAt = wood.transform.position + wood.transform.forward * 2.5f;
                int before = Treant.All.Count;
                EnemyDirector.Instance.ServerOnTreeHit(stumpAt, Element.Earth, TreeSize.Large, null);
                yield return new WaitForSeconds(1.2f);
                foreach (Treant t in Treant.All)
                {
                    bool voxel = false;
                    foreach (Transform c in t.GetComponentsInChildren<Transform>()) if (c.name.StartsWith("prop_sapling_")) { voxel = true; break; }
                    if (voxel) stumpVoxel++; else if (!t.ShowsGolem) stumpOld++;
                }
                if (PlayerCamera.Local != null) { PlayerCamera.Local.DebugSetDistance(4.5f); PlayerCamera.Local.DebugSetPitch(25f); }
                yield return new WaitForSeconds(0.6f);
                ui.DebugShow(0);
                yield return Shot(folder, "log_stump_saplings");
                Debug.Log($"[Log] stump saplings planted {Treant.All.Count - before}, voxel {stumpVoxel}, old look {stumpOld}");
            }
            bool skill0Ok = felled0 && swings0 > plainLarge && swings0 > largeHits;
            bool skill100Ok = felled100 && size100 == TreeSize.Large && swings100 >= 2;
            bool pass = skill0Ok && skill100Ok && mineStruck && fishCast;
            Debug.Log($"[Log] skill 0 large {size0} {swings0} swings (plain {plainLarge}, expect {expect0}); skill 100 + top gear {swings100} swings (expect {expect100}, large hits {largeHits}); mine {mineStruck}; fish {fishCast}");
            Debug.Log("[Log] RESULT " + (pass ? "PASS" : "FAIL"));
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

        // One herb up close, the lit voxel torch, and a campfire piece placed beside a workbench.
        private static IEnumerator PlantLook(string folder, PlayerWood wood, GameUi ui)
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Hut.Instance.ServerSetRepaired(true);
            DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.4f);
            if (WeatherSystem.Instance != null) WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
            yield return new WaitForSeconds(3f);
            ui.DebugShow(0);
            GameUi.HudHidden = true;

            // The first tour shot every subject under pine canopies. Each subject now stands in the open: no tree,
            // rock or bush within a few metres and nothing overhead (renderer bounds, see OpenSpot).
            GameObject herb = null;
            List<Renderer> obstacles = PlantObstacles(wood.transform);
            int herbsSeen = 0;
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("Herb_")) continue;
                herbsSeen++;
                if (herb == null) herb = t.gameObject;
                if (!OpenSpot(t.position, 2.5f, obstacles, t)) continue;
                herb = t.gameObject;
                Debug.Log($"[Plants] open herb {t.name} after {herbsSeen} tried");
                break;
            }
            bool herbOk = false;
            Vector3 openAt = new Vector3(40f, 0f, 40f);
            if (herb != null)
            {
                Vector3 at = herb.transform.position;
                openAt = at;
                yield return TeleportLocalPlayer(new Vector2(at.x + 5f, at.z - 5f), 0f);
                yield return new WaitForSeconds(0.5f);
                PlayerCamera cam = PlayerCamera.Local;
                cam.enabled = false;
                Camera view = Camera.main;
                obstacles = PlantObstacles(null);
                Vector3 camAt = at + new Vector3(1.1f, 1.0f, 1.1f);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * 30f * Mathf.Deg2Rad;
                    Vector3 c = at + new Vector3(Mathf.Sin(a) * 1.5f, 1.0f, Mathf.Cos(a) * 1.5f);
                    if (!SightClear(c, at + Vector3.up * 0.3f, obstacles, herb.transform)) continue;
                    camAt = c;
                    break;
                }
                view.transform.position = camAt;
                view.transform.LookAt(at + Vector3.up * 0.3f);
                yield return new WaitForEndOfFrame();
                ui.DebugShow(0);
                yield return Shot(folder, "plant_herb");
                cam.enabled = true;
                Renderer herbRenderer = herb.GetComponentInChildren<Renderer>();
                string mesh = herbRenderer != null && herbRenderer.GetComponent<MeshFilter>() != null
                    ? herbRenderer.GetComponent<MeshFilter>().sharedMesh.name : "none";
                herbOk = mesh.Contains("herb");
                Debug.Log($"[Plants] herb {herb.name} mesh {mesh} scale {herb.transform.localScale.y:F2}");
            }
            else Debug.Log("[Plants] herb missing");

            PlayerInventory inventory = wood.GetComponent<PlayerInventory>();
            if (inventory.Torches == 0) wood.GetComponent<PlayerDevTools>().Execute(DevCommand.GiveTorches, 2);
            if (!inventory.TorchLit) inventory.ServerToggleTorch();
            yield return new WaitForSeconds(0.6f);
            // Stand in the open, 3 m from the herb (or wherever the first open ring point is).
            Vector3 stand = openAt + new Vector3(3f, 0f, 0f);
            obstacles = PlantObstacles(wood.transform);
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f * Mathf.Deg2Rad;
                float r = 3f + (i / 8) * 3f;
                Vector3 c = openAt + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                c.y = WorldGround.HeightAt(c);
                if (!OpenSpot(c, 4f, obstacles, null)) continue;
                stand = c;
                break;
            }
            yield return TeleportLocalPlayer(new Vector2(stand.x, stand.z), 0f);
            yield return new WaitForSeconds(0.6f);
            obstacles = PlantObstacles(wood.transform);
            Debug.Log($"[Plants] torch stand {stand} open {OpenSpot(wood.transform.position, 4f, obstacles, null)}");
            bool torchOn = inventory.TorchLit;
            Transform held = null;
            foreach (Transform t in wood.GetComponentsInChildren<Transform>(true))
                if (t.name == "Voxel" && t.parent != null && t.parent.name == "Torch") held = t;
            PlayerCamera side = PlayerCamera.Local;
            // Own camera on the torch hand: in front of the player, a little to the torch side, clear of obstacles.
            Vector3 torchAt = held != null ? held.position : wood.transform.position + Vector3.up * 1.1f;
            Vector3 chest = wood.transform.position + Vector3.up * 1.2f;
            Vector3 focus = Vector3.Lerp(chest, torchAt, 0.6f);
            Vector3 fwd = wood.transform.forward;
            float torchSide = Vector3.Dot(torchAt - chest, wood.transform.right) >= 0f ? 1f : -1f;
            side.enabled = false;
            Camera torchView = Camera.main;
            Vector3 torchCam = focus + (fwd * 1.9f + wood.transform.right * torchSide * 0.9f) + Vector3.up * 0.35f;
            foreach (float turn in new[] { 25f, 45f, 5f, 65f, -15f, 90f, -40f, 120f })
            {
                Vector3 dir = Quaternion.Euler(0f, turn * torchSide, 0f) * fwd;
                Vector3 c = focus + dir * 2.1f + Vector3.up * 0.35f;
                if (!SightClear(c, focus, obstacles, wood.transform)) continue;
                torchCam = c;
                break;
            }
            torchView.transform.position = torchCam;
            torchView.transform.LookAt(focus);
            yield return new WaitForEndOfFrame();
            ui.DebugShow(0);
            yield return Shot(folder, "plant_torch");
            side.enabled = true;
            bool torchOk = torchOn && held != null;
            Debug.Log($"[Plants] torch lit {torchOn}, voxel {(held != null ? "present" : "missing")}");

            wood.ServerAddWood(40);
            yield return null;
            BuildManager build = BuildManager.Instance;
            PieceCatalog catalog = build != null ? build.Catalog : null;
            int bench = CabinKind(catalog, "Workbench");
            int fireKind = CabinKind(catalog, "Campfire");
            Vector3 spot = wood.transform.position + wood.transform.forward * 4f;
            spot.y = WorldGround.HeightAt(spot);
            PlaceResult benchResult = build != null ? build.ServerPlace(wood.gameObject, bench, spot, 0f) : PlaceResult.BadKind;
            Vector3 fireAt = spot + wood.transform.right * 2.2f;
            fireAt.y = WorldGround.HeightAt(fireAt);
            PlaceResult fireResult = build != null ? build.ServerPlace(wood.gameObject, fireKind, fireAt, 30f) : PlaceResult.BadKind;
            // Pieces spawn on the next LateUpdate reconcile; find the campfire by kind, nearest the placed spot.
            GameObject piece = null;
            for (float waited = 0f; waited < 3f && piece == null; waited += 0.25f)
            {
                yield return new WaitForSeconds(0.25f);
                float best = 3f;
                foreach (PieceView pv in UnityEngine.Object.FindObjectsByType<PieceView>(FindObjectsSortMode.None))
                {
                    if (pv.Kind != fireKind) continue;
                    float d = Vector3.Distance(pv.transform.position, fireAt);
                    if (d < best) { best = d; piece = pv.gameObject; }
                }
            }
            Debug.Log($"[Plants] campfire view {(piece != null ? piece.name + " under " + (piece.transform.parent != null ? piece.transform.parent.name : "root") : "missing")}");
            int voxelMeshes = 0;
            if (piece != null)
            {
                foreach (Transform t in piece.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Voxel") voxelMeshes += t.GetComponentsInChildren<Renderer>().Length;
                Vector3 look = piece.transform.position;
                side.enabled = false;
                Camera fireView = Camera.main;
                List<Renderer> around = PlantObstacles(null);
                Vector3 fireCam = look + new Vector3(2.4f, 1.6f, 2.4f);
                for (int i = 0; i < 12; i++)
                {
                    float a = (45f + i * 30f) * Mathf.Deg2Rad;
                    Vector3 c = look + new Vector3(Mathf.Sin(a) * 3.2f, 1.6f, Mathf.Cos(a) * 3.2f);
                    if (!SightClear(c, look + Vector3.up * 0.3f, around, piece.transform)) continue;
                    fireCam = c;
                    break;
                }
                fireView.transform.position = fireCam;
                fireView.transform.LookAt(look + Vector3.up * 0.3f);
                yield return new WaitForEndOfFrame();
                ui.DebugShow(0);
                yield return Shot(folder, "plant_fire");
                side.enabled = true;
            }
            Debug.Log($"[Plants] workbench {benchResult}, campfire {fireResult}, voxel meshes {voxelMeshes}");
            bool pass = herbOk && torchOk && voxelMeshes >= 12;
            Debug.Log("[Plants] RESULT " + (pass ? "PASS" : "FAIL"));
        }

        private static IEnumerator ArrivalTour(string folder, PlayerWood wood, GameUi ui, Vector3 firstSeen)
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.4f);
            if (WeatherSystem.Instance != null) WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
            ui.DebugShow(0);
            GameUi.HudHidden = true;
            PlayerAxe axe = wood.GetComponent<PlayerAxe>();
            PlayerUpgrades upgrades = wood.GetComponent<PlayerUpgrades>();
            PlayerMotor motor = wood.GetComponent<PlayerMotor>();
            float water = Chopper.World.Generation.WorldGen.WaterHeight;
            bool droppedIn = firstSeen.y > water + 10f;
            bool startedBare = !axe.HasAxe && !upgrades.Dressed && !upgrades.HasShield;
            int kitPieces = 0;
            foreach (GroundItem item in GroundItem.All)
                if (item != null && GroundItem.IsShoreGear(item.Kind)) kitPieces++;
            Debug.Log($"[Arrival] first seen at {firstSeen:F1} (water {water:F2}), dropped in {droppedIn}; no outfit and no axe {startedBare}; kit pieces on the ground {kitPieces}");
            PlayerCamera cam = wood.GetComponent<PlayerCamera>();
            cam.DebugSetDistance(7f);
            cam.DebugSetPitch(12f);
            yield return new WaitForSeconds(1f);
            yield return Shot(folder, "arrival_1_in_the_pond");

            // Swim to the kit and walk over both pieces.
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            float start = Time.time;
            for (int piece = 0; piece < 3; piece++)
            {
                Vector3 target = WorldStart.KitSpot(piece);
                float pieceStart = Time.time;
                while (Time.time - pieceStart < 45f)
                {
                    Vector3 to = target - wood.transform.position;
                    to.y = 0f;
                    if (to.magnitude < 0.7f)
                    {
                        InputSystem.QueueStateEvent(pad, new GamepadState());
                        break;
                    }
                    float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    cam.SnapYaw(yaw);
                    InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.up });
                    yield return null;
                }
                if (piece == 0)
                {
                    Vector3 saved = wood.transform.position;
                    Quaternion savedRot = wood.transform.rotation;
                    Vector3 outfitAt = WorldStart.KitSpot(0);
                    Vector3 axeAt = WorldStart.KitSpot(1);
                    Vector3 shieldAt = WorldStart.KitSpot(2);
                    Vector3 mid = (outfitAt + axeAt + shieldAt) / 3f;
                    Vector3 along = axeAt - outfitAt;
                    along.y = 0f;
                    if (along.sqrMagnitude < 0.01f) along = Vector3.forward;
                    along.Normalize();
                    Vector3 side = Vector3.Cross(along, Vector3.up);
                    Vector3 stand = mid + side * 2.8f;
                    stand.y = WorldGround.HeightAt(stand);
                    wood.transform.SetPositionAndRotation(stand, Quaternion.LookRotation(mid - stand));
                    Vector3 watch = mid - along * 1.7f + side * 1.2f + Vector3.up * 0.85f;
                    cam.DebugHold(watch, Quaternion.LookRotation(mid + Vector3.up * 0.1f - watch));
                    yield return Shot(folder, "arrival_2_on_the_bank");
                    cam.DebugRelease();
                    wood.transform.SetPositionAndRotation(saved, savedRot);
                }
                foreach (GroundItem item in GroundItem.All)
                    if (item != null && GroundItem.IsShoreGear(item.Kind))
                        Debug.Log($"[Arrival] piece {piece}: {item.Kind} at {item.transform.position:F1}, {Vector3.Distance(item.transform.position, wood.transform.position):F2} m");
                GroundItem reach = GroundItem.GearInReach(wood.transform.position, wood.gameObject);
                Debug.Log($"[Arrival] piece {piece}: player {wood.transform.position:F1}, kit spot {target:F1}, in reach {(reach != null ? reach.Kind.ToString() : "none")}, blocked {UiState.GameplayBlocked}");
                float pressUntil = Time.time + 0.35f;
                while (Time.time < pressUntil)
                {
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.West));
                    yield return null;
                }
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return new WaitForSeconds(0.35f);
                ui.DebugShow(0);
                yield return new WaitForSecondsRealtime(0.4f);
            }
            InputSystem.QueueStateEvent(pad, new GamepadState());
            InputSystem.RemoveDevice(pad);
            yield return new WaitForSeconds(1f);
            bool equipped = axe.HasAxe && upgrades.Dressed && upgrades.HasShield;
            Debug.Log($"[Arrival] reached the kit in {Time.time - start:F1} s, swimming {motor.IsSwimming}; axe {axe.HasAxe} (durability {axe.Durability}), outfit {upgrades.Dressed}, shield {upgrades.HasShield}");
            cam.SnapYaw(wood.transform.eulerAngles.y + 160f);
            cam.DebugSetDistance(3.4f);
            yield return new WaitForSeconds(0.8f);
            yield return Shot(folder, "arrival_3_dressed");

            // A fall: the axe and the outfit stay where the player fell, and they arrive in the pond again.
            Vector3 fellAt = wood.transform.position;
            PlayerHealth health = wood.GetComponent<PlayerHealth>();
            health.ServerFall(200f);
            yield return new WaitForSeconds(0.5f);
            int left = 0;
            foreach (GroundItem item in GroundItem.All)
            {
                if (item == null || !GroundItem.IsShoreGear(item.Kind)) continue;
                Vector3 off = item.transform.position - fellAt;
                off.y = 0f;
                if (off.magnitude < 3f) left++;
            }
            bool fell = health.IsDown;
            health.ServerRespawnNow();
            // The move reaches the owner by a TargetRpc; still far up in the drop after a quarter second.
            yield return new WaitForSeconds(0.25f);
            Vector3 again = wood.transform.position;
            bool backInAir = again.y > water + 10f;
            bool bareAgain = !axe.HasAxe && !upgrades.Dressed && !upgrades.HasShield;
            Debug.Log($"[Arrival] fell {fell}; gear left where they fell {left} of 3; arrives again at {again:F1}, in the air over the pond {backInAir}, empty-handed {bareAgain}");
            yield return new WaitForSeconds(2.5f);
            yield return Shot(folder, "arrival_4_back_in_the_pond");

            bool pass = droppedIn && startedBare && kitPieces >= 3 && equipped && fell && left == 3 && backInAir && bareAgain;
            Debug.Log("[Arrival] RESULT " + (pass ? "PASS" : "FAIL"));
        }

        // Renderers that can block a close-up: trees, rocks, bushes, props, players. Grass tufts (small) and terrain
        // chunks (wider than 10 m) are left out. Anything under ignore is left out too.
        private static List<Renderer> PlantObstacles(Transform ignore)
        {
            var list = new List<Renderer>();
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer) continue;
                Bounds b = r.bounds;
                if (b.size.magnitude < 0.8f || b.size.x > 10f || b.size.z > 10f) continue;
                if (ignore != null && r.transform.IsChildOf(ignore)) continue;
                list.Add(r);
            }
            return list;
        }

        // Nothing taller than knee height within radius (horizontal distance to the bounds), and nothing overhead.
        private static bool OpenSpot(Vector3 p, float radius, List<Renderer> obstacles, Transform ignore)
        {
            foreach (Renderer r in obstacles)
            {
                if (r == null || (ignore != null && r.transform.IsChildOf(ignore))) continue;
                Bounds b = r.bounds;
                if (b.max.y < p.y + 0.5f || b.min.y > p.y + 8f) continue;
                Vector3 q = b.ClosestPoint(new Vector3(p.x, Mathf.Clamp(p.y + 1f, b.min.y, b.max.y), p.z));
                float dx = q.x - p.x, dz = q.z - p.z;
                if (dx * dx + dz * dz < radius * radius) return false;
            }
            return true;
        }

        // No obstacle bounds between the camera and the subject (and the camera is not inside one).
        private static bool SightClear(Vector3 from, Vector3 to, List<Renderer> obstacles, Transform subject)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            var ray = new Ray(from, d / len);
            foreach (Renderer r in obstacles)
            {
                if (r == null || (subject != null && r.transform.IsChildOf(subject))) continue;
                Bounds b = r.bounds;
                if (b.Contains(from)) return false;
                if (b.IntersectRay(ray, out float hit) && hit < len - 0.2f) return false;
            }
            return true;
        }

        // Pond from the bank, then the camera pointed at the block sun and at one cloud. The gameplay camera cannot
        // tilt far enough to see a high sun, so those two frames turn the camera directly.
        private static IEnumerator SkyLook(string folder, PlayerWood wood, GameUi ui)
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Hut.Instance.ServerSetRepaired(true);
            DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.4f);
            if (WeatherSystem.Instance != null) WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
            yield return new WaitForSeconds(4f);
            ui.DebugShow(0);
            GameUi.HudHidden = true;

            Chopper.World.Generation.WorldGen gen = Chopper.World.Generation.WorldSession.Gen;
            Vector2 pond = gen.PondCenter;
            Vector2 bank = pond + (pond.sqrMagnitude > 1f ? pond.normalized : Vector2.right) * 22f;
            float yaw = Mathf.Atan2(pond.x - bank.x, pond.y - bank.y) * Mathf.Rad2Deg;
            yield return TeleportLocalPlayer(bank, yaw);
            PlayerCamera cam = PlayerCamera.Local;
            cam.DebugSetDistance(6f);
            cam.DebugSetPitch(8f);
            cam.SnapYaw(yaw);
            yield return new WaitForSeconds(1.5f);
            ui.DebugShow(0);
            yield return Shot(folder, "sky_pond");

            GameObject sky = GameObject.Find("BlockSky");
            Transform sun = sky != null ? sky.transform.Find("BlockSun") : null;
            Renderer sunRenderer = sun != null ? sun.GetComponent<Renderer>() : null;
            int clouds = 0;
            if (sky != null)
            {
                Transform cloudRoot = sky.transform.Find("Clouds");
                if (cloudRoot != null) clouds = cloudRoot.childCount;
            }
            Renderer pondRenderer = null;
            GameObject pondWater = GameObject.Find("PondWater");
            if (pondWater != null) pondRenderer = pondWater.GetComponent<Renderer>();
            Material water = pondRenderer != null ? pondRenderer.sharedMaterial : null;
            float ripple = water != null && water.HasProperty("_RippleStrength") ? water.GetFloat("_RippleStrength") : -1f;
            float swell = water != null && water.HasProperty("_Swell") ? water.GetFloat("_Swell") : -1f;
            Debug.Log($"[Sky] BlockSky {(sky != null ? "present" : "missing")}, clouds {clouds}, sun {(sunRenderer != null && sunRenderer.enabled ? "on" : "off")}, pond {(water != null ? water.name : "none")} ripple {ripple:F2} swell {swell:F2}");

            Camera view = Camera.main;
            float far = view.farClipPlane;
            view.farClipPlane = Mathf.Max(far, 500f);
            cam.enabled = false;
            Light daylight = RenderSettings.sun;
            Vector3 toward = daylight != null ? -daylight.transform.forward : Vector3.up;
            view.transform.position = wood.transform.position + Vector3.up * 2.2f;
            view.transform.rotation = Quaternion.LookRotation(toward, Vector3.up);
            yield return new WaitForEndOfFrame();
            yield return Shot(folder, "sky_sun");

            if (clouds > 0)
            {
                // Clouds follow the camera, so this look stays at the player's height and aims at the first puff.
                Transform puff = sky.transform.Find("Clouds").GetChild(0);
                Vector3 aim = new Vector3(view.transform.position.x, 0f, view.transform.position.z) + puff.localPosition;
                view.transform.LookAt(aim);
                yield return new WaitForEndOfFrame();
                yield return Shot(folder, "sky_cloud");
            }
            view.farClipPlane = far;
            cam.enabled = true;

            bool pass = sky != null && clouds >= 8 && sunRenderer != null && sunRenderer.enabled && ripple == 0f && swell == 0f;
            Debug.Log("[Sky] RESULT " + (pass ? "PASS" : "FAIL"));
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
                    if (probe[h] == null || Chopper.World.Voxel.VoxelWorld.IsGround(probe[h])) continue;
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
                if (hit == null || Chopper.World.Voxel.VoxelWorld.IsGround(hit) || hit is CharacterController) continue;
                solid++;
            }
            return solid;
        }

        private static IEnumerator GripStop(Gamepad pad, PlayerWood wood, string label, string folder)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState());
            PlayerMotor motor = wood.GetComponent<PlayerMotor>();
            bool slippery = motor.OnSlipperyGround(false);
            Debug.Log($"[Grip] {label}: release at {wood.transform.position:F1}, anim speed {wood.GetComponentInChildren<Animator>().GetFloat("Speed"):F2}");
            Vector3 from = wood.transform.position;
            float start = Time.time, stoppedAt = -1f;
            Vector3 last = from;
            bool shot = false;
            while (Time.time - start < 2.5f)
            {
                yield return null;
                Vector3 now = wood.transform.position;
                Vector3 step = now - last;
                step.y = 0f;
                last = now;
                if (!shot && Time.time - start > 0.1f) { shot = true; yield return Shot(folder, "grip_" + label.Replace(' ', '_')); }
                if (step.magnitude / Mathf.Max(Time.deltaTime, 1e-4f) < 0.05f) { stoppedAt = Time.time - start; break; }
            }
            Vector3 slide = wood.transform.position - from;
            slide.y = 0f;
            Debug.Log($"[Grip] {label}: anim speed after {wood.GetComponentInChildren<Animator>().GetFloat("Speed"):F2}, slippery {slippery}, slide {slide.magnitude:F2} m, stopped in {(stoppedAt < 0f ? "over 2.5" : stoppedAt.ToString("F2"))} s");
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

        // See -chopperFlicker. Frames go to <folder>/flicker/<place>_<setting>/f###.png, plus mask.png (the same first
        // frame with the player hidden, so the measuring script can tell character pixels from ground pixels).
        private static IEnumerator FlickerTour(string folder, PlayerWood wood, GameUi ui)
        {
            const int Frames = 40;
            const float YawStep = 0.35f;
            Hut.Instance.ServerSetRepaired(true);
            DayNightCycle.Instance.ServerDebugSetClock(DayPhase.Day, 0.4f);
            WeatherSystem.Instance.ServerSet(WeatherKind.Sunny);
            wood.GetComponent<PlayerAxe>().ServerSetHasAxe(true);
            yield return new WaitForSeconds(4f);
            GameUi.HudHidden = true;
            ui.DebugShow(0);
            Debug.developerConsoleVisible = false;

            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            Camera view = PlayerCamera.Local.View;
            var viewData = view.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            Light sun = null;
            foreach (Light l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.isActiveAndEnabled && (sun == null || l.intensity > sun.intensity)) sun = l;
            Renderer[] body = wood.GetComponentsInChildren<Renderer>(false);
            var atlases = new HashSet<Texture>();
            foreach (Renderer r in body)
                foreach (Material m in r.sharedMaterials)
                    if (m != null && m.HasTexture("_BaseMap") && m.GetTexture("_BaseMap") != null) atlases.Add(m.GetTexture("_BaseMap"));
            var blockMaterials = new HashSet<Material>();
            foreach (MeshRenderer r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name == "Chopper/Block") blockMaterials.Add(r.sharedMaterial);
            Debug.Log($"[Flicker] msaa {pipeline.msaaSampleCount}, aa {viewData.antialiasing}, shadows {pipeline.mainLightShadowmapResolution} px to {pipeline.shadowDistance:F0} m in {pipeline.shadowCascadeCount} cascades, " +
                      $"depth bias {pipeline.shadowDepthBias:F2} normal {pipeline.shadowNormalBias:F2}, sun {(sun ? sun.name : "none")} bias {(sun ? sun.shadowBias : 0f):F2}/{(sun ? sun.shadowNormalBias : 0f):F2}, " +
                      $"atlases {string.Join(", ", atlases.Select(t => $"{t.name} {t.width}px mips {t.mipmapCount} filter {t.filterMode} aniso {t.anisoLevel}"))}, block materials {blockMaterials.Count}, screen {Screen.width}x{Screen.height}");

            (Vector2 where, float yaw, string label, float distance, float pitch)[] places =
            {
                (new Vector2(0f, 9f), 180f, "yard", 4.5f, 18f),
                (new Vector2(150f, 0f), 90f, "meadow", 4.5f, 22f),
                (Vector2.zero, 200f, "hall", 3f, 12f),
                (Vector2.zero, 200f, "main", 3f, 12f),
                (Vector2.zero, 200f, "loft", 3f, 12f),
            };
            string[] settings = { "base", "noshadow", "noaa", "mipbias", "noblockdetail" };
            int msaa = pipeline.msaaSampleCount;
            var aa = viewData.antialiasing;
            LightShadows sunShadows = sun ? sun.shadows : LightShadows.None;
            var oldBlock = blockMaterials.ToDictionary(m => m, m => (m.GetFloat("_Jitter"), m.GetFloat("_Edge")));
            foreach (var place in places)
            {
                int storey = place.label == "hall" ? 0 : place.label == "main" ? 1 : place.label == "loft" ? 2 : -1;
                if (storey >= 0)
                    yield return TeleportExact(Hut.Instance.transform.position + new Vector3(Hut.HouseScale, 0.1f + storey * Hut.StoreyHeight, Hut.HouseScale), place.yaw);
                else
                    yield return TeleportLocalPlayer(place.where, place.yaw);
                PlayerCamera cam = PlayerCamera.Local;
                cam.DebugSetDistance(place.distance);
                cam.DebugSetPitch(place.pitch);
                yield return new WaitForSeconds(1.5f);
                foreach (string setting in settings)
                {
                    if (setting == "noshadow" && sun) sun.shadows = LightShadows.None;
                    if (setting == "noaa") { pipeline.msaaSampleCount = 1; viewData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None; }
                    if (setting == "mipbias") foreach (Texture t in atlases) t.mipMapBias = 1f;
                    if (setting == "noblockdetail") foreach (Material m in blockMaterials) { m.SetFloat("_Jitter", 0f); m.SetFloat("_Edge", 0f); }

                    string dir = Path.Combine(folder, "flicker", place.label + "_" + setting);
                    Directory.CreateDirectory(dir);
                    cam.SnapYaw(place.yaw);
                    yield return new WaitForSeconds(0.5f);
                    // A fixed game step per frame, so the idle sway and the orbit match in every setting.
                    Time.captureFramerate = 30;
                    Debug.developerConsoleVisible = false;
                    for (int i = 0; i < 6; i++) yield return null;
                    if (setting == "base")
                    {
                        foreach (Renderer r in body) r.enabled = false;
                        yield return new WaitForEndOfFrame();
                        WriteFrame(Path.Combine(dir, "mask.png"));
                        foreach (Renderer r in body) r.enabled = true;
                        yield return null;
                    }
                    for (int i = 0; i < Frames; i++)
                    {
                        cam.SnapYaw(place.yaw + i * YawStep);
                        yield return new WaitForEndOfFrame();
                        WriteFrame(Path.Combine(dir, $"f{i:000}.png"));
                        yield return null;
                    }
                    Time.captureFramerate = 0;
                    Debug.Log($"[Flicker] wrote {place.label} {setting} ({Frames} frames, {YawStep} deg per frame)");

                    if (sun) sun.shadows = sunShadows;
                    pipeline.msaaSampleCount = msaa;
                    viewData.antialiasing = aa;
                    foreach (Texture t in atlases) t.mipMapBias = 0f;
                    foreach (var kv in oldBlock) { kv.Key.SetFloat("_Jitter", kv.Value.Item1); kv.Key.SetFloat("_Edge", kv.Value.Item2); }
                }
            }
        }

        private static void WriteFrame(string path)
        {
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, shot.EncodeToPNG());
            UnityEngine.Object.Destroy(shot);
        }

        private static IEnumerator FeetTour(string folder, PlayerWood wood, GameUi ui)
        {
            Hut.Instance.ServerSetRepaired(true);
            wood.GetComponent<PlayerCombat>().ServerSetGod(true);
            var cam = new GameObject("FeetCamera").AddComponent<Camera>();
            cam.depth = 50f; cam.fieldOfView = 45f; cam.nearClipPlane = 0.02f; cam.enabled = false;
            float worstSole = 0f; int buried = 0; int stops = 0;
            var lines = new System.Collections.Generic.List<string>();
            IEnumerator Stop(string label, bool walk)
            {
                yield return new WaitForSeconds(1.4f);
                if (walk)
                {
                    Gamepad pad = InputSystem.AddDevice<Gamepad>();
                    for (float t = 0f; t < 0.8f; t += Time.deltaTime) { InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.up * 0.5f }); yield return null; }
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    yield return null;
                    InputSystem.RemoveDevice(pad);
                    yield return new WaitForSeconds(0.5f);
                }
                stops++;
                float stopWorst = 0f; string who = "";
                foreach (Chopper.Player.FootGrounder g in Chopper.Player.FootGrounder.All)
                {
                    if (g == null || !g.Ready || Vector3.Distance(g.transform.position, wood.transform.position) > 12f) continue;
                    float m = Mathf.Min(g.GapL, g.GapR);
                    who += $" {g.transform.root.name}:{g.GapL:F3}/{g.GapR:F3}(ankle {g.Ankle:F3})";
                    stopWorst = Mathf.Min(stopWorst, m);
                }
                foreach (Animator an in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
                {
                    if (Vector3.Distance(an.transform.position, wood.transform.position) > 12f) continue;
                    var fg = an.GetComponent<Chopper.Player.FootGrounder>();
                    string hits = "";
                    if (fg != null && fg.Ready) { fg.Ground(fg.FootL.position, out bool h1); var all = Physics.RaycastAll(fg.FootL.position + Vector3.up * 0.7f, Vector3.down, 1.8f, ~0, QueryTriggerInteraction.Ignore); foreach (var hh in all) hits += hh.collider.name + "@" + hh.point.y.ToString("F2") + " "; hits = $"hit {h1} foot y {fg.FootL.position.y:F2} root y {an.transform.position.y:F2} rays [{hits}]"; }
                    Debug.Log($"[Feet] {label} animator {an.transform.root.name}/{an.name} human {an.isHuman} grounder {(fg == null ? "none" : fg.Ready.ToString())} {hits}");
                }
                worstSole = Mathf.Min(worstSole, stopWorst);
                int stopBuried = 0;
                Animator anim = wood.GetComponentInChildren<Animator>();
                foreach (HumanBodyBones hb in new[] { HumanBodyBones.Chest, HumanBodyBones.Head, HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.Hips })
                {
                    Transform b = anim != null && anim.isHuman ? anim.GetBoneTransform(hb) : null;
                    if (b == null) continue;
                    foreach (Collider c in Physics.OverlapSphere(b.position, 0.04f, ~0, QueryTriggerInteraction.Ignore))
                        if (!(c is CharacterController) && !c.transform.IsChildOf(wood.transform)) { stopBuried++; Debug.Log($"[Feet] {label}: {hb} inside {c.name}"); break; }
                }
                buried += stopBuried;
                string line = $"[Feet] {label}: at {wood.transform.position:F2} worst sole {stopWorst:F3} m, buried bones {stopBuried};{who}";
                Debug.Log(line); lines.Add(line);
                Vector3 p = wood.transform.position;
                foreach (var (n, side) in new[] { ("side", wood.transform.right), ("front", wood.transform.forward) })
                {
                    cam.transform.position = p + side * 1.7f + Vector3.up * 0.55f;
                    cam.transform.LookAt(p + Vector3.up * 0.35f);
                    cam.enabled = true;
                    ui.DebugShow(0);
                    yield return Shot(folder, $"feet_{label}_{n}");
                }
                cam.enabled = false;
            }

            // Flat ground in the hub.
            yield return TeleportLocalPlayer(new Vector2(0f, 30f), 0f);
            yield return Stop("flat", false);

            // The steepest walkable slope near the hub.
            Vector2 slope = new Vector2(0f, 30f); float steep = 0f;
            for (int r = 20; r <= 90; r += 5)
                for (int a = 0; a < 360; a += 15)
                {
                    Vector2 q = new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad)) * r;
                    float s = Chopper.World.WorldGround.SteepnessAt(new Vector3(q.x, 0f, q.y));
                    if (s > steep && s < 40f) { steep = s; slope = q; }
                }
            Debug.Log($"[Feet] slope at {slope} steepness {steep:F0}");
            yield return TeleportLocalPlayer(slope, 0f);
            yield return Stop("slope", false);
            yield return Stop("slope_walk", true);
            Vector3 sp = wood.transform.position + wood.transform.forward * 2.2f;
            sp.y = Chopper.World.WorldGround.HeightAt(sp);
            EnemyDirector.Instance.ServerSpawn(Element.Earth, TreeSize.Small, true, sp);
            yield return Stop("slope_enemy", false);

            // The hut: on its door steps, then inside walking into the back wall.
            Transform hut = Hut.Instance.transform;
            Vector3 door = hut.position + hut.forward * 4.2f;
            yield return TeleportLocalPlayer(new Vector2(door.x, door.z), hut.eulerAngles.y + 180f);
            yield return Stop("hut_steps", true);
            Vector3 inside = hut.position + Vector3.up * 0.5f;
            yield return TeleportExact(inside, hut.eulerAngles.y + 180f);
            yield return Stop("hut_wall", true);

            // A mine chamber.
            var plans = Chopper.World.Generation.MineWorld.Plans;
            if (plans != null && plans.Count > 0 && plans[0].Rooms.Count > 1)
            {
                yield return TeleportExact(plans[0].RoomCenter(1) + Vector3.up * 0.1f, 0f);
                yield return Stop("cave", false);
                yield return Stop("cave_walk", true);
            }

            bool pass = stops >= 6 && worstSole > -0.04f && buried == 0;
            Debug.Log($"[Feet] RESULT {(pass ? "PASS" : "FAIL")} ({stops} stops, worst sole {worstSole:F3} m, {buried} buried bones)");
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
