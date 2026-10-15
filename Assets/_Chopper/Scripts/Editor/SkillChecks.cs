using Chopper.Core;
using UnityEditor;
using UnityEngine;

namespace Chopper.Editor
{
    // Batch: Chopper.Editor.SkillChecks.Gather (W11-57). Proves the gathering skill curves hold the stated limits:
    // a new logger (skill 0) is weaker than a plain swing, a maxed logger with the top axe still needs more than one
    // swing on a large tree, a new miner frees a vein slower than today and a maxed one faster but never in one strike,
    // and the fishing skill eases the fight. Logs "[Skills] RESULT".
    public static class SkillChecks
    {
        private const string GameConfigPath = "Assets/_Chopper/Config/SO_GameConfig.asset";

        public static void Gather()
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            int large = config != null ? config.GetHitsToFell(TreeSize.Large) : 9;
            float[] gears = config != null ? config.AxeDamageMultipliers : new[] { 1f, 1.25f, 1.5f, 2f };
            float topGear = gears != null && gears.Length > 0 ? gears[gears.Length - 1] : 2f;

            float newPower = SkillRules.TreeChopPower(1f, 0);
            int newSwings = ChopRules.SwingsToFell(large, newPower);
            int plainSwings = ChopRules.SwingsToFell(large, 1f);

            float bestPower = SkillRules.TreeChopPower(topGear, SkillRules.MaxSkill);
            int bestSwings = ChopRules.SwingsToFell(large, bestPower);
            int bestPoints = ChopRules.SwingPoints(bestPower);
            int halfTree = ChopRules.TreePoints(large) / 2;

            float cappedPower = SkillRules.TreeChopPower(topGear * 4f, SkillRules.MaxSkill);
            int cappedSwings = ChopRules.SwingsToFell(large, cappedPower);

            bool logOk = newSwings > plainSwings && newSwings > large && bestSwings >= 2 && bestPoints <= halfTree && cappedSwings >= 2;

            int mineLow = MineStrikesToFree(0);
            int mineHigh = MineStrikesToFree(SkillRules.MaxSkill);
            bool mineOk = mineLow > MiningRules.FreeAt && mineHigh > 1 && mineHigh < mineLow;

            FishSpecies pike = FishSpecies.Defaults()[1];
            float tensionLow = FishTension(0, pike);
            float tensionHigh = FishTension(SkillRules.MaxSkill, pike);
            bool fishOk = Mathf.Approximately(SkillRules.FishFightEase(0), 1f) && SkillRules.FishFightEase(SkillRules.MaxSkill) > 1f && tensionHigh < tensionLow;

            bool pass = logOk && mineOk && fishOk;
            Debug.Log($"[Skills] logging: large tree {large} hits ({ChopRules.TreePoints(large)} points), new player {newSwings} swings (power {newPower:F2}) vs plain {plainSwings}; skill 100 + top gear {topGear:F2} -> {bestSwings} swings, {bestPoints} points (half tree {halfTree}); runaway gear capped -> {cappedSwings} swings");
            Debug.Log($"[Skills] mining: skill 0 frees in {mineLow} clean strikes (FreeAt {MiningRules.FreeAt}), skill 100 in {mineHigh}");
            Debug.Log($"[Skills] fishing: ease {SkillRules.FishFightEase(0):F2} to {SkillRules.FishFightEase(SkillRules.MaxSkill):F2}, tension after a steady reel {tensionLow:F2} (skill 0) vs {tensionHigh:F2} (skill 100)");
            Debug.Log("[Skills] RESULT " + (pass ? "PASS" : "FAIL"));
            if (!pass) EditorApplication.Exit(1);
        }

        private static int MineStrikesToFree(int skill)
        {
            float mult = SkillRules.MineProgressMultiplier(skill);
            float progress = 0f;
            for (int strikes = 1; strikes <= 100; strikes++)
            {
                progress += MiningRules.RhythmProgress(StrikeQuality.Clean, false) * mult;
                if (SkillRules.MineFrees(progress, strikes)) return strikes;
            }
            return -1;
        }

        private static float FishTension(int skill, FishSpecies species)
        {
            Fight fight = FishingRules.Begin(FishKind.Pike, species, 10f);
            float ease = SkillRules.FishFightEase(skill);
            for (int i = 0; i < 20; i++) FishingRules.Step(ref fight, species, true, 1, 0f, 0.15f, 0.1f, ease);
            return fight.Tension;
        }
    }
}
