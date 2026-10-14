using Chopper.Core;
using UnityEngine;

namespace Chopper.World.Building
{
    // One kind of building piece (Valheim style): what it looks like, what it costs, how much it takes to break and
    // how it snaps. New pieces are new assets in the catalog (PieceCatalog), made by the editor builder.
    [CreateAssetMenu(fileName = "SO_Piece", menuName = "Chopper/Building Piece")]
    public class PieceDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName = "Piece";
        [Tooltip("The model, with colliders; a plain prefab (not networked). BuildManager places a copy on every peer.")]
        [SerializeField] private GameObject _prefab;
        [SerializeField, Min(0)] private int _woodCost = 4;
        [SerializeField, Min(0)] private int _fiberCost;
        [SerializeField, Min(1)] private int _health = 100;
        [Tooltip("Width (local X), height and depth (local Z) in meters, pivot at the bottom center.")]
        [SerializeField] private Vector3 _size = new Vector3(2f, 2f, 0.3f);
        [SerializeField] private PieceSnap _snap = PieceSnap.Edge;
        [Tooltip("This piece is the workbench.")]
        [SerializeField] private bool _isWorkbench;
        [Tooltip("Only built near a workbench. Basic pieces (floors, walls, roofs, stairs, torches, campfires) go up anywhere.")]
        [SerializeField] private bool _needsWorkbench;
        [Tooltip("Its icon in the piece list: a file in GameIcons_net/Resources/GameIcons, without .png.")]
        [SerializeField] private string _icon = string.Empty;
        [Tooltip("Where this piece joins others (Valheim's snap points), local to its pivot: corners of floors, walls and roofs.")]
        [SerializeField] private Vector3[] _snapPoints = new Vector3[0];

        public string DisplayName => _displayName;
        public GameObject Prefab => _prefab;
        public int WoodCost => _woodCost;
        public int FiberCost => _fiberCost;
        public int Health => _health;
        public Vector3 Size => _size;
        public PieceSnap Snap => _snap;
        public bool IsWorkbench => _isWorkbench;
        public bool NeedsWorkbench => _needsWorkbench && !_isWorkbench;
        public string Icon => _icon;
        public Vector3[] SnapPoints => _snapPoints;

        // Editor only: filled in by the builder.
        public void Configure(string displayName, GameObject prefab, int wood, int fiber, int health, Vector3 size, PieceSnap snap, bool workbench, string icon, Vector3[] snapPoints)
        {
            _snapPoints = snapPoints ?? new Vector3[0];
            _icon = icon;
            _displayName = displayName;
            _prefab = prefab;
            _woodCost = wood;
            _fiberCost = fiber;
            _health = health;
            _size = size;
            _snap = snap;
            _isWorkbench = workbench;
        }
    }
}
