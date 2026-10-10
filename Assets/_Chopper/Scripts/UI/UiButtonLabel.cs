using Chopper.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chopper.UI
{
    // Brightens a text-only button's label while it is hovered or selected (the button's own color tint only reaches
    // its glow image), and gives the menus their hover and click sounds.
    public class UiButtonLabel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Button _button;
        private Text _label;
        private Color _normal;
        private Color _active;
        private bool _hovered;
        private bool _wasActive;

        public void Init(Button button, Text label, Color normal, Color active)
        {
            _button = button;
            _button.onClick.AddListener(() => Sfx.PlayUi(ProceduralSfx.UiClick, 0.8f));
            _label = label;
            _normal = normal;
            _active = active;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData) => _hovered = false;

        private void OnDisable() => _hovered = false;

        private void Update()
        {
            if (_label == null || _button == null) return;
            EventSystem system = EventSystem.current;
            bool selected = system != null && system.currentSelectedGameObject == gameObject;
            Color target = !_button.interactable ? _normal * new Color(1f, 1f, 1f, 0.45f) : (_hovered || selected ? _active : _normal);
            if (_label.color != target) _label.color = target;
            // A faint tick when the highlight moves onto this button, by mouse or by pad.
            bool active = _button.interactable && (_hovered || selected);
            if (active && !_wasActive && Time.unscaledTime > 0.5f) Sfx.PlayUi(ProceduralSfx.UiHover, 0.6f);
            _wasActive = active;
        }
    }
}
