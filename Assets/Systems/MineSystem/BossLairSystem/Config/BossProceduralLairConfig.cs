using UnityEngine;
using UnityEngine.Tilemaps;

namespace Systems.MineSystem.BossLairSystem.Config
{
    /// <summary>
    /// Per-boss arena geometry. One asset per boss, referenced from that boss's
    /// <c>BossProfileScriptable</c>, so each boss can have a differently sized
    /// lair and its own camera zoom.
    /// </summary>
    /// <remarks>
    /// The lair is positioned relative to the mine's bottom edge in cell space,
    /// so it follows any mine size automatically and there is no absolute origin
    /// to keep in sync. Cell space is also immune to the mine grid's world
    /// transform, which currently only nets to the origin because the MineView
    /// root and its grid child carry equal and opposite offsets.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "BossProceduralLairConfig",
        menuName = "Boss/Boss Procedural Lair Config")]
    public sealed class BossProceduralLairConfig : ScriptableObject
    {
        [Header("Arena Size")]
        [Tooltip("Playable interior width in cells.")]
        [Min(1)] [SerializeField] private int interiorWidthInCells = 15;
        [Tooltip("Playable interior height in cells.")]
        [Min(1)] [SerializeField] private int interiorHeightInCells = 8;

        [Header("Placement")]
        [Tooltip(
            "Empty rows between the mine's bottom row and the lair's top row. " +
            "Must be at least 1 so lair cells never share coordinates with mine " +
            "cells. Raised automatically if the camera window would otherwise " +
            "reach the mine.")]
        [Min(1)] [SerializeField] private int gapBelowMineInCells = 6;
        [Tooltip(
            "Centres the arena horizontally under the mine. Off aligns it to " +
            "the mine's left edge.")]
        [SerializeField] private bool centreUnderMine = true;

        [Header("Shell")]
        [Tooltip(
            "Thickness of the unbreakable wall ring enclosing the interior. " +
            "The ring is sealed on all four sides including corners.")]
        [Min(1)] [SerializeField] private int borderThicknessInCells = 1;
        [Tooltip("Tile used for the unbreakable shell.")]
        [SerializeField] private TileBase borderTile;
        [Tooltip("Tile painted across every interior cell as the backdrop.")]
        [SerializeField] private TileBase interiorBackdropTile;

        [Header("Camera")]
        [Tooltip(
            "PixelPerfectCamera assets-per-unit used while in this lair. The " +
            "visible window is refResolutionY / assetsPPU world units tall, so " +
            "a smaller arena needs a higher value to fill the frame. Keep it a " +
            "multiple of 100 so pixel art stays crisp. A 15x8 arena frames " +
            "well at 600.")]
        [Min(1)] [SerializeField] private int lairAssetsPPU = 600;

        public int InteriorWidthInCells => interiorWidthInCells;
        public int InteriorHeightInCells => interiorHeightInCells;
        public int GapBelowMineInCells => gapBelowMineInCells;
        public bool CentreUnderMine => centreUnderMine;
        public int BorderThicknessInCells => borderThicknessInCells;
        public TileBase BorderTile => borderTile;
        public TileBase InteriorBackdropTile => interiorBackdropTile;
        public int LairAssetsPPU => lairAssetsPPU;

        public bool Validate(out string error)
        {
            if (interiorWidthInCells < 1 || interiorHeightInCells < 1)
            {
                error = $"{name} requires a positive interior size.";
                return false;
            }
            // Below 1 the lair's top row would share coordinates with the mine's
            // bottom row, and mining in the lair could resolve onto real cells.
            if (gapBelowMineInCells < 1)
            {
                error =
                    $"{name} gap below the mine must be at least 1 cell " +
                    $"(currently {gapBelowMineInCells}).";
                return false;
            }
            if (borderThicknessInCells < 1)
            {
                error =
                    $"{name} border thickness must be at least 1 or the arena " +
                    "would not be sealed.";
                return false;
            }
            if (borderTile == null)
            {
                error = $"{name} requires a border tile.";
                return false;
            }
            if (interiorBackdropTile == null)
            {
                error = $"{name} requires an interior backdrop tile.";
                return false;
            }
            if (lairAssetsPPU < 1)
            {
                error = $"{name} requires a positive lair assets-per-unit value.";
                return false;
            }
            error = null;
            return true;
        }
    }
}
