using UnityEngine.InputSystem;

namespace Chopper.UI
{
    // The hotbar on a pad, as in other survival games: D-pad left and right move a cursor along the hotbar (wrapping
    // at the ends), and D-pad up uses the slot under it: it takes the item out, or puts it away if it is already out.
    // D-pad down steps the camera out. No chords and no radial menu. The keyboard keeps its number keys.
    public static class PadHotbar
    {
        // The hotbar slots in order, left to right, as GameUi draws them.
        private static readonly KeyAction[] Actions = { KeyAction.Axe, KeyAction.Torch, KeyAction.Potion, KeyAction.Rod, KeyAction.Food, KeyAction.Build };

        public static int Slots => Actions.Length;

        // The slot under the cursor.
        public static int Selected
        {
            get
            {
                Tick();
                return _selected;
            }
        }

        private static int _selected;

        private static int _frame = -1;
        private static int _usedSlot = -1;
        private static bool _zoom;

        public static KeyAction ActionAt(int slot) => Actions[slot];

        public static bool OnHotbar(KeyAction action) => System.Array.IndexOf(Actions, action) >= 0;

        // The slot one step left (-1) or right (+1), wrapping around at either end.
        public static int Step(int slot, int direction) => ((slot + direction) % Actions.Length + Actions.Length) % Actions.Length;

        // True on the frame D-pad up uses the slot holding this action.
        public static bool Used(KeyAction action)
        {
            Tick();
            return _usedSlot >= 0 && Actions[_usedSlot] == action;
        }

        public static bool ZoomPressed
        {
            get
            {
                Tick();
                return _zoom;
            }
        }

        // Once a frame. Every connected pad counts (a Steam or DS4Windows virtual pad can sit beside the real one).
        // While a menu is open the D-pad belongs to the menu, so the cursor stays put.
        private static void Tick()
        {
            if (_frame == UnityEngine.Time.frameCount) return;
            _frame = UnityEngine.Time.frameCount;
            _usedSlot = -1;
            _zoom = false;
            if (UiState.GameplayBlocked) return;
            bool left = false, right = false, up = false, down = false;
            foreach (Gamepad pad in Gamepad.all)
            {
                left |= pad.dpad.left.wasPressedThisFrame;
                right |= pad.dpad.right.wasPressedThisFrame;
                up |= pad.dpad.up.wasPressedThisFrame;
                down |= pad.dpad.down.wasPressedThisFrame;
            }
            if (left && !right) _selected = Step(_selected, -1);
            if (right && !left) _selected = Step(_selected, 1);
            if (up) _usedSlot = _selected;
            _zoom = down;
        }
    }
}
