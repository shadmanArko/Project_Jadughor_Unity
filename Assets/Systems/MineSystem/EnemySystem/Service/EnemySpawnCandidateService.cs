using System.Collections.Generic;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.EnemySystem.Service
{
    public sealed class EnemySpawnCandidateService
    {
        private readonly Dictionary<int, List<GridPosition>> _offsetsByRange =
            new();

        public IReadOnlyList<GridPosition> GetOffsets(
            int minimumDistance,
            int maximumDistance)
        {
            var min = minimumDistance < 0 ? 0 : minimumDistance;
            var max = maximumDistance < min ? min : maximumDistance;
            var key = (min << 16) ^ max;
            if (_offsetsByRange.TryGetValue(key, out var offsets))
                return offsets;

            offsets = new List<GridPosition>();
            for (var x = -max; x <= max; x++)
            {
                for (var y = -max; y <= max; y++)
                {
                    var distance = Abs(x) + Abs(y);
                    if (distance >= min && distance <= max)
                        offsets.Add(new GridPosition(x, y));
                }
            }

            _offsetsByRange.Add(key, offsets);
            return offsets;
        }

        private static int Abs(int value) => value < 0 ? -value : value;
    }
}
