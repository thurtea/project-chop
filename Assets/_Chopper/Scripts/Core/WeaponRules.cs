using UnityEngine;

namespace Chopper.Core
{
    public enum PlayerWeapon
    {
        Axe = 0,
        Sword = 1,
        Mace = 2,
        Spear = 3
    }

    public struct WeaponStats
    {
        public float Damage;
        public float RangeScale;
        public float RadiusScale;
        public float CooldownScale;
        public int Targets;
        public float Stagger;
        public float SplashRadius;
        public float SplashDamage;
    }

    public static class WeaponRules
    {
        public const int Count = 4;
        public const int AllForged = (1 << 1) | (1 << 2) | (1 << 3);

        public static WeaponStats Stats(PlayerWeapon weapon)
        {
            switch (weapon)
            {
                case PlayerWeapon.Sword:
                    return new WeaponStats { Damage = 1.1f, RangeScale = 1.1f, RadiusScale = 1.6f, CooldownScale = 1f, Targets = 3, Stagger = 0.8f };
                case PlayerWeapon.Mace:
                    return new WeaponStats { Damage = 1.6f, RangeScale = 0.95f, RadiusScale = 1.1f, CooldownScale = 1.45f, Targets = 1, Stagger = 2f, SplashRadius = 1.8f, SplashDamage = 0.5f };
                case PlayerWeapon.Spear:
                    return new WeaponStats { Damage = 1.15f, RangeScale = 1.55f, RadiusScale = 0.6f, CooldownScale = 0.95f, Targets = 2, Stagger = 0.6f };
                default:
                    return new WeaponStats { Damage = 1f, RangeScale = 1f, RadiusScale = 1f, CooldownScale = 1f, Targets = 1, Stagger = 1f };
            }
        }

        public static SwingTool Tool(PlayerWeapon weapon)
        {
            switch (weapon)
            {
                case PlayerWeapon.Sword: return SwingTool.Sword;
                case PlayerWeapon.Mace: return SwingTool.Mace;
                case PlayerWeapon.Spear: return SwingTool.Spear;
                default: return SwingTool.AxeFight;
            }
        }

        public static string Name(PlayerWeapon weapon)
        {
            switch (weapon)
            {
                case PlayerWeapon.Sword: return "Sword";
                case PlayerWeapon.Mace: return "Mace";
                case PlayerWeapon.Spear: return "Spear";
                default: return "Axe";
            }
        }

        public static bool Owns(int mask, PlayerWeapon weapon) => weapon == PlayerWeapon.Axe || (mask & (1 << (int)weapon)) != 0;

        public static int Grant(int mask, PlayerWeapon weapon) => weapon == PlayerWeapon.Axe ? mask : mask | (1 << (int)weapon);

        public static int Take(int mask, PlayerWeapon weapon) => mask & ~(1 << (int)weapon);

        public static PlayerWeapon Next(int mask, PlayerWeapon current)
        {
            for (int i = 1; i <= Count; i++)
            {
                PlayerWeapon candidate = (PlayerWeapon)(((int)current + i) % Count);
                if (Owns(mask, candidate)) return candidate;
            }
            return PlayerWeapon.Axe;
        }

        public static bool ChopsTrees(PlayerWeapon weapon) => weapon == PlayerWeapon.Axe;

        public static PlayerWeapon FromItem(int kind) => (PlayerWeapon)(kind - ItemRules.Sword + 1);

        public static int ItemOf(PlayerWeapon weapon) => ItemRules.Sword + (int)weapon - 1;

        public static float SplashUnits(PlayerWeapon weapon, float primaryUnits, float distanceFromTarget)
        {
            WeaponStats stats = Stats(weapon);
            if (stats.SplashRadius <= 0f || distanceFromTarget > stats.SplashRadius) return 0f;
            return primaryUnits * stats.SplashDamage;
        }
    }
}
