using Chopper.Audio;
using Chopper.Core;
using Chopper.UI;
using Chopper.World;
using Chopper.World.Building;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Chopper.Player
{
    // The hammer: building mode, Valheim style. The Build key (6, or the hammer slot on a pad's hotbar) takes it out and puts it away.
    // While it is out a see-through copy of the chosen piece follows the crosshair, snapped to the 2 m grid, green
    // where it can go and red where it cannot. Attack places it, Block opens the piece list, the wheel or the Repair key
    // turns it a quarter, and Interact on a placed piece takes it down (its cost comes back). The server decides
    // (BuildManager); this side only aims and asks. Owner only: nothing here is synced.
    public class PlayerBuilder : NetworkBehaviour
    {
        private const float AimReach = 14f;
        private static readonly Color GoodTint = new Color(0.45f, 1f, 0.45f, 1f);
        private static readonly Color BadTint = new Color(1f, 0.3f, 0.25f, 1f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // Reused every frame while the hammer is out, so aiming makes no garbage.
        private static readonly RaycastHit[] AimHits = new RaycastHit[32];
        private static readonly System.Collections.Generic.IComparer<RaycastHit> ByDistance =
            System.Collections.Generic.Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));
        private Renderer[] _ghostRenderers = System.Array.Empty<Renderer>();
        private bool _ghostTintValid;
        private bool _ghostTinted;

        private int _kind;
        private float _yaw;
        private GameObject _ghost;
        private int _ghostKind = -1;
        private MaterialPropertyBlock _block;
        private bool _active;
        private PlayerFishing _fishing;
        private PlayerHealth _health;

        public static PlayerBuilder Local { get; private set; }

        // True while the local player has the hammer out (the axe, the guard and the camera wheel step aside).
        public static bool LocalBuilding => Local != null && Local._active;

        public bool Active => _active;
        public int Kind => _kind;
        public bool AimValid { get; private set; }
        public PlaceResult AimResult { get; private set; } = PlaceResult.BadKind;
        // The placed piece under the crosshair (0 for none).
        public int AimedPiece { get; private set; }

        private void Awake()
        {
            _fishing = GetComponent<PlayerFishing>();
            _health = GetComponent<PlayerHealth>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner) Local = this;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Local == this) Local = null;
            DestroyGhost();
        }

        public void SetActive(bool active)
        {
            if (_active == active) return;
            _active = active;
            if (!active)
            {
                DestroyGhost();
                AimedPiece = 0;
            }
            Sfx.Play(ProceduralSfx.UiClick, transform.position + Vector3.up, 0.5f, active ? 1.1f : 0.8f);
        }

        // The piece list chose a piece.
        public void Select(int kind)
        {
            BuildManager manager = BuildManager.Instance;
            if (manager == null || manager.Catalog == null || manager.Catalog.Get(kind) == null) return;
            _kind = kind;
            SetActive(true);
        }

        private void Update()
        {
            if (!IsOwner) return;
            BuildManager manager = BuildManager.Instance;
            bool down = _health != null && _health.IsDown;
            if (!UiState.GameplayBlocked && Keybinds.WasPressed(KeyAction.Build) && manager != null) SetActive(!_active);
            // Anything else in the hands puts the hammer away.
            if (_active && (down || (_fishing != null && _fishing.Busy) || Keybinds.WasPressed(KeyAction.Axe) || Keybinds.WasPressed(KeyAction.Unequip) || manager == null))
                SetActive(false);
            if (!_active)
            {
                if (_ghost != null) DestroyGhost();
                return;
            }

            if (UiState.InputBlocked)
            {
                if (_ghost != null) _ghost.SetActive(false);
                return;
            }

            if (!UiState.GameplayBlocked)
            {
                if (Keybinds.WasPressed(KeyAction.Block)) UiState.BuildMenuOpen = true;
                if (Keybinds.WasPressed(KeyAction.Repair)) _yaw = BuildRules.SnapYaw(_yaw + 90f);
                if (Mouse.current != null)
                {
                    float scroll = Mouse.current.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f) _yaw = BuildRules.SnapYaw(_yaw + Mathf.Sign(scroll) * 90f);
                }
            }

            Aim(manager, out Vector3 position);
            if (!UiState.GameplayBlocked && Keybinds.WasPressed(KeyAction.Attack))
            {
                if (AimValid) manager.RequestPlace(_kind, position, _yaw);
                else Sfx.Play(ProceduralSfx.UiClick, transform.position + Vector3.up, 0.5f, 0.6f);
            }
        }

        // Interact while the hammer is out and a piece is under the crosshair: take it down. True when it did.
        public bool TryRemoveAimed()
        {
            if (!IsOwner || !_active || AimedPiece == 0 || BuildManager.Instance == null) return false;
            BuildManager.Instance.RequestRemove(AimedPiece);
            return true;
        }

        private void Aim(BuildManager manager, out Vector3 position)
        {
            position = transform.position;
            PieceDefinition definition = manager.Catalog != null ? manager.Catalog.Get(_kind) : null;
            Camera view = PlayerCamera.Local != null ? PlayerCamera.Local.View : null;
            if (definition == null || view == null)
            {
                AimValid = false;
                return;
            }

            AimedPiece = 0;
            Ray ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 aim = transform.position + transform.forward * 3f;
            bool aimOnBlocks = false;
            int count = Physics.RaycastNonAlloc(ray, AimHits, AimReach + 6f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(AimHits, 0, count, ByDistance);
            for (int h = 0; h < count; h++)
            {
                RaycastHit hit = AimHits[h];
                if (hit.collider is CharacterController || hit.collider.transform.IsChildOf(transform)) continue;
                aim = hit.point;
                aimOnBlocks = Chopper.World.Voxel.VoxelWorld.IsGround(hit.collider);
                PieceView piece = hit.collider.GetComponentInParent<PieceView>();
                if (piece != null)
                {
                    AimedPiece = piece.Id;
                    // Aiming at the side of a piece builds on top of it (a wall on a wall); at a top face, on it.
                    if (Mathf.Abs(hit.normal.y) < 0.5f)
                    {
                        int index = manager.IndexOf(piece.Id);
                        PieceDefinition under = index >= 0 ? manager.Catalog.Get(manager.Pieces[index].Kind) : null;
                        if (under != null)
                        {
                            Bounds box = BuildRules.BoundsOf(manager.Pieces[index].Position, under.Size, manager.Pieces[index].Yaw);
                            aim = new Vector3(hit.point.x - hit.normal.x * 0.05f, box.max.y, hit.point.z - hit.normal.z * 0.05f);
                        }
                    }
                }
                break;
            }

            position = BuildRules.Snap(aim, definition.Snap, _yaw);
            if (aimOnBlocks) position.y = manager.OnBlockTop(_kind, position, _yaw);
            // Valheim's snapping: near a placed piece, join it corner to corner (the first join that fits).
            position = manager.SnapToPieces(gameObject, _kind, aim, _yaw, position);
            AimResult = manager.Check(gameObject, _kind, position, _yaw);
            AimValid = AimResult == PlaceResult.Ok;
            ShowGhost(definition, position);
        }

        private void ShowGhost(PieceDefinition definition, Vector3 position)
        {
            if (_ghost == null || _ghostKind != _kind) BuildGhost(definition);
            if (_ghost == null) return;
            if (!_ghost.activeSelf) _ghost.SetActive(true);
            _ghost.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, _yaw, 0f));
            // The tint only changes when the aim turns from good to bad or back.
            if (_ghostTinted && _ghostTintValid == AimValid) return;
            _ghostTinted = true;
            _ghostTintValid = AimValid;
            if (_block == null) _block = new MaterialPropertyBlock();
            Color tint = AimValid ? GoodTint : BadTint;
            foreach (Renderer renderer in _ghostRenderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, tint);
                renderer.SetPropertyBlock(_block);
            }
        }

        // A copy of the piece's model with nothing solid or alive in it: no colliders, lights, scripts or path blockers.
        private void BuildGhost(PieceDefinition definition)
        {
            DestroyGhost();
            if (definition.Prefab == null) return;
            // Made under an inactive parent and stripped before it wakes, so a workbench or campfire copy never counts.
            var holder = new GameObject("GhostHolder");
            holder.SetActive(false);
            _ghost = Instantiate(definition.Prefab, holder.transform);
            _ghost.name = "BuildGhost";
            _ghostKind = _kind;
            foreach (MonoBehaviour behaviour in _ghost.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(behaviour);
            foreach (UnityEngine.AI.NavMeshObstacle obstacle in _ghost.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true)) DestroyImmediate(obstacle);
            foreach (Collider collider in _ghost.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
            foreach (Light light in _ghost.GetComponentsInChildren<Light>(true)) DestroyImmediate(light);
            foreach (ParticleSystem particles in _ghost.GetComponentsInChildren<ParticleSystem>(true)) DestroyImmediate(particles.gameObject);
            foreach (Renderer renderer in _ghost.GetComponentsInChildren<Renderer>(true)) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ghost.transform.SetParent(null, false);
            Destroy(holder);
            _ghostRenderers = _ghost.GetComponentsInChildren<Renderer>(true);
            _ghostTinted = false;
        }

        private void DestroyGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
            _ghostKind = -1;
        }
    }
}
