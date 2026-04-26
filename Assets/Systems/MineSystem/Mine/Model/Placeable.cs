namespace Systems.MineSystem.Mine.Model
{
    /// <summary>
    /// Abstract base for all placeables (wall-mounted or cell-occupying).
    /// Shared fields: identity, position, asset references, and categorisation.
    /// </summary>
    public abstract class Placeable
    {
        public string Id { get; set; }
        public GridPosition Position { get; set; }

        public string Type { get; set; }
        public string Category { get; set; }
        public string Variant { get; set; }

        public string ScenePath { get; set; }
        public string PngPath { get; set; }
    }
}
