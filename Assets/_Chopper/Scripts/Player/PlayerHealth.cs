using System.Collections.Generic;
using Chopper.Audio;
using Chopper.Core;
using Chopper.Enemies;
using Chopper.World;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Chopper.Player
{
    // Player health (plan D19, T22). At zero health the player is down and the server puts them back at the hut
    // after the respawn delay, which is time the enemies use on the hut. Lives are infinite by default. In the finite
    // lives mode (config) each fall costs one life per night; with none left they stay down until dawn, and if every
    // player is out the run restarts.
    public class PlayerHealth : NetworkBehaviour
    {
        private readonly SyncVar<int> _health = new SyncVar<int>();
        private readonly SyncVar<bool> _down = new SyncVar<bool>();
        private readonly SyncVar<int> _lives = new SyncVar<int>();
        // What put the player down last, for the death screen.
        private readonly SyncVar<int> _cause = new SyncVar<int>();

        public enum DeathCause { Enemy, FallingTree, Fall, Burn }

        [SerializeField] private GameConfig _config;

        private static readonly List<PlayerHealth> _all = new List<PlayerHealth>();
        private PlayerUpgrades _upgrades;
        private PlayerMovementValidator _validator;
        private CharacterController _controller;
        private float _respawnAt;
        private float _burnElapsed;

        public static IReadOnlyList<PlayerHealth> All => _all;
        public static PlayerHealth Local { get; private set; }

        // Raised on the owning client when this player loses health (screen flash).
        public static event System.Action LocalDamaged;

        public int Health => _health.Value;
        public int MaxHealth => _config.PlayerMaxHealth;
        public bool IsDown => _down.Value;
        public int Lives => _lives.Value;
        public int MaxLives => _config.PlayerLives;
        // Lives are infinite unless the finite lives mode is switched on in the config.
        public bool FiniteLives => _config.FiniteLives;
        // Down with no lives left (finite mode only): waits for dawn.
        public bool IsOut => _down.Value && LivesRules.IsOut(_lives.Value, _config.FiniteLives);
        public float RespawnRemaining => Mathf.Max(0f, _respawnAt - Time.time);
        public DeathCause LastCause => (DeathCause)_cause.Value;
        // True when the player was carrying wood when they fell: it was left on the ground where they fell.
        public bool DroppedWood => _droppedWood.Value;
        private readonly SyncVar<bool> _droppedWood = new SyncVar<bool>();

        private void Awake()
        {
            _upgrades = GetComponent<PlayerUpgrades>();
            _validator = GetComponent<PlayerMovementValidator>();
            _controller = GetComponent<CharacterController>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _all.Add(this);
            _health.Value = _config.PlayerMaxHealth;
            _down.Value = false;
            _lives.Value = _config.PlayerLives;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _all.Remove(this);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner) Local = this;
            // Down timers are only known on the server; clients estimate from the moment they see the change.
            _down.OnChange += OnDownChanged;
            _health.OnChange += OnHealthChanged;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _down.OnChange -= OnDownChanged;
            _health.OnChange -= OnHealthChanged;
            if (Local == this) Local = null;
        }

        private void OnHealthChanged(int previous, int next, bool asServer)
        {
            // Once per change, with trustworthy arguments: a host's client side callback sees previous equal to next,
            // so the host reacts to the server side call and everyone else to the client side one (as PlayerWood).
            if (asServer != IsServerInitialized || next >= previous) return;
            Sfx.Play(ProceduralSfx.Hurt, transform.position + Vector3.up, IsOwner ? 1f : 0.6f, 1f, 0.08f);
            // The blow itself: a heavy wooden thump under the grunt, so a hit from a tree is unmistakable.
            Sfx.Play(ProceduralSfx.HutHit, transform.position + Vector3.up * 1.2f, IsOwner ? 1f : 0.7f, 1.25f, 0.08f);
            PlayerHitFlash flash = GetComponent<PlayerHitFlash>();
            if (flash == null) flash = gameObject.AddComponent<PlayerHitFlash>();
            flash.Flash();
            if (IsOwner) LocalDamaged?.Invoke();
            if (next > 0)
            {
                PlayerAnimator animator = GetComponent<PlayerAnimator>();
                if (animator != null) animator.PlayHit();
            }
        }

        private void OnDownChanged(bool previous, bool next, bool asServer)
        {
            if (next) _respawnAt = Time.time + _config.PlayerRespawnSeconds;
        }

        // Server only. Armor reduces the damage; the result is at least 1 while any damage was dealt.
        public void ServerDamage(int rawDamage, Treant source = null)
        {
            if (_down.Value || rawDamage <= 0) return;
            // Rolling, blocking and parrying decide how much of the hit lands.
            PlayerCombat combat = GetComponent<PlayerCombat>();
            if (combat != null)
            {
                rawDamage = combat.ServerMitigate(rawDamage, source);
                if (rawDamage <= 0) return;
            }
            // An elemental ward takes its share off hits from enemies of that element.
            if (source != null && _upgrades != null)
            {
                float ward = _upgrades.WardReduction(source.Element);
                if (ward > 0f) rawDamage = Mathf.Max(1, Mathf.CeilToInt(rawDamage * (1f - ward)));
            }

            ServerApply(rawDamage, DeathCause.Enemy, true);
        }

        // Server only. A large tree landed on the player. Blocking, rolling and wood armor do not help; without
        // Sturdy gear from the crypts it kills outright (CrushRules).
        public void ServerCrush()
        {
            if (_down.Value) return;
            int sturdy = _upgrades != null ? _upgrades.SturdyLevel : 0;
            ServerApply(CrushRules.Damage(MaxHealth, sturdy), DeathCause.FallingTree, false);
        }

        // Server only. The player landed after falling this far (reported by their own client, FallRules).
        public void ServerFall(float height)
        {
            if (_down.Value) return;
            int damage = FallRules.Damage(height, MaxHealth);
            if (damage > 0) ServerApply(damage, DeathCause.Fall, false);
        }

        // Server only. Flame ticks (BurnRules). Armor and a block do not stop a fire you are standing in.
        public void ServerBurn(int damage)
        {
            if (_down.Value || damage <= 0) return;
            ServerApply(damage, DeathCause.Burn, false);
        }

        private void ServerApply(int rawDamage, DeathCause cause, bool armor)
        {
            float reduction = armor && _upgrades != null ? _upgrades.ArmorReduction : 0f;
            int damage = Mathf.Max(1, Mathf.CeilToInt(rawDamage * (1f - reduction)));
            _health.Value = Mathf.Max(0, _health.Value - damage);
            if (_health.Value > 0) return;

            _cause.Value = (int)cause;
            // Like Valheim, what you carried stays where you fell: the wood is left there as a pile to come back for.
            PlayerWood wood = GetComponent<PlayerWood>();
            int carried = wood != null ? wood.ServerTakeAll() : 0;
            _droppedWood.Value = carried > 0;
            if (carried > 0 && EnemyDirector.Instance != null) EnemyDirector.Instance.ServerDropWood(transform.position, carried);
            // W9-01 (user, 2026-10-12): the axe and the outfit stay there too; the player arrives again in the pond
            // without them and comes back for them. Upgrades and the armour are the player's own and stay.
            ServerDropGear();
            _down.Value = true;
            _lives.Value = LivesRules.AfterFall(_lives.Value, _config.FiniteLives);
            _respawnAt = Time.time + _config.PlayerRespawnSeconds;
            if (LivesRules.IsOut(_lives.Value, _config.FiniteLives) && EnemyDirector.Instance != null) EnemyDirector.Instance.ServerCheckAllPlayersOut();
        }

        private void ServerDropGear()
        {
            EnemyDirector director = EnemyDirector.Instance;
            if (director == null || Chopper.World.TutorialSession.Active) return;
            Vector3 at = transform.position;
            PlayerAxe axe = GetComponent<PlayerAxe>();
            if (axe != null && axe.HasAxe)
                director.ServerDropItem(Chopper.World.GroundItemKind.Axe, axe.ServerDrop(), at, at + transform.right);
            PlayerUpgrades upgrades = GetComponent<PlayerUpgrades>();
            if (upgrades != null && upgrades.Dressed)
            {
                upgrades.ServerSetDressed(false);
                director.ServerDropItem(Chopper.World.GroundItemKind.Outfit, 1, at, at - transform.right);
            }
            if (upgrades != null && upgrades.HasShield)
            {
                upgrades.ServerSetShield(false);
                director.ServerDropItem(Chopper.World.GroundItemKind.Shield, 1, at, at + transform.forward);
            }
        }

        // Server only. Loading a saved player: their health when the world was saved (full if they were down).
        public void ServerLoadHealth(int health)
        {
            _down.Value = false;
            _health.Value = health > 0 ? Mathf.Min(health, _config.PlayerMaxHealth) : _config.PlayerMaxHealth;
        }

        // Server only. Restores health up to the maximum.
        public void ServerHeal(int amount)
        {
            if (_down.Value || amount <= 0) return;
            _health.Value = Mathf.Min(_config.PlayerMaxHealth, _health.Value + amount);
        }

        // Server only. Full health, back on their feet, and all lives back. Used at dawn and on restarts.
        public void ServerHealFull()
        {
            _health.Value = _config.PlayerMaxHealth;
            _down.Value = false;
            _lives.Value = _config.PlayerLives;
        }

        private void Update()
        {
            if (IsServerInitialized && !_down.Value)
            {
                int burn = BurnRules.Tick(ref _burnElapsed, Time.deltaTime, Campfire.IsInFlame(transform.position));
                if (burn > 0) ServerBurn(burn);
            }
            if (!IsServerInitialized || !_down.Value || LivesRules.IsOut(_lives.Value, _config.FiniteLives) || Time.time < _respawnAt) return;
            ServerRespawn();
        }

        // Server only. Brings a downed player back right now (tests; the normal path waits for the respawn timer).
        public void ServerRespawnNow() => ServerRespawn();

        private void ServerRespawn()
        {
            Vector3 position = Vector3.zero;
            if (!Chopper.World.TutorialSession.Active && Chopper.World.Generation.WorldSession.Gen != null)
            {
                // W9-01: back the way everyone arrives, falling into the hub pond.
                position = Chopper.World.WorldStart.DropPoint((int)(ObjectId % 4));
            }
            else
            {
                // The practice yard: beside the bed once the hut is repaired, otherwise where the game started.
                bool outside = Hut.Instance == null || !Hut.Instance.IsRepaired;
                PlayerSpawnPoint point = PlayerSpawnPoint.Pick(outside, outside ? Random.Range(0, 100000) : ObjectId);
                if (point != null) position = point.transform.position;
            }

            _health.Value = _config.PlayerMaxHealth;
            _down.Value = false;
            if (_validator != null) _validator.ServerNotifyTeleport(position);
            if (Owner.IsValid) RespawnTargetRpc(Owner, position);
        }

        [TargetRpc]
        private void RespawnTargetRpc(NetworkConnection connection, Vector3 position)
        {
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = wasEnabled;
            var networkTransform = GetComponent<FishNet.Component.Transforming.NetworkTransform>();
            if (networkTransform != null) networkTransform.Teleport();
        }
    }
}
