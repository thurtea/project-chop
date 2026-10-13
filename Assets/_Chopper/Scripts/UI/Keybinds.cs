using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Chopper.UI
{
    // The actions a player can rebind. Every action has a keyboard or mouse binding and a gamepad binding, and either
    // one triggers it. Movement and look stay on WASD and the mouse (left stick and right stick on a pad), the menu
    // key is Esc (Start), and zoom is the scroll wheel (on a pad: D-pad down). On a pad the six hotbar items have no
    // buttons of their own: the D-pad picks and uses them (PadHotbar). Bindings are Input System control paths
    // (for example "<Mouse>/forwardButton" or "<Gamepad>/buttonSouth") saved in PlayerPrefs.
    public enum KeyAction
    {
        Attack,
        Block,
        Roll,
        Jump,
        Interact,
        Repair,
        Sprint,
        Crouch,
        Torch,
        Potion,
        Inventory,
        Map,
        Food,
        Rod,
        Axe,
        Build,
        Unequip
    }

    public static class Keybinds
    {
        private const string PrefsPrefix = "Chopper.Key.";
        private const string PadPrefsPrefix = "Chopper.Pad.";

        private static readonly string[] Defaults =
        {
            "<Mouse>/leftButton",    // Attack
            "<Mouse>/rightButton",   // Block
            "<Keyboard>/leftAlt",    // Roll
            "<Keyboard>/space",      // Jump
            "<Keyboard>/e",          // Interact
            "<Keyboard>/r",          // Repair; turns the piece while building
            "<Keyboard>/leftShift",  // Sprint
            "<Keyboard>/leftCtrl",   // Crouch
            "<Keyboard>/2",          // Torch
            "<Keyboard>/3",          // Potion
            "<Keyboard>/tab",        // Inventory
            "<Keyboard>/m",          // Map
            "<Keyboard>/5",          // Food
            "<Keyboard>/4",          // Rod
            "<Keyboard>/1",          // Axe
            "<Keyboard>/6",          // Build (the hammer)
            "<Keyboard>/h"           // Unequip: put away whatever is in the hands
        };

        // Valheim style: triggers for the weapon and the guard, face buttons for jump, roll, interact and inventory,
        // L3 for sprint, R3 to put away what is in the hands (Valheim's hide), LB for crouch, the D-pad for the
        // hotbar (left and right to pick, up to use).
        private static readonly string[] PadDefaults =
        {
            "<Gamepad>/rightTrigger",      // Attack
            "<Gamepad>/leftTrigger",       // Block
            "<Gamepad>/buttonEast",        // Roll (B)
            "<Gamepad>/buttonSouth",       // Jump (A)
            "<Gamepad>/buttonWest",        // Interact (X)
            "<Gamepad>/rightShoulder",     // Repair (RB); turns the piece while building
            "<Gamepad>/leftStickPress",    // Sprint (L3)
            "<Gamepad>/leftShoulder",      // Crouch (LB)
            HotbarPath,                    // Torch
            HotbarPath,                    // Potion
            "<Gamepad>/buttonNorth",       // Inventory (Y)
            "<Gamepad>/select",            // Map (Back)
            HotbarPath,                    // Food
            HotbarPath,                    // Rod
            HotbarPath,                    // Axe (draw or put away)
            HotbarPath,                    // Build (the hammer)
            "<Gamepad>/rightStickPress"    // Unequip (R3)
        };

        private static readonly string[] Labels =
        {
            "Chop and attack", "Block", "Roll", "Jump", "Interact", "Repair the hut", "Toggle sprint",
            "Toggle crouch", "Torch on or off", "Bandage", "Inventory", "Map", "Eat fish", "Fishing rod",
            "Axe out or away", "Build (hammer)", "Put away (hands free)"
        };

        private static readonly string[] Cache = new string[Defaults.Length];
        private static readonly string[] PadCache = new string[Defaults.Length];
        private static readonly Dictionary<string, List<ButtonControl>> Controls = new Dictionary<string, List<ButtonControl>>();
        private static readonly List<ButtonControl> NoControls = new List<ButtonControl>();
        private static bool _hooked;

        public static int Count => Defaults.Length;

        public static string Label(KeyAction action) => Labels[(int)action];

        public static string DefaultPath(KeyAction action) => Defaults[(int)action];

        public static string DefaultPadPath(KeyAction action) => PadDefaults[(int)action];

        public static string PathOf(KeyAction action)
        {
            int i = (int)action;
            if (Cache[i] == null)
            {
                string saved = PlayerPrefs.GetString(PrefsPrefix + action, string.Empty);
                Cache[i] = string.IsNullOrEmpty(saved) ? Defaults[i] : saved;
            }
            return Cache[i];
        }

        public static string PadPathOf(KeyAction action)
        {
            int i = (int)action;
            if (PadCache[i] == null)
            {
                MovePadCrouchOffR3();
                string saved = PlayerPrefs.GetString(PadPrefsPrefix + action, string.Empty);
                // The hotbar items always use the D-pad hotbar, and the D-pad belongs to it, so older saves that put an
                // item on a button (LB for food) or anything on the D-pad or an LB chord fall back to the default.
                bool stale = PadHotbar.OnHotbar(action) || saved.StartsWith("<Gamepad>/dpad/") || saved.IndexOf('+') >= 0;
                PadCache[i] = string.IsNullOrEmpty(saved) || stale ? PadDefaults[i] : saved;
            }
            return PadCache[i];
        }

        // Binds an action. If another action already uses the control, the two swap so nothing is left unbound.
        public static void Set(KeyAction action, string path)
        {
            string old = PathOf(action);
            for (int i = 0; i < Defaults.Length; i++)
            {
                var other = (KeyAction)i;
                if (other == action || PathOf(other) != path) continue;
                Store(other, old);
            }
            Store(action, path);
        }

        public static void SetPad(KeyAction action, string path)
        {
            string old = PadPathOf(action);
            for (int i = 0; i < PadDefaults.Length; i++)
            {
                var other = (KeyAction)i;
                if (other == action || PadPathOf(other) != path) continue;
                StorePad(other, old);
            }
            StorePad(action, path);
        }

        // R3 became Unequip (2026-10-05). A saved crouch still on R3 was never chosen (a reset saves every default),
        // so it goes back to the new default, LB. Done once.
        private const string CrouchMovedKey = "Chopper.PadCrouchOffR3";

        private static void MovePadCrouchOffR3()
        {
            if (PlayerPrefs.HasKey(CrouchMovedKey)) return;
            PlayerPrefs.SetInt(CrouchMovedKey, 1);
            string crouch = PadPrefsPrefix + KeyAction.Crouch;
            if (PlayerPrefs.GetString(crouch, string.Empty) == "<Gamepad>/rightStickPress") PlayerPrefs.DeleteKey(crouch);
        }

        // Forgets what was read from PlayerPrefs so the next read loads it again (tests).
        public static void ClearCache()
        {
            System.Array.Clear(Cache, 0, Cache.Length);
            System.Array.Clear(PadCache, 0, PadCache.Length);
        }

        public static void ResetAll()
        {
            for (int i = 0; i < Defaults.Length; i++) Store((KeyAction)i, Defaults[i]);
        }

        public static void ResetAllPad()
        {
            for (int i = 0; i < PadDefaults.Length; i++) StorePad((KeyAction)i, PadDefaults[i]);
        }

        private static void Store(KeyAction action, string path)
        {
            Cache[(int)action] = path;
            PlayerPrefs.SetString(PrefsPrefix + action, path);
        }

        private static void StorePad(KeyAction action, string path)
        {
            PadCache[(int)action] = path;
            PlayerPrefs.SetString(PadPrefsPrefix + action, path);
        }

        // ---------- Reading ----------

        // True on the frame the action's keyboard, mouse or gamepad control goes down.
        public static bool WasPressed(KeyAction action)
        {
            foreach (ButtonControl control in FindAll(PathOf(action))) if (control.wasPressedThisFrame) return true;
            string pad = PadPathOf(action);
            if (pad == HotbarPath) return PadHotbar.Used(action);
            foreach (ButtonControl control in FindAll(pad)) if (control.wasPressedThisFrame) return true;
            return false;
        }

        // Not a real control path: the hotbar items on a pad, picked with D-pad left and right and used with D-pad up.
        // FindAll matches nothing for it.
        public const string HotbarPath = "<Gamepad>/hotbar";

        public static bool IsHeld(KeyAction action)
        {
            foreach (ButtonControl control in FindAll(PathOf(action))) if (control.isPressed) return true;
            foreach (ButtonControl control in FindAll(PadPathOf(action))) if (control.isPressed) return true;
            return false;
        }

        // Every matching control on every connected device. A path like <Gamepad>/leftTrigger matches each pad: taking
        // only the first one ignored the pad in use whenever a second one was present (a Steam or DS4Windows virtual
        // pad, or one controller seen twice), so its buttons did nothing.
        private static List<ButtonControl> FindAll(string path)
        {
            if (!_hooked)
            {
                _hooked = true;
                InputSystem.onDeviceChange += (device, change) => Controls.Clear();
            }
            if (string.IsNullOrEmpty(path) || path == HotbarPath) return NoControls;
            if (Controls.TryGetValue(path, out List<ButtonControl> cached)) return cached;

            var found = new List<ButtonControl>();
            InputControlList<InputControl> matches = InputSystem.FindControls(path);
            foreach (InputControl control in matches)
            {
                if (control is ButtonControl button) found.Add(button);
            }
            matches.Dispose();
            Controls[path] = found;
            return found;
        }

        // ---------- Names ----------

        public static string NameOf(KeyAction action) => DisplayName(PathOf(action));

        public static string PadNameOf(KeyAction action) => PadDisplayName(PadPathOf(action));

        // "[E]" style text for prompts, using the current binding.
        public static string Bracketed(KeyAction action) => "[" + NameOf(action) + "]";

        public static string PadBracketed(KeyAction action) => "[" + PadNameOf(action) + "]";

        public static string DisplayName(string path)
        {
            switch (path)
            {
                case "<Mouse>/leftButton": return "Left click";
                case "<Mouse>/rightButton": return "Right click";
                case "<Mouse>/middleButton": return "Middle click";
                case "<Mouse>/backButton": return "Mouse 4 (back thumb)";
                case "<Mouse>/forwardButton": return "Mouse 5 (forward thumb)";
                case "<Keyboard>/leftAlt": return "Left Alt";
                case "<Keyboard>/rightAlt": return "Right Alt";
                case "<Keyboard>/leftShift": return "Left Shift";
                case "<Keyboard>/rightShift": return "Right Shift";
                case "<Keyboard>/leftCtrl": return "Left Ctrl";
                case "<Keyboard>/rightCtrl": return "Right Ctrl";
                case "<Keyboard>/space": return "Space";
                case "<Keyboard>/tab": return "Tab";
            }
            // Cached: the Input System builds this name by parsing device layouts, which made the HUD (that names
            // keys in its prompts every frame) throw away over half a megabyte a frame and stutter in the editor.
            if (ReadableNames.TryGetValue(path, out string cached)) return cached;
            string readable = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            readable = string.IsNullOrEmpty(readable) ? path : readable;
            ReadableNames[path] = readable;
            return readable;
        }

        private static readonly Dictionary<string, string> ReadableNames = new Dictionary<string, string>();

        // True while the pad in use is a PlayStation controller (DualShock 4 or DualSense), so hints say Cross and R2.
        public static bool PlayStationPad => Gamepad.current is UnityEngine.InputSystem.DualShock.DualShockGamepad;

        // Button names as printed on the pad in use: PlayStation names for a DualShock or DualSense, Xbox names otherwise.
        public static string PadDisplayName(string path)
        {
            if (path == HotbarPath) return "Hotbar";
            if (PlayStationPad)
            {
                switch (path)
                {
                    case "<Gamepad>/buttonSouth": return "Cross";
                    case "<Gamepad>/buttonEast": return "Circle";
                    case "<Gamepad>/buttonWest": return "Square";
                    case "<Gamepad>/buttonNorth": return "Triangle";
                    case "<Gamepad>/leftShoulder": return "L1";
                    case "<Gamepad>/rightShoulder": return "R1";
                    case "<Gamepad>/leftTrigger": return "L2";
                    case "<Gamepad>/rightTrigger": return "R2";
                    case "<Gamepad>/select": return "Create";
                    case "<Gamepad>/start": return "Options";
                }
            }
            switch (path)
            {
                case "<Gamepad>/buttonSouth": return "A";
                case "<Gamepad>/buttonEast": return "B";
                case "<Gamepad>/buttonWest": return "X";
                case "<Gamepad>/buttonNorth": return "Y";
                case "<Gamepad>/leftShoulder": return "LB";
                case "<Gamepad>/rightShoulder": return "RB";
                case "<Gamepad>/leftTrigger": return "LT";
                case "<Gamepad>/rightTrigger": return "RT";
                case "<Gamepad>/leftStickPress": return "L3";
                case "<Gamepad>/rightStickPress": return "R3";
                case "<Gamepad>/dpad/up": return "D-pad Up";
                case "<Gamepad>/dpad/down": return "D-pad Down";
                case "<Gamepad>/dpad/left": return "D-pad Left";
                case "<Gamepad>/dpad/right": return "D-pad Right";
                case "<Gamepad>/select": return "Back";
                case "<Gamepad>/start": return "Start";
            }
            return path.Replace("<Gamepad>/", string.Empty);
        }

        // True for controls a player may bind: keyboard keys (not Escape) and mouse buttons. Not mouse motion or gamepads.
        public static bool Bindable(InputControl control)
        {
            if (control == null || !(control is ButtonControl)) return false;
            if (control.device is Keyboard) return control.path != "/Keyboard/escape" && control.path != "/Keyboard/anyKey";
            if (control.device is Mouse) return control.path.EndsWith("Button");
            return false;
        }

        // Gamepad buttons, triggers, shoulders and stick clicks. Start stays the menu button and the D-pad stays the hotbar.
        public static bool PadBindable(InputControl control)
        {
            if (control == null || !(control is ButtonControl) || !(control.device is Gamepad pad)) return false;
            return control != pad.startButton && control.parent != pad.dpad && control != pad.dpad;
        }

        // The Input System gives device paths like "/Keyboard/e"; bindings use "<Keyboard>/e". Every gamepad, whatever
        // its brand layout, is stored as "<Gamepad>/..." so a binding keeps working when another pad is plugged in.
        public static string ToBindingPath(InputControl control)
        {
            string rest = control.path.Substring(control.device.path.Length + 1);
            if (control.device is Gamepad) return "<Gamepad>/" + rest;
            return "<" + control.device.layout + ">/" + rest;
        }
    }
}
