using System.Collections.Generic;
using UnityEngine;
using Zenject;
using ProjectMuseum.Data;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Owns the scene's wall segments and their wallpapers:
    ///
    ///  • Registers every child of the assigned wall containers into
    ///    <see cref="MuseumData.Walls"/> (id = "container/childIndex").
    ///  • Wallpaper builder card click → applies that wallpaper to ALL walls
    ///    (basic flow for now — per-wall click selection comes later), writes data.
    ///  • On start and on save-load, re-applies each wall's saved wallpaper.
    ///  • Clearing restores the walls' original sprites (cached at startup).
    ///
    /// Visuals are a straight sprite swap to the wallpaper's first frame — crude
    /// until the real wall art pass, but fully testable and fully saved.
    /// </summary>
    public class MuseumWallpaperSystem : MonoBehaviour
    {
        [Inject] private MuseumDataModel _model;
        [Inject] private BuilderDatabase _database;

        [Header("Scene walls")]
        [Tooltip("Wall containers whose children are individual wall segments " +
                 "(e.g. Left Walls, Right Walls, Bottom Left Walls, Bottom Right Walls).")]
        [SerializeField] private GameObject[] wallContainers = new GameObject[0];

        [Header("Visuals")]
        [Tooltip("Pixels-per-unit for wallpaper sprites (match your wall art PPU).")]
        [SerializeField] private float wallpaperPixelsPerUnit = 100f;

        // wallId → segment renderer, and its original sprite for "clear".
        private readonly Dictionary<string, SpriteRenderer> _wallRenderers = new();
        private readonly Dictionary<string, Sprite> _originalSprites = new();

        // ── Lifecycle ───────────────────────────────────────────────────

        private void OnEnable()
        {
            BuilderActions.OnClickBuilderCard += OnClickBuilderCard;
            BuilderActions.OnWallpaperChanged += OnWallpaperChanged;
            BuilderActions.OnMuseumDataReloaded += ReapplyAllFromData;
        }

        private void OnDisable()
        {
            BuilderActions.OnClickBuilderCard -= OnClickBuilderCard;
            BuilderActions.OnWallpaperChanged -= OnWallpaperChanged;
            BuilderActions.OnMuseumDataReloaded -= ReapplyAllFromData;
        }

        private void Start()
        {
            _model.EnsureInitialized();
            RegisterSceneWalls();
            ReapplyAllFromData();
        }

        // ── Registration ────────────────────────────────────────────────

        /// <summary>Index every wall segment and make sure it has a data record.</summary>
        private void RegisterSceneWalls()
        {
            _wallRenderers.Clear();
            foreach (GameObject container in wallContainers)
            {
                if (container == null) continue;
                Transform t = container.transform;
                for (int i = 0; i < t.childCount; i++)
                {
                    var sr = t.GetChild(i).GetComponentInChildren<SpriteRenderer>();
                    if (sr == null) continue;

                    string id = $"{container.name}/{i}";
                    _wallRenderers[id] = sr;
                    if (!_originalSprites.ContainsKey(id))
                        _originalSprites[id] = sr.sprite;
                    _model.EnsureWall(id);
                }
            }
            Debug.Log($"[MuseumWallpaperSystem] Registered {_wallRenderers.Count} wall segment(s).");
        }

        // ── Card click → apply to all walls ─────────────────────────────

        private void OnClickBuilderCard(BuilderCardType type, string cardName)
        {
            if (type != BuilderCardType.Wallpaper) return;
            _model.SetAllWallpapers(cardName); // raises OnWallpaperChanged per wall
        }

        // ── Visual application ──────────────────────────────────────────

        private void OnWallpaperChanged(string wallId, string wallpaperName)
        {
            if (!_wallRenderers.TryGetValue(wallId, out SpriteRenderer sr) || sr == null)
                return;
            ApplyToRenderer(sr, wallId, wallpaperName);
        }

        /// <summary>Re-apply every registered wall's saved wallpaper (used after load).</summary>
        private void ReapplyAllFromData()
        {
            foreach (WallData wall in _model.Walls)
                if (_wallRenderers.TryGetValue(wall.Id, out SpriteRenderer sr) && sr != null)
                    ApplyToRenderer(sr, wall.Id, wall.WallpaperName);
        }

        private void ApplyToRenderer(SpriteRenderer sr, string wallId, string wallpaperName)
        {
            // Empty name = bare wall → restore the original sprite.
            if (string.IsNullOrEmpty(wallpaperName))
            {
                if (_originalSprites.TryGetValue(wallId, out Sprite original))
                    sr.sprite = original;
                return;
            }

            if (!_database.TryGetPlacementInfo(BuilderCardType.Wallpaper, wallpaperName,
                    out BuilderDatabase.PlacementInfo info) || info.Texture == null)
            {
                Debug.LogWarning($"[MuseumWallpaperSystem] Wallpaper '{wallpaperName}' has no texture.");
                return;
            }

            int frames = Mathf.Max(1, info.NumberOfFrames);
            var rect = new Rect(0f, 0f, info.Texture.width / (float)frames, info.Texture.height);
            sr.sprite = Sprite.Create(info.Texture, rect, new Vector2(0.5f, 0f), wallpaperPixelsPerUnit);
        }

        // ── Testing helpers ─────────────────────────────────────────────

        [ContextMenu("Clear All Wallpapers")]
        private void ClearAllWallpapers()
        {
            _model.ClearAllWallpapers(); // raises OnWallpaperChanged per wall → restores originals
        }
    }
}
