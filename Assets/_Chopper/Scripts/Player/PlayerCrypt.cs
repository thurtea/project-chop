using Chopper.Audio;
using Chopper.UI;
using Chopper.World;
using Chopper.World.Generation;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Chopper.Player
{
    // The player's side of the crypts: interact at a crypt door to go down, at the foot of the stairs to climb out,
    // and next to a chest to loot it. The server checks reach and moves the player (CryptManager decides).
    public class PlayerCrypt : NetworkBehaviour
    {
        private PlayerMovementValidator _validator;
        private CharacterController _controller;

        // Raised on the owning client right after a crypt door or the stairs moved this player (screen fade).
        public static event System.Action LocalTravelled;

        private void Awake()
        {
            _validator = GetComponent<PlayerMovementValidator>();
            _controller = GetComponent<CharacterController>();
        }

        // Owner only. Handles the interact press if a crypt door, the way out or a chest is in reach.
        public bool TryInteract()
        {
            if (!IsOwner) return false;
            Vector3 position = transform.position;
            if (CryptWorld.DoorInReach(position) >= 0)
            {
                EnterServerRpc();
                return true;
            }
            if (CryptWorld.ExitInReach(position) >= 0)
            {
                ExitServerRpc();
                return true;
            }
            if (CryptWorld.ArenaDoorInReach(position) >= 0)
            {
                EnterArenaServerRpc();
                return true;
            }
            if (CryptWorld.ArenaExitInReach(position) >= 0)
            {
                ExitArenaServerRpc();
                return true;
            }
            int chest = CryptWorld.ChestInReach(position);
            if (chest >= 0 && !CryptWorld.OpenedHere(chest))
            {
                OpenChestServerRpc();
                return true;
            }
            return false;
        }

        [ServerRpc]
        private void EnterServerRpc()
        {
            if (CryptManager.Instance != null) CryptManager.Instance.ServerTryEnter(this);
        }

        [ServerRpc]
        private void ExitServerRpc()
        {
            if (CryptManager.Instance != null) CryptManager.Instance.ServerTryExit(this);
        }

        [ServerRpc]
        private void EnterArenaServerRpc()
        {
            if (ArenaManager.Instance != null) ArenaManager.Instance.ServerTryEnter(this);
        }

        [ServerRpc]
        private void ExitArenaServerRpc()
        {
            if (ArenaManager.Instance != null) ArenaManager.Instance.ServerTryExit(this);
        }

        [ServerRpc]
        private void OpenChestServerRpc()
        {
            if (CryptManager.Instance != null) CryptManager.Instance.ServerTryOpenChest(this);
        }

        // ---------- Server ----------

        // Server only. Moves the player (owner authoritative movement, so the owner does the move).
        public void ServerTeleport(Vector3 position, float yaw)
        {
            if (_validator != null) _validator.ServerNotifyTeleport(position);
            if (Owner.IsValid) TeleportTargetRpc(Owner, position, yaw);
        }

        public void ServerToast(string text, Color color)
        {
            if (Owner.IsValid) ToastTargetRpc(Owner, text, color);
        }

        public void ServerChestOpened(int chest, int iron, int shards)
        {
            if (Owner.IsValid) ChestOpenedTargetRpc(Owner, chest, iron, shards);
        }

        // Server only. These chests were emptied by this player in an earlier session.
        public void ServerMarkOpened(int[] chests)
        {
            if (Owner.IsValid) MarkOpenedTargetRpc(Owner, chests);
        }

        [TargetRpc]
        private void MarkOpenedTargetRpc(NetworkConnection connection, int[] chests)
        {
            foreach (int chest in chests) CryptWorld.MarkOpenedHere(chest);
        }

        [TargetRpc]
        private void TeleportTargetRpc(NetworkConnection connection, Vector3 position, float yaw)
        {
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            _controller.enabled = wasEnabled;
            var networkTransform = GetComponent<FishNet.Component.Transforming.NetworkTransform>();
            if (networkTransform != null) networkTransform.Teleport();
            if (PlayerCamera.Local != null) PlayerCamera.Local.SnapYaw(yaw);
            LocalTravelled?.Invoke();
        }

        [TargetRpc]
        private void ToastTargetRpc(NetworkConnection connection, string text, Color color) => Toasts.Show(text, color);

        [TargetRpc]
        private void ChestOpenedTargetRpc(NetworkConnection connection, int chest, int iron, int shards)
        {
            CryptWorld.MarkOpenedHere(chest);
            Sfx.Play(ProceduralSfx.Upgrade, transform.position + Vector3.up, 0.8f);
        }
    }
}
