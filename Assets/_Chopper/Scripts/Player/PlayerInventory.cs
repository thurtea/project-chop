using Chopper.Audio;
using Chopper.Core;
using Chopper.UI;
using Chopper.World;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Chopper.Player
{
    // Crafted consumables: torches and bandages (server owned counts; the code still calls bandages salves, which is
    // also their name in save files), plus the carried materials. Recipes come from RecipeBook: a torch (carried wood)
    // is made at a workbench, a bandage (healing herbs and fiber) anywhere. Hotbar: 1 axe, 2 torch on or off, 3 use a
    // bandage.
    // A lit torch is held in the left hand and lights the night around the player for everyone to see.
    public class PlayerInventory : NetworkBehaviour
    {
        public const int RecipeTorch = RecipeBook.Torch;
        public const int RecipeSalve = RecipeBook.Bandage;

        [SerializeField] private GameConfig _config;
        [Tooltip("Torch model and light in the left hand, switched on while the torch is lit.")]
        [SerializeField] private GameObject _torchVisual;

        private readonly SyncVar<int> _torches = new SyncVar<int>();
        private readonly SyncVar<int> _salves = new SyncVar<int>();
        private bool _swimHolstered;
        private readonly SyncVar<bool> _torchLit = new SyncVar<bool>();
        // Whole seconds left on the torch in use, for the HUD.
        private readonly SyncVar<int> _torchSecondsLeft = new SyncVar<int>();

        private float _durability;
        private float _nextSync;
        private PlayerWood _wood;
        private PlayerHealth _health;
        private PlayerBoons _boons;

        public static PlayerInventory Local { get; private set; }

        // Raised on every client when this player wraps a bandage (the animator and the effects play it).
        public event System.Action Healed;

        private readonly SyncVar<int> _herbs = new SyncVar<int>();
        // Fabric, picked from small leafy bushes. Bandages are made from it and herbs.
        private readonly SyncVar<int> _fabric = new SyncVar<int>();
        // Rare materials from crypt chests: iron for Sturdy gear, rune shards for the Rune axe. Kept on death.
        private readonly SyncVar<int> _iron = new SyncVar<int>();
        private readonly SyncVar<int> _shards = new SyncVar<int>();
        // Elemental essence from crypt arena bosses: one byte per element (Water, Fire, Earth, Wind).
        private readonly SyncVar<int> _essence = new SyncVar<int>();

        public int Herbs => _herbs.Value;
        public int Fabric => _fabric.Value;
        public int Iron => _iron.Value;
        public int Shards => _shards.Value;
        public int EssencePacked => _essence.Value;

        public int Essence(Element element) => EssenceRules.Get(_essence.Value, element);
        public int Torches => _torches.Value;
        public int Salves => _salves.Value;
        public bool TorchLit => _torchLit.Value;
        public int TorchSecondsLeft => _torchSecondsLeft.Value;

        private void Awake()
        {
            _wood = GetComponent<PlayerWood>();
            _health = GetComponent<PlayerHealth>();
            _boons = GetComponent<PlayerBoons>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner) Local = this;
            _torchLit.OnChange += OnTorchChanged;
            _herbs.OnChange += OnHerbsChanged;
            _fabric.OnChange += OnFabricChanged;
            _torches.OnChange += OnTorchesChanged;
            _salves.OnChange += OnSalvesChanged;
            _iron.OnChange += OnIronChanged;
            _shards.OnChange += OnShardsChanged;
            _essence.OnChange += OnEssenceChanged;
            ApplyTorchVisual();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _torchLit.OnChange -= OnTorchChanged;
            _herbs.OnChange -= OnHerbsChanged;
            _fabric.OnChange -= OnFabricChanged;
            _torches.OnChange -= OnTorchesChanged;
            _salves.OnChange -= OnSalvesChanged;
            _iron.OnChange -= OnIronChanged;
            _shards.OnChange -= OnShardsChanged;
            _essence.OnChange -= OnEssenceChanged;
            if (Local == this) Local = null;
        }

        // True for the one call per change that this player's own machine should react to. A joined player hears it as
        // a client; on the host FishNet only reports the change on the server side, so the host reacts to that one.
        private bool OwnChange(bool asServer) => IsOwner && asServer == IsServerInitialized;

        private void OnHerbsChanged(int previous, int next, bool asServer)
        {
            if (!OwnChange(asServer) || next <= previous) return;
            Chopper.UI.PickupFeed.Add(Chopper.UI.PickupFeed.Kind.Herb, "Healing herb", next - previous, next);
        }

        private void OnFabricChanged(int previous, int next, bool asServer)
        {
            if (!OwnChange(asServer) || next <= previous) return;
            Chopper.UI.PickupFeed.Add(Chopper.UI.PickupFeed.Kind.Fabric, "Fiber", next - previous, next);
        }

        private void OnTorchesChanged(int previous, int next, bool asServer)
        {
            if (!OwnChange(asServer) || next <= previous) return;
            Chopper.UI.PickupFeed.Add(Chopper.UI.PickupFeed.Kind.Torch, "Torch", next - previous, next);
        }

        private void OnSalvesChanged(int previous, int next, bool asServer)
        {
            if (!OwnChange(asServer) || next <= previous) return;
            Chopper.UI.PickupFeed.Add(Chopper.UI.PickupFeed.Kind.Bandage, "Bandage", next - previous, next);
        }

        private void OnIronChanged(int previous, int next, bool asServer)
        {
            if (!OwnChange(asServer) || next <= previous) return;
            Chopper.UI.PickupFeed.Add(Chopper.UI.PickupFeed.Kind.Iron, "Crypt iron", next - previous, next);
        }

        private void OnShardsChanged(int previous, int next, bool asServer)
        {
            if (!OwnChange(asServer) || next <= previous) return;
            Chopper.UI.PickupFeed.Add(Chopper.UI.PickupFeed.Kind.Shard, "Rune shard", next - previous, next);
        }

        private void OnEssenceChanged(int previous, int next, bool asServer)
        {
            if (!OwnChange(asServer)) return;
            foreach (Element element in EssenceRules.Elements)
            {
                int gained = EssenceRules.Get(next, element) - EssenceRules.Get(previous, element);
                if (gained > 0) Chopper.UI.PickupFeed.Add(Chopper.UI.PickupFeed.Kind.Essence, element + " essence", gained, EssenceRules.Get(next, element));
            }
        }

        private void OnTorchChanged(bool previous, bool next, bool asServer)
        {
            ApplyTorchVisual();
        }

        private void ApplyTorchVisual()
        {
            if (_torchVisual != null) _torchVisual.SetActive(_torchLit.Value);
        }

        // ---------- Owner input ----------

        private void Update()
        {
            if (IsServerInitialized) ServerBurnTorch();

            if (!IsOwner || UiState.GameplayBlocked) return;
            if (Keybinds.WasPressed(KeyAction.Torch) && (_torchLit.Value || SwimHolster.MayDraw(GetComponent<PlayerMotor>() is PlayerMotor torchMotor && torchMotor.StowsTools))) ToggleTorchServerRpc();
            // Putting it out keeps what is left of it for next time.
            if (SwimHolster.Step(GetComponent<PlayerMotor>() is PlayerMotor swimMotor && swimMotor.StowsTools, _torchLit.Value, ref _swimHolstered)) ToggleTorchServerRpc();
            if (Keybinds.WasPressed(KeyAction.Unequip) && _torchLit.Value) ToggleTorchServerRpc();
            if (Keybinds.WasPressed(KeyAction.Potion)) UseSalveServerRpc();
        }

        public void RequestCraft(int recipe) => CraftServerRpc(recipe);

        public void RequestToggleTorch() => ToggleTorchServerRpc();

        public void RequestSalve() => UseSalveServerRpc();

        // ---------- Server ----------

        // Server only.
        public void ServerAddHerbs(int amount)
        {
            _herbs.Value = Mathf.Clamp(_herbs.Value + amount, 0, _config.HerbCarryCap);
        }

        // Server only.
        public void ServerAddFabric(int amount)
        {
            _fabric.Value = Mathf.Clamp(_fabric.Value + amount, 0, _config.FabricCarryCap);
        }

        // Server only. Spends carried fabric (the fishing rod); false if there is not enough.
        public bool ServerTrySpendFabric(int amount)
        {
            if (amount < 0 || _fabric.Value < amount) return false;
            _fabric.Value -= amount;
            return true;
        }

        // Server only. Loading a saved player.
        public void ServerLoad(int torches, int salves, int herbs, int fabric, int iron, int shards)
        {
            _torches.Value = Mathf.Clamp(torches, 0, _config.CraftStackCap);
            _salves.Value = Mathf.Clamp(salves, 0, _config.CraftStackCap);
            _herbs.Value = Mathf.Clamp(herbs, 0, _config.HerbCarryCap);
            _fabric.Value = Mathf.Clamp(fabric, 0, _config.FabricCarryCap);
            _iron.Value = Mathf.Max(0, iron);
            _shards.Value = Mathf.Max(0, shards);
        }

        // Server only. Loot from a crypt chest.
        public void ServerAddRelics(int iron, int shards)
        {
            _iron.Value = Mathf.Max(0, _iron.Value + iron);
            _shards.Value = Mathf.Max(0, _shards.Value + shards);
        }

        // Server only. Essence from a crypt arena boss.
        public void ServerAddEssence(Element element, int amount)
        {
            _essence.Value = EssenceRules.Set(_essence.Value, element, EssenceRules.Get(_essence.Value, element) + amount);
        }

        public bool ServerTrySpendEssence(Element element, int amount)
        {
            int have = EssenceRules.Get(_essence.Value, element);
            if (amount < 0 || have < amount) return false;
            _essence.Value = EssenceRules.Set(_essence.Value, element, have - amount);
            return true;
        }

        public void ServerLoadEssence(int packed) => _essence.Value = packed;

        // Server only. Spends crypt materials on an upgrade; false if there is not enough.
        public bool ServerTrySpendRelics(int iron, int shards)
        {
            if (iron < 0 || shards < 0 || _iron.Value < iron || _shards.Value < shards) return false;
            _iron.Value -= iron;
            _shards.Value -= shards;
            return true;
        }

        [ServerRpc]
        private void CraftServerRpc(int recipe) => ServerCraft(recipe, false);

        // Server only. Makes one of a recipe from carried items (RecipeBook). A recipe that needs a workbench is only made
        // standing at one, unless ignoreStation (tests only).
        public bool ServerCraft(int recipe, bool ignoreStation)
        {
            if (!RecipeBook.Valid(recipe)) return false;
            if (!ignoreStation && !RecipeBook.StationAllows(recipe, Workbench.AnyInReach(transform.position))) return false;
            PlayerFishing landedBy = GetComponent<PlayerFishing>();
            if (!RecipeBook.Unlocked(recipe, landedBy != null ? landedBy.FishLanded : 0, _config)) return false;
            Recipe r = RecipeBook.Get(recipe, _config);
            if (!RecipeBook.CanAfford(r, kind => PlayerItems.Get(gameObject, (int)kind), PlayerItems.Get(gameObject, (int)r.Output),
                    PlayerItems.Cap(_config, (int)r.Output))) return false;
            foreach (Ingredient input in r.Inputs) PlayerItems.ServerChange(gameObject, (int)input.Kind, -input.Amount);
            PlayerItems.ServerChange(gameObject, (int)r.Output, 1);
            return true;
        }

        // Server only. Adds (or with a negative delta removes) carried items this component owns: herbs, fiber,
        // bandages, torches, crypt iron, rune shards and essence. Returns how much actually changed.
        public int ServerChange(ItemKind kind, int delta)
        {
            int before;
            switch (kind)
            {
                case ItemKind.Herb:
                    before = _herbs.Value;
                    _herbs.Value = Mathf.Clamp(before + delta, 0, _config.HerbCarryCap);
                    return _herbs.Value - before;
                case ItemKind.Fiber:
                    before = _fabric.Value;
                    _fabric.Value = Mathf.Clamp(before + delta, 0, _config.FabricCarryCap);
                    return _fabric.Value - before;
                case ItemKind.Bandage:
                    before = _salves.Value;
                    _salves.Value = Mathf.Clamp(before + delta, 0, _config.CraftStackCap);
                    return _salves.Value - before;
                case ItemKind.Torch:
                    before = _torches.Value;
                    _torches.Value = Mathf.Clamp(before + delta, 0, _config.CraftStackCap);
                    return _torches.Value - before;
                case ItemKind.Iron:
                    before = _iron.Value;
                    _iron.Value = Mathf.Max(0, before + delta);
                    return _iron.Value - before;
                case ItemKind.Shard:
                    before = _shards.Value;
                    _shards.Value = Mathf.Max(0, before + delta);
                    return _shards.Value - before;
                default:
                    Element element = ItemRules.ElementOf((int)kind);
                    if (element == Element.Neutral) return 0;
                    before = EssenceRules.Get(_essence.Value, element);
                    _essence.Value = EssenceRules.Set(_essence.Value, element, before + delta);
                    return EssenceRules.Get(_essence.Value, element) - before;
            }
        }

        [ServerRpc]
        private void ToggleTorchServerRpc() => ServerToggleTorch();

        // Server only. Lights the torch (using a fresh one if the current is spent) or puts it out.
        public void ServerToggleTorch()
        {
            if (_torchLit.Value)
            {
                _torchLit.Value = false;
                return;
            }

            // Light the torch already in use, or start a fresh one.
            if (_durability <= 0f)
            {
                if (_torches.Value <= 0) return;
                _torches.Value--;
                _durability = _config.TorchBurnSeconds + (_boons != null ? _boons.TorchSecondsBonus : 0f);
            }
            _torchLit.Value = true;
            _torchSecondsLeft.Value = Mathf.CeilToInt(_durability);
        }

        [ServerRpc]
        private void UseSalveServerRpc() => ServerUseSalve();

        // Server only. Wraps a bandage if one is carried and health is not full.
        public bool ServerUseSalve()
        {
            if (_salves.Value <= 0 || _health == null || _health.IsDown || _health.Health >= _health.MaxHealth) return false;
            _salves.Value--;
            _health.ServerHeal(_config.SalveHeal + (_boons != null ? _boons.SalveHealBonus : 0));
            HealedObserversRpc();
            return true;
        }

        [ObserversRpc]
        private void HealedObserversRpc() => Healed?.Invoke();

        // Server only, development and tests: hands out items without crafting.
        public void ServerGrant(int torches, int salves)
        {
            _torches.Value = Mathf.Min(_config.CraftStackCap, _torches.Value + torches);
            _salves.Value = Mathf.Min(_config.CraftStackCap, _salves.Value + salves);
        }

        private void ServerBurnTorch()
        {
            if (!_torchLit.Value) return;

            _durability -= Time.deltaTime;
            if (_durability <= 0f)
            {
                _durability = 0f;
                _torchLit.Value = false;
                _torchSecondsLeft.Value = 0;
                return;
            }
            if (Time.time >= _nextSync)
            {
                _nextSync = Time.time + 1f;
                _torchSecondsLeft.Value = Mathf.CeilToInt(_durability);
            }
        }

        // Server only. Used by the game over restart.
        public void ServerReset()
        {
            // Everyone starts (and restarts) with a torch and a bandage.
            _torches.Value = _config.StartTorches;
            _salves.Value = _config.StartSalves;
            _torchLit.Value = false;
            _torchSecondsLeft.Value = 0;
            _durability = 0f;
            _herbs.Value = 0;
            _fabric.Value = 0;
            _iron.Value = 0;
            _shards.Value = 0;
            _essence.Value = 0;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerReset();
        }
    }
}
