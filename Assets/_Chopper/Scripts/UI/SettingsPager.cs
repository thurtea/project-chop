using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Chopper.UI
{
    // Lets the gamepad bumpers (LB and RB) switch the tabs of the settings panel it sits on, and shows the controller
    // hints while a pad is connected. Everything else (stick and D-pad to move, A to select, B to go back) is the
    // normal UI navigation.
    public class SettingsPager : MonoBehaviour
    {
        private SettingsView _view;
        private Text _hint;

        public void Init(SettingsView view, Text hint)
        {
            _view = view;
            _hint = hint;
        }

        private void Update()
        {
            if (_view == null) return;
            Gamepad pad = Gamepad.current;
            if (_hint != null) _hint.text = pad != null ? $"{Keybinds.PadDisplayName("<Gamepad>/leftShoulder")} / {Keybinds.PadDisplayName("<Gamepad>/rightShoulder")}  switch tab      {Keybinds.PadDisplayName("<Gamepad>/buttonSouth")}  select      {Keybinds.PadDisplayName("<Gamepad>/buttonEast")}  back" : string.Empty;
            if (pad == null || SettingsView.RebindActive) return;

            if (pad.leftShoulder.wasPressedThisFrame) _view.StepTab(-1);
            if (pad.rightShoulder.wasPressedThisFrame) _view.StepTab(1);
        }
    }
}
