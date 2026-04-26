using System;
using System.Collections.Generic;

namespace Systems.MineSystem.Mine.Model
{
    [Serializable]
    public class MineData
    {
        /// <summary>
        /// Increment this when the schema changes to support save-file migration.
        /// </summary>
        public int Version { get; set; } = 1;

        public int CellSize { get; set; }
        public int GridWidth { get; set; }
        public int GridHeight { get; set; }

        public List<Cell> Cells { get; set; }
        public List<Resource> Resources { get; set; }
        public List<Artifact> Artifacts { get; set; }
        public List<Cave> Caves { get; set; }
        public List<WallPlaceable> WallPlaceables { get; set; }
        public List<CellPlaceable> CellPlaceables { get; set; }
        public List<VineData> VineDatas { get; set; }
        public List<SpecialBackdropData> SpecialBackdropDatas { get; set; }
    }
}
