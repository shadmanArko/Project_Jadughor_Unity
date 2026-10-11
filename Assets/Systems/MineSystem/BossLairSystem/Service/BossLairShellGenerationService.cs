using Systems.MineSystem.BossLairSystem.Config;
using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.BossLairSystem.View;
using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.Service
{
    /// <summary>
    /// Paints the arena's sealed shell and backdrop into the lair's own
    /// tilemaps, in the lair's local cell space where (0,0) is the bottom-left
    /// interior cell.
    /// </summary>
    /// <remarks>
    /// Two properties fall out of painting into the lair's own tilemap rather
    /// than the mine's, and both are load-bearing:
    /// <list type="bullet">
    /// <item>The shell is <b>unbreakable</b> because these cells have no
    /// <c>Cell</c> record in <c>MineData</c>, and <c>MineModel.TryHitCell</c>
    /// returns false when the lookup yields null.</item>
    /// <item>The interior is <b>climbable</b> because climbability is decided by
    /// the <i>mine's</i> wall tilemap having no tile at that position, which is
    /// true everywhere out here. The player is kept inside by the sealed
    /// collider, not by a tilemap query.</item>
    /// </list>
    /// Painting here also avoids expanding the mine's own tilemap: a distant
    /// <c>SetTile</c> on it would grow its dense storage region and force its
    /// synchronous composite collider to re-weld across the whole span on every
    /// mined cell.
    /// </remarks>
    public sealed class BossLairShellGenerationService
    {
        public void Generate(
            BossLairView view,
            BossProceduralLairConfig config,
            BossLairPlacement placement)
        {
            if (view == null || config == null || !placement.IsValid)
                return;

            // Cleared rather than trusted: the prefab's tilemaps still carry
            // serialized bounds from previously authored content.
            view.wallTileMap.ClearAllTiles();
            view.backgroundTileMap.ClearAllTiles();

            PaintBackdrop(view, config, placement);
            PaintShell(view, config, placement);
        }

        public void Clear(BossLairView view)
        {
            if (view == null)
                return;
            view.wallTileMap.ClearAllTiles();
            view.backgroundTileMap.ClearAllTiles();
        }

        private static void PaintBackdrop(
            BossLairView view,
            BossProceduralLairConfig config,
            BossLairPlacement placement)
        {
            var tile = config.InteriorBackdropTile;
            if (tile == null)
                return;
            for (var x = 0; x < placement.WidthInCells; x++)
            for (var y = 0; y < placement.HeightInCells; y++)
                view.backgroundTileMap.SetTile(new Vector3Int(x, y, 0), tile);
        }

        /// <summary>
        /// Paints a closed ring of <c>borderThickness</c> cells around the
        /// interior. Iterating the expanded rect and skipping the interior
        /// guarantees the corners are filled, which a four-edge approach can
        /// miss.
        /// </summary>
        private static void PaintShell(
            BossLairView view,
            BossProceduralLairConfig config,
            BossLairPlacement placement)
        {
            var tile = config.BorderTile;
            if (tile == null)
                return;

            var thickness = Mathf.Max(1, placement.BorderThickness);
            var width = placement.WidthInCells;
            var height = placement.HeightInCells;

            for (var x = -thickness; x < width + thickness; x++)
            for (var y = -thickness; y < height + thickness; y++)
            {
                var isInterior = x >= 0 && x < width && y >= 0 && y < height;
                if (isInterior)
                    continue;
                view.wallTileMap.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }
    }
}
