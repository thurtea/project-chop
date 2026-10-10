using FishNet.Object;
using UnityEngine;

namespace Chopper.Player
{
    // Every player is Chopper. Each client dresses each player it sees (its own and everyone else's) in the fixed
    // default look, so nothing about the look is saved, chosen or sent over the network.
    public class PlayerAppearance : NetworkBehaviour
    {
        [SerializeField] private CharacterOutfitter _outfitter;

        public int Packed => AppearanceData.Default.Pack();

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (_outfitter != null) _outfitter.Apply(AppearanceData.Default);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Appearance] player {OwnerId} shown as Chopper (owner here: {IsOwner})");
#endif
        }
    }
}
