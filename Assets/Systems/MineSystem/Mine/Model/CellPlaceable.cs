namespace Systems.MineSystem.Mine.Model
{
    public class CellPlaceable : Placeable
    {
        public string Name { get; set; }

        /// <summary>
        /// The single cell this placeable is anchored to.
        /// </summary>
        public string OccupiedCellId { get; set; }

        public int ExtraOccupiedDimensionX { get; set; }
        public int ExtraOccupiedDimensionY { get; set; }
    }
}
