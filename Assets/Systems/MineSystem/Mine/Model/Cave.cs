using System.Collections.Generic;

namespace Systems.MineSystem.Mine.Model
{
    public class Cave
    {
        public string Id { get; set; }
        public bool IsRevealed { get; set; }

        /// <summary>
        /// Bounding box of the cave in grid space.
        /// </summary>
        public int TopBound { get; set; }
        public int LeftBound { get; set; }
        public int RightBound { get; set; }
        public int BottomBound { get; set; }

        public int NoOfFlyingEnemies { get; set; }

        public List<string> CellIds { get; set; }
        public List<string> StalagmiteCellIds { get; set; }
        public List<string> StalactiteCellIds { get; set; }
    }
}
