using System;
using System.Collections.Generic;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Systems.MineSystem.InventorySystem.View
{
    public sealed class InventoryCanvasView : MonoBehaviour
    {
        [SerializeField] public List<InventoryItemSlotView> inventoryItemSlotViews =
            new();

        private readonly Subject<Unit> _trashClicked = new();
        private readonly List<InventoryItemSlotView> _allSlots = new(36);
        private GameObject _contentRoot;
        private GameObject[] _bars;
        private Image _hoveredItemImage;
        private TextMeshProUGUI _descriptionText;
        private RectTransform _heldRoot;
        private Image _heldImage;
        private TextMeshProUGUI _heldCount;
        private Button _trashButton;
        private bool _isVisible;
        private Sprite _heldSprite;
        private int _heldStackCount;

        public IReadOnlyList<InventoryItemSlotView> AllSlots => _allSlots;
        public IObservable<Unit> TrashClicked => _trashClicked;

        private void Awake()
        {
            _contentRoot = transform.childCount > 0
                ? transform.GetChild(0).gameObject
                : gameObject;
            _bars = new[]
            {
                FindDescendant("FirstBarPanel"),
                FindDescendant("SecondBarPanel"),
                FindDescendant("ThirdBarPanel")
            };

            CacheSlots();
            _hoveredItemImage = FindComponent<Image>("ItemVisualSprite");
            _descriptionText = FindComponent<TextMeshProUGUI>(
                "ItemDescriptionText");
            CreateHeldItemView();
            CreateTrashButton();
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            _contentRoot.SetActive(visible);
            RefreshHeldVisibility();
        }

        public void SetUnlockedSlots(int count)
        {
            inventoryItemSlotViews.Clear();
            for (var i = 0; i < _allSlots.Count; i++)
            {
                var unlocked = i < count;
                _allSlots[i].gameObject.SetActive(unlocked);
                if (unlocked)
                    inventoryItemSlotViews.Add(_allSlots[i]);
            }

            for (var i = 0; i < _bars.Length; i++)
            {
                if (_bars[i] != null)
                    _bars[i].SetActive(count > i * 12);
            }
        }

        public void PresentHeld(Sprite sprite, int count)
        {
            _heldSprite = sprite;
            _heldStackCount = count;
            _heldImage.sprite = sprite;
            _heldCount.text = count > 1 ? count.ToString() : string.Empty;
            _heldCount.gameObject.SetActive(count > 1);
            RefreshHeldVisibility();
        }

        public void PresentHovered(Sprite sprite, string description)
        {
            if (_hoveredItemImage != null)
            {
                _hoveredItemImage.sprite = sprite;
                _hoveredItemImage.enabled = sprite != null;
            }

            if (_descriptionText != null)
                _descriptionText.text = description ?? string.Empty;
        }

        public void SetHeldScreenPosition(Vector2 position)
        {
            if (_heldRoot != null && _heldRoot.gameObject.activeSelf)
                _heldRoot.position = position + new Vector2(24f, -24f);
        }

        private void CacheSlots()
        {
            _allSlots.Clear();
            for (var barIndex = 0; barIndex < _bars.Length; barIndex++)
            {
                if (_bars[barIndex] == null)
                    continue;

                var slots = _bars[barIndex]
                    .GetComponentsInChildren<InventoryItemSlotView>(true);
                for (var i = 0; i < slots.Length; i++)
                {
                    slots[i].Initialize(_allSlots.Count);
                    _allSlots.Add(slots[i]);
                }
            }
        }

        private void CreateHeldItemView()
        {
            var heldObject = new GameObject(
                "HeldInventoryStack",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            heldObject.layer = gameObject.layer;
            _heldRoot = (RectTransform)heldObject.transform;
            _heldRoot.SetParent(transform, false);
            _heldRoot.sizeDelta = new Vector2(72f, 72f);
            _heldImage = heldObject.GetComponent<Image>();
            _heldImage.raycastTarget = false;
            _heldImage.preserveAspect = true;

            var countObject = new GameObject(
                "HeldStackCount",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            countObject.layer = gameObject.layer;
            var countRect = (RectTransform)countObject.transform;
            countRect.SetParent(_heldRoot, false);
            countRect.anchorMin = new Vector2(0.45f, 0f);
            countRect.anchorMax = Vector2.one;
            countRect.offsetMin = Vector2.zero;
            countRect.offsetMax = Vector2.zero;
            _heldCount = countObject.GetComponent<TextMeshProUGUI>();
            _heldCount.font = TMP_Settings.defaultFontAsset;
            _heldCount.raycastTarget = false;
            _heldCount.alignment = TextAlignmentOptions.BottomRight;
            _heldCount.fontSize = 24f;
            _heldCount.fontStyle = FontStyles.Bold;
            _heldCount.color = Color.white;
            heldObject.SetActive(false);
        }

        private void CreateTrashButton()
        {
            var buttonObject = new GameObject(
                "TrashButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.layer = gameObject.layer;
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(_contentRoot.transform, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-20f, 20f);
            rect.sizeDelta = new Vector2(150f, 60f);
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.48f, 0.18f, 0.24f, 1f);
            _trashButton = buttonObject.GetComponent<Button>();
            _trashButton.onClick.AddListener(OnTrashClicked);

            var labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.layer = gameObject.layer;
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.raycastTarget = false;
            label.text = "TRASH";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 24f;
            label.color = Color.white;
        }

        private void RefreshHeldVisibility()
        {
            if (_heldRoot != null)
            {
                _heldRoot.gameObject.SetActive(
                    _isVisible &&
                    _heldSprite != null &&
                    _heldStackCount > 0);
            }
        }

        private void OnTrashClicked()
        {
            _trashClicked.OnNext(Unit.Default);
        }

        private GameObject FindDescendant(string objectName)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].gameObject.name == objectName)
                    return transforms[i].gameObject;
            }

            return null;
        }

        private T FindComponent<T>(string objectName) where T : Component
        {
            var components = GetComponentsInChildren<T>(true);
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i].gameObject.name == objectName)
                    return components[i];
            }

            return null;
        }

        private void OnDestroy()
        {
            if (_trashButton != null)
                _trashButton.onClick.RemoveListener(OnTrashClicked);
            _trashClicked.Dispose();
        }
    }
}
