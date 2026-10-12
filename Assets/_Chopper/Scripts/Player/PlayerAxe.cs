using Chopper.Core;
using Chopper.Enemies;
using Chopper.Trees;
using Chopper.UI;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Chopper.Player
{
    // Axe swing flow (plan D3, T07). The owner plays the chop instantly and sends SwingAxeServerRpc with its
    // aim; the server enforces the cooldown (with latency tolerance) and tells everyone else to play it.
    // The client never names a target. Hit resolution arrives in T12 (trees) and T22 (Treants).
    public class PlayerAxe : NetworkBehaviour
    {
        private const string AttackActionName = "Player/Attack";
        // Aim vectors shorter than this after flattening are rejected as malformed.
        private const float MinAimSqrMagnitude = 0.01f;
        // How long the finisher lunge carries the owner before the normal slow down eats it.
        private const float LungeSeconds = 0.6f;

        [SerializeField] private GameConfig _config;
        [SerializeField] private PlayerCamera _playerCamera;
        [Tooltip("Pivot the chop rotates around its local X axis. Its rest pose is the pose in the prefab.")]
        [SerializeField] private Transform _axePivot;
        [Tooltip("Off when a character Animator plays the chop, so the axe is only moved by the animation.")]
        [SerializeField] private bool _useScriptedSwing = true;

        // Every player starts with the axe. It wears with every hit (ToolRules) and breaks at zero: then the player fights
        // and chops with fists until it is repaired at a workbench.
        private readonly SyncVar<bool> _hasAxe = new SyncVar<bool>();
        private readonly SyncVar<int> _durability = new SyncVar<int>();
        // Drawn: the axe is in the hand. Holstered (the Axe key again): it rides on the back and the player fights with
        // fists, like Valheim's unarmed. The server decides hits, so it keeps this too.
        private readonly SyncVar<bool> _drawn = new SyncVar<bool>(true);
        private Transform _backAxe;
        private Transform _backBone;
        private float _punchProgress;

        private PlayerWood _wood;
        private PlayerCombat _combat;
        private PlayerUpgrades _upgrades;
        private PlayerBuffs _buffs;
        private PlayerHealth _health;
        private PlayerStamina _stamina;
        private InputAction _attack;
        private float _nextSwingTime;
        private float _serverNextSwingTime;
        private float _swingStartTime = float.NegativeInfinity;
        private Quaternion _restRotation;
        // Combo state: the owner's guess for the animation, and the server's ruling for damage.
        private int _comboStep = -1;
        private float _comboTime = float.NegativeInfinity;
        private int _serverStep = -1;
        private float _serverStepTime = float.NegativeInfinity;
        private PlayerMotor _motor;
        private PlayerMovementValidator _validator;
        private PlayerFishing _fishing;
        private PlayerMining _mining;

        public bool HasAxe => _hasAxe.Value;
        public int Durability => _durability.Value;
        public int MaxDurability => _config.AxeDurability;
        public bool IsBroken => ToolRules.Broken(_durability.Value);
        public bool IsDrawn => _hasAxe.Value && _drawn.Value;

        private void Awake()
        {
            _wood = GetComponent<PlayerWood>();
            _combat = GetComponent<PlayerCombat>();
            _motor = GetComponent<PlayerMotor>();
            _validator = GetComponent<PlayerMovementValidator>();
            _upgrades = GetComponent<PlayerUpgrades>();
            _buffs = GetComponent<PlayerBuffs>();
            _health = GetComponent<PlayerHealth>();
            _stamina = GetComponent<PlayerStamina>();
            _fishing = GetComponent<PlayerFishing>();
            _mining = GetComponent<PlayerMining>();
            _restRotation = _axePivot.localRotation;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _serverNextSwingTime = 0f;
            // W9-01 (user, 2026-10-12): a new player wakes in the pond with empty hands; the axe lies on the shore with
            // the outfit (WorldStart). A saved player gets theirs back from ServerLoadWear.
            _durability.Value = _config.AxeDurability;
            ServerSetHasAxe(false);
        }

        // Server only. A new axe in the hand.
        public void ServerResetTool()
        {
            _durability.Value = _config.AxeDurability;
            ServerSetHasAxe(true);
        }

        // Server only. An axe picked up from the ground, as worn as it was when it was dropped.
        public void ServerPickUp(int durability)
        {
            _durability.Value = Mathf.Clamp(durability, 1, _config.AxeDurability);
            ServerSetHasAxe(true);
        }

        // Server only. The axe leaves the hand to lie where the player fell; returns how worn it is, for the pickup.
        public int ServerDrop()
        {
            int durability = _durability.Value;
            ServerSetHasAxe(false);
            return durability;
        }

        // Server only. Loading a saved player: how worn the axe was (0 is new, as in older saves).
        public void ServerLoadWear(int wear)
        {
            _durability.Value = ToolRules.FromWear(wear, _config.AxeDurability);
            ServerSetHasAxe(!IsBroken);
        }

        public int Wear => ToolRules.ToWear(_durability.Value, _config.AxeDurability);

        // Server only. Mended at a workbench: full again and back in the hand.
        public void ServerRepair()
        {
            _durability.Value = _config.AxeDurability;
            ServerSetHasAxe(true);
        }

        // Server only. One hit's wear; at zero the axe breaks and the player is down to fists.
        private void ServerWear()
        {
            if (!_hasAxe.Value || _durability.Value <= 0) return;
            _durability.Value = ToolRules.Wear(_durability.Value, 1);
            if (IsBroken)
            {
                ServerSetHasAxe(false);
                BrokeObserversRpc();
            }
        }

        [ObserversRpc]
        private void BrokeObserversRpc()
        {
            Chopper.Audio.Sfx.Play(Chopper.Audio.ProceduralSfx.Parry, transform.position + Vector3.up * 1.2f, 0.8f, 0.6f);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _swingStartTime = float.NegativeInfinity;
            _axePivot.localRotation = _restRotation;
            _hasAxe.OnChange += OnHasAxeChanged;
            _drawn.OnChange += OnDrawnChanged;
            _clientStartTime = Time.time;
            _shownDrawn = _drawn.Value;
            BuildBackAxe();
            ApplyAxeVisual();
            if (!IsOwner) return;

            _attack = InputSystem.actions.FindAction(AttackActionName, true);
            _attack.Enable();
            _nextSwingTime = 0f;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _hasAxe.OnChange -= OnHasAxeChanged;
            _drawn.OnChange -= OnDrawnChanged;
            _attack = null;
            if (_backAxe != null) Destroy(_backAxe.gameObject);
        }

        private void OnHasAxeChanged(bool previous, bool next, bool asServer)
        {
            if (!asServer) ApplyAxeVisual();
        }

        private void OnDrawnChanged(bool previous, bool next, bool asServer)
        {
            if (asServer) return;
            // A real draw or holster (not the first sync on joining): the hand reaches back over the shoulder and the
            // axe travels between the hand and the back while it is there, instead of jumping. Compared with what is
            // shown, not with previous: on the host the client callback gets the new value as previous too.
            bool changed = next != _shownDrawn;
            _shownDrawn = next;
            if (changed && _hasAxe.Value && !_stowed && Time.time - _clientStartTime > 0.5f)
            {
                _stowStart = Time.time;
                _stowToBack = !next;
                _stowing = true;
                StowPlayed?.Invoke();
                return;
            }
            ApplyAxeVisual();
        }

        // The axe shows while it is whole: in the hand while drawn, on the back while holstered or while the rod is out.
        private void ApplyAxeVisual()
        {
            _stowing = false;
            bool inHand = _hasAxe.Value && _drawn.Value && !_stowed && !_miningStowed;
            if (_axePivot != null) _axePivot.gameObject.SetActive(inHand);
            if (_backAxe != null) _backAxe.gameObject.SetActive(_hasAxe.Value && !inHand);
        }

        // Owner only. The Axe key: draw it or put it on the back.
        public void RequestToggleDrawn()
        {
            if (!IsOwner || !HasAxe) return;
            ToggleDrawnServerRpc();
        }

        [ServerRpc]
        private void ToggleDrawnServerRpc() => _drawn.Value = !_drawn.Value;

        private bool _stowed;
        // The Unequip key put the axe on the back, so pressing it again with empty hands takes it out (Valheim's hide).
        private bool _hiddenByUnequip;
        private bool _handsBusyLastFrame;

        // The Unequip key: the axe goes on the back; pressed again with nothing else in the hands, it comes back out.
        // The torch, the rod and the hammer put themselves away on the same key.
        private void Unequip()
        {
            if (IsDrawn)
            {
                _hiddenByUnequip = true;
                RequestToggleDrawn();
                return;
            }
            // Last frame's hands: the torch, rod and hammer may already have gone away on this same press.
            if (_hiddenByUnequip && HasAxe && !_handsBusyLastFrame)
            {
                _hiddenByUnequip = false;
                RequestToggleDrawn();
            }
        }

        // Put away while fishing (PlayerFishing), out again after.
        public void SetStowed(bool stowed)
        {
            if (_stowed == stowed) return;
            _stowed = stowed;
            ApplyAxeVisual();
        }

        // Put away while the pickaxe is out (PlayerMining), out again after. Separate from SetStowed, which the rod
        // sets every frame.
        private bool _miningStowed;

        public void SetMiningStowed(bool stowed)
        {
            if (_miningStowed == stowed) return;
            _miningStowed = stowed;
            ApplyAxeVisual();
        }

        // Server only.
        public void ServerSetHasAxe(bool has)
        {
            _hasAxe.Value = has;
            // Taking the axe puts it straight in the hand.
            if (has) _drawn.Value = true;
            if (!has) _punchProgress = 0f;
        }

        private void Update()
        {
            AnimateSwing();

            if (!IsOwner || _attack == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame) DebugSpamServer();
#endif
            if (Keybinds.WasPressed(KeyAction.Axe) && !UiState.GameplayBlocked && (_fishing == null || !_fishing.Busy))
            {
                _hiddenByUnequip = false;
                RequestToggleDrawn();
            }
            if (Keybinds.WasPressed(KeyAction.Unequip) && !UiState.GameplayBlocked) Unequip();
            PlayerInventory inventory = PlayerInventory.Local;
            _handsBusyLastFrame = (inventory != null && inventory.TorchLit) || (_fishing != null && _fishing.Busy) || PlayerBuilder.LocalBuilding;
            // Holding the button keeps chopping; the cooldown in TrySwing paces the swings.
            if (Keybinds.IsHeld(KeyAction.Attack)) TrySwing();
        }

        // Owner only. Returns false while the client cooldown is running.
        public bool TrySwing()
        {
            if (!IsOwner || Time.time < _nextSwingTime) return false;
            if (_health != null && _health.IsDown) return false;
            if (UiState.GameplayBlocked) return false;
            if (_combat != null && (_combat.IsBlocking || _combat.IsRolling)) return false;
            // While fishing, attack casts, hooks and reels instead; with the hammer out it places a piece.
            if (_fishing != null && _fishing.Busy) return false;
            if (PlayerBuilder.LocalBuilding) return false;
            // Chop where the camera looks, like Valheim; the body turn replicates through the NetworkTransform.
            Vector3 aim = Quaternion.Euler(0f, _playerCamera.Yaw, 0f) * Vector3.forward;
            // A mine vein in front takes the press: the pickaxe comes out and strikes its glint.
            if (_mining != null && _mining.TryStrike(aim)) return true;
            // Only fighting costs stamina. Chopping trees is free, so an exhausted player can still gather wood.
            if (_stamina != null && EnemyInSwing(aim) && !_stamina.TryUse(_config.SwingStamina)) return false;

            // A tree in front (and no enemy closer) is a plain chop. Anything else is a combo swing.
            int step = -1;
            bool tree = TreeManager.Instance != null && TreeManager.Instance.ClientFindTree(transform.position, aim, out _, out float treeDistance)
                        && !EnemyCloserThan(aim, treeDistance);
            if (!tree) step = ComboRules.NextStep(_comboStep, Time.time - _comboTime, Cooldown);
            _comboStep = step;
            _comboTime = Time.time;

            _nextSwingTime = Time.time + Cooldown * (step >= 0 ? ComboRules.CooldownScale(step) : 1f);
            transform.rotation = Quaternion.LookRotation(aim, Vector3.up);
            if (step == ComboRules.Steps - 1 && _motor != null) _motor.Lunge(aim, ComboRules.FinisherLunge);
            ClientPlaySwing(step);
            SwingAxeServerRpc(aim);
            return true;
        }

        [ServerRpc]
        private void SwingAxeServerRpc(Vector3 aim)
        {
            if (_health != null && _health.IsDown) return;
            if (_combat != null && _combat.IsBlocking) return;
            float now = Time.time;
            if (!SwingTiming.IsServerSwingAllowed(now, _serverNextSwingTime, _config.SwingLatencyTolerance))
            {
                Debug.LogWarning($"[PlayerAxe] Rejected swing from client {Owner.ClientId}: " +
                                 $"{_serverNextSwingTime - now:F2} s before cooldown ends (tolerance {_config.SwingLatencyTolerance:F2} s).");
                return;
            }

            aim.y = 0f;
            if (aim.sqrMagnitude < MinAimSqrMagnitude)
            {
                Debug.LogWarning($"[PlayerAxe] Rejected swing from client {Owner.ClientId}: invalid aim {aim}.");
                return;
            }
            aim.Normalize();
            int step = ServerResolveHit(aim);
            _serverNextSwingTime = now + Cooldown * (step >= 0 ? ComboRules.CooldownScale(step) : 1f);
            // The finisher lunges the owner forward: let the speed check expect it.
            if (step == ComboRules.Steps - 1 && _validator != null) _validator.ServerAllowBurst(ComboRules.FinisherLunge, LungeSeconds, "finisher lunge");

            SwingObserversRpc(step);
        }

        // True when an awake enemy stands in the strip this swing would cover.
        private bool EnemyInSwing(Vector3 aim)
        {
            foreach (Treant treant in Treant.ClientAll)
            {
                if (treant == null || !treant.IsActive) continue;
                float distance = TreeTargeting.HitDistance(transform.position, aim, _config.SwingRange, _config.SwingRadius,
                    treant.transform.position, treant.HitRadius);
                if (distance >= 0f) return true;
            }
            return false;
        }

        private bool EnemyCloserThan(Vector3 aim, float distance)
        {
            foreach (Treant treant in Treant.ClientAll)
            {
                if (treant == null || !treant.IsActive) continue;
                float d = TreeTargeting.HitDistance(transform.position, aim, _config.SwingRange, _config.SwingRadius, treant.transform.position, treant.HitRadius);
                if (d >= 0f && d < distance) return true;
            }
            return false;
        }

        private float Cooldown => _config.SwingCooldown * (_upgrades != null ? _upgrades.SwingCooldownMultiplier : 1f);

        // Server only. The nearest thing in the swing strip takes the hit: a standing tree or a treant.
        // Bare fists do a fraction of an axe hit: a tree only counts a hit every few punches, and fists never use
        // or earn element charges.
        // Returns the combo step of the swing (-1 for a plain tree chop).
        public int ServerResolveHit(Vector3 aim)
        {
            bool armed = IsDrawn;
            Vector3 origin = transform.position;
            int foundTreeId = -1;
            float foundTreeDistance = float.MaxValue;
            if (TreeManager.Instance != null) TreeManager.Instance.ServerFindTree(origin, aim, out foundTreeId, out foundTreeDistance);

            Treant treant = Treant.ServerFindTarget(origin, aim, _config.SwingRange, _config.SwingRadius, out float treantDistance);

            bool hitsTreant = treant != null && (foundTreeId < 0 || treantDistance < foundTreeDistance);
            bool hitsTree = !hitsTreant && foundTreeId >= 0;
            int step = -1;
            if (!hitsTree) step = ComboRules.NextStep(_serverStep, Time.time - _serverStepTime, Cooldown);
            _serverStep = step;
            _serverStepTime = Time.time;

            if (armed && (hitsTreant || foundTreeId >= 0)) ServerWear();
            if (hitsTreant)
            {
                float units = armed ? _config.AxeBaseDamage * (_upgrades != null ? _upgrades.AxeDamageMultiplier : 1f) : _config.FistDamageMultiplier;
                PlayerBuffs buffs = armed ? _buffs : null;
                if (buffs != null && treant.IsActive)
                {
                    float multiplier = buffs.DamageMultiplier(treant.Element);
                    units *= multiplier;
                    // Spent before ServerHit so a killing blow can load a fresh charge without losing one.
                    buffs.ServerConsumeHit(treant.Element);
                }
                units *= ComboRules.Damage(step);
                treant.ServerHit(units, buffs, _wood, transform.position);
                if (step == ComboRules.Steps - 1 && treant.IsActive) treant.ServerStagger(_config.TreantStaggerSeconds * ComboRules.FinisherStagger * 1.5f, transform.position, true);
            }
            else if (foundTreeId >= 0)
            {
                bool counts = armed || FistRules.Accumulate(ref _punchProgress, _config.FistDamageMultiplier);
                // A better axe fells in fewer swings; punches add up to one plain hit.
                float power = armed && _upgrades != null ? _upgrades.AxeDamageMultiplier : 1f;
                TreeManager.Instance.ServerHitTree(foundTreeId, _wood, aim, counts, power);
            }
            return step;
        }

        // The owner already played its swing locally; on a host the owner is the local client, so it is excluded too.
        [ObserversRpc(ExcludeOwner = true)]
        private void SwingObserversRpc(int step)
        {
            ClientPlaySwing(step);
        }

        // Raised on every client whenever a swing starts (with its combo step, -1 for a tree chop), so the character
        // animator can play the matching strike.
        public event System.Action<int> SwingPlayed;
        // Raised on every client when the axe is drawn from the back or put there.
        public event System.Action StowPlayed;

        private void ClientPlaySwing(int step)
        {
            _swingStartTime = Time.time;
            SwingPlayed?.Invoke(step);
        }

        // ---------- Carrying the axe ----------

        private const float CarrySharpness = 14f;
        // After a swing starts, the chop animation owns the axe this long before it settles back into the carry.
        private const float SwingHoldSeconds = 0.9f;
        private Vector3 _headInGrip;
        private bool _headKnown;
        private float _carry;

        private HandGrip _grip;

        private void LateUpdate()
        {
            SetGrip();
            ApplyCarry();
            HoldInFist();
            TickStow();
            PlaceBackAxe();
        }

        // The grip point of the handle sits in the hollow of the right fist. While the axe hangs at rest the walk clip
        // leaves the hand open, so the hollow is the palm and the fingers are closed around it here. A swing or the
        // guard uses the curled fingers the pose already has.
        private void HoldInFist()
        {
            if (_axePivot == null || !_axePivot.gameObject.activeInHierarchy) return;
            Transform hand = _axePivot.parent;
            if (hand == null) return;
            if (_grip == null) _grip = new HandGrip(hand, "r");
            if (_grip.Valid) _axePivot.position = _grip.Center;
        }

        // The right fist: the animator's Grip layer (a humanoid fist pose on the right fingers) is on while the axe is
        // in the hand, fading with the drawing and holstering so the hand opens as it lets go.
        private Animator _animator;
        private int _gripLayer = -2;
        private float _gripWeight;

        private void SetGrip()
        {
            if (_gripLayer == -2)
            {
                _animator = GetComponentInChildren<Animator>();
                _gripLayer = _animator != null ? _animator.GetLayerIndex("Grip") : -1;
            }
            if (_gripLayer < 0) return;
            bool holding = (_axePivot != null && _axePivot.gameObject.activeInHierarchy) || (_mining != null && _mining.PickOut);
            _gripWeight = Mathf.MoveTowards(_gripWeight, holding ? 1f : 0f, Time.deltaTime * 6f);
            _animator.SetLayerWeight(_gripLayer, _gripWeight);
        }

        // ---------- On the back ----------

        // On the back the axe hangs the way the hand can reach it: the handle end up behind the right shoulder (where the
        // reach back animation puts the hand), the head down by the left hip, the blade flat against the back with the
        // edge toward the middle, tipped a little out at the bottom so the head clears the hips. Body frame: x right,
        // y up, z forward, from the upper spine bone, in meters.
        private static readonly Vector3 BackGripOffset = new Vector3(0.1f, 0.22f, -0.17f);
        private static readonly Vector3 BackHeadDirection = new Vector3(-0.35f, -1f, -0.12f);
        // Edge now means the blade (it used to point at the poll). This is the old direction flipped so the axe
        // still hangs the same way on the back.
        private static readonly Vector3 BackEdgeDirection = new Vector3(-1f, 0.5f, 0f);
        // Seconds of the reach back clip (CharacterSetup.ReachClipSeconds): the hand is over the shoulder halfway.
        private const float StowSeconds = 0.56f;

        private HeldItemFrame _frame;
        private bool _stowing;
        private bool _stowToBack;
        private float _stowStart;
        private float _clientStartTime;
        private bool _shownDrawn;

        private void BuildBackAxe()
        {
            if (_axePivot == null || _backAxe != null) return;
            _backBone = FindDeep(transform, "spine_03");
            if (_backBone == null) return;
            GameObject copy = Instantiate(_axePivot.gameObject, transform);
            copy.name = "AxeOnBack";
            foreach (MonoBehaviour behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(behaviour);
            foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true)) Destroy(collider);
            copy.transform.localScale = _axePivot.lossyScale.x / Mathf.Max(0.0001f, transform.lossyScale.x) * Vector3.one;
            _backAxe = copy.transform;
        }

        private void PlaceBackAxe()
        {
            if (_backAxe == null || !_backAxe.gameObject.activeSelf) return;
            if (BackPose(out Vector3 position, out Quaternion rotation)) _backAxe.SetPositionAndRotation(position, rotation);
        }

        // Where the grip goes on the back and how it is turned, this frame.
        private bool BackPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (_backBone == null || _axePivot == null) return false;
            if (_frame == null) _frame = _axePivot.GetComponent<HeldItemFrame>();
            if (_frame == null) return false;
            Quaternion body = transform.rotation;
            rotation = _frame.RotationFor(body * BackHeadDirection.normalized, body * BackEdgeDirection.normalized);
            position = _backBone.position + body * BackGripOffset;
            return true;
        }

        // While the hand reaches back: the axe rides in the hand until the hand is nearly over the shoulder, slides onto
        // the back as it arrives (or off the back as it leaves, when drawing), and the other copy stays hidden.
        private void TickStow()
        {
            if (!_stowing) return;
            float t = (Time.time - _stowStart) / StowSeconds;
            if (t >= 1f || _axePivot == null || !_hasAxe.Value)
            {
                ApplyAxeVisual();
                return;
            }
            if (!_axePivot.gameObject.activeSelf) _axePivot.gameObject.SetActive(true);
            if (_backAxe != null && _backAxe.gameObject.activeSelf) _backAxe.gameObject.SetActive(false);
            // The axe turns toward its back pose all the way up (or out of it all the way down), and only moves onto
            // the back (or off it) while the hand is close to the shoulder.
            float place = _stowToBack ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.5f, t))
                                      : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.7f, t));
            float turn = _stowToBack ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.5f, t))
                                     : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.95f, t));
            if (turn <= 0f || !BackPose(out Vector3 position, out Quaternion rotation)) return;
            _axePivot.SetPositionAndRotation(Vector3.Lerp(_axePivot.position, position, place),
                Quaternion.Slerp(_axePivot.rotation, rotation, turn));
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // The grip runs across the fist, which is right for the chop and the guard, but with the arm hanging at rest it
        // left the axe sticking straight out beside Chopper. Between swings the forearm rolls so the head points ahead,
        // and the wrist tips it down only a little (a full bend to hang the head straight down looked broken), and the
        // fingers close on the handle. A swing, the guard or a roll takes the hand back.
        private void ApplyCarry()
        {
            if (_useScriptedSwing || _axePivot == null || !_axePivot.gameObject.activeInHierarchy)
            {
                _carry = 0f;
                SetCarryLayer(0f);
                return;
            }
            Transform hand = _axePivot.parent;
            if (hand == null) return;
            // Chopper's own takes hold the axe head up and blade forward through the Weapon_R socket. Turning the wrist
            // here (built for the library clips) hung the head down and the edge the wrong way (user, 2026-10-12).
            if (_frame == null) _frame = _axePivot.GetComponent<HeldItemFrame>();
            if (_frame != null && _frame.HandPosed)
            {
                _carry = 0f;
                _axePivot.localRotation = _restRotation;
                SetCarryLayer(0f);
                return;
            }
            if (!_headKnown) MeasureHead();
            if (!_headKnown) return;

            bool busy = Time.time - _swingStartTime < SwingHoldSeconds || _stowing
                        || (_combat != null && (_combat.IsBlocking || _combat.GuardRaised || _combat.IsRolling));
            _axePivot.localRotation = _restRotation;
            SetCarryLayer(0f);
            if (!busy) TrySolveCarry(hand);
            _carry = Mathf.Lerp(_carry, busy || !_carrySolved ? 0f : 1f, 1f - Mathf.Exp(-CarrySharpness * Time.deltaTime));
            if (_carry <= 0.001f) return;
            // A fixed wrist turn in the hand's own space: head forward and a little down, edge down, the arm relaxed at
            // the side. Fixed, so the axe swings with the arm while running and never flips.
            Quaternion turn = Quaternion.Slerp(_carryTurn, _moveTurn, _moving);
            hand.localRotation = hand.localRotation * Quaternion.Slerp(Quaternion.identity, turn, _carry);
        }

        private Quaternion _carryTurn = Quaternion.identity;
        // The run holds the hand differently from the idle (the palm turns in and back), so while moving a second turn is
        // used: worked out every frame of the stride and followed slowly, so it settles on the stride's average and the
        // axe swings with the arm instead of trailing behind it. Blended in by how much the player is moving.
        private const float MoveTurnSeconds = 0.8f;
        private const float MoveBlendSeconds = 0.25f;
        private Quaternion _moveTurn = Quaternion.identity;
        private float _moving;

        // The wrist turn is worked out here, from the pose the game really plays: on the first frame the axe is in hand,
        // then again whenever the player stands still, eased toward so it never snaps. Moving keeps the last one and
        // blends to the moving turn above.
        private const float CarryDownDegrees = 18f;
        private const float CarryOutDegrees = 8f;
        private const float StillSpeed = 0.15f;
        private const float StillSeconds = 0.3f;
        private bool _carrySolved;
        private float _stillSince = -1f;
        private Vector3 _lastCarryPosition;

        private void TrySolveCarry(Transform hand)
        {
            Vector3 moved = transform.position - _lastCarryPosition;
            _lastCarryPosition = transform.position;
            moved.y = 0f;
            bool still = Time.deltaTime > 0f && moved.magnitude / Time.deltaTime < StillSpeed;
            _moving = Mathf.MoveTowards(_moving, still ? 0f : 1f, Time.deltaTime / MoveBlendSeconds);
            if (_frame == null) _frame = _axePivot.GetComponent<HeldItemFrame>();
            if (_frame == null) return;
            Quaternion solved = SolveCarry(hand.rotation, _restRotation, _frame, transform);
            if (!still)
            {
                _moveTurn = _moveTurn == Quaternion.identity ? solved : Quaternion.Slerp(_moveTurn, solved, 1f - Mathf.Exp(-Time.deltaTime / MoveTurnSeconds));
                _stillSince = -1f;
            }
            // The first frame in hand solves at once, so the axe is never held the raw way round; standing still refines it.
            if (_carrySolved)
            {
                if (!still) return;
                if (_stillSince < 0f) _stillSince = Time.time;
                if (Time.time - _stillSince < StillSeconds) return;
            }
            _carryTurn = _carrySolved ? Quaternion.Slerp(_carryTurn, solved, 1f - Mathf.Exp(-CarrySharpness * Time.deltaTime)) : solved;
            _carrySolved = true;
        }

        // The turn, in the hand's space, that takes the axe from where the hand holds it now to head forward and a
        // little down and out, edge down and a little forward.
        internal static Quaternion SolveCarry(Quaternion handNow, Quaternion gripLocal, HeldItemFrame frame, Transform body)
        {
            Vector3 forward = body.forward, up = body.up, right = body.right;
            Vector3 head = Quaternion.AngleAxis(CarryOutDegrees, up) * (Quaternion.AngleAxis(CarryDownDegrees, right) * forward);
            Vector3 edge = Vector3.ProjectOnPlane(-up + forward * 0.3f, head);
            Quaternion want = Quaternion.LookRotation(head, edge);
            Vector3 ownHead = handNow * (gripLocal * frame.Head);
            Vector3 ownEdge = Vector3.ProjectOnPlane(handNow * (gripLocal * frame.Edge), ownHead);
            Quaternion worldTurn = want * Quaternion.Inverse(Quaternion.LookRotation(ownHead, ownEdge));
            return Quaternion.Inverse(handNow) * worldTurn * handNow;
        }


        // The arm holding the axe up in front of the chest is the animator's CarryRight layer (the library's torch idle,
        // mirrored). Solving the hand's turn in code here flipped while running and hung the head at the ground.
        private int _carryLayer = -2;

        private void SetCarryLayer(float weight)
        {
            if (_carryLayer == -2)
            {
                if (_animator == null) _animator = GetComponentInChildren<Animator>();
                _carryLayer = _animator != null ? _animator.GetLayerIndex("CarryRight") : -1;
            }
            if (_carryLayer >= 0) _animator.SetLayerWeight(_carryLayer, weight);
        }

        // Where the axe's head lies from the grip, in the grip's own space at rest (from the mesh, so it works while hidden).
        private void MeasureHead()
        {
            Renderer renderer = _axePivot.GetComponentInChildren<Renderer>(true);
            if (renderer == null) return;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Vector3 center = filter != null && filter.sharedMesh != null ? renderer.transform.TransformPoint(filter.sharedMesh.bounds.center) : renderer.bounds.center;
            Quaternion now = _axePivot.localRotation;
            _axePivot.localRotation = _restRotation;
            Vector3 local = _axePivot.InverseTransformPoint(center);
            _axePivot.localRotation = now;
            if (local.sqrMagnitude < 0.0001f) return;
            _headInGrip = local.normalized;
            _headKnown = true;
        }

        // Chopper's own takes already swing the arm. Spinning the grip on top of that turned the blade off the chop
        // (a weird angle). The scripted arc is only for a rig whose hand does not pose the axe.
        private void AnimateSwing()
        {
            if (!_useScriptedSwing || _axePivot == null) return;
            if (_frame == null) _frame = _axePivot.GetComponent<HeldItemFrame>();
            if (_frame != null && _frame.HandPosed)
            {
                if (_axePivot.localRotation != _restRotation) _axePivot.localRotation = _restRotation;
                return;
            }
            float t = (Time.time - _swingStartTime) / _config.SwingAnimationDuration;
            if (t < 0f || t >= 1f)
            {
                if (_axePivot.localRotation != _restRotation) _axePivot.localRotation = _restRotation;
                return;
            }

            float angle = Mathf.Sin(t * Mathf.PI) * _config.SwingArcDegrees;
            _axePivot.localRotation = _restRotation * Quaternion.Euler(angle, 0f, 0f);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Debug only: F9 bypasses the client cooldown and sends a burst of swings so the server check can be seen.
        private const int DebugSpamCount = 5;

        public void DebugSpamServer()
        {
            if (!IsOwner) return;
            Vector3 aim = transform.forward;
            for (int i = 0; i < DebugSpamCount; i++) SwingAxeServerRpc(aim);
            Debug.Log($"[PlayerAxe] Debug sent {DebugSpamCount} swings ignoring the client cooldown.");
        }
#endif
    }
}
