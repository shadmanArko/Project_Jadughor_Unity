namespace Systems.MineSystem.FungalVegetationSystem.Enum
{
    /// <summary>
    /// Which solid neighbour a growth clings to, named from the growth's own point
    /// of view. Deliberately NOT <see cref="Mine.Model.Direction"/>, whose meaning in
    /// this codebase is "impact side" and is inverted relative to this - compare
    /// ToolItemActionHandler.GetImpactSide (CardinalDirection.Left -> Direction.Right)
    /// and MineModel.Initialize ([Vector3Int.up] = BrokenEdges.Bottom).
    /// </summary>
    public enum FungalAnchor
    {
        /// <summary>Solid cell below - the growth stands on the floor.</summary>
        Floor = 0,

        /// <summary>Solid cell above - the growth hangs from the ceiling.</summary>
        Ceiling = 1,

        /// <summary>Solid cell to the left.</summary>
        LeftWall = 2,

        /// <summary>Solid cell to the right.</summary>
        RightWall = 3
    }
}
