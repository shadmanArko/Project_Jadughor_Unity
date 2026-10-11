using System;
using UnityEngine;
using UnityEngine.UI;

namespace Systems.MineSystem.BossLairSystem.View
{
    /// <summary>
    /// Presentation-only surface for the shared boss-lair health bar.
    /// This view owns the Slider and visibility; encounter lifecycle and health
    /// subscriptions belong to <c>BossLairHealthBarController</c>.
    /// </summary>
    public sealed class BossLairCanvasView : MonoBehaviour
    {
        [Tooltip("Slider that displays the active lair boss's current health.")]
        [SerializeField] private Slider healthSlider;

        public bool ValidateReferences()
        {
            if (healthSlider != null)
                return true;

            Debug.LogError(
                $"{name} requires a health Slider reference.", this);
            return false;
        }

        public void SetHealth(int currentHealth, int maximumHealth)
        {
            if (healthSlider == null)
                throw new InvalidOperationException(
                    $"{name} requires a health Slider reference.");

            var clampedMaximum = Mathf.Max(1, maximumHealth);
            healthSlider.minValue = 0f;
            healthSlider.maxValue = clampedMaximum;
            healthSlider.value = Mathf.Clamp(currentHealth, 0, clampedMaximum);
        }

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);
    }
}
