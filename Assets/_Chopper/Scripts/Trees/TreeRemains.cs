using System.Collections.Generic;
using Chopper.Core;

namespace Chopper.Trees
{
    public enum RemainsHit
    {
        Ignored = 0,
        Damaged = 1,
        Finished = 2
    }

    // Server-side bookkeeping for what a felled tree leaves (W11-09): the log on the ground, chop points on the stump,
    // and cleared stumps with the dawns left before the spot regrows. Keyed by tree id like TreeHitTracker.
    public class TreeRemains
    {
        public struct Log
        {
            public float Yaw;
            public int Taken;
        }

        private readonly Dictionary<int, Log> _logs = new Dictionary<int, Log>();
        private readonly Dictionary<int, int> _stumpTaken = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _cleared = new Dictionary<int, int>();

        public IReadOnlyDictionary<int, Log> Logs => _logs;
        public IReadOnlyDictionary<int, int> Cleared => _cleared;

        public bool HasLog(int treeId) => _logs.ContainsKey(treeId);
        public bool IsCleared(int treeId) => _cleared.ContainsKey(treeId);

        public bool TryGetLogYaw(int treeId, out float yaw)
        {
            bool found = _logs.TryGetValue(treeId, out Log log);
            yaw = log.Yaw;
            return found;
        }

        public void AddLog(int treeId, float yaw, int taken = 0)
        {
            _logs[treeId] = new Log { Yaw = yaw, Taken = taken < 0 ? 0 : taken };
        }

        public RemainsHit HitLog(int treeId, TreeSize size, int points, out int takenBefore, out int takenAfter)
        {
            takenBefore = takenAfter = 0;
            if (points <= 0 || !_logs.TryGetValue(treeId, out Log log)) return RemainsHit.Ignored;
            int total = LogRules.LogPoints(size);
            takenBefore = log.Taken;
            takenAfter = System.Math.Min(total, log.Taken + points);
            if (takenAfter >= total)
            {
                _logs.Remove(treeId);
                return RemainsHit.Finished;
            }
            log.Taken = takenAfter;
            _logs[treeId] = log;
            return RemainsHit.Damaged;
        }

        public RemainsHit HitStump(int treeId, int points)
        {
            if (points <= 0 || _cleared.ContainsKey(treeId)) return RemainsHit.Ignored;
            int total = (_stumpTaken.TryGetValue(treeId, out int taken) ? taken : 0) + points;
            if (total >= LogRules.StumpPoints)
            {
                _stumpTaken.Remove(treeId);
                _cleared[treeId] = LogRules.ClearedRegrowDawns;
                return RemainsHit.Finished;
            }
            _stumpTaken[treeId] = total;
            return RemainsHit.Damaged;
        }

        public void MarkCleared(int treeId, int dawnsLeft)
        {
            _stumpTaken.Remove(treeId);
            _cleared[treeId] = dawnsLeft < 1 ? 1 : dawnsLeft;
        }

        // Dawn: every felled tree with a stump regrows; a cleared spot counts down and regrows when its wait is over.
        // The regrowing ids are added to regrow and their remains forgotten.
        public void Dawn(List<int> felled, List<int> regrow)
        {
            foreach (int id in felled)
            {
                if (_cleared.TryGetValue(id, out int left))
                {
                    left = LogRules.AfterDawn(left);
                    if (left > 0)
                    {
                        _cleared[id] = left;
                        continue;
                    }
                }
                regrow.Add(id);
            }
            foreach (int id in regrow) Forget(id);
        }

        public void Forget(int treeId)
        {
            _logs.Remove(treeId);
            _stumpTaken.Remove(treeId);
            _cleared.Remove(treeId);
        }

        public void Clear()
        {
            _logs.Clear();
            _stumpTaken.Clear();
            _cleared.Clear();
        }

        // One felled tree packed into the save's id list (WorldSaver stores that list as-is). A plain id is a stump
        // with no log. A negative value also carries the log's yaw and, when the stump was cleared, the dawns left.
        public static int Pack(int treeId, bool hasLog, float yawDegrees, bool cleared, int dawnsLeft)
        {
            int yaw = 0;
            if (hasLog)
            {
                float turns = yawDegrees / 360f;
                turns -= (float)System.Math.Floor(turns);
                yaw = (int)(turns * 512f) & 511;
            }
            int n = treeId & 0x3FFF;
            n |= yaw << 14;
            if (hasLog) n |= 1 << 23;
            if (cleared) n |= 1 << 24;
            n |= (dawnsLeft & 0xF) << 25;
            return -n - 1;
        }

        public static bool TryUnpack(int packed, out int treeId, out bool hasLog, out float yawDegrees, out bool cleared, out int dawnsLeft)
        {
            treeId = 0;
            hasLog = false;
            yawDegrees = 0f;
            cleared = false;
            dawnsLeft = 0;
            if (packed >= 0) return false;
            int n = -packed - 1;
            treeId = n & 0x3FFF;
            hasLog = (n & (1 << 23)) != 0;
            cleared = (n & (1 << 24)) != 0;
            dawnsLeft = (n >> 25) & 0xF;
            yawDegrees = ((n >> 14) & 511) * (360f / 512f);
            return true;
        }
    }
}
