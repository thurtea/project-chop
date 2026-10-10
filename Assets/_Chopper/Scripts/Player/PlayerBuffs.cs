using System.Collections.Generic;
using Chopper.Core;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Chopper.Player
{
    // Elemental axe charges (plan D20, revised). Killing an elemental enemy loads the axe with that element for
    // ChargesPerKill hits. Each hit on an enemy spends a charge. While charged, hits do bonus damage against the
    // enemies the element beats. Charges are one server owned SyncVar, packed 8 bits per element, and reset at dawn.
    public class PlayerBuffs : NetworkBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly SyncVar<int> _charges = new SyncVar<int>();

        [SerializeField] private GameConfig _config;
        [SerializeField] private ElementPalette _palette;
        [Tooltip("Axe head renderer that shows the element with the most charges left.")]
        [SerializeField] private Renderer _axeHead;

        private static readonly List<PlayerBuffs> _all = new List<PlayerBuffs>();
        private MaterialPropertyBlock _block;
        private PlayerUpgrades _upgrades;

        // Element stones set in the axe: permanent enchantments that never run out.
        private int Stones => _upgrades != null ? _upgrades.Stones : 0;

        private void Awake()
        {
            _upgrades = GetComponent<PlayerUpgrades>();
        }

        public static IReadOnlyList<PlayerBuffs> All => _all;

        public int Charges(Element element) => ElementBuffRules.GetCharges(_charges.Value, element);
        public bool IsActive(Element element) => Charges(element) > 0;
        public int PackedCharges => _charges.Value;

        // The element with the most hits left, or Neutral when the axe is empty.
        public Element StrongestCharged => ElementBuffRules.Strongest(_charges.Value);

        public float DamageMultiplier(Element enemy)
        {
            float counter = ElementBuffRules.DamageMultiplier(_charges.Value, enemy, _config.CounterBonusPerElement, Stones);
            float enchant = CombatRules.EnchantMultiplier(ElementBuffRules.HasAny(_charges.Value) || Stones != 0, _config.EnchantDamageBonus);
            return counter * enchant;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _all.Add(this);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _all.Remove(this);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _charges.OnChange += OnChargesChanged;
            if (_upgrades != null) _upgrades.StonesChanged += RefreshAxeColor;
            RefreshAxeColor();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _charges.OnChange -= OnChargesChanged;
            if (_upgrades != null) _upgrades.StonesChanged -= RefreshAxeColor;
        }

        // Server only. An elemental enemy died to this player's axe.
        public void ServerAwardKill(Element element)
        {
            _charges.Value = ElementBuffRules.LoadCharges(_charges.Value, element, _config.ChargesPerKill, _config.ChargeCapPerElement);
        }

        // Server only. Call once per axe hit on an awake enemy, before the hit can kill it.
        public void ServerConsumeHit(Element enemy)
        {
            if (!ElementBuffRules.HasAny(_charges.Value)) return;
            _charges.Value = ElementBuffRules.ConsumeHit(_charges.Value, enemy, _config.ChargeOnlyOnEffectiveHits);
        }

        // Server only. Called at dawn and on restarts.
        public void ServerReset()
        {
            _charges.Value = 0;
        }

        private void OnChargesChanged(int previous, int next, bool asServer)
        {
            RefreshAxeColor();
        }

        private void RefreshAxeColor()
        {
            if (_axeHead == null || _palette == null) return;

            _block ??= new MaterialPropertyBlock();
            Element strongest = ElementBuffRules.Strongest(_charges.Value);
            // Without charges, a stone set in the axe gives it its color for good.
            if (strongest == Element.Neutral) strongest = ElementBuffRules.FirstStone(Stones);
            // One material per part. An empty axe keeps their own base colors; a charged or stoned one tints only the
            // metal head with the element color, the handle stays wood. The head is the material named Metal (the
            // textured axe), or else a grey one. Setting white on the whole renderer, as before, painted the axe white.
            Material[] parts = _axeHead.sharedMaterials;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null || !parts[i].HasProperty(BaseColorId)) continue;
                Color own = parts[i].GetColor(BaseColorId);
                bool named = parts[i].name.Contains("Metal") || parts[i].name.Contains("Wood");
                bool metal = named ? parts[i].name.Contains("Metal")
                    : Mathf.Max(own.r, Mathf.Max(own.g, own.b)) - Mathf.Min(own.r, Mathf.Min(own.g, own.b)) < 0.1f;
                _block.Clear();
                _block.SetColor(BaseColorId, strongest != Element.Neutral && metal ? _palette.GetBaseColor(strongest) : own);
                _axeHead.SetPropertyBlock(_block, i);
            }
        }
    }
}
