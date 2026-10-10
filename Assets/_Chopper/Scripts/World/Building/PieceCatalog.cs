using UnityEngine;

namespace Chopper.World.Building
{
    // Every building piece, in a fixed order: a piece is saved and synced by its index here, so new pieces go on the
    // end. Made by the editor builder (Chopper.Editor.PieceBuilder).
    [CreateAssetMenu(fileName = "SO_PieceCatalog", menuName = "Chopper/Piece Catalog")]
    public class PieceCatalog : ScriptableObject
    {
        [SerializeField] private PieceDefinition[] _pieces = new PieceDefinition[0];

        public int Count => _pieces != null ? _pieces.Length : 0;

        public PieceDefinition Get(int kind) => kind >= 0 && kind < Count ? _pieces[kind] : null;

        public void Configure(PieceDefinition[] pieces) => _pieces = pieces;
    }
}
