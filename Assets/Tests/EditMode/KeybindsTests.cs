using System.Collections.Generic;
using Chopper.UI;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    // The rebinding rules for keyboard, mouse and gamepad: defaults are unique, a clash swaps the two actions, a reset
    // restores the defaults, and the keyboard and pad sets do not disturb each other.
    public class KeybindsTests
    {
        [SetUp]
        public void ResetBindings()
        {
            Keybinds.ResetAll();
            Keybinds.ResetAllPad();
        }

        [TearDown]
        public void RestoreDefaults()
        {
            Keybinds.ResetAll();
            Keybinds.ResetAllPad();
        }

        [Test]
        public void KeyboardDefaultsAreUnique()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < Keybinds.Count; i++) Assert.IsTrue(seen.Add(Keybinds.DefaultPath((KeyAction)i)), "duplicate keyboard default");
        }

        [Test]
        public void PadDefaultsAreUniqueAndUseTheGamepadLayout()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < Keybinds.Count; i++)
            {
                var action = (KeyAction)i;
                string path = Keybinds.DefaultPadPath(action);
                Assert.IsTrue(path.StartsWith("<Gamepad>/"), path);
                if (PadHotbar.OnHotbar(action)) continue;
                Assert.IsTrue(seen.Add(path), "duplicate pad default " + path);
                Assert.IsFalse(path.StartsWith("<Gamepad>/dpad/"), "the D-pad is the hotbar, not " + action);
                Assert.IsFalse(path.Contains("+"), "no button chords: " + action);
            }
        }

        [Test]
        public void TheSevenHotbarItemsUseThePadHotbarAndTheNumberKeys()
        {
            KeyAction[] order = { KeyAction.Axe, KeyAction.Torch, KeyAction.Potion, KeyAction.Rod, KeyAction.Food, KeyAction.Build, KeyAction.Pick };
            Assert.AreEqual(order.Length, PadHotbar.Slots);
            for (int slot = 0; slot < order.Length; slot++)
            {
                Assert.AreEqual(order[slot], PadHotbar.ActionAt(slot));
                Assert.AreEqual(Keybinds.HotbarPath, Keybinds.DefaultPadPath(order[slot]));
                Assert.AreEqual("<Keyboard>/" + (slot + 1), Keybinds.DefaultPath(order[slot]));
            }
            Assert.AreEqual("Hotbar", Keybinds.PadNameOf(KeyAction.Build));
        }

        [Test]
        public void TheHotbarCursorWrapsAtBothEnds()
        {
            Assert.AreEqual(1, PadHotbar.Step(0, 1));
            Assert.AreEqual(PadHotbar.Slots - 1, PadHotbar.Step(0, -1));
            Assert.AreEqual(0, PadHotbar.Step(PadHotbar.Slots - 1, 1));
        }

        [Test]
        public void OldSavedItemButtonsAndChordsFallBackToTheHotbar()
        {
            PlayerPrefs.SetString("Chopper.Pad.Food", "<Gamepad>/leftShoulder");
            PlayerPrefs.SetString("Chopper.Pad.Build", "<Gamepad>/leftShoulder+dpad/right");
            PlayerPrefs.SetString("Chopper.Pad.Map", "<Gamepad>/dpad/left");
            Keybinds.ClearCache();
            Assert.AreEqual(Keybinds.HotbarPath, Keybinds.PadPathOf(KeyAction.Food));
            Assert.AreEqual(Keybinds.HotbarPath, Keybinds.PadPathOf(KeyAction.Build));
            Assert.AreEqual(Keybinds.DefaultPadPath(KeyAction.Map), Keybinds.PadPathOf(KeyAction.Map));
        }

        [Test]
        public void PadDefaultsFollowTheValheimLayout()
        {
            Assert.AreEqual("RT", Keybinds.PadNameOf(KeyAction.Attack));
            Assert.AreEqual("LT", Keybinds.PadNameOf(KeyAction.Block));
            Assert.AreEqual("A", Keybinds.PadNameOf(KeyAction.Jump));
            Assert.AreEqual("B", Keybinds.PadNameOf(KeyAction.Roll));
            Assert.AreEqual("X", Keybinds.PadNameOf(KeyAction.Interact));
            Assert.AreEqual("Y", Keybinds.PadNameOf(KeyAction.Inventory));
            Assert.AreEqual("L3", Keybinds.PadNameOf(KeyAction.Sprint));
            Assert.AreEqual("LB", Keybinds.PadNameOf(KeyAction.Crouch));
            Assert.AreEqual("R3", Keybinds.PadNameOf(KeyAction.Unequip));
            Assert.AreEqual("<Keyboard>/h", Keybinds.DefaultPath(KeyAction.Unequip));
        }

        [Test]
        public void AnOldSavedCrouchOnR3MovesToLbOnce()
        {
            PlayerPrefs.DeleteKey("Chopper.PadCrouchOffR3");
            PlayerPrefs.SetString("Chopper.Pad.Crouch", "<Gamepad>/rightStickPress");
            Keybinds.ClearCache();
            Assert.AreEqual("<Gamepad>/leftShoulder", Keybinds.PadPathOf(KeyAction.Crouch));
            // Chosen again after the move, R3 stays.
            Keybinds.SetPad(KeyAction.Crouch, "<Gamepad>/rightStickPress");
            Keybinds.ClearCache();
            Assert.AreEqual("<Gamepad>/rightStickPress", Keybinds.PadPathOf(KeyAction.Crouch));
        }

        [Test]
        public void BindingAKeyAlreadyInUseSwapsTheTwoActions()
        {
            string attack = Keybinds.PathOf(KeyAction.Attack);
            string jump = Keybinds.PathOf(KeyAction.Jump);
            Keybinds.Set(KeyAction.Attack, jump);
            Assert.AreEqual(jump, Keybinds.PathOf(KeyAction.Attack));
            Assert.AreEqual(attack, Keybinds.PathOf(KeyAction.Jump));
        }

        [Test]
        public void BindingAPadButtonAlreadyInUseSwapsTheTwoActions()
        {
            string roll = Keybinds.PadPathOf(KeyAction.Roll);
            string jump = Keybinds.PadPathOf(KeyAction.Jump);
            Keybinds.SetPad(KeyAction.Roll, jump);
            Assert.AreEqual(jump, Keybinds.PadPathOf(KeyAction.Roll));
            Assert.AreEqual(roll, Keybinds.PadPathOf(KeyAction.Jump));
        }

        [Test]
        public void ResettingOneDeviceLeavesTheOtherAlone()
        {
            Keybinds.Set(KeyAction.Interact, "<Keyboard>/f");
            Keybinds.SetPad(KeyAction.Interact, "<Gamepad>/buttonNorth");
            Keybinds.ResetAllPad();
            Assert.AreEqual("<Keyboard>/f", Keybinds.PathOf(KeyAction.Interact));
            Assert.AreEqual(Keybinds.DefaultPadPath(KeyAction.Interact), Keybinds.PadPathOf(KeyAction.Interact));
        }

        [Test]
        public void NoActionIsEverLeftUnboundAfterRebinding()
        {
            Keybinds.Set(KeyAction.Map, "<Keyboard>/g");
            Keybinds.SetPad(KeyAction.Map, "<Gamepad>/rightShoulder");
            for (int i = 0; i < Keybinds.Count; i++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(Keybinds.PathOf((KeyAction)i)));
                Assert.IsFalse(string.IsNullOrEmpty(Keybinds.PadPathOf((KeyAction)i)));
            }
            // The repair action had RB, so it took the map's old Back button.
            Assert.AreEqual("<Gamepad>/select", Keybinds.PadPathOf(KeyAction.Repair));
        }

        [Test]
        public void PadNamesAreXboxStyleAndShortForTheDpad()
        {
            Assert.AreEqual("D-pad Left", Keybinds.PadDisplayName("<Gamepad>/dpad/left"));
            Assert.AreEqual("Back", Keybinds.PadDisplayName("<Gamepad>/select"));
            Assert.AreEqual("LB", Keybinds.PadDisplayName("<Gamepad>/leftShoulder"));
        }

        [Test]
        public void GamepadSettingsHaveSensibleDefaults()
        {
            PlayerPrefs.DeleteKey("Chopper.PadSensitivity");
            PlayerPrefs.DeleteKey("Chopper.InvertPadY");
            Assert.AreEqual(UserSettings.DefaultSensitivity, UserSettings.PadSensitivity, 0.0001f);
            Assert.IsFalse(UserSettings.InvertPadY);
            UserSettings.PadSensitivity = 99f;
            Assert.AreEqual(UserSettings.MaxSensitivity, UserSettings.PadSensitivity, 0.0001f);
            PlayerPrefs.DeleteKey("Chopper.PadSensitivity");
        }
    }
}
