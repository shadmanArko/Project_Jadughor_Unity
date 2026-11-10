using UnityEngine;
using UnityEngine.UI;

namespace Systems.MainMenuSystem.Views
{
    /// <summary>
    /// Makes a uGUI <see cref="Toggle"/> look like Godot's CheckButton switch:
    /// knob slides left/right and an accent fill shows when on. Event-driven only
    /// (onValueChanged) — no Update.
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    public class SwitchToggleVisual : MonoBehaviour
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private Graphic onFill;
        [SerializeField] private RectTransform knob;
        [SerializeField] private float knobPadding = 3f;

        public void Init(Toggle t, Graphic fill, RectTransform knobRect)
        {
            toggle = t;
            onFill = fill;
            knob = knobRect;
            Apply(toggle.isOn);
        }

        private void OnEnable()
        {
            if (toggle == null) toggle = GetComponent<Toggle>();
            toggle.onValueChanged.AddListener(Apply);
            Apply(toggle.isOn);
        }

        private void OnDisable() => toggle.onValueChanged.RemoveListener(Apply);

        private void Apply(bool on)
        {
            if (knob == null) return;
            var trackWidth = ((RectTransform)transform).rect.width;
            var half = knob.sizeDelta.x / 2f + knobPadding;
            knob.anchoredPosition = new Vector2(on ? trackWidth - half : half, 0);
            if (onFill != null) onFill.enabled = on;
        }

        // Track width is 0 until the first layout pass — re-apply once it's known.
        private void OnRectTransformDimensionsChange()
        {
            if (toggle != null) Apply(toggle.isOn);
        }
    }
}
