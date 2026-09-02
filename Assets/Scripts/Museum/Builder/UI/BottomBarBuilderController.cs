using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Builds and drives the two-level bottom bar inside the bar's button rail.
    ///
    /// Row one lists the <see cref="BottomBarMenu.Categories"/>. Clicking one collapses
    /// that row off to the left, parks the clicked button at the left edge as a header,
    /// and slides that category's sub-buttons in behind it. Clicking the header again
    /// (or the same category twice) reverses the whole thing.
    ///
    /// Sub-buttons are the only things wired to gameplay: a backed one raises
    /// <see cref="BuilderActions.OnBottomPanelBuilderCardToggleClicked"/>, which the
    /// existing panel and placement systems already handle, so object placement keeps
    /// working exactly as before. Going back raises
    /// <see cref="BuilderActions.OnCloseBuilderPanel"/> so the panel does not linger.
    ///
    /// The whole hierarchy is built at runtime from <see cref="categoryButtonPrefab"/>,
    /// so the scene only has to supply that prefab and the rail to build into.
    /// </summary>
    public class BottomBarBuilderController : MonoBehaviour
    {
        [Header("Sources")]
        [Tooltip("Button prefab spawned for every category and sub-button. Its TMP_Text " +
                 "child is set to the button's label.")]
        [SerializeField] private Button categoryButtonPrefab;
        [Tooltip("The rail the rows are built into. Defaults to this object's transform.")]
        [SerializeField] private Transform buttonParent;

        [Header("Layout")]
        [SerializeField] private float buttonSpacing = 4f;
        [Tooltip("Gap between the parked category header and the sub-buttons.")]
        [SerializeField] private float headerGap = 12f;
        [Tooltip("Clip the rows to the rail so a long row can never draw over the " +
                 "money bar or off the screen edge.")]
        [SerializeField] private bool clipToRail = true;
        [Tooltip("Shrink/grow the rail to whatever space the bar's other children leave " +
                 "over. Turn this off to size the rail yourself in the scene instead.")]
        [SerializeField] private bool autoFitRail = true;
        [SerializeField] private float minRailWidth = 200f;

        [Header("Animation")]
        [SerializeField] private float slideDuration = 0.28f;
        [SerializeField] private Ease slideEase = Ease.OutCubic;

        [Header("Button tint")]
        [SerializeField] private Color idleColor = Color.white;
        [Tooltip("The parked category header, and the sub-button whose panel is open.")]
        [SerializeField] private Color activeColor = new Color(0.78f, 0.78f, 0.78f, 1f);

        // Built hierarchy
        private RectTransform _rail;
        private RectTransform _categoryRow;
        private CanvasGroup _categoryGroup;
        private RectTransform _detailRow;
        private CanvasGroup _detailGroup;
        private RectTransform _subViewport;
        private RectTransform _subRow;
        private Button _headerButton;

        private int _openCategory = -1;
        private int _openSubItem = -1;
        private readonly List<Button> _subButtons = new();
        private float _lastBarWidth = -1f;

        // ── Lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            if (categoryButtonPrefab == null)
            {
                Debug.LogError("[BottomBarBuilderController] Category Button Prefab not assigned.", this);
                enabled = false;
                return;
            }

            _rail = (buttonParent != null ? buttonParent : transform) as RectTransform;
            if (_rail == null)
            {
                Debug.LogError("[BottomBarBuilderController] Button Parent must be a RectTransform.", this);
                enabled = false;
                return;
            }

            if (clipToRail && _rail.GetComponent<RectMask2D>() == null)
                _rail.gameObject.AddComponent<RectMask2D>();

            FitRail();
            BuildCategoryRow();
            BuildDetailShell();
            ShowCategories(instant: true);
        }

        private void OnDisable()
        {
            KillTweens();
        }

        private void LateUpdate()
        {
            if (!autoFitRail || _rail == null) return;

            RectTransform bar = _rail.parent as RectTransform;
            if (bar == null) return;

            float width = bar.rect.width;
            if (Mathf.Approximately(width, _lastBarWidth)) return;
            _lastBarWidth = width;

            FitRail();
            if (_openCategory >= 0) LayoutDetailRow();
        }

        // ── Building ────────────────────────────────────────────────────

        private void BuildCategoryRow()
        {
            _categoryRow = CreateRow("CategoryRow", _rail);
            _categoryGroup = _categoryRow.gameObject.AddComponent<CanvasGroup>();
            AddHorizontalLayout(_categoryRow);

            for (int i = 0; i < BottomBarMenu.Categories.Length; i++)
            {
                int index = i; // avoid closure capture of the loop variable
                Button button = CreateButton(BottomBarMenu.Categories[i].Label, _categoryRow);
                button.onClick.AddListener(() => OpenCategory(index));
            }
        }

        /// <summary>
        /// The detail row's fixed parts: the header slot and the masked viewport the
        /// sub-buttons slide inside. The buttons themselves are rebuilt per category.
        /// </summary>
        private void BuildDetailShell()
        {
            _detailRow = CreateRow("DetailRow", _rail);
            _detailGroup = _detailRow.gameObject.AddComponent<CanvasGroup>();

            _headerButton = CreateButton(string.Empty, _detailRow);
            _headerButton.onClick.AddListener(ShowCategories);

            // The prefab is authored for a layout group, which would normally place it.
            // Here it sits loose in the detail row, so re-anchor it to the left edge —
            // its own centred pivot would otherwise hang it off the corner.
            var header = (RectTransform)_headerButton.transform;
            header.anchorMin = new Vector2(0f, 0.5f);
            header.anchorMax = new Vector2(0f, 0.5f);
            header.pivot = new Vector2(0f, 0.5f);
            header.anchoredPosition = Vector2.zero;

            _subViewport = CreateRow("SubViewport", _detailRow);
            _subViewport.gameObject.AddComponent<RectMask2D>();

            _subRow = CreateRow("SubRow", _subViewport);
            AddHorizontalLayout(_subRow);
        }

        private void BuildSubButtons(int categoryIndex)
        {
            for (int i = _subRow.childCount - 1; i >= 0; i--)
            {
                Transform old = _subRow.GetChild(i);
                // Destroy only takes effect at end of frame, so unparent first or this
                // frame's rebuild still measures the previous category's buttons.
                old.SetParent(null, false);
                Destroy(old.gameObject);
            }
            _subButtons.Clear();

            BottomBarSubItem[] items = BottomBarMenu.Categories[categoryIndex].Items;
            for (int i = 0; i < items.Length; i++)
            {
                BottomBarSubItem item = items[i];
                int index = i;

                Button button = CreateButton(item.Label, _subRow);
                _subButtons.Add(button);

                if (item.Kind == BottomBarActionKind.NotImplemented)
                {
                    // Built so the bar shows its finished shape, but visibly dead
                    // rather than a live button that swallows clicks.
                    button.interactable = false;
                    TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                    if (label != null) label.alpha = 0.45f;
                    continue;
                }

                button.onClick.AddListener(() => SelectSubItem(categoryIndex, index));
            }
        }

        // ── Interaction ─────────────────────────────────────────────────

        private void OpenCategory(int index)
        {
            if (_openCategory == index) { ShowCategories(); return; }

            _openCategory = index;
            _openSubItem = -1;

            KillTweens();

            // Activate before measuring — a rebuild on an inactive rect is a no-op, and
            // the slide distances below are read straight off these widths.
            _detailRow.gameObject.SetActive(true);
            SetLabel(_headerButton, BottomBarMenu.Categories[index].Label);
            Tint(_headerButton, activeColor);
            BuildSubButtons(index);
            LayoutDetailRow();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_categoryRow);

            _detailGroup.alpha = 1f;
            _detailGroup.blocksRaycasts = true;
            _detailRow.SetAsLastSibling();

            // The row is still on screen while it fades, so stop it taking clicks now.
            _categoryGroup.blocksRaycasts = false;

            // Sub-buttons sweep in from the left, over the categories on their way out.
            _subRow.anchoredPosition = new Vector2(-_subRow.rect.width, 0f);
            _subRow.DOAnchorPosX(0f, slideDuration).SetEase(slideEase).SetUpdate(true);

            _categoryRow.DOAnchorPosX(-_categoryRow.rect.width, slideDuration)
                        .SetEase(slideEase).SetUpdate(true)
                        .OnComplete(() => _categoryRow.gameObject.SetActive(false));
            _categoryGroup.DOFade(0f, slideDuration).SetUpdate(true);
        }

        private void SelectSubItem(int categoryIndex, int itemIndex)
        {
            BottomBarSubItem item = BottomBarMenu.Categories[categoryIndex].Items[itemIndex];
            if (item.Kind != BottomBarActionKind.OpenBuilderCategory) return;

            // The panel toggles itself shut when the open category is re-clicked, so
            // mirror that here rather than keeping a second copy of its open state.
            _openSubItem = _openSubItem == itemIndex ? -1 : itemIndex;
            RefreshSubTints();

            BuilderActions.OnBottomPanelBuilderCardToggleClicked?.Invoke(item.CardType);
        }

        private void ShowCategories() => ShowCategories(instant: false);

        private void ShowCategories(bool instant)
        {
            bool wasOpen = _openCategory >= 0;
            _openCategory = -1;
            _openSubItem = -1;

            KillTweens();
            _categoryRow.gameObject.SetActive(true);
            _categoryGroup.alpha = 1f;
            _categoryGroup.blocksRaycasts = true;
            _detailGroup.blocksRaycasts = false;

            if (instant)
            {
                _categoryRow.anchoredPosition = Vector2.zero;
                _detailRow.gameObject.SetActive(false);
                return;
            }

            _categoryRow.DOAnchorPosX(0f, slideDuration).SetEase(slideEase).SetUpdate(true);
            _subRow.DOAnchorPosX(-_subRow.rect.width, slideDuration).SetEase(slideEase).SetUpdate(true);
            _detailGroup.DOFade(0f, slideDuration).SetUpdate(true)
                        .OnComplete(() => _detailRow.gameObject.SetActive(false));

            if (wasOpen) BuilderActions.OnCloseBuilderPanel?.Invoke();
        }

        private void RefreshSubTints()
        {
            for (int i = 0; i < _subButtons.Count; i++)
                Tint(_subButtons[i], i == _openSubItem ? activeColor : idleColor);
        }

        // ── Layout ──────────────────────────────────────────────────────

        /// <summary>
        /// Parks the header at the rail's left edge and gives the viewport whatever
        /// width is left, so the sub-buttons clip instead of overflowing the bar.
        /// </summary>
        private void LayoutDetailRow()
        {
            RectTransform header = (RectTransform)_headerButton.transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(header);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_subRow);

            header.anchoredPosition = Vector2.zero;

            float offset = header.rect.width + headerGap;
            _subViewport.anchoredPosition = new Vector2(offset, 0f);
            _subViewport.sizeDelta = new Vector2(Mathf.Max(0f, _rail.rect.width - offset),
                                                 _subViewport.sizeDelta.y);
        }

        /// <summary>
        /// Sizes the rail to whatever the bar's other children leave over, so the row
        /// never grows under the money bar or past the screen edge on a non-16:9 window.
        /// </summary>
        private void FitRail()
        {
            if (!autoFitRail) return;

            RectTransform bar = _rail.parent as RectTransform;
            if (bar == null) return;

            float available = bar.rect.width;
            int siblings = 0;

            HorizontalLayoutGroup group = bar.GetComponent<HorizontalLayoutGroup>();
            if (group != null) available -= group.padding.left + group.padding.right;

            foreach (RectTransform child in bar)
            {
                if (!child.gameObject.activeSelf) continue;
                siblings++;
                if (child != _rail) available -= child.rect.width;
            }
            if (group != null) available -= group.spacing * Mathf.Max(0, siblings - 1);

            float width = Mathf.Max(minRailWidth, available);
            if (Mathf.Approximately(width, _rail.sizeDelta.x)) return;

            _rail.sizeDelta = new Vector2(width, _rail.sizeDelta.y);
            LayoutRebuilder.MarkLayoutForRebuild(bar);
        }

        // ── Small builders ──────────────────────────────────────────────

        /// <summary>A left-anchored, full-height child that the rail's own layout group ignores.</summary>
        private static RectTransform CreateRow(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;

            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            go.AddComponent<LayoutElement>().ignoreLayout = true;
            return rect;
        }

        private void AddHorizontalLayout(RectTransform row)
        {
            HorizontalLayoutGroup group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.childAlignment = TextAnchor.MiddleLeft;
            group.spacing = buttonSpacing;
            group.childControlWidth = false;
            group.childControlHeight = false;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;

            // The buttons size themselves to their label, so the row has to size to
            // them for the slide distance to be right.
            ContentSizeFitter fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        private Button CreateButton(string label, Transform parent)
        {
            Button button = Instantiate(categoryButtonPrefab, parent);
            button.name = $"BottomBarButton_{(string.IsNullOrEmpty(label) ? "Header" : label)}";
            button.onClick.RemoveAllListeners();
            SetLabel(button, label);
            Tint(button, idleColor);
            return button;
        }

        private static void SetLabel(Button button, string label)
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = label;
        }

        /// <summary>
        /// Recolours through the Button's ColorBlock — the transition is ColorTint, so
        /// writing the Image colour directly would be overwritten on its next state change.
        /// </summary>
        private static void Tint(Button button, Color color)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.selectedColor = color;
            colors.highlightedColor = new Color(color.r * 0.94f, color.g * 0.94f, color.b * 0.94f, color.a);
            button.colors = colors;
        }

        private void KillTweens()
        {
            if (_categoryRow != null) _categoryRow.DOKill();
            if (_categoryGroup != null) _categoryGroup.DOKill();
            if (_subRow != null) _subRow.DOKill();
            if (_detailGroup != null) _detailGroup.DOKill();
        }
    }
}
