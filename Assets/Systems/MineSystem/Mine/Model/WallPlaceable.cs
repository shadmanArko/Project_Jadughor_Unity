using System.Collections.Generic;

namespace Systems.MineSystem.Mine.Model
{
    public class WallPlaceable : Placeable
    {
        /// <summary>
        /// IDs of all cells this placeable occupies (can span multiple cells).
        /// </summary>
        public List<string> OccupiedCellIds { get; set; }

        public int ExtraOccupiedDimensionX { get; set; }
        public int ExtraOccupiedDimensionY { get; set; }
    }
}
