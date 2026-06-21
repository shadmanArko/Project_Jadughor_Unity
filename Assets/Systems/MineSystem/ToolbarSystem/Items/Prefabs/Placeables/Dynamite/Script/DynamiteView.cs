using TMPro;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Dynamite.Script
{
    public sealed class DynamiteView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer dynamiteRenderer;
        [SerializeField] private TextMeshPro countdownText;

        public void Configure(DynamiteConfig config)
        {
            EnsureCountdownText();
            countdownText.transform.localPosition = config.TimerOffset;
            countdownText.fontSize = config.TimerFontSize;
            countdownText.color = config.TimerColor;
            countdownText.alignment = TextAlignmentOptions.Center;
            if (dynamiteRenderer != null)
            {
                countdownText.sortingLayerID =
                    dynamiteRenderer.sortingLayerID;
                countdownText.sortingOrder =
                    dynamiteRenderer.sortingOrder + 1;
            }
            else
            {
                countdownText.sortingOrder = 1;
            }
        }

        public void PresentCountdown(int seconds)
        {
            EnsureCountdownText();
            countdownText.text = seconds.ToString("00");
        }

        public void ResetView()
        {
            if (countdownText != null)
                countdownText.text = string.Empty;
        }

        private void EnsureCountdownText()
        {
            if (countdownText != null)
                return;

            var textObject = new GameObject("Countdown");
            textObject.transform.SetParent(transform, false);
            countdownText = textObject.AddComponent<TextMeshPro>();
            countdownText.raycastTarget = false;
        }
    }
}
