using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Systems.MineSystem.InventorySystem.View
{
    public sealed class InventoryItemSlotView :
        MonoBehaviour,
        IPointerClickHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private readonly Subject<PointerEventData.InputButton> _clicked = new();
        private readonly Subject<PointerEventData.InputButton> _pointerDown = new();
        private readonly Subject<PointerEventData.InputButton> _pointerUp = new();
        private readonly Subject<Unit> _pointerEntered = new();
        private readonly Subject<Unit> _pointerExited = new();

        private Image _icon;
        private Image _border;
        private Sprite _defaultBorderSprite;
        private Sprite _selectedBorderSprite;
        private TextMeshProUGUI _stackCount;

        public int Index { get; private set; }
        public IObservable<PointerEventData.InputButton> Clicked => _clicked;
        public IObservable<PointerEventData.InputButton> PointerDown => _pointerDown;
        public IObservable<PointerEventData.InputButton> PointerUp => _pointerUp;
        public IObservable<Unit> PointerEntered => _pointerEntered;
        public IObservable<Unit> PointerExited => _pointerExited;

        private void Awake()
        {
            _icon = FindImage("Icon");
            _border = FindImage("Border");
            if (_border != null)
                _defaultBorderSprite = _border.sprite;
            _stackCount = CreateStackCount();
            Present(null, 0);
        }

        public void Initialize(int index)
        {
            Index = index;
        }

        public void ConfigureSelectionSprite(Sprite sprite)
        {
            _selectedBorderSprite = sprite;
        }

        public void Present(Sprite sprite, int count)
        {
            if (_icon != null)
            {
                _icon.sprite = sprite;
                _icon.enabled = sprite != null;
            }

            if (_stackCount != null)
            {
                _stackCount.text = count > 1 ? count.ToString() : string.Empty;
                _stackCount.gameObject.SetActive(count > 1);
            }
        }

        public void SetSelected(bool selected)
        {
            if (_border != null)
            {
                _border.sprite = selected && _selectedBorderSprite != null
                    ? _selectedBorderSprite
                    : _defaultBorderSprite;
                _border.color = Color.white;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _clicked.OnNext(eventData.button);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointerDown.OnNext(eventData.button);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pointerUp.OnNext(eventData.button);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerEntered.OnNext(Unit.Default);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerExited.OnNext(Unit.Default);
        }

        private Image FindImage(string objectName)
        {
            var images = GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i].gameObject.name == objectName)
                    return images[i];
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

        private void OnDestroy()
        {
            _clicked.Dispose();
            _pointerDown.Dispose();
            _pointerUp.Dispose();
            _pointerEntered.Dispose();
            _pointerExited.Dispose();
        }
    }
}
