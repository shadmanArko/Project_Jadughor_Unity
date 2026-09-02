using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Fills the builder panel's scroll content with object cards for the selected
    /// category, and shows/hides the panel. Listens to
    /// <see cref="BuilderActions.OnBottomPanelBuilderCardToggleClicked"/>.
    ///
    /// Keep this component on an ALWAYS-ACTIVE object (e.g. the BuilderPanel root) and
    /// toggle a child <see cref="panelVisual"/> — if the component's own GameObject were
    /// disabled it would stop receiving the open event.
    /// </summary>
    public class BuilderPanelController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private BuilderDatabase database;
        [Tooltip("Provides the live tileset for Flooring cards (icon = tile sprite).")]
        [SerializeField] private MuseumTilePlacementManager tileManager;

        [Header("UI")]
        [Tooltip("The panel visuals to show/hide (NOT this object — see class summary).")]
        [SerializeField] private GameObject panelVisual;
        [Tooltip("The ScrollView Content the cards are parented under.")]
        [SerializeField] private RectTransform contentParent;
        [SerializeField] private BuilderCard cardPrefab;
        [Tooltip("Shown on cards whose icon couldn't be resolved.")]
        [SerializeField] private Sprite placeholderIcon;

        private BuilderCardType _currentType;
        private bool _isOpen;

        private void Awake()
        {
            if (panelVisual != null) panelVisual.SetActive(false);
            _isOpen = false;
        }

        private void OnEnable()
        {
            BuilderActions.OnBottomPanelBuilderCardToggleClicked += OnToggle;
            BuilderActions.OnCloseBuilderPanel += OnCloseRequested;
        }

        private void OnDisable()
        {
            BuilderActions.OnBottomPanelBuilderCardToggleClicked -= OnToggle;
            BuilderActions.OnCloseBuilderPanel -= OnCloseRequested;
        }

        /// <summary>The bar left this category behind, so the panel has nothing to show.</summary>
        private void OnCloseRequested()
        {
            if (_isOpen) Close();
        }

        private void OnToggle(BuilderCardType type)
        {
            // Clicking the already-open category closes the panel.
            if (_isOpen && _currentType == type)
            {
                Close();
                return;
            }

            _currentType = type;
            Populate(type);
            Open();
        }

        private void Open()
        {
            if (panelVisual != null) panelVisual.SetActive(true);
            _isOpen = true;
        }

        private void Close()
        {
            if (panelVisual != null) panelVisual.SetActive(false);
            _isOpen = false;
        }

        private void Populate(BuilderCardType type)
        {
            if (cardPrefab == null || contentParent == null)
            {
                Debug.LogError("[BuilderPanelController] Card Prefab / Content Parent not assigned.", this);
                return;
            }

            ClearContent();

            List<BuilderCardData> cards = type == BuilderCardType.Flooring
                ? BuildFlooringCards()
                : (database != null ? database.GetCards(type) : new List<BuilderCardData>());

            foreach (BuilderCardData card in cards)
            {
                BuilderCard instance = Instantiate(cardPrefab, contentParent);
                instance.Setup(card, placeholderIcon);
            }
            // Empty list → an empty panel; not an error.
        }

        /// <summary>Flooring cards come from the live tileset, not the JSON database.</summary>
        private List<BuilderCardData> BuildFlooringCards()
        {
            var cards = new List<BuilderCardData>();
            if (tileManager == null)
            {
                Debug.LogWarning("[BuilderPanelController] Tile Manager not assigned — no flooring cards.");
                return cards;
            }

            foreach (TileBase tile in tileManager.AvailableTiles)
            {
                if (tile == null) continue;
                Sprite sprite = (tile as Tile)?.sprite;
                cards.Add(new BuilderCardData(BuilderCardType.Flooring, tile.name, string.Empty, sprite));
            }
            return cards;
        }

        private void ClearContent()
        {
            for (int i = contentParent.childCount - 1; i >= 0; i--)
                Destroy(contentParent.GetChild(i).gameObject);
        }
    }
}
