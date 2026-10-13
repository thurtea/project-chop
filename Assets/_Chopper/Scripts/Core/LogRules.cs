using UnityEngine;

namespace Chopper.Core
{
    // What is left of a felled tree (W11-09). Felling pays a third of the tree's wood; the rest, plus a bonus, comes
    // from chopping up the log it leaves on the ground. The stump regrows into a tree at dawn unless it is cleared;
    // a cleared spot stays bare for ClearedRegrowDawns dawns.
    public static class LogRules
    {
        public const int StumpHits = 3;
        public const int StumpWood = 1;
        public const int ClearedRegrowDawns = 5;

        public static int FellWood(int treeWood) => Mathf.Max(0, treeWood) / 3;

        public static int LogWood(int treeWood, TreeSize size) => Mathf.Max(0, treeWood) - FellWood(treeWood) + (int)size + 1;

        public static int LogHits(TreeSize size) => 2 + (int)size;

        public static int LogPoints(TreeSize size) => ChopRules.TreePoints(LogHits(size));

        public static int StumpPoints => ChopRules.TreePoints(StumpHits);

        // Wood paid for chopping a log from takenBefore to takenAfter points; over a whole log it adds up to logWood.
        public static int WoodBetween(int logWood, int totalPoints, int takenBefore, int takenAfter)
        {
            if (totalPoints <= 0) return 0;
            int before = Mathf.Clamp(takenBefore, 0, totalPoints);
            int after = Mathf.Clamp(takenAfter, 0, totalPoints);
            return logWood * after / totalPoints - logWood * before / totalPoints;
        }

        // Dawns left for a cleared spot after one more dawn; it regrows when this reaches zero.
        public static int AfterDawn(int dawnsLeft) => Mathf.Max(0, dawnsLeft - 1);
    }
}
