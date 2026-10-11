using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.Model
{
    /// <summary>
    /// Resolved geometry of a boss lair, expressed in the mine's cell space plus
    /// the world anchor derived from it. Data only: produced by
    /// <c>BossLairPlacementService</c>, never authored or mutated.
    /// </summary>
    /// <remarks>
    /// <see cref="RootWorldPosition"/> is the world position of the interior's
    /// bottom-left cell *corner*, not its centre, so a lair whose local cell
    /// (0,0) starts at that point aligns exactly with the mine's grid.
    /// </remarks>
    public readonly struct BossLairPlacement
    {
        public BossLairPlacement(
            BoundsInt interiorCells,
            int borderThickness,
            Vector2 rootWorldPosition,
            float cellWorldSize)
        {
            InteriorCells = interiorCells;
            BorderThickness = borderThickness;
            RootWorldPosition = rootWorldPosition;
            CellWorldSize = cellWorldSize;
        }

        /// <summary>Playable interior in mine cell space.</summary>
        public BoundsInt InteriorCells { get; }

        public int BorderThickness { get; }

        /// <summary>World position of the interior's bottom-left corner.</summary>
        public Vector2 RootWorldPosition { get; }

        public float CellWorldSize { get; }

        public int WidthInCells => InteriorCells.size.x;
        public int HeightInCells => InteriorCells.size.y;

        /// <summary>Top row of the interior in mine cell space.</summary>
        public int TopCellY => InteriorCells.yMax - 1;

        public Vector2 InteriorWorldSize => new(
            WidthInCells * CellWorldSize,
            HeightInCells * CellWorldSize);

        public Vector2 InteriorWorldCenter =>
            RootWorldPosition + InteriorWorldSize * 0.5f;

        /// <summary>
        /// World-space center and size of the interior padded outward by
        /// <paramref name="paddingInCells"/> cells on the top, left, and right,
        /// and independently by <paramref name="bottomPaddingInCells"/> cells
        /// on the bottom (the side away from the mine). Asymmetric padding
        /// shifts the center toward whichever side has more of it, so callers
        /// must use the returned center rather than
        /// <see cref="InteriorWorldCenter"/> once any padding is applied.
        /// </summary>
        public void GetPaddedWorldBounds(
            int paddingInCells,
            int bottomPaddingInCells,
            out Vector2 center,
            out Vector2 size)
        {
            var pad = Mathf.Max(0, paddingInCells) * CellWorldSize;
            var bottomPad = Mathf.Max(0, bottomPaddingInCells) * CellWorldSize;

            var minX = RootWorldPosition.x - pad;
            var minY = RootWorldPosition.y - bottomPad;
            var maxX = RootWorldPosition.x + InteriorWorldSize.x + pad;
            var maxY = RootWorldPosition.y + InteriorWorldSize.y + pad;

            center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            size = new Vector2(maxX - minX, maxY - minY);
        }

        public bool IsValid =>
            WidthInCells > 0 && HeightInCells > 0 &&
            BorderThickness > 0 && CellWorldSize > 0f;
    }
}
