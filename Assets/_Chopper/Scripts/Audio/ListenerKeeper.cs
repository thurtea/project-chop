using UnityEngine;

namespace Chopper.Audio
{
    // Exactly one AudioListener, always. The boot camera, the World scene camera and the player camera each carry
    // one, but while FishNet swaps the scenes (Boot unloads before World is in) and until the player camera takes over,
    // there were frames with none ("There are no audio listeners in the scene"). This lives on the NetworkManager,
    // which survives scene changes, and switches its own listener on only while no other listener is active.
    [RequireComponent(typeof(AudioListener))]
    public class ListenerKeeper : MonoBehaviour
    {
        private AudioListener _own;
        private AudioListener _other;

        private void Awake()
        {
            _own = GetComponent<AudioListener>();
            _own.enabled = false;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            // The listener seen last time is still on: nothing to do (the common case, no search).
            if (_other != null && _other.isActiveAndEnabled)
            {
                if (_own.enabled) _own.enabled = false;
                return;
            }
            _other = null;
            foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
            {
                if (listener == _own || !listener.isActiveAndEnabled) continue;
                _other = listener;
                break;
            }
            bool needed = _other == null;
            if (_own.enabled != needed) _own.enabled = needed;
        }
    }
}
