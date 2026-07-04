using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Model
{
    public readonly struct GridPositionSpawnData
    {
        public readonly GridPosition GridPosition;
        public readonly Vector3 WorldPosition;

        public GridPositionSpawnData(
            GridPosition gridPosition,
            Vector3 worldPosition)
        {
            GridPosition = gridPosition;
            WorldPosition = worldPosition;
        }
    }
}
