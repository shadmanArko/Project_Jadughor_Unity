using Systems.MineSystem.InventorySystem.Model;

namespace Systems.MineSystem.Mine.Model
{
    /// <summary>
    /// Abstract base for all placeables (wall-mounted or cell-occupying).
    /// Shared fields: identity, position, asset references, and categorisation.
    /// </summary>
    public abstract class Placeable : Item
    {
        public GridPosition Position { get; set; }

        public string ScenePath { get; set; }
        public string PngPath { get; set; }
    }
}
