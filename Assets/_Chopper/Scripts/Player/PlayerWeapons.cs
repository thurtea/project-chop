using Chopper.Core;
using Chopper.UI;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Chopper.Player
{
    public class PlayerWeapons : NetworkBehaviour
    {
        [SerializeField] private GameConfig _config;

        private readonly SyncVar<int> _owned = new SyncVar<int>();
        private readonly SyncVar<int> _selected = new SyncVar<int>();

        public static PlayerWeapons Local { get; private set; }

        public int OwnedMask => _owned.Value;
        public PlayerWeapon Selected => WeaponRules.Owns(_owned.Value, (PlayerWeapon)_selected.Value) ? (PlayerWeapon)_selected.Value : PlayerWeapon.Axe;
        public bool Owns(PlayerWeapon weapon) => WeaponRules.Owns(_owned.Value, weapon);
        public string LastCue { get; private set; } = string.Empty;

        private PlayerAxe _axe;
        private Transform _shown;
        private PlayerWeapon _shownWeapon = PlayerWeapon.Axe;
        private Renderer _axeRenderer;
        private static readonly GameObject[] Models = new GameObject[WeaponRules.Count];
        private static Material _world;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner) Local = this;
        }

        private void Awake()
        {
            _axe = GetComponent<PlayerAxe>();
        }

        private void Update()
        {
            ShowModel();
        }

        public void RequestNext()
        {
            PlayerWeapon next = WeaponRules.Next(_owned.Value, Selected);
            RequestSelect(next);
        }

        public void RequestSelect(PlayerWeapon weapon)
        {
            LastCue = WeaponRules.Name(weapon);
            SelectServerRpc((int)weapon);
        }

        [ServerRpc]
        private void SelectServerRpc(int weapon)
        {
            ServerSelect((PlayerWeapon)Mathf.Clamp(weapon, 0, WeaponRules.Count - 1));
        }

        public void ServerSelect(PlayerWeapon weapon)
        {
            if (!WeaponRules.Owns(_owned.Value, weapon)) weapon = PlayerWeapon.Axe;
            _selected.Value = (int)weapon;
        }

        public int ServerChange(PlayerWeapon weapon, int delta)
        {
            bool had = Owns(weapon);
            if (delta > 0) _owned.Value = WeaponRules.Grant(_owned.Value, weapon);
            else if (delta < 0) _owned.Value = WeaponRules.Take(_owned.Value, weapon);
            if (!Owns(Selected)) _selected.Value = 0;
            return (Owns(weapon) ? 1 : 0) - (had ? 1 : 0);
        }

        public void ServerLoad(int mask, int selected)
        {
            _owned.Value = mask & WeaponRules.AllForged;
            _selected.Value = WeaponRules.Owns(_owned.Value, (PlayerWeapon)selected) ? selected : 0;
        }

        public void ServerGrantAll() => _owned.Value = WeaponRules.AllForged;

        public void RequestDevAll() => DevAllServerRpc();

        [ServerRpc]
        private void DevAllServerRpc() => ServerGrantAll();

        private void ShowModel()
        {
            Transform pivot = _axe != null ? _axe.AxePivot : null;
            if (pivot == null) return;
            if (_axeRenderer == null)
            {
                foreach (Renderer r in pivot.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.transform == _shown || (_shown != null && r.transform.IsChildOf(_shown))) continue;
                    _axeRenderer = r;
                    break;
                }
                if (_axeRenderer == null) return;
            }
            PlayerWeapon want = Selected;
            if (want != _shownWeapon || (_shown == null && want != PlayerWeapon.Axe))
            {
                if (_shown != null) Destroy(_shown.gameObject);
                _shown = null;
                _shownWeapon = want;
                if (want != PlayerWeapon.Axe) _shown = Build(want, _axeRenderer.transform);
            }
            _axeRenderer.enabled = _shown == null;
        }

        private static Transform Build(PlayerWeapon weapon, Transform axeMesh)
        {
            GameObject model = Load(weapon);
            if (model == null) return null;
            MeshFilter source = model.GetComponentInChildren<MeshFilter>();
            if (source == null || source.sharedMesh == null) return null;
            var go = new GameObject("Weapon_" + WeaponRules.Name(weapon));
            go.transform.SetParent(axeMesh.parent, false);
            go.transform.localPosition = axeMesh.localPosition;
            go.transform.localRotation = axeMesh.localRotation;
            go.transform.localScale = axeMesh.localScale;
            go.layer = axeMesh.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = WorldMaterial(axeMesh.GetComponent<Renderer>());
            return go.transform;
        }

        public static GameObject Load(PlayerWeapon weapon)
        {
            int i = (int)weapon;
            if (Models[i] == null) Models[i] = Resources.Load<GameObject>("Weapons/chopper_" + WeaponRules.Name(weapon).ToLowerInvariant());
            return Models[i];
        }

        private static Material WorldMaterial(Renderer fallback)
        {
            if (_world != null) return _world;
            GameObject boat = Resources.Load<GameObject>("Props/prop_boat_voxel");
            Renderer r = boat != null ? boat.GetComponentInChildren<Renderer>() : null;
            _world = r != null ? r.sharedMaterial : (fallback != null ? fallback.sharedMaterial : null);
            return _world;
        }

        public string Report()
        {
            string shown = _shown != null ? _shown.name + " mesh " + (_shown.GetComponent<MeshFilter>().sharedMesh != null) : "axe";
            return $"owned {_owned.Value}, selected {Selected}, shown {shown}, axe renderer {(_axeRenderer != null ? _axeRenderer.enabled.ToString() : "none")}";
        }
    }
}
