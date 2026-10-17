using UnityEngine;

namespace Chopper.Core
{
    public enum SwingTool
    {
        Axe,
        Pick,
        Hammer,
        AxeFight,
        Sword,
        Mace,
        Spear
    }

    public struct ToolPoseKey
    {
        public float Time;
        public Vector3 RightUpper;
        public Vector3 RightLower;
        public Vector3 LeftUpper;
        public Vector3 LeftLower;
        public float Bend;
        public float Twist;

        public ToolPoseKey(float time, Vector3 rightUpper, Vector3 rightLower, Vector3 leftUpper, Vector3 leftLower, float bend, float twist)
        {
            Time = time;
            RightUpper = rightUpper.normalized;
            RightLower = rightLower.normalized;
            LeftUpper = leftUpper.normalized;
            LeftLower = leftLower.normalized;
            Bend = bend;
            Twist = twist;
        }
    }

    public static class ToolMotion
    {
        public const float ChopReach = 1.3f;
        public const int AxeVariants = 2;

        private static readonly Vector3 RestRU = new Vector3(0.15f, -0.9f, 0.35f);
        private static readonly Vector3 RestRL = new Vector3(0.1f, -0.5f, 0.85f);
        private static readonly Vector3 RestLU = new Vector3(-0.15f, -0.9f, 0.35f);
        private static readonly Vector3 RestLL = new Vector3(-0.1f, -0.5f, 0.85f);

        private static readonly ToolPoseKey[] AxeOverhead =
        {
            new ToolPoseKey(0f, RestRU, RestRL, RestLU, RestLL, 0f, 0f),
            new ToolPoseKey(0.38f, new Vector3(0.3f, 0.85f, -0.25f), new Vector3(-0.1f, 0.5f, -0.85f), new Vector3(0.05f, 0.8f, -0.1f), new Vector3(0.2f, 0.3f, -0.9f), -12f, 20f),
            new ToolPoseKey(0.5f, new Vector3(0.1f, -0.25f, 1f), new Vector3(0f, -0.45f, 1f), new Vector3(0.05f, -0.35f, 1f), new Vector3(0.15f, -0.4f, 1f), 18f, -8f),
            new ToolPoseKey(0.62f, new Vector3(0.05f, -0.6f, 0.8f), new Vector3(0f, -0.8f, 0.6f), new Vector3(0f, -0.65f, 0.75f), new Vector3(0.1f, -0.8f, 0.6f), 22f, -12f),
            new ToolPoseKey(1f, RestRU, RestRL, RestLU, RestLL, 0f, 0f),
        };

        private static readonly ToolPoseKey[] AxeDiagonal =
        {
            new ToolPoseKey(0f, RestRU, RestRL, RestLU, RestLL, 0f, 0f),
            new ToolPoseKey(0.38f, new Vector3(0.75f, 0.6f, -0.25f), new Vector3(0.4f, 0.4f, -0.8f), new Vector3(0.4f, 0.6f, 0.2f), new Vector3(0.6f, 0.3f, -0.5f), -5f, 35f),
            new ToolPoseKey(0.5f, new Vector3(-0.15f, -0.15f, 1f), new Vector3(-0.35f, -0.3f, 1f), new Vector3(-0.25f, -0.25f, 1f), new Vector3(-0.3f, -0.3f, 1f), 12f, -20f),
            new ToolPoseKey(0.62f, new Vector3(-0.5f, -0.4f, 0.7f), new Vector3(-0.7f, -0.5f, 0.5f), new Vector3(-0.5f, -0.5f, 0.6f), new Vector3(-0.7f, -0.5f, 0.4f), 15f, -35f),
            new ToolPoseKey(1f, RestRU, RestRL, RestLU, RestLL, 0f, 0f),
        };

        private static readonly ToolPoseKey[] PickOverhead =
        {
            new ToolPoseKey(0f, RestRU, RestRL, RestLU, RestLL, 0f, 0f),
            new ToolPoseKey(0.38f, new Vector3(0.25f, 0.9f, -0.2f), new Vector3(0.1f, 0.7f, -0.7f), RestLU, RestLL, -8f, 10f),
            new ToolPoseKey(0.5f, new Vector3(0.1f, -0.1f, 1f), new Vector3(0f, -0.5f, 1f), RestLU, RestLL, 15f, -5f),
            new ToolPoseKey(0.62f, new Vector3(0.05f, -0.5f, 0.85f), new Vector3(0f, -0.85f, 0.5f), RestLU, RestLL, 18f, -6f),
            new ToolPoseKey(1f, RestRU, RestRL, RestLU, RestLL, 0f, 0f),
        };

        private static readonly ToolPoseKey[] HammerTap =
        {
            new ToolPoseKey(0f, RestRU, RestRL, RestLU, RestLL, 0f, 0f),
            new ToolPoseKey(0.3f, new Vector3(0.3f, 0.1f, 0.9f), new Vector3(0.1f, 0.8f, 0.5f), RestLU, RestLL, 0f, 5f),
            new ToolPoseKey(0.45f, new Vector3(0.25f, -0.2f, 0.95f), new Vector3(0.05f, -0.6f, 0.8f), RestLU, RestLL, 6f, 0f),
            new ToolPoseKey(0.6f, new Vector3(0.25f, -0.1f, 0.95f), new Vector3(0.05f, -0.2f, 0.95f), RestLU, RestLL, 3f, 0f),
            new ToolPoseKey(1f, RestRU, RestRL, RestLU, RestLL, 0f, 0f),
        };

        private static readonly ToolPoseKey[] AxeFastChop =
        {
            new ToolPoseKey(0f, RestRU, RestRL, RestLU, RestLL, 0f, 0f),
            new ToolPoseKey(0.3f, new Vector3(0.6f, 0.5f, -0.1f), new Vector3(0.3f, 0.6f, -0.5f), RestLU, RestLL, -4f, 20f),
            new ToolPoseKey(0.42f, new Vector3(0f, -0.1f, 1f), new Vector3(-0.2f, -0.3f, 1f), RestLU, RestLL, 8f, -10f),
            new ToolPoseKey(0.55f, new Vector3(-0.4f, -0.4f, 0.8f), new Vector3(-0.5f, -0.5f, 0.6f), RestLU, RestLL, 10f, -18f),
            new ToolPoseKey(1f, RestRU, RestRL, RestLU, RestLL, 0f, 0f),
        };

        private static readonly ToolPoseKey[] SwordSweep =
        {
            new ToolPoseKey(0f, RestRU, RestRL, RestLU, RestLL, 0f, 0f),
            new ToolPoseKey(0.35f, new Vector3(0.9f, 0.2f, -0.2f), new Vector3(0.7f, 0.1f, -0.6f), RestLU, RestLL, 0f, 40f),
            new ToolPoseKey(0.5f, new Vector3(0f, 0f, 1f), new Vector3(-0.3f, 0f, 1f), RestLU, RestLL, 5f, 0f),
            new ToolPoseKey(0.65f, new Vector3(-0.8f, -0.1f, 0.5f), new Vector3(-0.9f, -0.1f, 0.2f), RestLU, RestLL, 5f, -40f),
            new ToolPoseKey(1f, RestRU, RestRL, RestLU, RestLL, 0f, 0f),
        };

        private static readonly ToolPoseKey[] MaceSmash =
        {
            new ToolPoseKey(0f, RestRU, RestRL, RestLU, RestLL, 0f, 0f),
            new ToolPoseKey(0.45f, new Vector3(0.2f, 1f, -0.4f), new Vector3(0f, 0.6f, -0.9f), new Vector3(-0.05f, 1f, -0.3f), new Vector3(0.15f, 0.5f, -0.9f), -18f, 10f),
            new ToolPoseKey(0.58f, new Vector3(0.05f, -0.5f, 0.9f), new Vector3(0f, -0.8f, 0.6f), new Vector3(0f, -0.55f, 0.85f), new Vector3(0.1f, -0.8f, 0.6f), 28f, -5f),
            new ToolPoseKey(0.72f, new Vector3(0.05f, -0.7f, 0.7f), new Vector3(0f, -0.9f, 0.4f), new Vector3(0f, -0.7f, 0.7f), new Vector3(0.1f, -0.9f, 0.4f), 30f, -5f),
            new ToolPoseKey(1f, RestRU, RestRL, RestLU, RestLL, 0f, 0f),
        };

        private static readonly ToolPoseKey[] SpearThrust =
        {
            new ToolPoseKey(0f, RestRU, RestRL, RestLU, RestLL, 0f, 0f),
            new ToolPoseKey(0.32f, new Vector3(0.3f, -0.3f, -0.6f), new Vector3(0.1f, 0f, 1f), new Vector3(-0.2f, -0.4f, 0.6f), new Vector3(0.1f, 0f, 1f), -5f, 15f),
            new ToolPoseKey(0.45f, new Vector3(0.1f, 0f, 1f), new Vector3(0f, 0.05f, 1f), new Vector3(-0.05f, -0.1f, 1f), new Vector3(0.05f, 0f, 1f), 10f, -10f),
            new ToolPoseKey(0.6f, new Vector3(0.1f, 0f, 1f), new Vector3(0f, 0.05f, 1f), new Vector3(-0.05f, -0.1f, 1f), new Vector3(0.05f, 0f, 1f), 12f, -10f),
            new ToolPoseKey(1f, RestRU, RestRL, RestLU, RestLL, 0f, 0f),
        };

        public static float Contact(SwingTool kind)
        {
            switch (kind)
            {
                case SwingTool.Hammer: return 0.45f;
                case SwingTool.AxeFight: return 0.42f;
                case SwingTool.Mace: return 0.58f;
                case SwingTool.Spear: return 0.45f;
                default: return 0.5f;
            }
        }

        public static bool TwoHanded(SwingTool kind) => kind == SwingTool.Axe || kind == SwingTool.Mace || kind == SwingTool.Spear;

        public static ToolPoseKey[] Keys(SwingTool kind, int variant)
        {
            switch (kind)
            {
                case SwingTool.Pick: return PickOverhead;
                case SwingTool.Hammer: return HammerTap;
                case SwingTool.AxeFight: return AxeFastChop;
                case SwingTool.Sword: return SwordSweep;
                case SwingTool.Mace: return MaceSmash;
                case SwingTool.Spear: return SpearThrust;
                default: return Mathf.Abs(variant) % AxeVariants == 1 ? AxeDiagonal : AxeOverhead;
            }
        }

        public static float Weight(float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            return Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((1f - t) / 0.18f);
        }

        public static ToolPoseKey Sample(SwingTool kind, int variant, float t)
        {
            ToolPoseKey[] keys = Keys(kind, variant);
            t = Mathf.Clamp01(t);
            for (int i = 1; i < keys.Length; i++)
            {
                if (t > keys[i].Time) continue;
                ToolPoseKey a = keys[i - 1];
                ToolPoseKey b = keys[i];
                float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.Time, b.Time, t));
                return new ToolPoseKey(t,
                    Vector3.Slerp(a.RightUpper, b.RightUpper, u),
                    Vector3.Slerp(a.RightLower, b.RightLower, u),
                    Vector3.Slerp(a.LeftUpper, b.LeftUpper, u),
                    Vector3.Slerp(a.LeftLower, b.LeftLower, u),
                    Mathf.Lerp(a.Bend, b.Bend, u),
                    Mathf.Lerp(a.Twist, b.Twist, u));
            }
            return keys[keys.Length - 1];
        }

        public static Vector3 SnapPoint(Vector3 player, Vector3 trunk, float reach)
        {
            Vector3 away = player - trunk;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = Vector3.back;
            Vector3 spot = trunk + away.normalized * reach;
            spot.y = player.y;
            return spot;
        }
    }
}
