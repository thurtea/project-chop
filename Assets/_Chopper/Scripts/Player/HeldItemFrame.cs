using UnityEngine;

namespace Chopper.Player
{
    // How a held item lies around its grip point, in the grip transform's own space: which way the head (or blade
    // tip) points from the grip, and which way the edge faces. Written once when the player prefab is built, from
    // the item's mesh, so placing the item (in the hand, on the back) never has to guess from bounds at runtime.
    public sealed class HeldItemFrame : MonoBehaviour
    {
        [Tooltip("From the grip toward the head, in this transform's space.")]
        public Vector3 Head = Vector3.up;
        [Tooltip("The way the edge faces, at right angles to Head, in this transform's space.")]
        public Vector3 Edge = Vector3.forward;

        // A world rotation for this transform that points the head along head and the edge along edge.
        public Quaternion RotationFor(Vector3 head, Vector3 edge)
        {
            Quaternion want = Quaternion.LookRotation(head, Vector3.ProjectOnPlane(edge, head));
            Quaternion own = Quaternion.LookRotation(Head, Vector3.ProjectOnPlane(Edge, Head));
            return want * Quaternion.Inverse(own);
        }
    }
}
