using System.Collections.Generic;

namespace Systems.MineSystem.Mine.Model
{
    public class VineData
    {
        public string SourceId { get; set; }

        /// <summary>
        /// Cell IDs that this vine occupies, ordered from root to tip.
        /// </summary>
        public List<string> VineCellIds { get; set; }
    }
}
