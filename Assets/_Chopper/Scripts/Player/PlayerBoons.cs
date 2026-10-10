using Chopper.Audio;
using Chopper.Core;
using Chopper.UI;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Chopper.Player
{
    // Permanent upgrades won from bosses: a better axe, sturdier outfit, longer burning torch, stronger salve or
    // more stamina. Levels are packed into one synced int (see BoonRules). They last for the whole session.
    public class PlayerBoons : NetworkBehaviour
    {
        private readonly SyncVar<int> _packed = new SyncVar<int>();

        [SerializeField] private GameConfig _config;

        public int Packed => _packed.Value;

        // Raised on every client whenever the levels change (stamina reads it to resize its bar).
        public event System.Action Changed;

        public int Level(BoonKind kind) => BoonRules.Level(_packed.Value, kind);

        public float AxeDamageBonus => Level(BoonKind.Axe) * _config.BoonAxeDamage;
        public float ArmorBonus => Level(BoonKind.Armor) * _config.BoonArmor;
        public float TorchSecondsBonus => Level(BoonKind.Torch) * _config.BoonTorchSeconds;
        public int SalveHealBonus => Level(BoonKind.Salve) * _config.BoonSalveHeal;
        public float StaminaBonus => Level(BoonKind.Stamina) * _config.BoonStamina;

        public override void OnStartClient()
        {
            base.OnStartClient();
            _packed.OnChange += OnPackedChanged;
            Changed?.Invoke();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _packed.OnChange -= OnPackedChanged;
        }

        private void OnPackedChanged(int previous, int next, bool asServer)
        {
            // Once per change, with trustworthy arguments: a host's client side callback sees previous equal to next,
            // so the host reacts to the server side call and everyone else to the client side one (as PlayerWood).
            if (asServer != IsServerInitialized) return;
            Changed?.Invoke();
            if (next == previous || !IsOwner) return;
            Sfx.Play(ProceduralSfx.Upgrade, transform.position + Vector3.up, 1f);
        }

        // Server only. Loading a saved player.
        public void ServerLoad(int packed) => _packed.Value = Mathf.Max(0, packed);

        // Server only. Adds one level of the boon (up to the cap).
        public void ServerGrant(BoonKind kind)
        {
            _packed.Value = BoonRules.Grant(_packed.Value, kind);
        }

        // One line per boon the player has, for the inventory screen.
        public string Describe()
        {
            var text = new System.Text.StringBuilder();
            foreach (BoonKind kind in System.Enum.GetValues(typeof(BoonKind)))
            {
                int level = Level(kind);
                if (level > 0) text.AppendLine($"{BoonRules.Name(kind)}  level {level}");
            }
            return text.ToString();
        }
    }
}
