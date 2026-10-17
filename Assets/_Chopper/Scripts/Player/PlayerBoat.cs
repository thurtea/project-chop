using Chopper.Core;
using Chopper.UI;
using Chopper.World;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Chopper.Player
{
    public class PlayerBoat : NetworkBehaviour
    {
        [SerializeField] private GameConfig _config;

        private readonly SyncVar<int> _boats = new SyncVar<int>();
        private readonly SyncVar<bool> _sharkRod = new SyncVar<bool>();
        private readonly SyncVar<bool> _out = new SyncVar<bool>();
        private readonly SyncVar<bool> _aboard = new SyncVar<bool>();
        private readonly SyncVar<Vector3> _boatPos = new SyncVar<Vector3>();
        private readonly SyncVar<float> _boatYaw = new SyncVar<float>();
        private readonly SyncVar<int> _sharks = new SyncVar<int>();

        public static PlayerBoat Local { get; private set; }

        public const float BoardReach = 3.2f;
        public const float RowSpeed = 4.2f;
        public const float TurnSpeed = 75f;
        public const float SeatHeight = 0.32f;
        public const float SharkSpawnDistance = 16f;
        public const float SharkApproachSpeed = 5f;

        public int Boats => _boats.Value;
        public bool HasSharkRod => _sharkRod.Value;
        public bool BoatOut => _out.Value;
        public bool Aboard => _aboard.Value;
        public Vector3 BoatPosition => _aboard.Value ? transform.position : _boatPos.Value;
        public int SharksLanded => _sharks.Value;
        public SharkFight Fight => _fight;
        public SharkStage Stage => _stage;
        public float ReelProgress => _reel;
        public bool Leaping => _leaping;
        public Transform SharkView => _shark;
        public string LastCue { get; private set; } = string.Empty;
        public float Steer { get; set; }
        public bool DriveByScript { get; set; }
        public Vector2 ScriptRow { get; set; }
        public bool ScriptReel { get; set; }
        public bool ScriptHold { get; set; }

        public enum SharkStage { None = 0, Waiting = 1, Approach = 2, Towing = 3, Reeling = 4 }

        private Transform _view;
        private Transform _shark;
        private CharacterController _controller;
        private SharkFight _fight;
        private SharkStage _stage;
        private float _stageAt;
        private float _reel;
        private bool _leaping;
        private float _nextLeap;
        private float _speed;
        private float _holdB;
        private int _seed;
        private Canvas _canvas;
        private RectTransform _arcRoot;
        private Image[] _dots;
        private Image _marker;
        private Image _stamina;
        private Image _reelBar;
        private Text _cue;
        private float _cueAt = -10f;

        private void Awake() => _controller = GetComponent<CharacterController>();

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner) Local = this;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Local == this) Local = null;
            if (_view != null) Destroy(_view.gameObject);
            if (_shark != null) Destroy(_shark.gameObject);
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        public static bool IsOcean(Vector3 p, float minDepth)
        {
            float r = new Vector2(p.x, p.z).magnitude;
            return r > Chopper.World.Generation.WorldGen.SeaInnerRadius && WaterSystem.DepthAt(p, out _) >= minDepth;
        }

        public static float SurfaceAt(Vector3 p)
        {
            WaterSystem.DepthAt(p, out float surface);
            return surface;
        }

        public bool OceanAhead(out Vector3 spot)
        {
            Vector3 f = transform.forward;
            f.y = 0f;
            f.Normalize();
            for (int i = 0; i < 4; i++)
            {
                spot = transform.position + f * (1.5f + i * 1.2f);
                if (IsOcean(spot, 0.6f))
                {
                    spot.y = SurfaceAt(spot);
                    return true;
                }
            }
            spot = transform.position;
            if (IsOcean(spot, 0.6f))
            {
                spot.y = SurfaceAt(spot);
                return true;
            }
            return false;
        }

        public int ServerChangeBoats(int delta)
        {
            int before = _boats.Value;
            _boats.Value = Mathf.Clamp(before + delta, 0, 1);
            return _boats.Value - before;
        }

        public void ServerGrantSharkRod() => _sharkRod.Value = true;

        public int ServerChangeSharkRod(int delta)
        {
            bool before = _sharkRod.Value;
            if (delta > 0) _sharkRod.Value = true;
            else if (delta < 0) _sharkRod.Value = false;
            return (_sharkRod.Value ? 1 : 0) - (before ? 1 : 0);
        }

        public void ServerLoad(int boats, bool sharkRod)
        {
            _boats.Value = Mathf.Clamp(boats, 0, 1);
            _sharkRod.Value = sharkRod;
            _out.Value = false;
            _aboard.Value = false;
        }

        public void RequestDeploy()
        {
            if (!OceanAhead(out Vector3 spot)) { Cue("Not on the ocean"); return; }
            DeployServerRpc(spot, transform.eulerAngles.y);
        }

        public void RequestBoard() => BoardServerRpc(true);
        public void RequestLeave() => BoardServerRpc(false);
        public void RequestPickUp() => PickUpServerRpc();
        public void RequestSharkRod() => SharkRodServerRpc();
        public void RequestDevBoat() => DevBoatServerRpc();

        [ServerRpc]
        private void DevBoatServerRpc() => _boats.Value = 1;

        [ServerRpc]
        private void SharkRodServerRpc()
        {
            _sharkRod.Value = true;
        }

        [ServerRpc]
        private void DeployServerRpc(Vector3 spot, float yaw)
        {
            if (!BoatRules.CanDeploy(_boats.Value > 0, IsOcean(spot, 0.6f), _out.Value)) return;
            _boats.Value--;
            _out.Value = true;
            _boatPos.Value = spot;
            _boatYaw.Value = yaw;
        }

        [ServerRpc]
        private void BoardServerRpc(bool board)
        {
            if (!_out.Value) return;
            if (board)
            {
                if (Flat(transform.position - _boatPos.Value) > BoardReach + 1f) return;
                _aboard.Value = true;
            }
            else
            {
                _boatPos.Value = new Vector3(transform.position.x, SurfaceAt(transform.position), transform.position.z);
                _boatYaw.Value = transform.eulerAngles.y;
                _aboard.Value = false;
            }
        }

        [ServerRpc]
        private void PickUpServerRpc()
        {
            if (!BoatRules.CanPickUp(_out.Value, _aboard.Value, Flat(transform.position - _boatPos.Value), BoardReach + 1f)) return;
            _out.Value = false;
            _boats.Value = Mathf.Min(1, _boats.Value + 1);
        }

        [ServerRpc]
        private void LandedServerRpc() => _sharks.Value++;

        private static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }

        private void Update()
        {
            UpdateView();
            if (!IsOwner) return;
            bool blocked = UiState.GameplayBlocked;
            Keyboard kb = Keyboard.current;
            if (!blocked && kb != null)
            {
                if (kb.bKey.isPressed) _holdB += Time.deltaTime;
                if (kb.bKey.wasReleasedThisFrame)
                {
                    if (_holdB < 0.5f) TapBoat();
                    _holdB = 0f;
                }
                if (_holdB >= 0.5f && _out.Value && !_aboard.Value)
                {
                    RequestPickUp();
                    _holdB = -10f;
                }
            }
            if (_aboard.Value) Ride(blocked);
            else if (_controller != null && _wasRiding)
            {
                _controller.enabled = true;
                _wasRiding = false;
                EndShark(string.Empty);
            }
            UpdateArc();
        }

        private bool _wasRiding;

        private void TapBoat()
        {
            if (_aboard.Value) { RequestLeave(); return; }
            if (_out.Value && Flat(transform.position - _boatPos.Value) <= BoardReach) { RequestBoard(); return; }
            if (!_out.Value && _boats.Value > 0) RequestDeploy();
        }

        private Vector2 RowInput()
        {
            if (DriveByScript) return ScriptRow;
            Vector2 v = Vector2.zero;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) v.y += 1f;
                if (kb.sKey.isPressed) v.y -= 1f;
                if (kb.dKey.isPressed) v.x += 1f;
                if (kb.aKey.isPressed) v.x -= 1f;
            }
            if (Gamepad.current != null) v += Gamepad.current.leftStick.ReadValue();
            return Vector2.ClampMagnitude(v, 1f);
        }

        public const float HullSink = 0.12f;
        public const float SeatAboveKeel = 0.47f;

        private float _hipHeight = -1f;
        private float _roll;
        private float _oarPhase;
        private float _rowAmount;
        private float _sharkYaw;
        private float _showUntil;
        private Vector3 _lastViewPos;
        private Transform _oarL;
        private Transform _oarR;
        private Transform _rod;
        private LineRenderer _line;
        private Animator _body;
        private readonly System.Collections.Generic.Dictionary<HumanBodyBones, Quaternion> _rest = new System.Collections.Generic.Dictionary<HumanBodyBones, Quaternion>();
        private static readonly HumanBodyBones[] PoseBones =
        {
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm
        };

        public bool Seated => _aboard.Value;
        public float HookDistance => _config != null ? _config.SharkHookDistance : 25f;
        public float SharkDistance => _shark != null ? Flat(_shark.position - transform.position) : 0f;
        public bool LineVisible => _line != null && _line.enabled;

        private float Bob() => Mathf.Sin(Time.time * 1.6f) * 0.035f;

        private float KeelY(Vector3 at) => SurfaceAt(at) - HullSink + Bob();

        private float FeetY(Vector3 at) => KeelY(at) + SeatAboveKeel - Mathf.Max(0.3f, _hipHeight > 0f ? _hipHeight : 0.9f);

        private void Ride(bool blocked)
        {
            if (!_wasRiding)
            {
                if (_controller != null) _controller.enabled = false;
                transform.position = new Vector3(_boatPos.Value.x, transform.position.y, _boatPos.Value.z);
                transform.rotation = Quaternion.Euler(0f, _boatYaw.Value, 0f);
                _speed = 0f;
                PlayerCamera cam0 = GetComponent<PlayerCamera>();
                if (cam0 != null) { cam0.DebugSetDistance(6.5f); cam0.DebugSetPitch(20f); cam0.SnapYaw(_boatYaw.Value); }
            }
            _wasRiding = true;
            if (_stage == SharkStage.Waiting || (_stage == SharkStage.None && Time.time >= _showUntil)) _camFar = 0f;
            if (_controller != null && _controller.enabled) _controller.enabled = false;
            Vector2 row = blocked && !DriveByScript ? Vector2.zero : RowInput();
            Vector3 p = transform.position;
            float turn = 0f;
            if (_stage == SharkStage.Towing || _stage == SharkStage.Reeling)
            {
                SharkTick(row);
                p = transform.position;
                _rowAmount = Mathf.MoveTowards(_rowAmount, 0f, Time.deltaTime);
            }
            else
            {
                turn = row.x * TurnSpeed * Mathf.Lerp(0.5f, 1f, Mathf.Abs(_speed) / RowSpeed + 0.3f);
                transform.Rotate(0f, turn * Time.deltaTime, 0f);
                _speed = Mathf.MoveTowards(_speed, row.y * RowSpeed, (Mathf.Abs(row.y) > 0.1f ? 2.2f : 0.9f) * Time.deltaTime);
                Vector3 next = p + transform.forward * _speed * Time.deltaTime;
                if (WaterSystem.DepthAt(next, out _) >= 0.5f) p = next;
                else _speed = 0f;
                _rowAmount = Mathf.MoveTowards(_rowAmount, Mathf.Max(Mathf.Abs(row.y), Mathf.Abs(row.x)), 3f * Time.deltaTime);
                if (_stage == SharkStage.Waiting) SharkWait();
                else if ((!blocked || DriveByScript) && HasSharkRod && IsOcean(p, 1.0f) && (Keybinds.WasPressed(KeyAction.Rod) || (DriveByScript && ScriptReel)))
                    StartShark();
            }
            _roll = Mathf.Lerp(_roll, -turn * 0.12f + Mathf.Sin(Time.time * 1.1f) * 2f, 3f * Time.deltaTime);
            p.y = FeetY(p);
            transform.position = p;
            FightCamera();
        }

        private float _camFar;
        private float _camLift;

        public float CameraFar => _camFar;

        public bool CameraHitsShark()
        {
            Camera view = Camera.main;
            if (view == null || _shark == null) return false;
            Renderer[] parts = _shark.GetComponentsInChildren<Renderer>();
            if (parts.Length == 0) return false;
            Bounds box = parts[0].bounds;
            foreach (Renderer r in parts) box.Encapsulate(r.bounds);
            box.Expand(0.8f);
            Vector3 eye = view.transform.position;
            Vector3 target = transform.position + Vector3.up * 1.2f;
            if (box.Contains(eye)) return true;
            Ray look = new Ray(eye, target - eye);
            return box.IntersectRay(look, out float hit) && hit < Vector3.Distance(eye, target) - 1f;
        }

        private void FightCamera()
        {
            PlayerCamera cam = GetComponent<PlayerCamera>();
            if (cam == null) return;
            bool fighting = _stage == SharkStage.Towing || _stage == SharkStage.Reeling || Time.time < _showUntil;
            if (!fighting) return;
            float far = _shark != null ? Mathf.Clamp(SharkDistance * 0.55f + 6f, 12f, 20f) : 12f;
            _camFar = Mathf.Max(_camFar, far);
            _camLift = Mathf.MoveTowards(_camLift, CameraHitsShark() ? 25f : 0f, (CameraHitsShark() ? 60f : 10f) * Time.deltaTime);
            cam.DebugSetDistance(_camFar);
            cam.DebugSetPitch(Mathf.Lerp(18f, 26f, Mathf.InverseLerp(7f, 20f, _camFar)) + _camLift);
            if (_stage == SharkStage.None)
            {
                cam.DebugSetPitch(30f + _camLift);
                cam.SnapYaw(transform.eulerAngles.y + 60f);
                return;
            }
            if (_shark != null)
            {
                Vector3 to = _shark.position - transform.position;
                cam.SnapYaw(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            }
        }

        public void StartShark()
        {
            if (!SharkRules.CanHook(HasSharkRod, _aboard.Value && IsOcean(transform.position, 1.0f))) { Cue("Needs the shark rod, out at sea"); return; }
            _stage = SharkStage.Waiting;
            _stageAt = Time.time;
            _seed = Random.Range(0, 1000);
            Cue("Line out");
        }

        private void SharkWait()
        {
            if (Time.time - _stageAt < 2f) return;
            _sharkYaw = transform.eulerAngles.y;
            Vector3 dir = Quaternion.Euler(0f, _sharkYaw, 0f) * Vector3.forward;
            Vector3 at = transform.position + dir * HookDistance;
            at.y = SurfaceAt(transform.position) - 1.4f;
            SpawnShark(at);
            Splash(new Vector3(at.x, SurfaceAt(transform.position), at.z), 14);
            _stage = SharkStage.Towing;
            _fight = SharkFight.Start();
            _stageAt = Time.time;
            Cue("Hooked a shark!");
        }

        private void SpawnShark(Vector3 at)
        {
            if (_shark != null) Destroy(_shark.gameObject);
            GameObject prefab = Resources.Load<GameObject>("Props/prop_fish_voxel");
            GameObject go = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Shark";
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            go.transform.localScale = Vector3.one * 6f;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", new Color(0.36f, 0.42f, 0.5f));
                r.SetPropertyBlock(block);
            }
            GameObject fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(fin.GetComponent<Collider>());
            fin.transform.SetParent(go.transform, false);
            fin.transform.localScale = new Vector3(0.025f, 0.16f, 0.1f);
            fin.transform.localPosition = new Vector3(0f, 0.1f, -0.01f);
            fin.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);
            Paint(fin, new Color(0.28f, 0.32f, 0.38f));
            var wake = go.AddComponent<TrailRenderer>();
            wake.time = 1.4f;
            wake.startWidth = 1.1f;
            wake.endWidth = 0.1f;
            wake.sharedMaterial = WorldMaterial();
            wake.startColor = new Color(1f, 1f, 1f, 0.75f);
            wake.endColor = new Color(1f, 1f, 1f, 0f);
            wake.alignment = LineAlignment.TransformZ;
            go.transform.position = at;
            _shark = go.transform;
        }

        private Vector3 SharkSpot(float distance, float lift)
        {
            Vector3 dir = Quaternion.Euler(0f, _sharkYaw, 0f) * Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 at = transform.position + dir * distance + side * _fight.Offset * distance * 0.35f;
            at.y = SurfaceAt(transform.position) - 0.22f + lift;
            return at;
        }

        private void FaceShark(Vector3 at, float rate)
        {
            if (_shark == null) return;
            Vector3 heading = at - _shark.position;
            heading.y = 0f;
            Vector3 dir = Quaternion.Euler(0f, _sharkYaw, 0f) * Vector3.forward;
            _shark.rotation = Quaternion.Slerp(_shark.rotation, Quaternion.LookRotation(heading.sqrMagnitude > 0.01f ? heading.normalized : dir), rate * Time.deltaTime);
        }

        private void SharkTick(Vector2 row)
        {
            SharkTuning tuning = SharkTuning.From(_config);
            if (_stage == SharkStage.Towing)
            {
                float steer = DriveByScript ? Steer : row.x;
                float pull = SharkRules.SharkPull(_fight.Time, _seed);
                SharkRules.Step(ref _fight, steer, pull, Time.deltaTime, tuning);
                _sharkYaw += pull * 14f * Time.deltaTime;
                Vector3 toShark = _shark != null ? _shark.position - transform.position : transform.forward;
                toShark.y = 0f;
                Vector3 tow = SharkRules.Tow(toShark.sqrMagnitude > 0.01f ? toShark : transform.forward, _fight, tuning, Time.deltaTime);
                Vector3 next = transform.position + tow;
                if (WaterSystem.DepthAt(next, out _) >= 0.5f) transform.position = next;
                if (toShark.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toShark.normalized), 40f * Time.deltaTime);
                if (_shark != null)
                {
                    float rise = Mathf.Clamp01((Time.time - _stageAt) / 1.2f);
                    Vector3 want = SharkSpot(HookDistance, Mathf.Lerp(-1.2f, 0f, rise));
                    FaceShark(want + (want - transform.position).normalized * 3f, 3f);
                    _shark.position = Vector3.Lerp(_shark.position, want, 3f * Time.deltaTime);
                }
                if (_fight.Phase == SharkPhase.Lost) { EndShark("Got away", false); return; }
                if (_fight.Phase == SharkPhase.Reeling)
                {
                    _stage = SharkStage.Reeling;
                    _reel = 0f;
                    _nextLeap = Time.time + 1.5f;
                    Cue("Tired! Reel it in");
                }
                return;
            }
            bool reel = DriveByScript ? ScriptReel : Keybinds.IsHeld(KeyAction.Attack);
            bool hold = DriveByScript ? ScriptHold : Keybinds.IsHeld(KeyAction.Sprint);
            if (!_leaping && Time.time >= _nextLeap && _reel < 0.75f)
            {
                _leaping = true;
                _stageAt = Time.time;
                if (_shark != null) Splash(new Vector3(_shark.position.x, SurfaceAt(transform.position), _shark.position.z), 12);
                Cue("Leap! Hold");
            }
            float distance = Mathf.Lerp(HookDistance, 3.2f, _reel * _reel);
            float jump = 0f;
            if (_leaping)
            {
                float t = Mathf.Clamp01((Time.time - _stageAt) / 1.1f);
                jump = Mathf.Sin(t * Mathf.PI) * 3f;
                if (reel && !hold && Time.time - _stageAt > SharkRules.LeapGrace) { EndShark("Snapped", false); return; }
                if (t >= 1f)
                {
                    _leaping = false;
                    _nextLeap = Time.time + 2.5f + Random.value * 1.5f;
                    if (_shark != null) Splash(new Vector3(_shark.position.x, SurfaceAt(transform.position), _shark.position.z), 16);
                }
            }
            else
            {
                _reel = Mathf.Clamp01(_reel + (reel ? 0.16f : -0.03f) * Time.deltaTime);
            }
            Vector3 toS = _shark != null ? _shark.position - transform.position : transform.forward;
            toS.y = 0f;
            if (toS.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toS.normalized), 40f * Time.deltaTime);
            if (_shark != null)
            {
                Vector3 want = SharkSpot(distance, jump);
                FaceShark(transform.position, 2f);
                if (_leaping) _shark.rotation = _shark.rotation * Quaternion.Euler(-Mathf.Cos(Mathf.Clamp01((Time.time - _stageAt) / 1.1f) * Mathf.PI) * 30f * Time.deltaTime, 0f, 0f);
                _shark.position = Vector3.Lerp(_shark.position, want, 5f * Time.deltaTime);
            }
            if (_reel >= 1f)
            {
                LandedServerRpc();
                EndShark("Shark caught!", true);
            }
        }

        private void EndShark(string word, bool caught)
        {
            _stage = SharkStage.None;
            _leaping = false;
            if (caught && _shark != null)
            {
                Vector3 side = transform.right * 1.7f;
                Vector3 at = transform.position + side;
                at.y = SurfaceAt(transform.position) - 0.05f;
                _shark.position = at;
                _shark.rotation = Quaternion.LookRotation(transform.forward) * Quaternion.Euler(0f, 0f, 70f);
                TrailRenderer wake = _shark.GetComponent<TrailRenderer>();
                if (wake != null) wake.emitting = false;
                Splash(at, 10);
                _showUntil = Time.time + 3f;
                Destroy(_shark.gameObject, 3f);
                _shark = null;
            }
            else if (_shark != null)
            {
                Destroy(_shark.gameObject);
                _shark = null;
            }
            if (word.Length > 0) Cue(word);
        }

        private void EndShark(string word) => EndShark(word, false);

        private static Material _world;

        private static Material WorldMaterial()
        {
            if (_world != null) return _world;
            GameObject prefab = Resources.Load<GameObject>("Props/prop_boat_voxel");
            Renderer r = prefab != null ? prefab.GetComponentInChildren<Renderer>() : null;
            _world = r != null ? r.sharedMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            return _world;
        }

        private static void Paint(GameObject go, Color color)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r == null) return;
            r.sharedMaterial = WorldMaterial();
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            r.SetPropertyBlock(block);
        }

        private void Splash(Vector3 at, int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(drop.GetComponent<Collider>());
                drop.transform.position = at + Random.insideUnitSphere * 0.6f;
                drop.transform.localScale = Vector3.one * Random.Range(0.12f, 0.26f);
                Paint(drop, new Color(0.92f, 0.97f, 1f));
                var body = drop.AddComponent<Rigidbody>();
                body.linearVelocity = new Vector3(Random.Range(-2f, 2f), Random.Range(3f, 6f), Random.Range(-2f, 2f));
                Destroy(drop, 1.1f);
            }
        }

        private void Cue(string word)
        {
            LastCue = word;
            _cueAt = Time.time;
            Debug.Log("[Boat] " + word);
        }

        private void UpdateView()
        {
            if (!_out.Value)
            {
                if (_view != null) _view.gameObject.SetActive(false);
                return;
            }
            if (_view == null)
            {
                GameObject prefab = Resources.Load<GameObject>("Props/prop_boat_voxel");
                GameObject go = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Boat";
                foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
                _view = go.transform;
                _oarL = Oar(-1f);
                _oarR = Oar(1f);
            }
            _view.gameObject.SetActive(true);
            Vector3 b = _aboard.Value ? transform.position : _boatPos.Value;
            float yaw = _aboard.Value ? transform.eulerAngles.y : _boatYaw.Value;
            float roll = _aboard.Value ? _roll : Mathf.Sin(Time.time * 1.1f) * 2f;
            float pitch = Mathf.Sin(Time.time * 0.9f + 1f) * 1.2f;
            Vector3 pos = new Vector3(b.x, KeelY(b), b.z);
            _view.SetPositionAndRotation(pos, Quaternion.Euler(pitch, yaw, roll));
            float moved = Flat(pos - _lastViewPos) / Mathf.Max(0.001f, Time.deltaTime);
            _lastViewPos = pos;
            float stroke = _aboard.Value ? Mathf.Max(_rowAmount, IsOwner ? 0f : Mathf.Clamp01(moved / RowSpeed)) : 0f;
            if (_stage == SharkStage.Towing || _stage == SharkStage.Reeling) stroke = 0f;
            _oarPhase += stroke * Time.deltaTime * 5.5f;
            float sweep = Mathf.Sin(_oarPhase) * 32f * stroke;
            float dip = Mathf.Cos(_oarPhase) * 10f * stroke;
            _oarL.localRotation = Quaternion.Euler(-12f + dip, 90f + sweep, 0f);
            _oarR.localRotation = Quaternion.Euler(-12f + dip, -90f - sweep, 0f);
            UpdateRodLine();
        }

        private Transform Oar(float side)
        {
            var pivot = new GameObject(side < 0f ? "OarL" : "OarR").transform;
            pivot.SetParent(_view, false);
            pivot.localPosition = new Vector3(side * 0.82f, 0.56f, 0.05f);
            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(shaft.GetComponent<Collider>());
            shaft.transform.SetParent(pivot, false);
            shaft.transform.localScale = new Vector3(0.07f, 0.07f, 2.1f);
            shaft.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            Paint(shaft, new Color(0.55f, 0.36f, 0.2f));
            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(blade.GetComponent<Collider>());
            blade.transform.SetParent(pivot, false);
            blade.transform.localScale = new Vector3(0.24f, 0.04f, 0.5f);
            blade.transform.localPosition = new Vector3(0f, 0f, 1.5f);
            Paint(blade, new Color(0.62f, 0.42f, 0.24f));
            return pivot;
        }

        private void UpdateRodLine()
        {
            bool fishing = _aboard.Value && _shark != null && (_stage == SharkStage.Towing || _stage == SharkStage.Reeling);
            if (_rod == null && fishing)
            {
                GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(rod.GetComponent<Collider>());
                rod.name = "SharkRod";
                Paint(rod, new Color(0.25f, 0.18f, 0.12f));
                _rod = rod.transform;
                var lineGo = new GameObject("SharkLine");
                _line = lineGo.AddComponent<LineRenderer>();
                _line.positionCount = 2;
                _line.startWidth = 0.035f;
                _line.endWidth = 0.035f;
                _line.sharedMaterial = WorldMaterial();
                _line.startColor = new Color(0.95f, 0.95f, 0.9f);
                _line.endColor = new Color(0.95f, 0.95f, 0.9f);
            }
            if (_rod == null) return;
            _rod.gameObject.SetActive(fishing);
            _line.enabled = fishing;
            if (!fishing) return;
            Vector3 toShark = _shark.position - transform.position;
            toShark.y = 0f;
            Vector3 fwd = toShark.sqrMagnitude > 0.01f ? toShark.normalized : transform.forward;
            Vector3 grip = transform.position + Vector3.up * (Mathf.Max(0.3f, _hipHeight) + 0.35f) + transform.forward * 0.35f;
            Vector3 tip = grip + (fwd * 0.8f + Vector3.up * 1.0f).normalized * 1.9f;
            _rod.position = (grip + tip) * 0.5f;
            _rod.rotation = Quaternion.LookRotation(tip - grip);
            _rod.localScale = new Vector3(0.05f, 0.05f, 1.9f);
            _line.SetPosition(0, tip);
            _line.SetPosition(1, _shark.position + Vector3.up * 0.2f);
        }

        private void LateUpdate()
        {
            if (!_aboard.Value) { _rest.Clear(); return; }
            if (_body == null) _body = GetComponentInChildren<Animator>();
            if (_body == null || !_body.isHuman) return;
            Transform hips = _body.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) return;
            if (_rest.Count == 0)
            {
                if (_hipHeight < 0f) _hipHeight = hips.position.y - transform.position.y;
                foreach (HumanBodyBones bone in PoseBones)
                {
                    Transform t = _body.GetBoneTransform(bone);
                    if (t != null) _rest[bone] = t.localRotation;
                }
            }
            Vector3 right = transform.right;
            float stroke = Mathf.Sin(_oarPhase);
            Vector3 fwd = transform.forward;
            Vector3 down = -transform.up;
            foreach (HumanBodyBones bone in PoseBones)
            {
                Transform t = _body.GetBoneTransform(bone);
                if (t == null || !_rest.TryGetValue(bone, out Quaternion rest)) continue;
                t.localRotation = rest;
                float angle;
                switch (bone)
                {
                    case HumanBodyBones.LeftUpperLeg:
                    case HumanBodyBones.RightUpperLeg: angle = -88f; break;
                    case HumanBodyBones.LeftLowerLeg:
                    case HumanBodyBones.RightLowerLeg: angle = 92f; break;
                    case HumanBodyBones.LeftUpperArm:
                    case HumanBodyBones.RightUpperArm:
                    case HumanBodyBones.LeftLowerArm:
                    case HumanBodyBones.RightLowerArm:
                        bool left = bone == HumanBodyBones.LeftUpperArm || bone == HumanBodyBones.LeftLowerArm;
                        bool upper = bone == HumanBodyBones.LeftUpperArm || bone == HumanBodyBones.RightUpperArm;
                        float pull = stroke * _rowAmount;
                        Vector3 side = right * (left ? -1f : 1f);
                        Vector3 aim = upper ? fwd * (0.55f + 0.35f * pull) + down * 0.75f + side * 0.25f : fwd * (0.9f + 0.3f * pull) + down * 0.15f + side * 0.1f;
                        AimBone(t, bone, aim.normalized);
                        continue;
                    default: angle = 0f; break;
                }
                t.rotation = Quaternion.AngleAxis(angle, right) * t.rotation;
            }
        }

        private void AimBone(Transform t, HumanBodyBones bone, Vector3 aim)
        {
            HumanBodyBones child = bone == HumanBodyBones.LeftUpperArm ? HumanBodyBones.LeftLowerArm
                : bone == HumanBodyBones.RightUpperArm ? HumanBodyBones.RightLowerArm
                : bone == HumanBodyBones.LeftLowerArm ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            Transform end = _body.GetBoneTransform(child);
            if (end == null) return;
            Vector3 now = end.position - t.position;
            if (now.sqrMagnitude < 0.0001f) return;
            t.rotation = Quaternion.FromToRotation(now.normalized, aim) * t.rotation;
        }

        private void BuildArc()
        {
            var go = new GameObject("SharkArc");
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 40;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _arcRoot = new GameObject("Arc", typeof(RectTransform)).GetComponent<RectTransform>();
            _arcRoot.SetParent(go.transform, false);
            _arcRoot.sizeDelta = new Vector2(10f, 10f);
            _dots = new Image[25];
            for (int i = 0; i < _dots.Length; i++)
            {
                var d = new GameObject("Dot", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                d.rectTransform.SetParent(_arcRoot, false);
                d.rectTransform.sizeDelta = new Vector2(14f, 14f);
                d.rectTransform.anchoredPosition = ArcRules.Point(i / (float)(_dots.Length - 1), 150f);
                _dots[i] = d;
            }
            _marker = new GameObject("Marker", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _marker.rectTransform.SetParent(_arcRoot, false);
            _marker.rectTransform.sizeDelta = new Vector2(30f, 30f);
            _marker.color = new Color(1f, 0.95f, 0.75f);
            _stamina = Bar(new Vector2(0f, 110f), new Color(0.85f, 0.25f, 0.2f));
            _reelBar = Bar(new Vector2(0f, 80f), new Color(0.3f, 0.9f, 0.45f));
            _cue = new GameObject("Cue", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            _cue.rectTransform.SetParent(_arcRoot, false);
            _cue.rectTransform.sizeDelta = new Vector2(700f, 70f);
            _cue.rectTransform.anchoredPosition = new Vector2(0f, 210f);
            _cue.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _cue.fontSize = 46;
            _cue.alignment = TextAnchor.MiddleCenter;
            _cue.color = new Color(0.98f, 0.86f, 0.5f);
            _cue.gameObject.AddComponent<Outline>().effectColor = Color.black;
        }

        private Image Bar(Vector2 at, Color color)
        {
            var back = new GameObject("BarBack", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            back.rectTransform.SetParent(_arcRoot, false);
            back.rectTransform.sizeDelta = new Vector2(240f, 14f);
            back.rectTransform.anchoredPosition = at;
            back.color = new Color(0f, 0f, 0f, 0.6f);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fill.rectTransform.SetParent(back.rectTransform, false);
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(1f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            fill.color = color;
            return fill;
        }

        private void UpdateArc()
        {
            bool fighting = _stage == SharkStage.Towing || _stage == SharkStage.Reeling;
            bool cue = Time.time - _cueAt < 1.8f;
            if (!fighting && !cue)
            {
                if (_canvas != null && _canvas.enabled) _canvas.enabled = false;
                return;
            }
            if (_canvas == null) BuildArc();
            _canvas.enabled = true;
            Camera cam = Camera.main;
            Vector3 anchor = _shark != null ? _shark.position + Vector3.up * 1.2f : transform.position + transform.forward * 6f + Vector3.up * 2f;
            if (cam != null)
            {
                Vector3 s = cam.WorldToScreenPoint(anchor);
                if (s.z > 0f)
                {
                    float k = 1080f / Mathf.Max(1f, Screen.height);
                    _arcRoot.anchoredPosition = new Vector2((s.x - Screen.width * 0.5f) * k, Mathf.Clamp((s.y - Screen.height * 0.5f) * k, -300f, 260f));
                }
            }
            SharkTuning t = SharkTuning.From(_config);
            for (int i = 0; i < _dots.Length; i++)
            {
                float off = ArcRules.Offset(i / (float)(_dots.Length - 1));
                bool centre = ArcRules.Within(off, t.CentreBand);
                bool edge = Mathf.Abs(off) >= t.LoseOffset - 0.08f;
                _dots[i].color = !fighting || _stage == SharkStage.Reeling ? new Color(1f, 1f, 1f, 0.15f)
                    : centre ? new Color(0.3f, 1f, 0.4f, 0.95f) : edge ? new Color(1f, 0.2f, 0.15f, 0.95f) : new Color(1f, 0.7f, 0.2f, 0.85f);
            }
            _marker.enabled = _stage == SharkStage.Towing;
            _marker.rectTransform.anchoredPosition = ArcRules.Point(ArcRules.FromOffset(_fight.Offset), 150f);
            _stamina.transform.parent.gameObject.SetActive(fighting);
            _stamina.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(_fight.Stamina), 1f);
            _reelBar.transform.parent.gameObject.SetActive(_stage == SharkStage.Reeling);
            _reelBar.rectTransform.anchorMax = new Vector2(_reel, 1f);
            _reelBar.color = _leaping ? new Color(1f, 0.3f, 0.2f) : new Color(0.3f, 0.9f, 0.45f);
            _cue.text = cue ? LastCue : string.Empty;
        }
    }
}
