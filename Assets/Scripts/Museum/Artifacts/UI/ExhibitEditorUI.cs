using System.Collections.Generic;
using ProjectMuseum.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// The exhibit editor screen. Opens when a placed exhibit is clicked
    /// (<see cref="BuilderActions.OnExhibitClicked"/>). Left panel = the player's
    /// owned artifacts not yet placed anywhere (draggable cards with tags); right
    /// panel = this exhibit's display slots (a 2×2 grid per exhibit tile). Drag a
    /// card onto a slot to place it; left-click a filled slot to remove it.
    ///
    /// Unity port of Godot's ExhibitEditorUi. Put on the (initially hidden) editor
    /// panel object in the Museum scene (needs the Zenject SceneContext + installer).
    /// </summary>
    public class ExhibitEditorUI : MonoBehaviour
    {
        [Inject] private MuseumDataModel _model;
        [Inject] private MuseumArtifactDatabase _artifacts;

        [Header("Root")]
        [Tooltip("The panel shown/hidden. Defaults to this object.")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;

        [Header("Left — artifact storage list")]
        [Tooltip("Scroll Content the artifact cards are parented under.")]
        [SerializeField] private RectTransform storageContent;
        [SerializeField] private ArtifactCard cardPrefab;

        [Header("Right — exhibit display slots")]
        [Tooltip("Grid Content the slots are parented under (has the GridLayoutGroup below).")]
        [SerializeField] private RectTransform slotGridContent;
        [SerializeField] private GridLayoutGroup slotGrid;
        [SerializeField] private ArtifactSlot slotPrefab;
        [Tooltip("Display slots along each tile axis. 4 → a 1×1 exhibit shows a 4×4 " +
                 "(16-slot) grid, a 2×2 shows 8×8, etc.")]
        [SerializeField] private int slotsPerTileAxis = 4;

        [Header("Drag")]
        [Tooltip("Top-level RectTransform the drag-ghost icon is parented to (e.g. the editor canvas root).")]
        [SerializeField] private RectTransform dragLayer;
        [SerializeField] private Vector2 dragGhostSize = new Vector2(90f, 90f);

        [Header("Debug")]
        [Tooltip("On first open, if the player owns no artifacts, seed one of every " +
                 "catalog artifact so there's something to drag. Turn OFF for real play.")]
        [SerializeField] private bool debugFillStorageFromCatalog = true;

        private string _currentExhibitId;
        private readonly List<ArtifactSlot> _slots = new();
        private GameObject _dragGhost;

        // ── Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (panelRoot == null) panelRoot = gameObject;
            panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            BuilderActions.OnExhibitClicked += Open;
            BuilderActions.OnMuseumDataReloaded += OnDataReloaded;
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            BuilderActions.OnExhibitClicked -= Open;
            BuilderActions.OnMuseumDataReloaded -= OnDataReloaded;
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        }

        private void OnDataReloaded()
        {
            if (panelRoot.activeSelf) Close(); // stale exhibit id after a reload
        }

        // ── Open / close ────────────────────────────────────────────────

        private void Open(string exhibitId)
        {
            if (_model == null || _artifacts == null)
            {
                Debug.LogError("[ExhibitEditorUI] Not injected — needs a SceneContext with " +
                               "MuseumInstaller (and an Artifact Database assigned).", this);
                return;
            }

            _model.EnsureInitialized();
            MaybeSeedDebugStorage();

            _currentExhibitId = exhibitId;
            panelRoot.SetActive(true);
            BuildSlots(exhibitId);
            RefreshAll();
        }

        public void Close()
        {
            EndCardDrag(null);
            panelRoot.SetActive(false);
            _currentExhibitId = null;
        }

        private void MaybeSeedDebugStorage()
        {
            if (!debugFillStorageFromCatalog || _model.OwnedArtifacts.Count > 0) return;
            foreach (MuseumArtifactDatabase.Entry e in _artifacts.Artifacts)
                if (e?.Id != null) _model.AddOwnedArtifact(e.Id);
            Debug.Log($"[ExhibitEditorUI] Debug-seeded {_model.OwnedArtifacts.Count} owned artifact(s).");
        }

        // ── Build / refresh ─────────────────────────────────────────────

        private PlacedObjectData FindPlaced(string id)
        {
            foreach (PlacedObjectData p in _model.PlacedObjects)
                if (p.Id == id) return p;
            return null;
        }

        /// <summary>
        /// Build the slot grid from the exhibit's footprint: <c>slotsPerTileAxis</c>
        /// slots along each tile axis, so a 1×1 exhibit → 4×4 (16 slots) by default,
        /// a 2×2 → 8×8, and so on. Columns = width × slotsPerTileAxis.
        /// </summary>
        private void BuildSlots(string exhibitId)
        {
            for (int i = slotGridContent.childCount - 1; i >= 0; i--)
                Destroy(slotGridContent.GetChild(i).gameObject);
            _slots.Clear();

            PlacedObjectData placed = FindPlaced(exhibitId);
            int w = placed != null ? Mathf.Max(1, placed.WidthInTiles) : 1;
            int l = placed != null ? Mathf.Max(1, placed.LengthInTiles) : 1;
            int axis = Mathf.Max(1, slotsPerTileAxis);
            int cols = w * axis;
            int rows = l * axis;
            int count = cols * rows;

            if (slotGrid != null)
            {
                slotGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                slotGrid.constraintCount = cols;
            }

            for (int i = 0; i < count; i++)
            {
                ArtifactSlot slot = Instantiate(slotPrefab, slotGridContent);
                slot.Setup(i, this);
                _slots.Add(slot);
            }
        }

        private void RefreshAll()
        {
            RefreshStorage();
            RefreshSlots();
        }

        private void RefreshStorage()
        {
            for (int i = storageContent.childCount - 1; i >= 0; i--)
                Destroy(storageContent.GetChild(i).gameObject);

            foreach (OwnedArtifactData owned in _model.GetUnplacedArtifacts())
            {
                MuseumArtifactDatabase.Entry entry = _artifacts.GetById(owned.RawArtifactId);
                if (entry == null) continue;
                ArtifactCard card = Instantiate(cardPrefab, storageContent);
                card.Setup(entry, owned.InstanceId, this);
            }
        }

        private void RefreshSlots()
        {
            foreach (ArtifactSlot s in _slots) s.SetIcon(null);

            ExhibitData exhibit = _model.GetExhibitData(_currentExhibitId);
            if (exhibit == null) return;

            foreach (ArtifactSlotAssignment a in exhibit.Slots)
            {
                if (a.SlotIndex < 0 || a.SlotIndex >= _slots.Count) continue;
                OwnedArtifactData owned = _model.GetOwnedArtifact(a.ArtifactInstanceId);
                if (owned == null) continue;
                MuseumArtifactDatabase.Entry entry = _artifacts.GetById(owned.RawArtifactId);
                if (entry != null) _slots[a.SlotIndex].SetIcon(entry.Icon);
            }
        }

        // ── Called by slots ─────────────────────────────────────────────

        public void AssignToSlot(int slotIndex, string instanceId)
        {
            if (_model.AssignArtifactToSlot(_currentExhibitId, slotIndex, instanceId))
                RefreshAll();
        }

        public void ClearSlot(int slotIndex)
        {
            if (_model.RemoveArtifactFromSlot(_currentExhibitId, slotIndex))
                RefreshAll();
        }

        // ── Drag ghost (driven by ArtifactCard) ─────────────────────────

        public void BeginCardDrag(ArtifactCard card, PointerEventData e)
        {
            if (dragLayer == null || card.Icon == null) return;

            _dragGhost = new GameObject("ArtifactDragGhost", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)_dragGhost.transform;
            rt.SetParent(dragLayer, false);
            rt.sizeDelta = dragGhostSize;

            var img = _dragGhost.GetComponent<Image>();
            img.sprite = card.Icon;
            img.raycastTarget = false;   // so the slot underneath receives the drop
            img.preserveAspect = true;

            DragCard(e);
        }

        public void DragCard(PointerEventData e)
        {
            if (_dragGhost == null) return;
            var rt = (RectTransform)_dragGhost.transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragLayer, e.position, e.pressEventCamera, out Vector2 local))
                rt.localPosition = local;
        }

        public void EndCardDrag(PointerEventData e)
        {
            if (_dragGhost != null) Destroy(_dragGhost);
            _dragGhost = null;
        }
    }
}
