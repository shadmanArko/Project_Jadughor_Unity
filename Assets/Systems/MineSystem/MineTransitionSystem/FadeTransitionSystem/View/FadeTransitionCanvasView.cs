using System;
using TMPro;
using UnityEngine;

namespace Systems.MineSystem.MineTransitionSystem.FadeTransitionSystem.View
{
    public sealed class FadeTransitionCanvasView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text dayLabel;

        public CanvasGroup CanvasGroup => canvasGroup;

        public void Validate()
        {
            if (canvasGroup == null || dayLabel == null)
                throw new InvalidOperationException(
                    "FadeTransitionCanvasView requires a CanvasGroup and day label.");
        }

        public void SetDayLabel(string label)
        {
            dayLabel.text = label ?? string.Empty;
            dayLabel.gameObject.SetActive(!string.IsNullOrEmpty(label));
        }

        public void SetVisible(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }
    }
}
