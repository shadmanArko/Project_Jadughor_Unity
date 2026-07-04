using System.Collections.Generic;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.EnemySystem.Model
{
    public readonly struct PathResult
    {
        public readonly bool Succeeded;
        public readonly GridPosition Destination;
        public readonly int Generation;
        public readonly IReadOnlyList<EnemyPathStep> Steps;
        public readonly string Error;

        private PathResult(
            bool succeeded,
            GridPosition destination,
            int generation,
            IReadOnlyList<EnemyPathStep> steps,
            string error)
        {
            Succeeded = succeeded;
            Destination = destination;
            Generation = generation;
            Steps = steps;
            Error = error;
        }

        public static PathResult Success(
            GridPosition destination,
            int generation,
            IReadOnlyList<EnemyPathStep> steps) =>
            new(true, destination, generation, steps, null);

        public static PathResult Failure(
            GridPosition destination,
            int generation,
            string error) =>
            new(false, destination, generation, null, error);
    }
}
