using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chopper.UI
{
    // Makes a whole settings row light up while its control is selected (gamepad) or hovered (mouse): a warm band
    // behind the row, a gold bar at its left edge and a gold label. With a pad there is no pointer, so without this it
    // was hard to tell which slider or option the stick was on.
    public class UiRowHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float FadeSpeed = 10f;

        private Selectable _target;
        private Text _label;
        private Image _band;
        private Image _bar;
        private Color _labelNormal;
        private float _amount;
        private bool _hovered;

        public void Init(Selectable target, Text label, Image band, Image bar)
        {
            _target = target;
            _label = label;
            _band = band;
            _bar = bar;
            _labelNormal = label != null ? label.color : Color.white;
            Apply(0f);
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData) => _hovered = false;

        private void OnDisable()
        {
            _hovered = false;
            _amount = 0f;
            Apply(0f);
        }

        private void Update()
        {
            if (_target == null) return;
            EventSystem system = EventSystem.current;
            bool selected = system != null && system.currentSelectedGameObject == _target.gameObject;
            float goal = selected ? 1f : (_hovered ? 0.6f : 0f);
            if (Mathf.Approximately(goal, _amount)) return;
            _amount = Mathf.MoveTowards(_amount, goal, FadeSpeed * Time.unscaledDeltaTime);
            Apply(_amount);
        }

        private void Apply(float amount)
        {
            if (_band != null) _band.color = new Color(UiKit.ButtonGlow.r, UiKit.ButtonGlow.g, UiKit.ButtonGlow.b, 0.22f * amount);
            if (_bar != null) _bar.color = new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, amount);
            if (_label != null) _label.color = Color.Lerp(_labelNormal, UiKit.Accent, amount);
        }
    }
}
