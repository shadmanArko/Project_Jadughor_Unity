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
        public string ItemId { get; set; }
        public GridPosition Position { get; set; }

        public int MaxHitPoint { get; set; }
        public int HitPoint { get; set; }

        public bool IsBreakable { get; set; }
        public bool IsBroken { get; set; }
        public bool IsRevealed { get; set; }
        public bool IsBlank { get; set; }
        public Vector3Int GetPosition() => new(Position.X, Position.Y, 0);

        public bool HasResource { get; set; }
        public bool HasArtifact { get; set; }
        public bool HasVine { get; set; }
        public bool HasCellPlaceable { get; set; }
        public bool HasWallPlaceable { get; set; }

        /// <summary>
        /// Bitmask of which edges and corners of this cell are broken.
        /// Use BrokenEdges flags to read/write individual sides.
        /// Example: cell.BrokenSides.HasFlag(BrokenEdges.Top)
        /// </summary>
        public BrokenEdges BrokenSides { get; set; }
    }
}
