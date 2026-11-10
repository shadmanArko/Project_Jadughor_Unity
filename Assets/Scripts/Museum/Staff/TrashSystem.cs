using System.Collections.Generic;
using ProjectMuseum.Builder;
using ProjectMuseum.Data;
using ProjectMuseum.Guests;
using ProjectMuseum.Narrative;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace ProjectMuseum.Staff
{
    /// <summary>
    /// Shows the museum's litter (<see cref="MuseumData.Trash"/>) and produces it.
    ///
    /// - Visuals: one pooled SpriteRenderer per piece, on the placed-object layer at a very low
    ///   order, so it sits on the floor under every exhibit and character. Rebuilt on load.
    /// - Littering: every museum clock tick (10 game minutes) each guest inside has
    ///   <see cref="litterChancePerTick"/> to drop something on their tile. Pausing the clock
    ///   stops littering too.
    /// - Debug: press <see cref="spawnKey"/> (default T) or use the context menu to drop trash on
    ///   a random tile, handy for watching the janitor work.
    ///
    /// Put one in Museum.unity under the SceneContext (it needs MuseumDataModel injected).
    /// </summary>
    public class TrashSystem : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("Trash sprites — assign your art here. Each piece of litter picks one (by its saved " +
                 "Variant index). While this is empty, litter still exists and gets swept, it's just invisible.")]
        [SerializeField] private Sprite[] trashSprites;
        [SerializeField] private string sortingLayer = "PlacedMuseumObject";
        [Tooltip("Far below MuseumSortingSystem's base order, so litter never covers an object.")]
        [SerializeField] private int sortingOrder = -500;
        [Tooltip("Random offset from the tile centre, in tiles (kept stable per piece).")]
        [SerializeField, Range(0f, 0.3f)] private float scatter = 0.18f;

        [Header("Guest littering")]
        [Tooltip("Chance per guest inside, per 10 game minutes, to drop litter. 0 = off.")]
        [SerializeField, Range(0f, 1f)] private float litterChancePerTick = 0.04f;
        [Tooltip("Stop producing litter past this many pieces.")]
        [SerializeField, Min(1)] private int maxTrash = 30;

        [Header("Debug")]
        [SerializeField] private Key spawnKey = Key.T;

        [InjectOptional] private MuseumDataModel _model;

        private Grid _grid;
        private GuestController _guests;
        private bool _warnedNoSprites;
        private int _layerId;
        private readonly Dictionary<string, SpriteRenderer> _visuals = new Dictionary<string, SpriteRenderer>();
        private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();

        private void OnEnable()
        {
            StaffActions.OnTrashAdded += Show;
            StaffActions.OnTrashRemoved += Hide;
            BuilderActions.OnMuseumDataReloaded += Rebuild;
            MuseumActions.OnTimeUpdated += OnClockTick;
        }

        private void OnDisable()
        {
            StaffActions.OnTrashAdded -= Show;
            StaffActions.OnTrashRemoved -= Hide;
            BuilderActions.OnMuseumDataReloaded -= Rebuild;
            MuseumActions.OnTimeUpdated -= OnClockTick;
        }

        private void Start()
        {
            _grid = FindFirstObjectByType<Grid>();
            _guests = FindFirstObjectByType<GuestController>();
            _layerId = SortingLayer.NameToID(sortingLayer);
            if (!SortingLayer.IsValid(_layerId))
                Debug.LogError($"[TrashSystem] Sorting layer '{sortingLayer}' does not exist.", this);
            if (_model == null)
                Debug.LogWarning("[TrashSystem] No MuseumDataModel — keep this under the SceneContext.", this);

            _model?.EnsureInitialized();
            Rebuild();
        }

        private void Update()
        {
            if (spawnKey != Key.None && Keyboard.current != null && Keyboard.current[spawnKey].wasPressedThisFrame)
                SpawnRandomTrash();
        }

        // ── Producing litter ──────────────────────────────────────────────

        [ContextMenu("Spawn Random Trash")]
        public TrashData SpawnRandomTrash()
        {
            if (_model == null || _model.Tiles.Count == 0) return null;
            for (var attempt = 0; attempt < 30; attempt++)
            {
                var tile = _model.Tiles[Random.Range(0, _model.Tiles.Count)];
                if (!tile.Walkable) continue;
                var trash = SpawnTrashAt(tile.Cell);
                if (trash != null) return trash;
            }
            return null;
        }

        /// <summary>Drop litter on a tile (respects the cap). Null if the tile can't take it.</summary>
        public TrashData SpawnTrashAt(Vector2Int cell)
        {
            if (_model == null || _model.Trash.Count >= maxTrash) return null;
            return _model.AddTrash(cell, Random.Range(0, VariantCount));
        }

        private void OnClockTick(int minutes, int hours, int days, int months, int years)
        {
            if (litterChancePerTick <= 0f || _guests == null || _model == null) return;

            var guests = _guests.ActiveGuests;
            for (var i = 0; i < guests.Count; i++)
            {
                var g = guests[i];
                if (g == null || !g.IsInside) continue;
                if (Random.value < litterChancePerTick) SpawnTrashAt(g.Cell);
            }
        }

        [ContextMenu("Clear All Trash")]
        private void ClearAll()
        {
            if (_model == null) return;
            for (var i = _model.Trash.Count - 1; i >= 0; i--) _model.RemoveTrash(_model.Trash[i].Id);
        }

        // ── Visuals ───────────────────────────────────────────────────────

        private bool HasSprites => trashSprites != null && trashSprites.Length > 0;
        private int VariantCount => HasSprites ? trashSprites.Length : 1;

        private void Rebuild()
        {
            foreach (var r in _visuals.Values) Release(r);
            _visuals.Clear();
            if (_model == null) return;
            foreach (var t in _model.Trash) Show(t);
        }

        private void Show(TrashData trash)
        {
            if (trash == null || _grid == null || _visuals.ContainsKey(trash.Id)) return;
            if (!HasSprites)
            {
                if (!_warnedNoSprites)
                {
                    _warnedNoSprites = true;
                    Debug.LogWarning("[TrashSystem] No Trash Sprites assigned — litter is invisible " +
                                     "(it still exists and janitors still sweep it). Assign sprites on this component.", this);
                }
                return;
            }

            var r = _pool.Count > 0 ? _pool.Pop() : CreateRenderer();
            r.sprite = trashSprites[Mathf.Abs(trash.Variant) % trashSprites.Length];

            // Stable scatter per piece, so a reload doesn't reshuffle the floor.
            var h = trash.Id.GetHashCode();
            var offset = new Vector2(((h & 0xFF) / 255f - 0.5f) * 2f * scatter,
                                     (((h >> 8) & 0xFF) / 255f - 0.5f) * 2f * scatter);
            r.flipX = (h & 0x10000) != 0;

            var local = _grid.CellToLocalInterpolated(new Vector3(trash.X + 0.5f + offset.x, trash.Y + 0.5f + offset.y, 0f));
            var world = _grid.transform.TransformPoint(local);
            world.z = 0f;
            r.transform.position = world;
            r.gameObject.SetActive(true);
            _visuals[trash.Id] = r;
        }

        private void Hide(TrashData trash)
        {
            if (trash == null || !_visuals.TryGetValue(trash.Id, out var r)) return;
            _visuals.Remove(trash.Id);
            Release(r);
        }

        private void Release(SpriteRenderer r)
        {
            r.gameObject.SetActive(false);
            _pool.Push(r);
        }

        private SpriteRenderer CreateRenderer()
        {
            var go = new GameObject("Trash");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingLayerID = _layerId;
            r.sortingOrder = sortingOrder;
            return r;
        }
    }
}
