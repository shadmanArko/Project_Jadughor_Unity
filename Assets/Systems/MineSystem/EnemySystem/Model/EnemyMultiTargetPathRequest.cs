using System.Collections.Generic;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.EnemySystem.Model
{
    public readonly struct EnemyMultiTargetPathRequest
    {
        public readonly GridPosition Start;
        public readonly GridPosition Target;
        public readonly GridPosition PreferredDestination;
        public readonly IReadOnlyList<GridPosition> Destinations;
        public readonly EnemyMovementType MovementType;
        public readonly int MaxFallDistanceInTiles;
        public readonly int Generation;
        public readonly int RouteVariant;
        public readonly bool PrioritizePreferredDestination;

        public EnemyMultiTargetPathRequest(
            GridPosition start,
            GridPosition target,
            GridPosition preferredDestination,
            IReadOnlyList<GridPosition> destinations,
            EnemyMovementType movementType,
            int maxFallDistanceInTiles,
            int generation,
            int routeVariant,
            bool prioritizePreferredDestination)
        {
            Start = start;
            Target = target;
            PreferredDestination = preferredDestination;
            Destinations = destinations;
            MovementType = movementType;
            MaxFallDistanceInTiles = maxFallDistanceInTiles;
            Generation = generation;
            RouteVariant = routeVariant;
            PrioritizePreferredDestination = prioritizePreferredDestination;
        }
    }
}
