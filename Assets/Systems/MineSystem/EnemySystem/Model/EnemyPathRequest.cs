using System.Collections.Generic;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.EnemySystem.Model
{
    public readonly struct EnemyPathRequest
    {
        public readonly GridPosition Start;
        public readonly GridPosition Destination;
        public readonly int MaxFallDistanceInTiles;
        public readonly int Generation;
        public readonly bool Chasing;
        public readonly IReadOnlyCollection<GridPosition> OccupiedPositions;

        public EnemyPathRequest(
            GridPosition start,
            GridPosition destination,
            int maxFallDistanceInTiles,
            int generation,
            bool chasing,
            IReadOnlyCollection<GridPosition> occupiedPositions = null)
        {
            Start = start;
            Destination = destination;
            MaxFallDistanceInTiles = maxFallDistanceInTiles;
            Generation = generation;
            Chasing = chasing;
            OccupiedPositions = occupiedPositions;
        }
    }
}
