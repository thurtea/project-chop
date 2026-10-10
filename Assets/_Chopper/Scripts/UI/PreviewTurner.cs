using UnityEngine;

namespace Chopper.UI
{
    // Turns the character creator's preview slowly (unscaled time, so it turns while menus pause the game).
    public class PreviewTurner : MonoBehaviour
    {
        public Transform Target;
        public float Speed = 28f;

        private void Update()
        {
            if (Target != null) Target.Rotate(0f, Speed * Time.unscaledDeltaTime, 0f, Space.World);
        }
    }
}
