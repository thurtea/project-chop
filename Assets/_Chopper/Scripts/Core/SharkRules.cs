using UnityEngine;

namespace Chopper.Core
{
    public enum SharkPhase { Towing = 0, Reeling = 1, Lost = 2 }

    public enum SharkRodUnlock { Undecided = 0, BossDrop = 1, Merchant = 2, Craft = 3 }

    public struct SharkTuning
    {
        public float CentreBand;
        public float LoseOffset;
        public float StaminaDrain;
        public float StaminaRecover;
        public float DriftSpeed;
        public float SteerSpeed;
        public float TowSpeed;

        public static SharkTuning From(GameConfig config) => config == null ? Default : new SharkTuning
        {
            CentreBand = config.SharkCentreBand,
            LoseOffset = config.SharkLoseOffset,
            StaminaDrain = config.SharkStaminaDrain,
            StaminaRecover = config.SharkStaminaRecover,
            DriftSpeed = config.SharkDriftSpeed,
            SteerSpeed = config.SharkSteerSpeed,
            TowSpeed = config.SharkTowSpeed
        };

        public static SharkTuning Default => new SharkTuning
        {
            CentreBand = 0.25f, LoseOffset = 0.95f, StaminaDrain = 0.12f, StaminaRecover = 0.05f,
            DriftSpeed = 0.9f, SteerSpeed = 1.6f, TowSpeed = 3f
        };
    }

    public struct SharkFight
    {
        public SharkPhase Phase;
        public float Offset;
        public float Stamina;
        public float Drift;
        public float Time;

        public static SharkFight Start() => new SharkFight { Phase = SharkPhase.Towing, Stamina = 1f };
    }

    public static class SharkRules
    {
        public const float LeapGrace = 0.3f;

        public static bool CanHook(bool hasSharkRod, bool inBoatOnOcean) => hasSharkRod && inBoatOnOcean;

        public static float SharkPull(float time, int seed) =>
            Mathf.Sin(time * 1.3f + seed) * 0.7f + Mathf.Sin(time * 2.9f + seed * 1.7f) * 0.3f;

        public static void Step(ref SharkFight fight, float steer, float pull, float dt, SharkTuning tuning)
        {
            if (fight.Phase != SharkPhase.Towing || dt <= 0f) return;
            fight.Time += dt;
            fight.Drift = pull;
            fight.Offset += (pull * tuning.DriftSpeed + Mathf.Clamp(steer, -1f, 1f) * tuning.SteerSpeed) * dt;
            if (Mathf.Abs(fight.Offset) >= tuning.LoseOffset)
            {
                fight.Phase = SharkPhase.Lost;
                return;
            }
            if (ArcRules.Within(fight.Offset, tuning.CentreBand)) fight.Stamina -= tuning.StaminaDrain * dt;
            else fight.Stamina = Mathf.Min(1f, fight.Stamina + tuning.StaminaRecover * dt);
            if (fight.Stamina <= 0f)
            {
                fight.Stamina = 0f;
                fight.Phase = SharkPhase.Reeling;
            }
        }

        public static Vector3 Tow(Vector3 heading, SharkFight fight, SharkTuning tuning, float dt)
        {
            if (fight.Phase != SharkPhase.Towing) return Vector3.zero;
            Vector3 side = Vector3.Cross(Vector3.up, heading.normalized);
            return (heading.normalized + side * fight.Offset * 0.6f) * tuning.TowSpeed * (0.4f + 0.6f * fight.Stamina) * dt;
        }
    }

    public static class BoatRules
    {
        public static bool CanDeploy(bool carriesBoat, bool aimedAtOcean, bool boatAlreadyOut) => carriesBoat && aimedAtOcean && !boatAlreadyOut;

        public static bool CanPickUp(bool boatOut, bool aboard, float distance, float reach) => boatOut && !aboard && distance <= reach;
    }
}