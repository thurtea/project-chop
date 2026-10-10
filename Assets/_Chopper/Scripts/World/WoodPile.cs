using System.Collections.Generic;
using Chopper.Player;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Chopper.World
{
    // The wood a player was carrying when they fell, left on the ground where it happened (like Valheim's tombstone,
    // but only for wood). Anyone can walk up and press interact to take it back; it disappears once it is empty.
    // Spawned by EnemyDirector.ServerDropWood.
    public class WoodPile : NetworkBehaviour
    {
        public const float Reach = 2.6f;

        [SerializeField] private Material _material;

        private readonly SyncVar<int> _amount = new SyncVar<int>();
        private static readonly List<WoodPile> _all = new List<WoodPile>();
        private Mesh _mesh;

        public static IReadOnlyList<WoodPile> All => _all;

        public int Amount => _amount.Value;

        // Piles are only sent to players within NearbyClients.WoodPileRange (a NetworkObserver on the prefab), so the
        // list is kept from the network start, not the client start: the host saves every pile, seen or not.
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!_all.Contains(this)) _all.Add(this);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _all.Remove(this);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            BuildView();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (_mesh != null) Destroy(_mesh);
        }

        // Server only, right after spawning.
        public void ServerSetAmount(int amount) => _amount.Value = Mathf.Max(0, amount);

        // Server only. Moves as much wood as the player can carry; despawns the pile when it is empty.
        public int ServerTake(PlayerWood wood)
        {
            if (wood == null) return 0;
            int room = Mathf.Max(0, wood.Cap - wood.Carried);
            int moved = Mathf.Min(room, _amount.Value);
            if (moved <= 0) return 0;
            wood.ServerAddWood(moved);
            _amount.Value -= moved;
            if (_amount.Value <= 0) ServerManager.Despawn(gameObject);
            return moved;
        }

        // The nearest pile within reach of a position, or null.
        public static WoodPile Nearest(Vector3 position)
        {
            WoodPile best = null;
            float bestSqr = Reach * Reach;
            foreach (WoodPile pile in _all)
            {
                if (pile == null || pile.Amount <= 0) continue;
                Vector3 offset = pile.transform.position - position;
                if (Mathf.Abs(offset.y) > 3f) continue;
                offset.y = 0f;
                if (offset.sqrMagnitude < bestSqr)
                {
                    bestSqr = offset.sqrMagnitude;
                    best = pile;
                }
            }
            return best;
        }

        // A small crossed stack of logs (the branch mesh made thicker), easy to spot in the grass.
        private void BuildView()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            _mesh = BranchManager.BuildBranchMesh(7);
            for (int i = 0; i < 5; i++)
            {
                var log = new GameObject("Log" + i);
                log.transform.SetParent(transform, false);
                log.transform.localPosition = new Vector3(0f, 0.08f + 0.12f * (i / 2), 0f);
                log.transform.localRotation = Quaternion.Euler(0f, i * 72f + (i % 2) * 30f, 0f);
                log.transform.localScale = new Vector3(1.1f, 2.6f, 2.6f);
                log.AddComponent<MeshFilter>().sharedMesh = _mesh;
                log.AddComponent<MeshRenderer>().sharedMaterial = _material;
            }
        }
    }
}
