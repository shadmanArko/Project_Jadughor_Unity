using Systems.MineSystem.BossLairSystem.Config;
using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.Mine.Config;
using Systems.MineSystem.Mine.View;
using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.Service
{
    /// <summary>
    /// Places the boss lair directly beneath the mine, derived in cell space so
    /// it follows any mine size without configuration changes.
    /// </summary>
    /// <remarks>
    /// Mirrors the generator's cell layout: the mine occupies
    /// <c>x in [-(sizeX/2), -(sizeX/2) + sizeX - 1]</c> and
    /// <c>y in [0, -(sizeY - 1)]</c>, matching
    /// <c>MineGenerationService</c> and <c>ConfigureMineBounds</c>.
    /// </remarks>
    public sealed class BossLairPlacementService
    {
        private readonly MineGenerationConfig _mineConfig;
        private readonly MineView _mineView;
        private readonly BossLairConfig _lairConfig;

        public BossLairPlacementService(
            MineGenerationConfig mineConfig,
            MineView mineView,
            BossLairConfig lairConfig)
        {
            _mineConfig = mineConfig;
            _mineView = mineView;
            _lairConfig = lairConfig;
        }

        /// <summary>Authored cell size of the mine grid, in world units.</summary>
        public float CellWorldSize => _mineView.grid.cellSize.x;

        /// <summary>Lowest cell row the mine occupies.</summary>
        private int ResolveMineBottomCellY() => -(_mineConfig.mineSizeY - 1);

        /// <summary>Leftmost cell column the mine occupies.</summary>
        private int ResolveMineLeftCellX() => -(_mineConfig.mineSizeX / 2);

        /// <summary>
        /// Resolves the arena rect. <paramref name="effectiveGapInCells"/> is
        /// supplied by the caller because the camera may require a larger gap
        /// than the config asks for, to keep the mine out of frame.
        /// </summary>
        public BossLairPlacement Resolve(
            BossProceduralLairConfig config,
            int effectiveGapInCells)
        {
            var width = config.InteriorWidthInCells;
            var height = config.InteriorHeightInCells;
            var gap = Mathf.Max(1, effectiveGapInCells);

            var topCellY = ResolveMineBottomCellY() - gap;
            var bottomCellY = topCellY - height + 1;

            var mineLeftX = ResolveMineLeftCellX();
            var originX = config.CentreUnderMine
                ? mineLeftX + (_mineConfig.mineSizeX - width) / 2
                : mineLeftX;

            var interiorCells = new BoundsInt(
                new Vector3Int(originX, bottomCellY, 0),
                new Vector3Int(width, height, 1));

            // CellToWorld gives the cell's minimum corner, which is exactly the
            // anchor a lair whose local cell (0,0) starts there needs.
            var rootWorld = _mineView.grid.CellToWorld(
                new Vector3Int(originX, bottomCellY, 0));
            var cellWorldSize = _mineView.grid.cellSize.x;

            return new BossLairPlacement(
                interiorCells,
                config.BorderThicknessInCells,
                new Vector2(rootWorld.x, rootWorld.y),
                cellWorldSize);
        }

        /// <summary>
        /// Smallest gap that keeps the mine outside the camera window, plus the
        /// confiner's own padding (<see cref="BossLairConfig.CameraBoundsPaddingInCells"/>).
        /// The confiner box extends that many extra cells past the arena's south
        /// edge, so a camera confined to it can pan that much closer to the mine
        /// than the raw arena footprint implies. When the arena is larger than
        /// the window the confiner handles it and 1 (plus padding) is enough;
        /// when it is smaller the camera holds a fixed shot whose window
        /// overhangs the arena, and the gap has to cover that overhang too (plus
        /// padding).
        /// </summary>
        public int ResolveRequiredGapInCells(
            BossProceduralLairConfig config,
            float cameraWindowHeightInCells)
        {
            var padding = _lairConfig.CameraBoundsPaddingInCells;
            var overhang = cameraWindowHeightInCells * 0.5f -
                           config.InteriorHeightInCells * 0.5f;
            if (overhang <= 0f)
                return 1 + padding;
            return Mathf.CeilToInt(overhang) + 1 + padding;
        }
    }
}
