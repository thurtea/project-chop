using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Chopper.UI
{
    // A button that acts only after it is held down: with the mouse on it, or with Enter or the pad's A button while it
    // is selected. While it is held the whole button fills with color from left to right (red for deleting a world), so
    // it is plain what is happening, and the fill drains back if it is let go early. Used for things that cannot be
    // undone, like deleting a saved world, instead of a second "are you sure" click.
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float DrainSpeed = 3f;

        private Action _onHeld;
        private float _seconds = 1f;
        private Image _fill;
        private bool _pointerDown;
        private float _progress;
        private bool _fired;
        private bool _debugFrozen;

        // Screenshots: shows the fill part way, as if held, without ever firing.
        public void DebugShowProgress(float progress)
        {
            _debugFrozen = true;
            _progress = Mathf.Clamp01(progress);
            if (_fill != null) _fill.rectTransform.anchorMax = new Vector2(_progress, 1f);
        }

        // Turns an existing button into a hold button: its click does nothing, holding it for seconds calls onHeld.
        public static HoldButton Attach(Button button, float seconds, Color fillColor, Action onHeld)
        {
            button.onClick.RemoveAllListeners();
            HoldButton hold = button.gameObject.AddComponent<HoldButton>();
            hold._onHeld = onHeld;
            hold._seconds = Mathf.Max(0.1f, seconds);
            Image fill = UiKit.Box(button.transform, "HoldFill", fillColor);
            fill.raycastTarget = false;
            fill.rectTransform.SetAsFirstSibling();
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            hold._fill = fill;
            return hold;
        }

        public void OnPointerDown(PointerEventData eventData) => _pointerDown = eventData.button == PointerEventData.InputButton.Left;

        public void OnPointerUp(PointerEventData eventData) => _pointerDown = false;

        public void OnPointerExit(PointerEventData eventData) => _pointerDown = false;

        private bool SubmitHeld()
        {
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != gameObject) return false;
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed || keyboard.spaceKey.isPressed))
                   || (pad != null && pad.buttonSouth.isPressed);
        }

        private void Update()
        {
            if (_debugFrozen) return;
            bool held = _pointerDown || SubmitHeld();
            if (!held) _fired = false;
            _progress = held && !_fired
                ? _progress + Time.unscaledDeltaTime / _seconds
                : Mathf.Max(0f, _progress - Time.unscaledDeltaTime * DrainSpeed);
            if (_progress >= 1f && !_fired)
            {
                _fired = true;
                _progress = 0f;
                _onHeld?.Invoke();
            }
            if (_fill != null) _fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(_progress), 1f);
        }

        private void OnDisable()
        {
            _pointerDown = false;
            _progress = 0f;
            if (_fill != null) _fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        }
    }
}
