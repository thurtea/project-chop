using UnityEngine;
using UnityEngine.InputSystem;

namespace Chopper.UI
{
    // Tracks whether the player is currently using a gamepad or the keyboard and mouse, so the interface can show
    // the right button names (hotbar labels, prompts, hints).
    public static class InputHints
    {
        private const float StickActivity = 0.3f;
        private const float MouseActivity = 2f;

        public static bool UsingGamepad { get; private set; }

        public static void Tick()
        {
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                bool padActive = pad.leftStick.ReadValue().magnitude > StickActivity
                                 || pad.rightStick.ReadValue().magnitude > StickActivity
                                 || pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame
                                 || pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame
                                 || pad.leftTrigger.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame
                                 || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame
                                 || pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame
                                 || pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame
                                 || pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame;
                if (padActive) UsingGamepad = true;
            }

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool keyboardActive = keyboard != null && keyboard.anyKey.wasPressedThisFrame;
            bool mouseActive = mouse != null && (mouse.delta.ReadValue().magnitude > MouseActivity || mouse.leftButton.wasPressedThisFrame);
            if (keyboardActive || mouseActive) UsingGamepad = false;
            if (pad == null) UsingGamepad = false;
        }

        // Slot labels on the hotbar: the key number. On a pad the slots have no labels: the cursor shows which one
        // D-pad up will use.
        public static string Hotbar(int slot)
        {
            return UsingGamepad ? string.Empty : Keybinds.NameOf(PadHotbar.ActionAt(slot));
        }

        public static string MenuHint => UsingGamepad
            ? "Start Menu    " + Keybinds.PadNameOf(KeyAction.Inventory) + " Inventory    " + Keybinds.PadNameOf(KeyAction.Map) + " Map"
            : "Esc Menu    " + Keybinds.NameOf(KeyAction.Inventory) + " Inventory    " + Keybinds.NameOf(KeyAction.Map) + " Map";

        // The name of the control bound to an action on the device in use, for sentences like "Press E".
        public static string Name(KeyAction action) => UsingGamepad ? Keybinds.PadNameOf(action) : Keybinds.NameOf(action);

        // Prompts are Valheim's: what it is on the first line, then the key in yellow and what it does, beside the
        // crosshair ("Healing herb" over "[E] Pick"). Each finished prompt is kept, keyed by its parts and the key's
        // current name (itself cached by Keybinds), so nothing is built again every frame.
        private const string KeyColor = "#F2C94C";
        private static readonly System.Collections.Generic.Dictionary<(string, string, KeyAction, string), string> _prompts =
            new System.Collections.Generic.Dictionary<(string, string, KeyAction, string), string>();
        private static readonly System.Collections.Generic.Dictionary<(string, string, string), string> _pairs =
            new System.Collections.Generic.Dictionary<(string, string, string), string>();

        public static string Prompt(string thing, KeyAction action, string verb)
        {
            string key = Name(action);
            var id = (thing, verb, action, key);
            if (!_prompts.TryGetValue(id, out string text))
            {
                text = $"{thing}\n{Action(action, verb)}";
                _prompts[id] = text;
            }
            return text;
        }

        // A thing with two things to do (the hut: store wood and repair).
        public static string Prompt(string thing, KeyAction first, string firstVerb, KeyAction second, string secondVerb)
        {
            string a = Prompt(string.Empty, first, firstVerb);
            string b = Prompt(string.Empty, second, secondVerb);
            var id = (thing, a, b);
            if (!_pairs.TryGetValue(id, out string text))
            {
                text = $"{thing}{a}{b}";
                _pairs[id] = text;
            }
            return text;
        }

        private static string Action(KeyAction action, string verb) => $"<color={KeyColor}>[{Name(action)}]</color> {verb}";

        public static string AxeOnGroundPrompt => Prompt("Axe", KeyAction.Interact, "Pick up");
        public static string OutfitOnGroundPrompt => Prompt("Outfit", KeyAction.Interact, "Put on");
        public static string ShieldOnGroundPrompt => Prompt("Shield", KeyAction.Interact, "Pick up");

        public static string WorkbenchPrompt => Prompt("Workbench", KeyAction.Interact, "Use");

        public static string StoragePrompt => Prompt("Storage chest", KeyAction.Interact, "Open");

        // The bed: sleep from dusk on; by day it only says so.
        public static string BedPrompt(bool canSleep) => canSleep ? Prompt("Bed", KeyAction.Interact, "Sleep") : BedByDay;

        private const string BedByDay = "Bed\nSleep at night";

        public static string HerbPrompt(bool fabric) => Prompt(fabric ? "Fern" : "Healing herb", KeyAction.Interact, "Pick");

        public static string SignPrompt => Prompt("Sign", KeyAction.Interact, "Read");

        public static string CryptDoorPrompt => Prompt("Crypt", KeyAction.Interact, "Enter");

        public static string CryptExitPrompt => Prompt("Way out", KeyAction.Interact, "Climb out");

        public static string ChestPrompt => Prompt("Chest", KeyAction.Interact, "Open");

        public static string MineDoorPrompt(string name) => Prompt(name, KeyAction.Interact, "Enter");

        public static string MineExitPrompt => Prompt("Ladder", KeyAction.Interact, "Climb out");

        // A vein: strike when the crack glows. Worked out for today once a find has come out (or cracked).
        public static string VeinPrompt => Prompt("Vein", KeyAction.Interact, "Take out the pick");

        public const string WorkedVeinPrompt = "Vein\nWorked out until dawn";

        public const string EmptyChestPrompt = "Chest\nEmpty";

        public static string ArenaDoorPrompt => Prompt("Archway", KeyAction.Interact, "Descend");

        public static string ArenaExitPrompt => Prompt("Stairs", KeyAction.Interact, "Climb");

        public static string BranchPrompt => Prompt("Branch", KeyAction.Interact, "Pick up");

        public static string WoodPilePrompt => Prompt("Wood pile", KeyAction.Interact, "Take");

        private static int _hutHave = -1, _hutNeed = -1;
        private static string _hutName = string.Empty;

        private static string HutName(int have, int need)
        {
            if (have != _hutHave || need != _hutNeed)
            {
                _hutHave = have;
                _hutNeed = need;
                _hutName = $"Hut  {have}/{need}";
            }
            return _hutName;
        }

        // The broken hut: how much wood it has and needs; with wood in the arms, interact puts it in.
        public static string RepairHutPrompt(int have, int need, int carried)
        {
            return carried > 0 ? Prompt(HutName(have, need), KeyAction.Interact, "Repair") : HutName(have, need);
        }

        // The repaired hut: store carried wood, and repair it.
        public static string HutPrompt(int carried) => carried > 0
            ? Prompt("Hut", KeyAction.Interact, "Store wood", KeyAction.Repair, "Repair")
            : Prompt("Hut", KeyAction.Repair, "Repair");
    }
}
