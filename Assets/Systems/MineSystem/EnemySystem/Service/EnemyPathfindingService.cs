using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;
using UniRx;
using Zenject;

namespace Systems.MineSystem.EnemySystem.Service
{
    public sealed class EnemyPathfindingService :
        IEnemyPathfindingService,
        IInitializable,
        IDisposable
    {
        private readonly MineModel _mine;
        private readonly CompositeDisposable _disposables = new();
        private readonly Subject<GridPosition> _navigationChanged = new();
        private EnemyNavigationSnapshot _snapshot;
        private int _navigationRevision;

        public EnemyPathfindingService(MineModel mine)
        {
            _mine = mine;
        }

        public void Initialize()
        {
            _mine.MineData.Subscribe(Rebuild).AddTo(_disposables);
            _mine.OnCellModified
                .Subscribe(HandleCellModified)
                .AddTo(_disposables);
        }

        public IObservable<GridPosition> NavigationChanged =>
            _navigationChanged;

        public bool IsWalkable(GridPosition position) =>
            _snapshot != null && _snapshot.WalkableCells.Contains(position);

        public int WalkableCount => _snapshot?.WalkablePositions.Count ?? 0;

        public int NavigationRevision => _navigationRevision;

        public bool TryFindWalkableNear(
            GridPosition origin,
            int minimumDistance,
            int maximumDistance,
            int startOffset,
            out GridPosition position)
        {
            position = default;
            var snapshot = _snapshot;
            if (snapshot == null ||
                snapshot.WalkablePositions.Count == 0 ||
                maximumDistance < minimumDistance)
                return false;

            var candidates = snapshot.WalkablePositions;
            var start = NormalizeStart(startOffset, candidates.Count);
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[(start + i) % candidates.Count];
                var distance = Heuristic(candidate, origin);
                if (distance < minimumDistance || distance > maximumDistance)
                    continue;
                position = candidate;
                return true;
            }
            return false;
        }

        public bool TryFindAnyWalkable(int startOffset, out GridPosition position)
        {
            position = default;
            var snapshot = _snapshot;
            if (snapshot == null || snapshot.WalkablePositions.Count == 0)
                return false;

            var candidates = snapshot.WalkablePositions;
            position = candidates[NormalizeStart(startOffset, candidates.Count)];
            return true;
        }

        public bool TryFindFarthestDirectional(
            GridPosition origin,
            int direction,
            int maximumDistance,
            out GridPosition position)
        {
            position = default;
            var snapshot = _snapshot;
            if (snapshot == null || maximumDistance <= 0)
                return false;

            var signedDirection = direction < 0 ? -1 : 1;
            var found = false;
            var bestHorizontalDistance = -1;
            var bestDistance = int.MaxValue;
            var candidates = snapshot.WalkablePositions;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var horizontalDistance = candidate.X - origin.X;
                if (horizontalDistance * signedDirection <= 0)
                    continue;
                var absoluteHorizontalDistance = Math.Abs(horizontalDistance);
                var distance = absoluteHorizontalDistance +
                               Math.Abs(candidate.Y - origin.Y);
                if (absoluteHorizontalDistance > maximumDistance ||
                    distance > maximumDistance)
                    continue;
                if (found && (absoluteHorizontalDistance < bestHorizontalDistance ||
                              absoluteHorizontalDistance == bestHorizontalDistance &&
                              distance >= bestDistance))
                    continue;

                position = candidate;
                bestHorizontalDistance = absoluteHorizontalDistance;
                bestDistance = distance;
                found = true;
            }
            return found;
        }

        public async UniTask<PathResult> FindPathAsync(
            EnemyPathRequest request,
            CancellationToken cancellationToken)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            var destinations = new[] { request.Destination };
            return FindPath(
                request.Start,
                request.Destination,
                request.Generation,
                request.MaxFallDistanceInTiles,
                destinations,
                request.Destination,
                request.OccupiedPositions,
                cancellationToken);
        }

        public async UniTask<PathResult> FindPathToAnyAsync(
            EnemyMultiTargetPathRequest request,
            CancellationToken cancellationToken)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            return FindPath(
                request.Start,
                request.Target,
                request.Generation,
                request.MaxFallDistanceInTiles,
                request.Destinations,
                request.PreferredDestination,
                null,
                cancellationToken);
        }

        private PathResult FindPath(
            GridPosition start,
            GridPosition fallbackDestination,
            int generation,
            int maxFallDistanceInTiles,
            IReadOnlyList<GridPosition> destinations,
            GridPosition preferredDestination,
            IReadOnlyCollection<GridPosition> occupiedPositions,
            CancellationToken cancellationToken)
        {
            var snapshot = _snapshot;
            if (snapshot == null ||
                !snapshot.WalkableCells.Contains(start) ||
                destinations == null || destinations.Count == 0)
            {
                return PathResult.Failure(
                    fallbackDestination,
                    generation,
                    "Path start or destinations are unavailable.");
            }

            var destinationSet = new HashSet<GridPosition>();
            for (var i = 0; i < destinations.Count; i++)
            {
                if (snapshot.WalkableCells.Contains(destinations[i]))
                    destinationSet.Add(destinations[i]);
            }
            if (destinationSet.Count == 0)
            {
                return PathResult.Failure(
                    fallbackDestination,
                    generation,
                    "No path destination is walkable.");
            }

            var occupied = occupiedPositions == null
                ? null
                : new HashSet<GridPosition>(occupiedPositions);
            occupied?.Remove(start);
            foreach (var destination in destinationSet)
                occupied?.Remove(destination);

            var open = new List<GridPosition> { start };
            var closed = new HashSet<GridPosition>();
            var cameFrom = new Dictionary<GridPosition, GridPosition>();
            var stepTypes = new Dictionary<GridPosition, EnemyPathStepType>();
            var gScore = new Dictionary<GridPosition, int> { [start] = 0 };
            var fScore = new Dictionary<GridPosition, int>
            {
                [start] = HeuristicToAny(start, destinationSet)
            };
            var neighbours = new List<EnemyPathStep>(4);
            var expansions = 0;

            while (open.Count > 0)
            {
                if ((expansions++ & 63) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
                var currentIndex = FindBestIndex(
                    open,
                    fScore,
                    preferredDestination);
                var current = open[currentIndex];
                open.RemoveAt(currentIndex);
                if (destinationSet.Contains(current))
                {
                    return PathResult.Success(
                        current,
                        generation,
                        Reconstruct(current, cameFrom, stepTypes));
                }

                closed.Add(current);
                neighbours.Clear();
                AddNeighbours(
                    snapshot,
                    current,
                    maxFallDistanceInTiles,
                    neighbours);
                for (var i = 0; i < neighbours.Count; i++)
                {
                    var edge = neighbours[i];
                    var next = edge.Position;
                    if (closed.Contains(next) || occupied?.Contains(next) == true)
                        continue;
                    var tentative = gScore[current] +
                                    (edge.Type == EnemyPathStepType.Fall ? 2 : 1);
                    if (gScore.TryGetValue(next, out var oldScore) &&
                        tentative >= oldScore)
                        continue;

                    cameFrom[next] = current;
                    stepTypes[next] = edge.Type;
                    gScore[next] = tentative;
                    fScore[next] = tentative + HeuristicToAny(next, destinationSet);
                    if (!open.Contains(next))
                        open.Add(next);
                }
            }

            return PathResult.Failure(
                fallbackDestination,
                generation,
                "No platform path exists.");
        }

        private void Rebuild(MineData mineData)
        {
            _navigationRevision++;
            if (mineData == null)
            {
                _snapshot = null;
                return;
            }

            var open = new HashSet<GridPosition>();
            var brokenPositions = _mine.BrokenCellPositions;
            for (var i = 0; i < brokenPositions.Count; i++)
            {
                var position = brokenPositions[i];
                if (_mine.TryGetCell(position, out var cell) &&
                    cell.IsRevealed &&
                    cell.IsBroken)
                    open.Add(position);
            }

            var walkable = new HashSet<GridPosition>();
            foreach (var position in open)
            {
                var below = mineData.GetCell(new GridPosition(position.X, position.Y - 1));
                if (below != null && !below.IsBroken && !below.IsBlank)
                    walkable.Add(position);
            }
            _snapshot = new EnemyNavigationSnapshot(open, walkable);
        }

        private void HandleCellModified(Cell cell)
        {
            Rebuild(_mine.MineData.Value);
            if (cell != null)
                _navigationChanged.OnNext(cell.Position);
        }

        private static int NormalizeStart(int startOffset, int count)
        {
            if (count <= 0)
                return 0;
            var start = startOffset % count;
            return start < 0 ? start + count : start;
        }

        private static void AddNeighbours(
            EnemyNavigationSnapshot snapshot,
            GridPosition current,
            int maxFall,
            List<EnemyPathStep> output)
        {
            for (var direction = -1; direction <= 1; direction += 2)
            {
                var horizontal = new GridPosition(current.X + direction, current.Y);
                if (snapshot.WalkableCells.Contains(horizontal))
                {
                    output.Add(new EnemyPathStep(horizontal, EnemyPathStepType.Walk));
                    continue;
                }
                if (!snapshot.OpenCells.Contains(horizontal))
                    continue;
                for (var depth = 1; depth <= Math.Max(0, maxFall); depth++)
                {
                    var fall = new GridPosition(horizontal.X, horizontal.Y - depth);
                    if (!snapshot.OpenCells.Contains(fall))
                        break;
                    if (!snapshot.WalkableCells.Contains(fall))
                        continue;
                    output.Add(new EnemyPathStep(fall, EnemyPathStepType.Fall));
                    break;
                }
            }
        }

        private static int FindBestIndex(
            List<GridPosition> open,
            Dictionary<GridPosition, int> scores,
            GridPosition preferredDestination)
        {
            var bestIndex = 0;
            var bestScore = scores[open[0]];
            var bestPreference = Heuristic(
                open[0],
                preferredDestination);
            for (var i = 1; i < open.Count; i++)
            {
                var score = scores[open[i]];
                var preference = Heuristic(
                    open[i],
                    preferredDestination);
                if (score > bestScore ||
                    score == bestScore && preference >= bestPreference)
                    continue;
                bestIndex = i;
                bestScore = score;
                bestPreference = preference;
            }
            return bestIndex;
        }

        private static int Heuristic(GridPosition from, GridPosition to) =>
            Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);

        private static int HeuristicToAny(
            GridPosition from,
            HashSet<GridPosition> destinations)
        {
            var best = int.MaxValue;
            foreach (var destination in destinations)
                best = Math.Min(best, Heuristic(from, destination));
            return best;
        }

        private static IReadOnlyList<EnemyPathStep> Reconstruct(
            GridPosition current,
            Dictionary<GridPosition, GridPosition> cameFrom,
            Dictionary<GridPosition, EnemyPathStepType> stepTypes)
        {
            var path = new List<EnemyPathStep>();
            while (cameFrom.TryGetValue(current, out var previous))
            {
                path.Add(new EnemyPathStep(current, stepTypes[current]));
                current = previous;
            }
            path.Reverse();
            return path;
        }

        public void Dispose()
        {
            _disposables.Dispose();
            _navigationChanged.OnCompleted();
            _navigationChanged.Dispose();
            _snapshot = null;
        }
    }
}
