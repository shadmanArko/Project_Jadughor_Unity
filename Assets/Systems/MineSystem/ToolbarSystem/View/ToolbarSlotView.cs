using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Systems.MineSystem.ToolbarSystem.View
{
    public sealed class ToolbarSlotView : MonoBehaviour
    {
        private Image _icon;
        private Image _border;
        private Sprite _defaultBorderSprite;
        private Sprite _selectedBorderSprite;
        private TextMeshProUGUI _stackCount;

        public int Index { get; private set; }

        private void Awake()
        {
            _icon = FindImage("Icon");
            _border = FindImage("Border");
            _defaultBorderSprite = _border != null ? _border.sprite : null;
            _stackCount = CreateStackCount();
            Present(null, 0);
        }

        public void Initialize(int index, Sprite selectedBorderSprite)
        {
            Index = index;
            _selectedBorderSprite = selectedBorderSprite;
        }

        public void Present(Sprite sprite, int count)
        {
            if (_icon != null)
            {
                _icon.sprite = sprite;
                _icon.enabled = sprite != null;
            }

            if (_stackCount == null)
                return;

            _stackCount.text = count > 1 ? count.ToString() : string.Empty;
            _stackCount.gameObject.SetActive(count > 1);
        }

        public void SetHighlighted(bool highlighted)
        {
            if (_border == null)
                return;

            _border.sprite = highlighted && _selectedBorderSprite != null
                ? _selectedBorderSprite
                : _defaultBorderSprite;
        }

        private Image FindImage(string objectName)
        {
            var images = GetComponentsInChildren<Image>(true);
            for (var index = 0; index < images.Length; index++)
            {
                if (images[index].gameObject.name == objectName)
                    return images[index];
            }

            return null;
        }

        private TextMeshProUGUI CreateStackCount()
        {
            var countObject = new GameObject(
                "StackCount",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            countObject.layer = gameObject.layer;

            var rect = (RectTransform)countObject.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0.45f, 0f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(-3f, -2f);

            var text = countObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.BottomRight;
            text.fontSize = 20f;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.outlineWidth = 0.2f;
            return text;
        }
    }
}
