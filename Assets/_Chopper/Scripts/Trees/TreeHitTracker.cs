using System.Collections.Generic;
using Chopper.Core;

namespace Chopper.Trees
{
    public enum TreeHitResult
    {
        // The tree id is unknown, already felled, or the hit count was not positive.
        Ignored = 0,
        Damaged = 1,
        Felled = 2
    }

    // Server-side chop bookkeeping for standing trees (plan D4, D14, D18). Trees are identified by the stable
    // int id baked into the tree database, so no GameObject per tree is needed. Only damaged or felled trees
    // take memory. A tree holds chop points (its hits to fell from GameConfig, ChopRules); a swing takes some off.
    public class TreeHitTracker
    {
        private readonly GameConfig _config;
        private readonly Dictionary<int, int> _hitsTaken = new Dictionary<int, int>();
        private readonly HashSet<int> _felled = new HashSet<int>();

        public TreeHitTracker(GameConfig config)
        {
            _config = config;
        }

        public int FelledCount => _felled.Count;

        public bool IsFelled(int treeId) => _felled.Contains(treeId);

        // Chop points taken so far.
        public int HitsTaken(int treeId) => _hitsTaken.TryGetValue(treeId, out int hits) ? hits : 0;

        // points is what this swing takes off (ChopRules.SwingPoints; a plain axe hit is ChopRules.PointsPerHit).
        public TreeHitResult ApplyHit(int treeId, TreeSize size, int points)
        {
            if (points <= 0 || _felled.Contains(treeId)) return TreeHitResult.Ignored;

            int total = HitsTaken(treeId) + points;
            if (total >= ChopRules.TreePoints(_config.GetHitsToFell(size)))
            {
                _hitsTaken.Remove(treeId);
                _felled.Add(treeId);
                return TreeHitResult.Felled;
            }

            _hitsTaken[treeId] = total;
            return TreeHitResult.Damaged;
        }

        public void CopyFelledTo(List<int> destination)
        {
            destination.AddRange(_felled);
        }

        // Loading a saved world: the tree is down without anyone chopping it.
        public void MarkFelled(int treeId)
        {
            _hitsTaken.Remove(treeId);
            _felled.Add(treeId);
        }

        // Dawn regrowth (plan D6).
        public void Regrow(int treeId)
        {
            _felled.Remove(treeId);
            _hitsTaken.Remove(treeId);
        }
    }
}
