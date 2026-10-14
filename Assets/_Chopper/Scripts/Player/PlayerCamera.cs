using Chopper.Core;
using Chopper.UI;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Chopper.Player
{
    // Owner-only third person camera with the PC Valheim scheme: the mouse always steers the camera (the cursor
    // is locked to the window), the scroll wheel zooms, and the camera trails smoothly behind the player.
    // Movement is relative to the camera yaw (see PlayerMotor). Escape frees the cursor for menus and locks it again.
    // The camera child starts inactive in the prefab and is only activated on the owning client, which also
    // hides the World overview camera while it is in use.
    public class PlayerCamera : NetworkBehaviour
    {
        private const string LookActionName = "Player/Look";
        // A camera this close to the ground clips through the terrain, so it is kept slightly above it.
        private const float MinHeightAboveTerrain = 0.4f;
        private const float MinHeightAboveWater = 0.35f;
        // Mouse wheel values from the Input System come in steps of about 120; only the sign matters.
        private const float ScrollDeadZone = 0.01f;
        // The camera stops in front of walls, trees and the hut instead of passing through them.
        private const float CameraRadius = 0.3f;
        private const float CameraWallMargin = 0.15f;
        private const float MinCameraDistance = 0.6f;
        // Over the right shoulder, like Valheim: the camera sits this far to the right of the player, so the
        // character stands a little left of the crosshair instead of blocking it.
        private const float ShoulderOffset = 0.6f;
        private float _shoulder = ShoulderOffset;

        [SerializeField] private GameConfig _config;
        [SerializeField] private Camera _camera;

        private InputAction _look;
        private GameObject _sceneCamera;
        private float _yaw;
        private float _pitch;
        private float _targetDistance;
        private float _distance;
        private Vector3 _smoothedPivot;
        // How far the camera may sit after wall collision, eased outward (see UpdateCameraTransform).
        private float _collisionDistance = 100f;
        private const float CameraReturnSpeed = 3f;
        private float _shake;
        private const float KickDecay = 22f;
        private const float KickPitchPerMeter = 20f;
        private float _kick;
        private Vector3 _kickDirection;
        private float _crouchBlend;
        private PlayerMotor _motor;
        // While flying (developer console) the camera may pull much further back to look over the land.
        private const float FlyingMaxDistance = 60f;

        public static PlayerCamera Local { get; private set; }

        public float Yaw => _yaw;
        public Camera View => _camera;

        // Development and screenshots: puts the camera at a distance from the player at once.
        public void DebugSetDistance(float meters)
        {
            _targetDistance = Mathf.Clamp(meters, _config.CameraMinDistance, MaxDistance);
            _distance = _targetDistance;
        }

        // Development and screenshots: tilts the camera (negative looks up), within the normal limits.
        public void DebugSetPitch(float degrees)
        {
            _pitch = Mathf.Clamp(degrees, _config.CameraMinPitch, _config.CameraMaxPitch);
        }

        // Points the camera along a world yaw and puts it straight behind the player (teleports, screenshots).
        public void SnapYaw(float yaw)
        {
            _yaw = yaw;
            _smoothedPivot = PivotPosition();
            _distance = _targetDistance;
            _collisionDistance = 100f;
        }

        // Adds camera shake in meters; the strongest recent value wins so rapid hits do not stack up.
        public void AddShake(float magnitude)
        {
            _shake = Mathf.Max(_shake, magnitude);
        }

        public void AddKick(Vector3 direction, float meters)
        {
            direction.y = 0f;
            if (meters <= _kick || direction.sqrMagnitude < 0.0001f) return;
            _kick = meters;
            _kickDirection = direction.normalized;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner) return;

            _motor = GetComponent<PlayerMotor>();
            _yaw = transform.eulerAngles.y;
            _pitch = _config.CameraDefaultPitch;
            _targetDistance = _config.CameraDistance;
            _distance = _targetDistance;
            _smoothedPivot = PivotPosition();
            _look = InputSystem.actions.FindAction(LookActionName, true);
            _look.Enable();
            Local = this;

            Camera current = Camera.main;
            if (current != null && current != _camera)
            {
                _sceneCamera = current.gameObject;
                _sceneCamera.SetActive(false);
            }
            _camera.gameObject.SetActive(true);
            // 0.3 m near plane corners stick out past the 0.3 m wall sphere at this field of view, so tight
            // orbits clip through stair treads. 0.1 m keeps those corners inside the sphere.
            _camera.nearClipPlane = 0.1f;
            ApplyCursor();
            UserSettings.Apply();
            UpdateCameraTransform();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!IsOwner) return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (Local == this) Local = null;
            _look = null;
            if (!_camera.gameObject.activeSelf) return;

            _camera.gameObject.SetActive(false);
            if (_sceneCamera != null) _sceneCamera.SetActive(true);
            _sceneCamera = null;
        }

        // The cursor is locked for looking around, and freed whenever a menu is open.
        private static void ApplyCursor()
        {
            bool locked = !UiState.InputBlocked;
            CursorLockMode wanted = locked ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != wanted) Cursor.lockState = wanted;
            if (Cursor.visible == locked) Cursor.visible = !locked;
        }

        private void LateUpdate()
        {
            if (!IsOwner || _look == null) return;

            ApplyCursor();
            bool blocked = UiState.InputBlocked;
            Vector2 look = _look.ReadValue<Vector2>();
            bool fromPointer = _look.activeControl != null && _look.activeControl.device is Pointer;
            if (blocked) look = Vector2.zero;
            else if (fromPointer) look *= _config.MouseLookSensitivity * UserSettings.Sensitivity;
            else
            {
                look *= _config.StickLookSpeed * UserSettings.PadSensitivity * Time.deltaTime;
                if (UserSettings.InvertPadY) look.y = -look.y;
            }

            _yaw += look.x;
            _pitch = Mathf.Clamp(_pitch - look.y, _config.CameraMinPitch, _config.CameraMaxPitch);

            // With the hammer out the wheel turns the piece (PlayerBuilder) instead of zooming.
            if (Mouse.current != null && !blocked && !PlayerBuilder.LocalBuilding)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > ScrollDeadZone)
                {
                    float step = _config.CameraZoomStep * (_targetDistance > _config.CameraMaxDistance ? 4f : 1f);
                    _targetDistance = Mathf.Clamp(_targetDistance - Mathf.Sign(scroll) * step, _config.CameraMinDistance, MaxDistance);
                }
            }

            Gamepad pad = Gamepad.current;
            if (pad != null && !blocked)
            {
                // D-pad down steps the camera out, and from the farthest it comes back to the closest. The rest of the
                // D-pad is the hotbar (PadHotbar).
                if (PadHotbar.ZoomPressed)
                {
                    _targetDistance = _targetDistance >= MaxDistance - 0.01f
                        ? _config.CameraMinDistance
                        : Mathf.Min(_targetDistance + _config.CameraZoomStep, MaxDistance);
                }
            }
            // Landing brings a far flying camera back in.
            if (_targetDistance > MaxDistance) _targetDistance = MaxDistance;

            UpdateCameraTransform();
        }

        private float MaxDistance => _motor != null && _motor.IsFlying ? FlyingMaxDistance : _config.CameraMaxDistance;

        private Vector3 PivotPosition()
        {
            float height = Mathf.Lerp(_config.CameraPivotHeight, _config.CameraCrouchPivotHeight, _crouchBlend);
            return transform.position + Vector3.up * height;
        }

        private void UpdateCameraTransform()
        {
            float dt = Time.deltaTime;
            float crouchTarget = _motor != null && _motor.IsCrouching ? 1f : 0f;
            _crouchBlend = Mathf.MoveTowards(_crouchBlend, crouchTarget, dt * 6f);
            // Exponential smoothing keeps the trailing feel the same at any frame rate.
            _distance = Mathf.Lerp(_distance, _targetDistance, 1f - Mathf.Exp(-_config.CameraZoomSmoothing * dt));
            _smoothedPivot = Vector3.Lerp(_smoothedPivot, PivotPosition(), 1f - Mathf.Exp(-_config.CameraFollowSpeed * dt));

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 backward = rotation * Vector3.back;

            // Slide out to the shoulder, but not into a wall beside the player; ease back out like the distance below.
            Vector3 right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
            float shoulder = ShoulderOffset;
            if (Physics.SphereCast(_smoothedPivot, CameraRadius, right, out RaycastHit sideHit, ShoulderOffset, ~0, QueryTriggerInteraction.Ignore))
            {
                shoulder = Mathf.Max(0f, sideHit.distance - CameraWallMargin);
            }
            _shoulder = shoulder < _shoulder ? shoulder : Mathf.MoveTowards(_shoulder, shoulder, CameraReturnSpeed * dt);
            Vector3 pivot = _smoothedPivot + right * _shoulder;

            float wanted = _distance;
            if (Physics.SphereCast(pivot, CameraRadius, backward, out RaycastHit hit, _distance, ~0, QueryTriggerInteraction.Ignore))
            {
                wanted = Mathf.Max(MinCameraDistance, hit.distance - CameraWallMargin);
            }
            // Pull in at once when a wall comes between, ease back out when it clears. In tight rooms the cast flips
            // between hit and miss from frame to frame, and following it directly made the view pop and flicker.
            _collisionDistance = wanted < _collisionDistance
                ? wanted
                : Mathf.MoveTowards(_collisionDistance, wanted, CameraReturnSpeed * dt);
            float distance = Mathf.Min(_distance, _collisionDistance);
            Vector3 position = pivot + backward * distance;

            bool hasGround = Chopper.World.WorldGround.Exists;
            // Inside a crypt the land is far overhead; the walls and ceiling stop the camera instead.
            if (hasGround && !Chopper.World.Generation.CryptWorld.IsUnderground(_smoothedPivot))
            {
                float minY = Chopper.World.WorldGround.HeightAt(position) + MinHeightAboveTerrain;
                if (position.y < minY) position.y = minY;
                // Nobody dives, so the camera never goes under the water either: looking up while swimming swung it
                // below the surface, where the pond turned into a grey wall.
                if (Chopper.World.WaterSystem.DepthAt(position, out float surfaceY) > 0f && position.y < surfaceY + MinHeightAboveWater)
                {
                    position.y = surfaceY + MinHeightAboveWater;
                }
            }

            if (_shake > 0f)
            {
                position += Random.insideUnitSphere * _shake;
                _shake = Mathf.MoveTowards(_shake, 0f, _config.ShakeRecoverySpeed * dt);
            }
            if (_kick > 0f)
            {
                position += _kickDirection * _kick;
                rotation = Quaternion.Euler(_pitch + _kick * KickPitchPerMeter, _yaw, 0f);
                _kick *= Mathf.Exp(-KickDecay * dt);
                if (_kick < 0.001f) _kick = 0f;
            }
            _camera.fieldOfView = UserSettings.FieldOfView;
            _camera.transform.SetPositionAndRotation(position, rotation);
        }
    }
}
