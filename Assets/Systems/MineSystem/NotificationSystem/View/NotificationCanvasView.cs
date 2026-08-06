using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Systems.MineSystem.NotificationSystem.View
{
    public sealed class NotificationCanvasView : MonoBehaviour
    {
        [SerializeField] private GameObject notificationPanel;
        [SerializeField] private TMP_Text notificationText;
        [SerializeField] private CanvasGroup canvasGroup;

        public void ShowNotification(string content)
        {
            if (notificationPanel == null || notificationText == null || canvasGroup == null)
                return;

            notificationPanel.SetActive(true);
            notificationText.SetText(content);
            notificationText.ForceMeshUpdate();

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                notificationText.rectTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                (RectTransform)notificationPanel.transform);
            Canvas.ForceUpdateCanvases();

            canvasGroup.alpha = 1f;
        }

        public void HideNotification()
        {
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            if (notificationPanel != null)
                notificationPanel.SetActive(false);
        }

        public void SetAlpha(float alpha)
        {
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }
}
