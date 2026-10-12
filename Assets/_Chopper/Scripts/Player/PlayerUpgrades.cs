using Chopper.Audio;
using Chopper.Core;
using Chopper.World;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Chopper.Player
{
    // Axe and armor levels, bought with the hut's wood store (shared by everyone), plus the crypt upgrades bought with
    // the player's own crypt materials: Sturdy gear (iron, lets you live through a falling tree) and the Rune axe
    // (shards, more damage). All are bought at a workbench, where worn tools are repaired too. Costs and effects come
    // from GameConfig tables.
    public class PlayerUpgrades : NetworkBehaviour
    {
        public const int KindAxe = 1;
        public const int KindArmor = 2;
        public const int KindHut = 3;
        public const int KindSturdy = 4;
        public const int KindRune = 5;
        private const float MaxArmorReduction = 0.85f;
        // The armoured outfit (user, 2026-10-12): everyone starts unarmoured. Each boss tree felled gives every player an
        // armour kit; using one puts on the helmet, chainmail, bracers and shield for good, and takes this much more off
        // every blow (on top of the armor level, within MaxArmorReduction).
        public const int MaxArmourKits = 9;
        public const float ArmouredReduction = 0.10f;

        private readonly SyncVar<int> _axeLevel = new SyncVar<int>();
        private readonly SyncVar<int> _armorLevel = new SyncVar<int>();
        private readonly SyncVar<int> _sturdyLevel = new SyncVar<int>();
        private readonly SyncVar<int> _runeLevel = new SyncVar<int>();
        // Elemental wards (outfit upgrades from arena essence): a level per element, packed (EssenceRules).
        private readonly SyncVar<int> _wards = new SyncVar<int>();
        // Element stones set in the axe (permanent enchantments, ElementBuffRules): one bit per element.
        private readonly SyncVar<int> _stones = new SyncVar<int>();
        private readonly SyncVar<int> _armourKits = new SyncVar<int>();
        private readonly SyncVar<bool> _armoured = new SyncVar<bool>();

        // Raised on every client when this player puts the armour on (or a save loads it).
        public event System.Action OutfitChanged;

        // Raised on every client when a stone is set in this player's axe (the axe takes its color).
        public event System.Action StonesChanged;

        [SerializeField] private GameConfig _config;

        private PlayerBoons _boons;
        private PlayerInventory _inventory;

        private void Awake()
        {
            _boons = GetComponent<PlayerBoons>();
            _inventory = GetComponent<PlayerInventory>();
        }

        public int AxeLevel => _axeLevel.Value;
        public int ArmorLevel => _armorLevel.Value;
        public int SturdyLevel => _sturdyLevel.Value;
        public int RuneLevel => _runeLevel.Value;
        public int WardsPacked => _wards.Value;
        public int Stones => _stones.Value;
        public int ArmourKits => _armourKits.Value;
        public bool Armoured => _armoured.Value;
        public bool HasStone(Element element) => ElementBuffRules.HasStone(_stones.Value, element);
        public int WardLevel(Element element) => EssenceRules.Get(_wards.Value, element);
        // Share of damage from enemies of this element that the player's ward takes away.
        public float WardReduction(Element element) => element == Element.Neutral ? 0f : DropRules.Lookup(_config.WardReduction, WardLevel(element));
        public bool NextWardCost(Element element, out int cost) => DropRules.TryGetNextCost(_config.WardEssenceCosts, WardLevel(element), out cost);
        public float RuneBonus => _runeLevel.Value > 0 ? DropRules.Lookup(_config.RuneDamageBonus, _runeLevel.Value) : 0f;
        public float SwingCooldownMultiplier => DropRules.Lookup(_config.AxeCooldownMultipliers, _axeLevel.Value);
        public float AxeDamageMultiplier => DropRules.Lookup(_config.AxeDamageMultipliers, _axeLevel.Value)
                                            * (1f + (_boons != null ? _boons.AxeDamageBonus : 0f)) * (1f + RuneBonus);
        public float ArmorReduction => Mathf.Min(MaxArmorReduction, DropRules.Lookup(_config.ArmorDamageReduction, _armorLevel.Value) + (_boons != null ? _boons.ArmorBonus : 0f)
                                                                     + (_armoured.Value ? ArmouredReduction : 0f));

        public bool NextAxeCost(out int cost) => DropRules.TryGetNextCost(_config.AxeUpgradeCosts, _axeLevel.Value, out cost);
        public bool NextArmorCost(out int cost) => DropRules.TryGetNextCost(_config.ArmorUpgradeCosts, _armorLevel.Value, out cost);
        public bool NextSturdyCost(out int cost) => DropRules.TryGetNextCost(_config.SturdyIronCosts, _sturdyLevel.Value, out cost);
        public bool NextRuneCost(out int cost) => DropRules.TryGetNextCost(_config.RuneShardCosts, _runeLevel.Value, out cost);

        public override void OnStartClient()
        {
            base.OnStartClient();
            _axeLevel.OnChange += OnLevelChanged;
            _armorLevel.OnChange += OnLevelChanged;
            _sturdyLevel.OnChange += OnLevelChanged;
            _runeLevel.OnChange += OnLevelChanged;
            _wards.OnChange += OnLevelChanged;
            _stones.OnChange += OnStonesChanged;
            _armoured.OnChange += OnArmouredChanged;
            OutfitChanged?.Invoke();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _axeLevel.OnChange -= OnLevelChanged;
            _armorLevel.OnChange -= OnLevelChanged;
            _sturdyLevel.OnChange -= OnLevelChanged;
            _runeLevel.OnChange -= OnLevelChanged;
            _wards.OnChange -= OnLevelChanged;
            _stones.OnChange -= OnStonesChanged;
            _armoured.OnChange -= OnArmouredChanged;
        }

        private void OnArmouredChanged(bool previous, bool next, bool asServer)
        {
            if (asServer != IsServerInitialized) return;
            OutfitChanged?.Invoke();
            if (next && !previous) Sfx.Play(ProceduralSfx.Upgrade, transform.position + Vector3.up, 1f);
        }

        // Server only. A boss tree fell: one more armour kit to carry.
        public void ServerGrantArmourKit() => _armourKits.Value = Mathf.Min(MaxArmourKits, _armourKits.Value + 1);

        // Called from the bag: put the armour on. Anywhere, once.
        public void RequestUseArmourKit()
        {
            if (IsOwner) UseArmourKitServerRpc();
        }

        [ServerRpc]
        private void UseArmourKitServerRpc() => ServerUseArmourKit();

        // Server only. True when a kit was used.
        public bool ServerUseArmourKit()
        {
            if (_armoured.Value || _armourKits.Value <= 0) return false;
            _armourKits.Value--;
            _armoured.Value = true;
            return true;
        }

        // Server only. Loading a saved player.
        public void ServerLoadOutfit(int kits, bool armoured)
        {
            _armourKits.Value = Mathf.Clamp(kits, 0, MaxArmourKits);
            _armoured.Value = armoured;
        }

        private void OnStonesChanged(int previous, int next, bool asServer)
        {
            if (asServer != IsServerInitialized) return;
            StonesChanged?.Invoke();
            if (next != previous) Sfx.Play(ProceduralSfx.Upgrade, transform.position + Vector3.up, 0.9f);
        }

        private void OnLevelChanged(int previous, int next, bool asServer)
        {
            // Once per change, with trustworthy arguments: a host's client side callback sees previous equal to next,
            // so the host reacts to the server side call and everyone else to the client side one (as PlayerWood).
            if (asServer == IsServerInitialized && next > previous) Sfx.Play(ProceduralSfx.Upgrade, transform.position + Vector3.up, 0.8f);
        }

        // Upgrades, the forge and repairs are workbench work (RecipeBook.UpgradeAllowed).
        private bool AtWorkbench => RecipeBook.UpgradeAllowed(Workbench.AnyInReach(transform.position));

        // Called by the workbench screen: mend the axe or the rod for carried wood (and fiber for the rod).
        public void RequestRepair(ToolKind tool)
        {
            if (IsOwner) RepairServerRpc((int)tool);
        }

        [ServerRpc]
        private void RepairServerRpc(int tool)
        {
            if (!AtWorkbench) return;
            ServerRepair((ToolKind)tool);
        }

        public int RepairWood(ToolKind tool) => tool == ToolKind.Axe ? _config.AxeRepairWood : _config.RodRepairWood;

        public int RepairFabric(ToolKind tool) => tool == ToolKind.Axe ? 0 : _config.RodRepairFabric;

        // True when the tool is worn or broken (a full tool needs no repair).
        public bool NeedsRepair(ToolKind tool)
        {
            if (tool == ToolKind.Axe)
            {
                PlayerAxe axe = GetComponent<PlayerAxe>();
                return axe != null && ToolRules.Worn(axe.Durability, axe.MaxDurability);
            }
            PlayerFishing fishing = GetComponent<PlayerFishing>();
            return fishing != null && fishing.Rod > 0 && ToolRules.Worn(fishing.RodDurability, fishing.RodMaxDurability);
        }

        // Server only. Pays the repair from carried items and puts the tool back to full. False if it needs no repair
        // or the player cannot pay.
        public bool ServerRepair(ToolKind tool)
        {
            if (!NeedsRepair(tool)) return false;
            int wood = RepairWood(tool);
            int fabric = RepairFabric(tool);
            if (PlayerItems.Get(gameObject, (int)ItemKind.Wood) < wood || PlayerItems.Get(gameObject, (int)ItemKind.Fiber) < fabric) return false;
            PlayerItems.ServerChange(gameObject, (int)ItemKind.Wood, -wood);
            PlayerItems.ServerChange(gameObject, (int)ItemKind.Fiber, -fabric);
            if (tool == ToolKind.Axe) GetComponent<PlayerAxe>().ServerRepair();
            else GetComponent<PlayerFishing>().ServerRepairRod();
            return true;
        }

        // Called by the workbench screen when the owner presses an upgrade button.
        public void RequestUpgrade(int kind)
        {
            if (IsOwner) UpgradeServerRpc(kind);
        }

        // Called by the workbench screen: forge the next ward against this element from its essence.
        public void RequestWard(Element element)
        {
            if (IsOwner) WardServerRpc((int)element);
        }

        [ServerRpc]
        private void WardServerRpc(int element)
        {
            if (!AtWorkbench) return;
            ServerBuyWard((Element)element);
        }

        // Server only. Spends essence on the next ward level for this element.
        public bool ServerBuyWard(Element element)
        {
            if (element == Element.Neutral || _inventory == null) return false;
            if (!NextWardCost(element, out int cost) || !_inventory.ServerTrySpendEssence(element, cost)) return false;
            _wards.Value = EssenceRules.Set(_wards.Value, element, WardLevel(element) + 1);
            return true;
        }

        public void ServerLoadWards(int packed) => _wards.Value = packed;

        // Called by the workbench screen: forge this element's stone and set it in the axe.
        public void RequestStone(Element element)
        {
            if (IsOwner) StoneServerRpc((int)element);
        }

        [ServerRpc]
        private void StoneServerRpc(int element)
        {
            if (!AtWorkbench) return;
            ServerBuyStone((Element)element);
        }

        // Server only. Spends the element's essence and rune shards on its stone. Each stone is forged once.
        public bool ServerBuyStone(Element element)
        {
            if (element == Element.Neutral || HasStone(element) || _inventory == null) return false;
            if (_inventory.Essence(element) < _config.StoneEssenceCost || _inventory.Shards < _config.StoneShardCost) return false;
            if (!_inventory.ServerTrySpendRelics(0, _config.StoneShardCost)) return false;
            _inventory.ServerTrySpendEssence(element, _config.StoneEssenceCost);
            _stones.Value = ElementBuffRules.AddStone(_stones.Value, element);
            return true;
        }

        public void ServerLoadStones(int stones) => _stones.Value = Mathf.Max(0, stones);

        [ServerRpc]
        private void UpgradeServerRpc(int kind)
        {
            if (!AtWorkbench) return;
            ServerBuy(kind);
        }

        // Server only. Buys the next level of the given kind with the hut's store wood. Returns true on success.
        public bool ServerBuy(int kind)
        {
            Hut hut = Hut.Instance;
            if (hut == null) return false;

            switch (kind)
            {
                case KindAxe:
                    if (DropRules.TryGetNextCost(_config.AxeUpgradeCosts, _axeLevel.Value, out int axeCost) && hut.ServerTrySpend(axeCost))
                    {
                        _axeLevel.Value++;
                        return true;
                    }
                    return false;
                case KindArmor:
                    if (DropRules.TryGetNextCost(_config.ArmorUpgradeCosts, _armorLevel.Value, out int armorCost) && hut.ServerTrySpend(armorCost))
                    {
                        _armorLevel.Value++;
                        return true;
                    }
                    return false;
                case KindHut:
                    return hut.ServerTryUpgrade();
                case KindSturdy:
                    if (_inventory != null && DropRules.TryGetNextCost(_config.SturdyIronCosts, _sturdyLevel.Value, out int iron)
                        && _inventory.ServerTrySpendRelics(iron, 0))
                    {
                        _sturdyLevel.Value++;
                        return true;
                    }
                    return false;
                case KindRune:
                    if (_inventory != null && DropRules.TryGetNextCost(_config.RuneShardCosts, _runeLevel.Value, out int shards)
                        && _inventory.ServerTrySpendRelics(0, shards))
                    {
                        _runeLevel.Value++;
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        // Server only. Loading a saved player.
        public void ServerLoad(int axe, int armor, int sturdy, int rune)
        {
            _axeLevel.Value = Mathf.Clamp(axe, 0, _config.AxeUpgradeCosts.Length);
            _armorLevel.Value = Mathf.Clamp(armor, 0, _config.ArmorUpgradeCosts.Length);
            _sturdyLevel.Value = Mathf.Clamp(sturdy, 0, _config.SturdyIronCosts.Length);
            _runeLevel.Value = Mathf.Clamp(rune, 0, _config.RuneShardCosts.Length);
        }

        // Server only. Used by the game over restart.
        public void ServerReset()
        {
            _axeLevel.Value = 0;
            _armorLevel.Value = 0;
            _sturdyLevel.Value = 0;
            _runeLevel.Value = 0;
            _wards.Value = 0;
            _stones.Value = 0;
            _armourKits.Value = 0;
            _armoured.Value = false;
        }
    }
}
