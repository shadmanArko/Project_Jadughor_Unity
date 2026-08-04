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
    /// (<see cref="BuilderActions.OnExhibitClicked"/>). Left = the player's owned,
    /// unplaced artifacts (draggable cards with tags). Right = this exhibit's display
    /// grid, in TINY cells (<see cref="slotsPerTileAxis"/> per tile axis, default 2 →
    /// a 1×1 exhibit is a 2×2 grid = 4 cells, a 2×2 exhibit is 4×4 = 16).
    ///
    /// Each artifact covers a footprint of cells by its size, and can only be placed
    /// on a size-ALIGNED anchor with all covered cells free:
    ///   Tiny 1×1 · Small 1×2 · Medium 2×2 · Large 2×4 · Huge 4×4 (w × h cells).
    /// Picking up / dragging an artifact re-highlights the grid: green = a valid
    /// aligned spot under the cursor, red = invalid, and occupied cells stay tinted.
    /// Drag a card onto a valid spot to place; drag a placed artifact out (or click
    /// it) to return it to storage. Everything persists per-exhibit in the data.
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
        [SerializeField] private RectTransform storageContent;
        [SerializeField] private ArtifactCard cardPrefab;

        [Header("Right — exhibit display grid")]
        [SerializeField] private RectTransform slotGridContent;
        [SerializeField] private GridLayoutGroup slotGrid;
        [SerializeField] private ArtifactSlot slotPrefab;
        [Tooltip("Tiny cells along each tile axis. 2 → a 1×1 exhibit is a 2×2 (4-cell) " +
                 "grid, a 2×2 exhibit is 4×4 (16).")]
        [SerializeField] private int slotsPerTileAxis = 2;

        [Header("Cell background colours")]
        [Tooltip("A free cell (idle, or not part of a valid group during placement).")]
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.06f);
        [Tooltip("A cell occupied by a placed artifact.")]
        [SerializeField] private Color occupiedColor = new Color(0.85f, 0.55f, 0.25f, 0.35f);
        [Tooltip("Free cells of a valid placement group while an artifact is grabbed.")]
        [SerializeField] private Color availableColor = new Color(0.4f, 1f, 0.4f, 0.18f);

        [Header("Border line colours (borders always visible; recoloured per group)")]
        [Tooltip("Idle grid lines (no artifact grabbed).")]
        [SerializeField] private Color lineDefaultColor = new Color(1f, 1f, 1f, 0.25f);
        [Tooltip("Outer edges of a group the grabbed artifact CAN be placed in.")]
        [SerializeField] private Color availablePerimeterColor = Color.white;
        [Tooltip("Inner edges (between cells) of a placeable group.")]
        [SerializeField] private Color availableInnerColor = new Color(1f, 1f, 1f, 0.4f);
        [Tooltip("Outer edges of a group it CANNOT be placed in.")]
        [SerializeField] private Color unavailablePerimeterColor = Color.black;
        [Tooltip("Inner edges of an unplaceable group.")]
        [SerializeField] private Color unavailableInnerColor = new Color(0f, 0f, 0f, 0.4f);

        [Header("Drag ghost")]
        [SerializeField] private RectTransform dragLayer;
        [Tooltip("Backdrop tint behind the dragged artifact (sized to its placement group).")]
        [SerializeField] private Color ghostBackdropColor = new Color(1f, 1f, 1f, 0.25f);
        [Tooltip("Inset of the artifact icon inside the group-sized ghost backdrop.")]
        [SerializeField] private float ghostIconPadding = 6f;

        [Header("Debug")]
        [Tooltip("On first open, if the player owns no artifacts, seed one of every " +
                 "catalog artifact so there's something to drag. Turn OFF for real play.")]
        [SerializeField] private bool debugFillStorageFromCatalog = true;

        private string _currentExhibitId;
        private readonly List<ArtifactSlot> _slots = new();
        private int _cols;
        private int _rows;
        private string[] _occupant;   // per cell: instance id covering it, or null
        private RectTransform _artifactOverlay; // holds placed-artifact visuals ABOVE the cells

        // Drag state
        private GameObject _dragGhost;
        private string _dragInstanceId;
        private int _dragW, _dragH;
        private bool _dragFromSlot;
        private bool _dropHandled;

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
            if (panelRoot.activeSelf) Close();
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
            BuildGrid(exhibitId);
            RefreshAll();
        }

        public void Close()
        {
            CancelDrag();
            panelRoot.SetActive(false);
            _currentExhibitId = null;
        }

        private void MaybeSeedDebugStorage()
        {
            if (!debugFillStorageFromCatalog || _model.OwnedArtifacts.Count > 0) return;
            foreach (MuseumArtifactDatabase.Entry e in _artifacts.Artifacts)
                if (e?.Id != null) _model.AddOwnedArtifact(e.Id);
        }

        // ── Grid build ──────────────────────────────────────────────────

        private PlacedObjectData FindPlaced(string id)
        {
            foreach (PlacedObjectData p in _model.PlacedObjects)
                if (p.Id == id) return p;
            return null;
        }

        private void BuildGrid(string exhibitId)
        {
            for (int i = slotGridContent.childCount - 1; i >= 0; i--)
                Destroy(slotGridContent.GetChild(i).gameObject);
            _slots.Clear();

            PlacedObjectData placed = FindPlaced(exhibitId);
            int w = placed != null ? Mathf.Max(1, placed.WidthInTiles) : 1;
            int l = placed != null ? Mathf.Max(1, placed.LengthInTiles) : 1;
            int axis = Mathf.Max(1, slotsPerTileAxis);
            _cols = w * axis;
            _rows = l * axis;
            _occupant = new string[_cols * _rows];

            if (slotGrid != null)
            {
                slotGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                slotGrid.constraintCount = _cols;
            }

            // Rows top→bottom, cols left→right — matches GridLayoutGroup fill order.
            for (int row = 0; row < _rows; row++)
                for (int col = 0; col < _cols; col++)
                {
                    ArtifactSlot slot = Instantiate(slotPrefab, slotGridContent);
                    slot.Setup(row * _cols + col, col, row, this);
                    _slots.Add(slot);
                }

            // Overlay for placed-artifact visuals — a LAST child of the content
            // (renders above every cell) that the GridLayoutGroup ignores. Placed
            // artifacts live here so a multi-cell artifact draws over its whole group.
            var overlayGo = new GameObject("ArtifactOverlay", typeof(RectTransform), typeof(LayoutElement));
            _artifactOverlay = (RectTransform)overlayGo.transform;
            _artifactOverlay.SetParent(slotGridContent, false);
            _artifactOverlay.anchorMin = Vector2.zero;
            _artifactOverlay.anchorMax = Vector2.one;
            _artifactOverlay.offsetMin = Vector2.zero;
            _artifactOverlay.offsetMax = Vector2.zero;
            overlayGo.GetComponent<LayoutElement>().ignoreLayout = true;
        }

        private void RefreshAll()
        {
            RefreshStorage();
            RebuildOccupancy();
            RefreshCells();
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

        /// <summary>Mark which instance covers each cell, from the exhibit's assignments.</summary>
        private void RebuildOccupancy()
        {
            for (int i = 0; i < _occupant.Length; i++) _occupant[i] = null;

            ExhibitData exhibit = _model.GetExhibitData(_currentExhibitId);
            if (exhibit == null) return;

            foreach (ArtifactSlotAssignment a in exhibit.Slots)
            {
                (int fw, int fh) = FootprintForInstance(a.ArtifactInstanceId);
                int aCol = a.SlotIndex % _cols;
                int aRow = a.SlotIndex / _cols;
                for (int dr = 0; dr < fh; dr++)
                    for (int dc = 0; dc < fw; dc++)
                    {
                        int c = aCol + dc, r = aRow + dr;
                        if (c < _cols && r < _rows) _occupant[r * _cols + c] = a.ArtifactInstanceId;
                    }
            }
        }

        /// <summary>Idle paint: default grid lines, occupancy backgrounds, and each
        /// placed artifact rendered CENTERED across its whole footprint group.</summary>
        private void RefreshCells()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].SetBackground(_occupant[i] != null ? occupiedColor : emptyColor);
                _slots[i].SetBorderColor(lineDefaultColor);
            }

            // Rebuild the group-spanning artifact visuals.
            if (_artifactOverlay != null)
                for (int i = _artifactOverlay.childCount - 1; i >= 0; i--)
                    Destroy(_artifactOverlay.GetChild(i).gameObject);

            ExhibitData exhibit = _model.GetExhibitData(_currentExhibitId);
            if (exhibit == null) return;
            foreach (ArtifactSlotAssignment a in exhibit.Slots)
            {
                OwnedArtifactData owned = _model.GetOwnedArtifact(a.ArtifactInstanceId);
                MuseumArtifactDatabase.Entry entry = owned != null ? _artifacts.GetById(owned.RawArtifactId) : null;
                if (entry == null) continue;
                (int w, int h) = FootprintForInstance(a.ArtifactInstanceId);
                BuildArtifactVisual(a.SlotIndex % _cols, a.SlotIndex / _cols, w, h, entry.Icon);
            }
        }

        /// <summary>
        /// A placed artifact's world visual: an icon centred over its whole footprint
        /// group (same centred look as the drag ghost, minus the backdrop). Positioned
        /// on <see cref="_artifactOverlay"/> from the grid's own cell/spacing/padding.
        /// </summary>
        private void BuildArtifactVisual(int aCol, int aRow, int w, int h, Sprite icon)
        {
            if (_artifactOverlay == null || slotGrid == null) return;

            Vector2 cs = slotGrid.cellSize;
            Vector2 sp = slotGrid.spacing;
            RectOffset pad = slotGrid.padding;
            float gw = w * cs.x + (w - 1) * sp.x;
            float gh = h * cs.y + (h - 1) * sp.y;
            float x = pad.left + aCol * (cs.x + sp.x);
            float y = pad.top + aRow * (cs.y + sp.y);

            var go = new GameObject("ArtifactVisual", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_artifactOverlay, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); // top-left of the content
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(gw, gh);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var irt = (RectTransform)iconGo.transform;
            irt.SetParent(rt, false);
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(ghostIconPadding, ghostIconPadding);
            irt.offsetMax = new Vector2(-ghostIconPadding, -ghostIconPadding);
            var img = iconGo.GetComponent<Image>();
            img.sprite = icon;
            img.raycastTarget = false; // clicks/drags pass through to the cells beneath
            img.preserveAspect = true;
        }

        // ── Footprint / validity ────────────────────────────────────────

        private (int w, int h) FootprintForInstance(string instanceId)
        {
            OwnedArtifactData owned = _model.GetOwnedArtifact(instanceId);
            string size = owned != null ? _artifacts.GetById(owned.RawArtifactId)?.Functional?.ObjectSize : null;
            return SizeToFootprint(size);
        }

        /// <summary>Artifact size → footprint in tiny cells (width × height). See class summary.</summary>
        public static (int w, int h) SizeToFootprint(string objectSize)
        {
            switch ((objectSize ?? "").Trim().ToLowerInvariant())
            {
                case "tiny": return (1, 1);
                case "small": return (1, 2);
                case "medium": return (2, 2);
                case "large": return (2, 4);
                case "huge": return (4, 4);
                default: return (1, 1);
            }
        }

        /// <summary>Snap a hovered cell to the aligned anchor of a (w×h) footprint.</summary>
        private (int aCol, int aRow) AlignedAnchor(int col, int row, int w, int h)
        {
            int aCol = Mathf.Clamp((col / w) * w, 0, Mathf.Max(0, _cols - w));
            int aRow = Mathf.Clamp((row / h) * h, 0, Mathf.Max(0, _rows - h));
            return (aCol, aRow);
        }

        private bool IsValid(int aCol, int aRow, int w, int h, string ignoreInstance)
        {
            if (aCol < 0 || aRow < 0 || aCol + w > _cols || aRow + h > _rows) return false;
            for (int dr = 0; dr < h; dr++)
                for (int dc = 0; dc < w; dc++)
                {
                    string occ = _occupant[(aRow + dr) * _cols + (aCol + dc)];
                    if (occ != null && occ != ignoreInstance) return false;
                }
            return true;
        }

        // ── Drag: start ─────────────────────────────────────────────────

        public void BeginCardDrag(ArtifactCard card, PointerEventData e)
        {
            (int w, int h) = FootprintForInstance(card.InstanceId);
            StartDrag(card.InstanceId, w, h, card.Icon, fromSlot: false, e);
        }

        public void BeginSlotDrag(int cellIndex, PointerEventData e)
        {
            string instance = _occupant != null && cellIndex < _occupant.Length ? _occupant[cellIndex] : null;
            if (instance == null) return; // empty cell — nothing to drag

            (int w, int h) = FootprintForInstance(instance);
            OwnedArtifactData owned = _model.GetOwnedArtifact(instance);
            Sprite icon = owned != null ? _artifacts.GetById(owned.RawArtifactId)?.Icon : null;
            StartDrag(instance, w, h, icon, fromSlot: true, e);
        }

        private void StartDrag(string instanceId, int w, int h, Sprite icon, bool fromSlot, PointerEventData e)
        {
            _dragInstanceId = instanceId;
            _dragW = w; _dragH = h;
            _dragFromSlot = fromSlot;
            _dropHandled = false;

            if (dragLayer != null)
            {
                // Backdrop sized to the artifact's placement GROUP (footprint × cell),
                // with the icon centred on top — so it reads as sitting in the group.
                Vector2 cell = slotGrid != null ? slotGrid.cellSize : new Vector2(88, 88);
                Vector2 sp = slotGrid != null ? slotGrid.spacing : Vector2.zero;
                float gw = w * cell.x + (w - 1) * sp.x;
                float gh = h * cell.y + (h - 1) * sp.y;

                _dragGhost = new GameObject("ArtifactDragGhost", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)_dragGhost.transform;
                rt.SetParent(dragLayer, false);
                rt.sizeDelta = new Vector2(gw, gh);
                var backdrop = _dragGhost.GetComponent<Image>();
                backdrop.color = ghostBackdropColor;
                backdrop.raycastTarget = false;

                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                var irt = (RectTransform)iconGo.transform;
                irt.SetParent(rt, false);
                irt.anchorMin = Vector2.zero;
                irt.anchorMax = Vector2.one;
                irt.offsetMin = new Vector2(ghostIconPadding, ghostIconPadding);
                irt.offsetMax = new Vector2(-ghostIconPadding, -ghostIconPadding);
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.sprite = icon;
                iconImg.raycastTarget = false;
                iconImg.preserveAspect = true; // centres the artwork within the group rect
            }
            DragUpdate(e);
        }

        // ── Drag: update (ghost follow + grid highlight) ────────────────

        public void DragUpdate(PointerEventData e)
        {
            if (_dragGhost != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragLayer, e.position, e.pressEventCamera, out Vector2 local))
                ((RectTransform)_dragGhost.transform).localPosition = local;

            if (_dragInstanceId == null) return;
            HighlightForFootprint(_dragW, _dragH, _dragInstanceId);
        }

        /// <summary>
        /// Preview an artifact's placement options WITHOUT dragging yet (on
        /// pointer-down of its card).
        /// </summary>
        public void PreviewFootprint(string instanceId)
        {
            if (_dragInstanceId != null) return; // a real drag owns the highlight
            (int w, int h) = FootprintForInstance(instanceId);
            HighlightForFootprint(w, h, instanceId);
        }

        public void ClearPreview()
        {
            if (_dragInstanceId != null) return; // drag will repaint on its own
            RefreshCells();
        }

        /// <summary>
        /// Recolour every cell by the placement group it belongs to for a footprint
        /// (w×h). Each cell maps to its size-aligned group; the group's OUTER edges get
        /// the perimeter colour and its INNER edges (shared with same-group cells) get
        /// the inner colour — separate colour sets for placeable (available) vs not.
        /// Backgrounds also tint (occupied / available / empty).
        /// </summary>
        private void HighlightForFootprint(int w, int h, string ignoreInstance)
        {
            for (int row = 0; row < _rows; row++)
                for (int col = 0; col < _cols; col++)
                {
                    int i = row * _cols + col;
                    (int aCol, int aRow) = AlignedAnchor(col, row, w, h);
                    bool valid = IsValid(aCol, aRow, w, h, ignoreInstance);

                    Color perimeter = valid ? availablePerimeterColor : unavailablePerimeterColor;
                    Color inner = valid ? availableInnerColor : unavailableInnerColor;
                    int dc = col - aCol, dr = row - aRow; // position within the group

                    _slots[i].SetEdgeColors(
                        top: dr == 0 ? perimeter : inner,
                        bottom: dr == h - 1 ? perimeter : inner,
                        left: dc == 0 ? perimeter : inner,
                        right: dc == w - 1 ? perimeter : inner);

                    Color bg = _occupant[i] != null ? occupiedColor : (valid ? availableColor : emptyColor);
                    _slots[i].SetBackground(bg);
                }
        }

        // ── Drag: drop / end ────────────────────────────────────────────

        public void DropOnCell(int cellIndex, PointerEventData e)
        {
            if (_dragInstanceId == null) return;
            int col = cellIndex % _cols, row = cellIndex / _cols;
            (int aCol, int aRow) = AlignedAnchor(col, row, _dragW, _dragH);
            if (!IsValid(aCol, aRow, _dragW, _dragH, _dragInstanceId)) return; // EndDrag handles the rest

            _model.AssignArtifactToSlot(_currentExhibitId, aRow * _cols + aCol, _dragInstanceId);
            _dropHandled = true;
        }

        public void EndDrag(PointerEventData e)
        {
            // Dragged a placed artifact out to nowhere valid → send it back to storage.
            if (!_dropHandled && _dragFromSlot && _dragInstanceId != null)
            {
                int anchor = AnchorOf(_dragInstanceId);
                if (anchor >= 0) _model.RemoveArtifactFromSlot(_currentExhibitId, anchor);
            }
            CancelDrag();
            RefreshAll();
        }

        private void CancelDrag()
        {
            if (_dragGhost != null) Destroy(_dragGhost);
            _dragGhost = null;
            _dragInstanceId = null;
            _dragFromSlot = false;
            _dropHandled = false;
        }

        // ── Click a filled cell → remove ────────────────────────────────

        public void ClickCell(int cellIndex)
        {
            if (_occupant == null || cellIndex >= _occupant.Length) return;
            string instance = _occupant[cellIndex];
            if (instance == null) return;
            int anchor = AnchorOf(instance);
            if (anchor >= 0 && _model.RemoveArtifactFromSlot(_currentExhibitId, anchor)) RefreshAll();
        }

        private int AnchorOf(string instanceId)
        {
            ExhibitData exhibit = _model.GetExhibitData(_currentExhibitId);
            if (exhibit == null) return -1;
            foreach (ArtifactSlotAssignment a in exhibit.Slots)
                if (a.ArtifactInstanceId == instanceId) return a.SlotIndex;
            return -1;
        }
    }
}
