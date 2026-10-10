using Chopper.Core;
using FishNet.Object;
using UnityEngine;

namespace Chopper.Player
{
    // Owner side stamina (see StaminaModel). Sprinting drains it, swinging the axe and jumping spend it, and it
    // refills after a short pause. Boons and a meal of fish (PlayerFishing.FedBonus) raise the maximum. It is local to the owning player: co-op, so no server check is needed.
    public class PlayerStamina : NetworkBehaviour
    {
        [SerializeField] private GameConfig _config;

        private StaminaModel _model;
        private PlayerBoons _boons;
        private PlayerCombat _combat;
        private PlayerMotor _motor;
        private PlayerFishing _fishing;
        private int _fedShown;

        public static PlayerStamina Local { get; private set; }

        public float Current => _model != null ? _model.Current : 0f;
        public float Max => _model != null ? _model.Max : 1f;
        public float Fraction => _model != null ? _model.Fraction : 0f;
        public bool Exhausted => _model != null && _model.Exhausted;
        public bool CanSprint => _model != null && _model.CanSprint;

        private void Awake()
        {
            _model = new StaminaModel(_config.StaminaMax, _config.StaminaRegenPerSecond, _config.StaminaRegenDelay,
                _config.StaminaExhaustedRecoverAt);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner) Local = this;
            _model.Refill();
            _boons = GetComponent<PlayerBoons>();
            if (_boons != null) _boons.Changed += RefreshMax;
            RefreshMax();
        }

        // A stamina boon raises the maximum, and so does a meal of fish while it lasts.
        private void RefreshMax()
        {
            _fedShown = _fishing != null ? _fishing.FedBonus : 0;
            _model.SetMax(_config.StaminaMax + (_boons != null ? _boons.StaminaBonus : 0f) + _fedShown);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (_boons != null) _boons.Changed -= RefreshMax;
            if (Local == this) Local = null;
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (_combat == null) _combat = GetComponent<PlayerCombat>();
            if (_combat != null && _combat.God) _model.Refill();
            if (_motor == null) _motor = GetComponent<PlayerMotor>();
            if (_fishing == null) _fishing = GetComponent<PlayerFishing>();
            if (_fishing != null && _fishing.FedBonus != _fedShown)
            {
                bool gained = _fishing.FedBonus > _fedShown;
                RefreshMax();
                // A meal tops stamina up as well.
                if (gained) _model.Refill();
            }
            _model.Tick(Time.deltaTime, _motor != null && _motor.IsWet ? WetRules.StaminaRegenMultiplier : 1f);
        }

        public bool TryUse(float amount) => _model.TryUse(amount);

        public void Drain(float amount) => _model.Drain(amount);

        public void Refill() => _model.Refill();
    }
}
