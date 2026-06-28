using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Elevator.Script
{
    public sealed class ElevatorShaftNode
    {
        public ElevatorShaftNode(Vector3Int cell, ElevatorShaftRuntime runtime)
        {
            Cell = cell;
            Runtime = runtime;
        }

        public Vector3Int Cell { get; }
        public ElevatorShaftRuntime Runtime { get; }
    }
}
