using System.Collections.Generic;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.EnemySystem.Model
{
    public readonly struct EnemyMultiTargetPathRequest
    {
        public readonly GridPosition Start;
        public readonly GridPosition Target;
        public readonly IReadOnlyList<GridPosition> Destinations;
        public readonly int MaxFallDistanceInTiles;
        public readonly int Generation;

        public EnemyMultiTargetPathRequest(
            GridPosition start,
            GridPosition target,
            IReadOnlyList<GridPosition> destinations,
            int maxFallDistanceInTiles,
            int generation)
        {
            Start = start;
            Target = target;
            Destinations = destinations;
            MaxFallDistanceInTiles = maxFallDistanceInTiles;
            Generation = generation;
        }
    }
}
