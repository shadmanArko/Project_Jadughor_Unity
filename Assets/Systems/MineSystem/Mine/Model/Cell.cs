using System;
using Systems.MineSystem.Mine.Enum;
using UnityEngine;

namespace Systems.MineSystem.Mine.Model
{
    [Serializable]
    public class Cell
    {
        public string Id { get; set; }
        public string CaveId { get; set; }
        public GridPosition Position { get; set; }

        public int MaxHitPoint { get; set; }
        public int HitPoint { get; set; }

        public bool IsBreakable { get; set; }
        public bool IsBroken { get; set; }
        public bool IsRevealed { get; set; }
        public bool IsBlank { get; set; }
        public Vector3Int GetPosition() => new Vector3Int(Position.X, Position.Y, 0);

        /// <summary>
        /// Bitmask of which edges and corners of this cell are broken.
        /// Use BrokenEdges flags to read/write individual sides.
        /// Example: cell.BrokenSides.HasFlag(BrokenEdges.Top)
        /// </summary>
        public BrokenEdges BrokenSides { get; set; }
    }
}

/*
 * // Set them one by one
cell.BrokenSides |= BrokenEdges.Top;
cell.BrokenSides |= BrokenEdges.Bottom;
cell.BrokenSides |= BrokenEdges.Left;
cell.BrokenSides |= BrokenEdges.Right;

// Or set them all at once for cleaner code
cell.BrokenSides |= BrokenEdges.Top | BrokenEdges.Bottom | BrokenEdges.Left | BrokenEdges.Right;

// To Remove
cell.BrokenSides &= ~BrokenEdges.Top;

// To check if side broken
if (cell.BrokenSides.HasFlag(BrokenEdges.Top))
{
    // The top side is broken
}
*/
