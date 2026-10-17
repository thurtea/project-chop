using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Chopper.Editor
{
    public static class WeaponAnimSetup
    {
        public const string ControllerPath = "Assets/_Chopper/Prefabs/Player/PlayerAnimator.controller";


        public static void Apply()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) { Debug.LogError("[WeaponAnim] RESULT FAIL no controller"); EditorApplication.Exit(1); return; }
            AnimatorControllerLayer layer = controller.layers.FirstOrDefault(l => l.name == "Chop");
            if (layer == null) { Debug.LogError("[WeaponAnim] RESULT FAIL no Chop layer"); EditorApplication.Exit(1); return; }
            AnimatorStateMachine machine = layer.stateMachine;
            AnimatorState empty = Find(machine, "Empty");
            AnimatorState chop = Find(machine, "Chop");
            var strikes = new List<(AnimatorState state, string trigger)>();
            foreach (string name in new[] { "Punch", "Punch2", "Chop", "Mine", "HitReact", "Parry" })
            {
                AnimatorState s = Find(machine, name);
                if (s != null) strikes.Add((s, name == "HitReact" ? "Hit" : name));
            }
            int found = 0;
            var lengths = new List<string>();
            var moveStates = new List<(AnimatorState state, string trigger)>();
            foreach (var move in Chopper.Core.WeaponRules.MoveClips)
            {
                AnimationClip clip = KayKitImport.Clip(KayKitImport.MeleePath, move.clip);
                if (clip != null) found++;
                lengths.Add($"{move.state}={move.clip} {(clip != null ? clip.length.ToString("F3") : "MISSING")}s");
                if (!controller.parameters.Any(p => p.name == move.state)) controller.AddParameter(move.state, AnimatorControllerParameterType.Trigger);
                AnimatorState state = Find(machine, move.state);
                bool fresh = state == null;
                if (fresh) state = machine.AddState(move.state);
                if (clip != null) state.motion = clip;
                state.speedParameterActive = true;
                state.speedParameter = "SwingSpeed";
                state.writeDefaultValues = false;
                moveStates.Add((state, move.state));
            }
            foreach (var to in moveStates)
            {
                foreach (AnimatorStateTransition t in to.state.transitions.ToArray()) to.state.RemoveTransition(t);
            }
            foreach (var to in moveStates)
            {
                RemoveInto(empty, to.state);
                foreach (var s in strikes) RemoveInto(s.state, to.state);
                foreach (var from in moveStates) Link(from.state, to.state, to.trigger);
                foreach (var s in strikes) Link(s.state, to.state, to.trigger);
                Link(empty, to.state, to.trigger);
                AnimatorStateTransition leave = to.state.AddTransition(empty);
                leave.hasExitTime = true;
                leave.exitTime = 0.92f;
                leave.duration = 0.1f;
            }
            foreach (var to in strikes)
                foreach (var from in moveStates)
                    Link(from.state, to.state, to.trigger);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            bool chopDistinct = chop != null && chop.motion != null && moveStates.All(m => m.state.motion != chop.motion);
            int distinct = moveStates.Select(m => m.state.motion).Where(m => m != null).Distinct().Count();
            bool pass = found == Chopper.Core.WeaponRules.MoveClips.Length && distinct == Chopper.Core.WeaponRules.MoveClips.Length && chopDistinct;
            Debug.Log($"[WeaponAnim] {string.Join(", ", lengths)}; tree chop clip {(chop != null && chop.motion != null ? chop.motion.name : "none")}");
            Debug.Log($"[WeaponAnim] RESULT {(pass ? "PASS" : "FAIL")}: {found}/{Chopper.Core.WeaponRules.MoveClips.Length} clips found, {distinct} distinct, tree chop distinct {chopDistinct}");
            if (!pass) EditorApplication.Exit(1);
        }

        private static AnimatorState Find(AnimatorStateMachine machine, string name)
        {
            foreach (ChildAnimatorState child in machine.states) if (child.state.name == name) return child.state;
            return null;
        }

        private static void RemoveInto(AnimatorState from, AnimatorState to)
        {
            if (from == null) return;
            foreach (AnimatorStateTransition t in from.transitions.ToArray()) if (t.destinationState == to) from.RemoveTransition(t);
        }

        private static void Link(AnimatorState from, AnimatorState to, string trigger)
        {
            if (from == null || to == null) return;
            foreach (AnimatorStateTransition t in from.transitions)
                if (t.destinationState == to && t.conditions.Any(c => c.parameter == trigger)) return;
            AnimatorStateTransition link = from.AddTransition(to);
            link.hasExitTime = false;
            link.duration = 0.05f;
            link.canTransitionToSelf = true;
            link.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }
    }
}
