using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject;
using ProjectMuseum.Data;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Repaints the floor tilemap from <see cref="MuseumData.Tiles"/> whenever the
    /// data is reloaded (save load / new game). Painting during play is recorded
    /// the other way round (MuseumTilePlacementManager → OnFloorTilePainted → data);
    /// this closes the loop so a loaded save LOOKS like it was saved.
    ///
    /// Tiles are matched by TileBase asset name against the tile manager's
    /// AvailableTiles. Records whose name isn't in the set (e.g. the seeded
    /// default on a fresh chunk) are left untouched.
    /// </summary>
    public class MuseumFloorSync : MonoBehaviour
    {
        [Inject] private MuseumDataModel _model;

        [Header("Scene references")]
        [Tooltip("The floor tilemap the placement manager paints on.")]
        [SerializeField] private Tilemap floorTilemap;
        [Tooltip("Source of the placeable tiles (matched by asset name).")]
        [SerializeField] private MuseumTilePlacementManager tileManager;

        private void OnEnable() => BuilderActions.OnMuseumDataReloaded += RepaintFromData;
        private void OnDisable() => BuilderActions.OnMuseumDataReloaded -= RepaintFromData;

        private void Start()
        {
            _model.EnsureInitialized();
            RepaintFromData();
        }

        private void RepaintFromData()
        {
            if (floorTilemap == null || tileManager == null)
            {
                Debug.LogWarning("[MuseumFloorSync] Floor Tilemap / Tile Manager not assigned.");
                return;
            }

            // name → TileBase from the manager's tileset
            var byName = new Dictionary<string, TileBase>();
            foreach (TileBase tile in tileManager.AvailableTiles)
                if (tile != null) byName[tile.name] = tile;

            int painted = 0;
            foreach (MuseumTileData tile in _model.Tiles)
            {
                if (string.IsNullOrEmpty(tile.TileVariationName)) continue;
                if (!byName.TryGetValue(tile.TileVariationName, out TileBase tb)) continue;

                floorTilemap.SetTile(new Vector3Int(tile.X, tile.Y, 0), tb);
                painted++;
            }

            if (painted > 0)
                Debug.Log($"[MuseumFloorSync] Repainted {painted} floor tile(s) from data.");
        }
    }
}
