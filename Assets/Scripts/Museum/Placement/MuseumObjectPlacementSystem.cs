using System;
using System.Collections.Generic;
using ProjectMuseum.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zenject;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Places museum objects (Exhibit / DecorationShop / DecorationOther /
    /// Sanitation) using real prefabs — one per (category, footprint size), assigned
    /// in <see cref="PlaceablePrefabConfig"/> — so ghosts and placed objects use an
    /// artist-built shape rather than a runtime-computed sprite size. The prefab is
    /// shared by every variation of that size; the specific look is applied by
    /// swapping in that variation's cropped artwork via
    /// <see cref="PlaceableObjectView.ApplyVariationSprite"/> right after spawning.
    ///
    ///  • Builder card click → look up the variation's size + texture, spawn a ghost
    ///    copy of that size's prefab (with the right sprite already applied) that
    ///    follows the mouse (snapped to the grid), tinted green/red for
    ///    valid+affordable / not.
    ///  • Left-click places (stays in placement mode so you can place several).
    ///  • Right-click / Esc cancels.
    ///  • On Start (and after a save load), every object already in the data is
    ///    respawned from its prefab.
    ///
    /// Flooring/Wallpaper card clicks are ignored here — handled elsewhere.
    /// </summary>
    public class MuseumObjectPlacementSystem : MonoBehaviour
    {
        [Inject] private MuseumDataModel _model;
        [Inject] private BuilderDatabase _database;
        [Inject] private PlaceablePrefabConfig _prefabConfig;

        [Header("Scene references")]
        [SerializeField] private Grid grid;
        [Tooltip("Parent for spawned museum objects (keeps hierarchy tidy).")]
        [SerializeField] private Transform objectsParent;

        [Header("Ghost preview")]
        [SerializeField] private Color validTint = new Color(0.5f, 1f, 0.5f, 0.75f);
        [SerializeField] private Color invalidTint = new Color(1f, 0.4f, 0.4f, 0.75f);

        [Header("Diagnostics")]
        [Tooltip("Log why a click didn't place anything (over UI / no space / can't afford).")]
        [SerializeField] private bool logPlacementBlocks = true;

        private Camera _cam;

        // Ghost/pending placement state
        private bool _isPlacing;
        private BuilderCardType _pendingType;
        private string _pendingName;
        private BuilderDatabase.PlacementInfo _pendingInfo;
        private GameObject _ghostGo;
        private PlaceableObjectView _ghostView;

        // Spawned visuals by PlacedObjectData.Id (for removal support)
        private readonly Dictionary<string, GameObject> _spawned = new();

        private static readonly BuilderCardType[] PlaceableTypes =
        {
            BuilderCardType.Exhibit, BuilderCardType.DecorationShop,
            BuilderCardType.DecorationOther, BuilderCardType.Sanitation
        };

        // ── Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (grid == null) grid = FindFirstObjectByType<Grid>();
            if (objectsParent == null) objectsParent = transform;
        }

        private void OnEnable()
        {
            BuilderActions.OnClickBuilderCard += OnClickBuilderCard;
            BuilderActions.OnObjectRemoved += OnObjectRemoved;
            BuilderActions.OnMuseumDataReloaded += OnDataReloaded;
        }

        private void OnDisable()
        {
            BuilderActions.OnClickBuilderCard -= OnClickBuilderCard;
            BuilderActions.OnObjectRemoved -= OnObjectRemoved;
            BuilderActions.OnMuseumDataReloaded -= OnDataReloaded;
            CancelPlacement();
        }

        private void Start()
        {
            if (!ValidateInjection()) return;
            _model.EnsureInitialized(); // guard against Start-vs-IInitializable order races
            SpawnLoadedObjects();
        }

        /// <summary>
        /// Zenject only fills [Inject] fields when a SceneContext with MuseumInstaller
        /// is present. Without it these stay null and every click would silently
        /// throw — fail loudly and disable instead.
        /// </summary>
        private bool ValidateInjection()
        {
            if (_model != null && _database != null && _prefabConfig != null) return true;
            Debug.LogError("[MuseumObjectPlacementSystem] Missing injected dependency — add a " +
                "Zenject SceneContext to this scene with MuseumInstaller in its Scriptable Object " +
                "Installers, and make sure MuseumInstaller has a PlaceablePrefabConfig assigned " +
                "(see README_Builder_Setup.md). Disabling placement.", this);
            enabled = false;
            return false;
        }

        /// <summary>Data was replaced (save load / new game) — rebuild all visuals.</summary>
        private void OnDataReloaded()
        {
            CancelPlacement();
            ClearAllSpawned();
            SpawnLoadedObjects();
        }

        private void ClearAllSpawned()
        {
            foreach (GameObject go in _spawned.Values)
                if (go != null) Destroy(go);
            _spawned.Clear();
        }

        /// <summary>Respawn visuals for every object already in the data (save load).</summary>
        private void SpawnLoadedObjects()
        {
            int count = 0;
            foreach (PlacedObjectData placed in _model.PlacedObjects)
                if (SpawnVisual(placed) != null) count++;
            if (count > 0)
                Debug.Log($"[MuseumObjectPlacementSystem] Respawned {count} saved object(s).");
        }

        // ── Card click → start placing ──────────────────────────────────

        private void OnClickBuilderCard(BuilderCardType type, string cardName)
        {
            if (Array.IndexOf(PlaceableTypes, type) < 0) return; // flooring/wallpaper: not ours
            if (_model == null || _database == null || _prefabConfig == null) return; // guarded + logged in Start

            if (!_database.TryGetPlacementInfo(type, cardName, out BuilderDatabase.PlacementInfo info))
            {
                Debug.LogWarning($"[MuseumObjectPlacementSystem] Unknown variation '{cardName}' ({type}).");
                return;
            }

            GameObject prefab = _prefabConfig.GetPrefab(type, info.WidthInTiles, info.LengthInTiles);
            if (prefab == null) return; // PlaceablePrefabConfig already logged the configured sizes

            CancelPlacement(); // replace any pending ghost

            _pendingType = type;
            _pendingName = cardName;
            _pendingInfo = info;
            _isPlacing = true;

            _ghostGo = Instantiate(prefab);
            _ghostGo.name = $"Ghost_{cardName}";
            _ghostView = _ghostGo.GetComponent<PlaceableObjectView>();
            if (_ghostView == null) _ghostView = _ghostGo.AddComponent<PlaceableObjectView>();
            _ghostView.ApplyVariationSprite(BuilderSpriteUtil.FirstFrameSprite(info.Texture, info.NumberOfFrames));
            StripGhostComponents(_ghostGo);

            BuilderActions.OnPlacementStarted?.Invoke(type, cardName);
        }

        /// <summary>A ghost shouldn't collide or otherwise act like a real placed object.</summary>
        private static void StripGhostComponents(GameObject go)
        {
            foreach (Collider2D c in go.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        // ── Ghost follow + place/cancel ──────────────────────────────────

        private void Update()
        {
            if (!_isPlacing || _ghostGo == null) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Keyboard kb = Keyboard.current;

            if (mouse.rightButton.wasPressedThisFrame || (kb != null && kb.escapeKey.wasPressedThisFrame))
            {
                CancelPlacement();
                BuilderActions.OnPlacementCancelled?.Invoke();
                return;
            }

            Vector2Int anchor = MouseCell(mouse);
            _ghostGo.transform.position = CellToWorld(anchor);

            bool canPlace = _model.CanPlace(anchor, _pendingInfo.WidthInTiles, _pendingInfo.LengthInTiles);
            bool canAfford = _model.CanAfford(_pendingInfo.Cost);
            bool valid = canPlace && canAfford;
            _ghostView.SetGhostTint(valid ? validTint : invalidTint);

            if (!mouse.leftButton.wasPressedThisFrame) return;

            bool overUi = IsPointerOverUi();
            if (overUi)
            {
                if (logPlacementBlocks)
                    Debug.Log("[MuseumObjectPlacementSystem] Click ignored — pointer is over UI.");
            }
            else if (!valid)
            {
                if (logPlacementBlocks)
                    Debug.Log($"[MuseumObjectPlacementSystem] Can't place at {anchor} — " +
                        (!canPlace ? "blocked or undeveloped tile(s)." : $"can't afford (${_pendingInfo.Cost})."));
            }
            else
            {
                TryPlaceAt(anchor);
            }
        }

        private void TryPlaceAt(Vector2Int anchor)
        {
            PlacedObjectData placed = _model.PlaceObject(
                _pendingType, _pendingName, anchor,
                _pendingInfo.WidthInTiles, _pendingInfo.LengthInTiles, _pendingInfo.Cost);
            if (placed == null) return; // model already logged why

            SpawnVisual(placed);
            // Stays in placement mode so several can be placed in a row; right-click/Esc to stop.
        }

        private void CancelPlacement()
        {
            _isPlacing = false;
            if (_ghostGo != null) Destroy(_ghostGo);
            _ghostGo = null;
            _ghostView = null;
        }

        private void OnObjectRemoved(PlacedObjectData placed)
        {
            if (_spawned.TryGetValue(placed.Id, out GameObject go) && go != null)
                Destroy(go);
            _spawned.Remove(placed.Id);
        }

        // ── Visual spawning ──────────────────────────────────────────────

        private GameObject SpawnVisual(PlacedObjectData placed)
        {
            if (!_database.TryGetPlacementInfo(placed.Type, placed.VariationName,
                    out BuilderDatabase.PlacementInfo info))
            {
                Debug.LogWarning($"[MuseumObjectPlacementSystem] No variation data for saved " +
                                 $"object '{placed.VariationName}' ({placed.Type}) — skipped.");
                return null;
            }

            GameObject prefab = _prefabConfig.GetPrefab(placed.Type, info.WidthInTiles, info.LengthInTiles);
            if (prefab == null) return null; // PlaceablePrefabConfig already logged the configured sizes

            GameObject go = Instantiate(prefab, objectsParent);
            go.name = $"{placed.Type}_{placed.VariationName}";
            go.transform.position = CellToWorld(placed.AnchorCell);

            var view = go.GetComponent<PlaceableObjectView>();
            if (view == null) view = go.AddComponent<PlaceableObjectView>();
            view.ApplyVariationSprite(BuilderSpriteUtil.FirstFrameSprite(info.Texture, info.NumberOfFrames));
            view.Initialize(placed);

            if (go.GetComponent<YSortable>() == null) go.AddComponent<YSortable>();

            _spawned[placed.Id] = go;
            return go;
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private Vector2Int MouseCell(Mouse mouse)
        {
            Vector3 world = _cam.ScreenToWorldPoint(mouse.position.ReadValue());
            world.z = 0f;
            Vector3Int cell = grid.WorldToCell(world);
            return new Vector2Int(cell.x, cell.y);
        }

        private Vector3 CellToWorld(Vector2Int cell)
        {
            Vector3 pos = grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            pos.z = 0f;
            return pos;
        }

        private static bool IsPointerOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
