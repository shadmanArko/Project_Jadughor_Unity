namespace Systems.MineSystem.FungalVegetationSystem.Model
{
    /// <summary>
    /// Result of asking the placement service to grow something in a matured cell.
    /// </summary>
    /// <remarks>
    /// A plain bool cannot express "not now, ask again", which is the whole point of the
    /// camera gate: a cell the player is currently looking at must keep its unspent growth
    /// roll rather than being discarded.
    /// </remarks>
    public enum FungalGrowthOutcome
    {
        /// <summary>At least one growth was produced. The cell has spent its roll.</summary>
        Placed = 0,

        /// <summary>
        /// The cell can never grow anything (already decorated, spacing violation, no solid
        /// neighbour, or the chance roll failed). Its roll is spent; drop it.
        /// </summary>
        Rejected = 1,

        /// <summary>
        /// The cell is inside the camera view and the config forbids growing on screen. The
        /// roll has NOT been spent - hold the cell and retry once the camera moves away.
        /// </summary>
        CameraBlocked = 2
    }
}
