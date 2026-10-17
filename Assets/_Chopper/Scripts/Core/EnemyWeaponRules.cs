using UnityEngine;

namespace Chopper.Core
{
    public enum EnemyWeapon
    {
        Axe = 0,
        Mace = 1,
        Sword = 2,
        Spear = 3
    }

    public struct WeaponProfile
    {
        public EnemyWeapon Weapon;
        public float Windup;
        public float Reach;
        public float Arc;
        public float Damage;
        public float Recover;
        public float SplashRadius;
        public int Take;
        public float LeanBack;
        public float SideTwist;
    }

    public static class EnemyWeaponRules
    {
        public static EnemyWeapon For(Element element)
        {
            switch (element)
            {
                case Element.Earth: return EnemyWeapon.Mace;
                case Element.Fire: return EnemyWeapon.Sword;
                case Element.Water: return EnemyWeapon.Spear;
                default: return EnemyWeapon.Axe;
            }
        }

        public static bool Enchanted(Element element) => element != Element.Neutral;

        public static EnemyWeapon Pick(Element element, int roll) =>
            element == Element.Neutral ? (EnemyWeapon)(((roll % 4) + 4) % 4) : For(element);

        public static string GroupName(EnemyWeapon weapon) => "Weapon0" + weapon;

        public static WeaponProfile Profile(EnemyWeapon weapon)
        {
            switch (weapon)
            {
                case EnemyWeapon.Mace: return new WeaponProfile { Weapon = weapon, Windup = 1.45f, Reach = 0.95f, Arc = 60f, Damage = 1.5f, Recover = 1.35f, SplashRadius = 1.3f, Take = 2, LeanBack = 1.6f, SideTwist = 0f };
                case EnemyWeapon.Sword: return new WeaponProfile { Weapon = weapon, Windup = 1.0f, Reach = 1.1f, Arc = 150f, Damage = 1.0f, Recover = 1.0f, SplashRadius = 0f, Take = 0, LeanBack = 0.8f, SideTwist = 38f };
                case EnemyWeapon.Spear: return new WeaponProfile { Weapon = weapon, Windup = 1.1f, Reach = 1.55f, Arc = 28f, Damage = 1.05f, Recover = 1.0f, SplashRadius = 0f, Take = 1, LeanBack = 1.3f, SideTwist = 0f };
                default: return new WeaponProfile { Weapon = weapon, Windup = 0.7f, Reach = 1.0f, Arc = 80f, Damage = 0.85f, Recover = 0.7f, SplashRadius = 0f, Take = 0, LeanBack = 0.7f, SideTwist = 0f };
            }
        }

        public static WeaponProfile ProfileFor(Element element) => Profile(For(element));

        public static bool Hits(WeaponProfile profile, float baseReach, Vector3 forward, Vector3 toTarget, float slack)
        {
            toTarget.y = 0f;
            forward.y = 0f;
            float distance = toTarget.magnitude;
            if (profile.SplashRadius > 0f && distance <= profile.SplashRadius) return true;
            if (distance > baseReach * profile.Reach * slack) return false;
            if (distance < 0.3f) return true;
            float angle = Vector3.Angle(forward.sqrMagnitude > 1e-4f ? forward : Vector3.forward, toTarget);
            return angle <= profile.Arc * 0.5f + 10f;
        }
    }
}
