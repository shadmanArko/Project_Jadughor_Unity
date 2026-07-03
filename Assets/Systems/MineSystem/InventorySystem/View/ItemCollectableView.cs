using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Systems.MineSystem.InventorySystem.View
{
    public sealed class ItemCollectableView : MonoBehaviour
    {
        public Image icon;
        public TMP_Text nameText;
        public TMP_Text countText;
        private CanvasGroup _canvasGroup;

        public void Present(Sprite sprite, string itemName, int count)
        {
            EnsureCanvasGroup();
            SetAlpha(1f);
            gameObject.SetActive(true);

            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (nameText != null)
                nameText.text = itemName ?? string.Empty;
            if (countText != null)
                countText.text = $"x{Mathf.Max(1, count)}";
        }

        public void SetAlpha(float alpha)
        {
            EnsureCanvasGroup();
            _canvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        public void ResetView()
        {
            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
            }

            if (nameText != null)
                nameText.text = string.Empty;
            if (countText != null)
                countText.text = string.Empty;

            SetAlpha(0f);
            gameObject.SetActive(false);
        }

        private void EnsureCanvasGroup()
        {
            if (_canvasGroup != null)
                return;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }
}
