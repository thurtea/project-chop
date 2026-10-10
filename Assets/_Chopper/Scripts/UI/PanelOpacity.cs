using UnityEngine;
using UnityEngine.UI;

namespace Chopper.UI
{
    // Keeps a menu window's background at the Window opacity chosen in Settings, and follows the slider live.
    [RequireComponent(typeof(Image))]
    public class PanelOpacity : MonoBehaviour
    {
        private Image _image;

        private void OnEnable()
        {
            _image = GetComponent<Image>();
            UserSettings.MenuOpacityChanged += Apply;
            Apply();
        }

        private void OnDisable()
        {
            UserSettings.MenuOpacityChanged -= Apply;
        }

        private void Apply()
        {
            if (_image == null) return;
            Color color = _image.color;
            color.a = UserSettings.MenuOpacity;
            _image.color = color;
        }
    }
}
