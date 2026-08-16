using ProjectMuseum.Data;
using UnityEngine;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Placed exhibit prefab. Left-clicking it opens the exhibit editor UI (where
    /// the player drags artifacts into its display slots) via
    /// <see cref="BuilderActions.OnExhibitClicked"/>, keyed by this exhibit's Id.
    ///
    /// It also DISPLAYS the artifacts assigned to this exhibit: for each saved slot
    /// assignment it sets the artifact's isometric sprite on the pre-placed
    /// SpriteRenderer configured in <see cref="ExhibitSlotLayout"/> and enables it;
    /// every unassigned slot renderer is disabled. Redraws live when the assignments
    /// change (<see cref="BuilderActions.OnExhibitArtifactsChanged"/>) and on load
    /// (the placement system respawns and re-runs setup). Removing the exhibit frees
    /// its artifacts back to storage (handled by the data model).
    /// </summary>
    public class ExhibitObjectView : PlaceableObjectView
    {
        [Tooltip("How far in front of the exhibit body the artifacts draw. 1 = just in " +
                 "front of the body (they still Y-sort among themselves).")]
        [SerializeField] private int artifactSortOffset = 1;

        private MuseumDataModel _model;
        private MuseumArtifactDatabase _artifacts;
        private ExhibitSlotLayout _layout;
        private int _widthInTiles = 1;
        private bool _subscribed;

        public override void Interact()
        {
            if (!IsPlaced || string.IsNullOrEmpty(Id)) return;
            BuilderActions.OnExhibitClicked?.Invoke(Id);
        }

        /// <summary>
        /// Wire the services this view needs (it is spawned via plain Instantiate, so
        /// it isn't Zenject-injected) and draw the currently-assigned artifacts.
        /// Called by <c>MuseumObjectPlacementSystem</c> right after registration.
        /// </summary>
        public void SetupArtifacts(MuseumDataModel model, MuseumArtifactDatabase artifacts, int widthInTiles)
        {
            _model = model;
            _artifacts = artifacts;
            _widthInTiles = Mathf.Max(1, widthInTiles);
            _layout = GetComponent<ExhibitSlotLayout>();

            if (_layout == null)
                Debug.LogWarning($"[ExhibitObjectView] '{name}' has no ExhibitSlotLayout — " +
                                 "artifacts can't be displayed. Add one and assign its slot renderers.", this);
            else
                // Tag every slot renderer so the sorting system draws it in front of the
                // exhibit body. Must happen BEFORE the sorting system registers this
                // object (the placement system calls SetupArtifacts first for that reason).
                foreach (SpriteRenderer r in _layout.SlotRenderers)
                {
                    var off = r.GetComponent<MuseumSortOffset>();
                    if (off == null) off = r.gameObject.AddComponent<MuseumSortOffset>();
                    off.offset = artifactSortOffset;
                }

            if (!_subscribed)
            {
                BuilderActions.OnExhibitArtifactsChanged += OnArtifactsChanged;
                _subscribed = true;
            }
            RenderArtifacts();
        }

        private void OnDestroy()
        {
            if (_subscribed) BuilderActions.OnExhibitArtifactsChanged -= OnArtifactsChanged;
        }

        private void OnArtifactsChanged(string exhibitId)
        {
            if (exhibitId == Id) RenderArtifacts();
        }

        // ── Rendering ────────────────────────────────────────────────────

        private void RenderArtifacts()
        {
            if (_layout == null) return;

            _layout.SetAllSlotsVisible(false); // hide every slot; occupied ones re-shown below

            if (_model == null || _artifacts == null || string.IsNullOrEmpty(Id)) return;
            ExhibitData exhibit = _model.GetExhibitData(Id);
            if (exhibit == null) return;

            int cols = _widthInTiles * _layout.SlotsPerTileAxis;
            foreach (ArtifactSlotAssignment a in exhibit.Slots)
                ShowAssignment(a, cols);
        }

        private void ShowAssignment(ArtifactSlotAssignment a, int cols)
        {
            OwnedArtifactData owned = _model.GetOwnedArtifact(a.ArtifactInstanceId);
            MuseumArtifactDatabase.Entry entry = owned != null ? _artifacts.GetById(owned.RawArtifactId) : null;
            if (entry == null || entry.IsometricSprite == null) return;

            ArtifactSize size = ExhibitSlotLayout.ParseSize(entry.Functional?.ObjectSize);
            int col = a.SlotIndex % cols;
            int row = a.SlotIndex / cols;

            SpriteRenderer r = _layout.GetRenderer(size, col, row);
            if (r == null)
            {
                Debug.LogWarning($"[ExhibitObjectView] No slot renderer for {size} ({col},{row}) on " +
                                 $"'{name}'. Assign it in the ExhibitSlotLayout.", this);
                return;
            }
            r.gameObject.SetActive(true);
            r.sprite = entry.IsometricSprite;
            r.enabled = true;
        }
    }
}
