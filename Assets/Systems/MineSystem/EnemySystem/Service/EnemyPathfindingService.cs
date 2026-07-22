using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.ToolbarSystem.Interface;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.EnemySystem.Service
{
    public sealed class EnemyPathfindingService :
        IEnemyPathfindingService,
        IInitializable,
        IDisposable
    {
        private readonly MineModel _mine;
        private readonly IPlaceableRuntimeRegistry _runtimeRegistry;
        private readonly CompositeDisposable _disposables = new();
        private readonly Subject<GridPosition> _navigationChanged = new();
        private readonly HashSet<GridPosition> _blockedCells = new();
        private EnemyNavigationSnapshot _snapshot;
        private int _navigationRevision;

        public EnemyPathfindingService(
            MineModel mine,
            IPlaceableRuntimeRegistry runtimeRegistry)
        {
            _mine = mine;
            _runtimeRegistry = runtimeRegistry;
        }

        public void Initialize()
        {
            _runtimeRegistry.RuntimeRegistered += HandleRuntimeRegistered;
            _runtimeRegistry.RuntimeUnregistered += HandleRuntimeUnregistered;
            _mine.MineData.Subscribe(Rebuild).AddTo(_disposables);
            _mine.OnCellModified
                .Subscribe(HandleCellModified)
                .AddTo(_disposables);
        }

        public IObservable<GridPosition> NavigationChanged =>
            _navigationChanged;

        public bool IsWalkable(GridPosition position) =>
            _snapshot != null &&
            _snapshot.WalkableCells.Contains(position) &&
            !_blockedCells.Contains(position);

        public bool IsFlyable(GridPosition position) =>
            _snapshot != null &&
            _snapshot.OpenCells.Contains(position) &&
            !_blockedCells.Contains(position);

        public int WalkableCount => CountAvailable(
            _snapshot?.WalkableCells,
            _snapshot?.WalkablePositions.Count ?? 0);

        public int FlyableCount => CountAvailable(
            _snapshot?.OpenCells,
            _snapshot?.OpenPositions.Count ?? 0);

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
                if (_blockedCells.Contains(candidate))
                    continue;
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
            return TryFindAvailable(candidates, startOffset, out position);
        }

        public bool TryFindFlyableNear(
            GridPosition origin,
            int minimumDistance,
            int maximumDistance,
            int startOffset,
            out GridPosition position)
        {
            position = default;
            var snapshot = _snapshot;
            if (snapshot == null ||
                snapshot.OpenPositions.Count == 0 ||
                maximumDistance < minimumDistance)
                return false;

            var candidates = snapshot.OpenPositions;
            var start = NormalizeStart(startOffset, candidates.Count);
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[(start + i) % candidates.Count];
                if (_blockedCells.Contains(candidate))
                    continue;
                var distance = Heuristic(candidate, origin);
                if (distance < minimumDistance || distance > maximumDistance)
                    continue;
                position = candidate;
                return true;
            }
            return false;
        }

        public bool TryFindAnyFlyable(
            int startOffset,
            out GridPosition position)
        {
            position = default;
            var snapshot = _snapshot;
            if (snapshot == null || snapshot.OpenPositions.Count == 0)
                return false;

            var candidates = snapshot.OpenPositions;
            return TryFindAvailable(candidates, startOffset, out position);
        }

        public bool TryFindFallLanding(
            GridPosition origin,
            int direction,
            int maximumFallDistance,
            out GridPosition position)
        {
            position = default;
            var snapshot = _snapshot;
            if (snapshot == null || maximumFallDistance <= 0)
                return false;

            var signedDirection = direction < 0 ? -1 : 1;
            var adjacent = new GridPosition(
                origin.X + signedDirection,
                origin.Y);
            if (!snapshot.OpenCells.Contains(adjacent) ||
                _blockedCells.Contains(adjacent) ||
                snapshot.WalkableCells.Contains(adjacent))
                return false;

            for (var depth = 1; depth <= maximumFallDistance; depth++)
            {
                var candidate = new GridPosition(
                    adjacent.X,
                    adjacent.Y - depth);
                if (!snapshot.OpenCells.Contains(candidate) ||
                    _blockedCells.Contains(candidate))
                    break;
                if (!snapshot.WalkableCells.Contains(candidate))
                    continue;
                position = candidate;
                return true;
            }
            return false;
        }

        public async UniTask<PathResult> FindPathToAnyAsync(
            EnemyMultiTargetPathRequest request,
            CancellationToken cancellationToken)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            if (request.PrioritizePreferredDestination &&
                Contains(
                    request.Destinations,
                    request.PreferredDestination))
            {
                var preferredResult = FindPath(
                    request.Start,
                    request.Target,
                    request.Generation,
                    request.MovementType,
                    request.MaxFallDistanceInTiles,
                    new[] { request.PreferredDestination },
                    request.PreferredDestination,
                    request.RouteVariant,
                    cancellationToken);
                if (preferredResult.Succeeded)
                    return preferredResult;
                cancellationToken.ThrowIfCancellationRequested();
            }
            return FindPath(
                request.Start,
                request.Target,
                request.Generation,
                request.MovementType,
                request.MaxFallDistanceInTiles,
                request.Destinations,
                request.PreferredDestination,
                request.RouteVariant,
                cancellationToken);
        }

        private PathResult FindPath(
            GridPosition start,
            GridPosition fallbackDestination,
            int generation,
            EnemyMovementType movementType,
            int maxFallDistanceInTiles,
            IReadOnlyList<GridPosition> destinations,
            GridPosition preferredDestination,
            int routeVariant,
            CancellationToken cancellationToken)
        {
            var snapshot = _snapshot;
            var navigationCells = movementType == EnemyMovementType.Flying
                ? snapshot?.OpenCells
                : snapshot?.WalkableCells;
            if (snapshot == null || navigationCells == null ||
                !navigationCells.Contains(start) ||
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
                if (navigationCells.Contains(destinations[i]) &&
                    !_blockedCells.Contains(destinations[i]))
                    destinationSet.Add(destinations[i]);
            }
            if (destinationSet.Count == 0)
            {
                return PathResult.Failure(
                    fallbackDestination,
                    generation,
                    "No path destination is walkable.");
            }

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
                    _blockedCells,
                    current,
                    movementType,
                    maxFallDistanceInTiles,
                    routeVariant,
                    neighbours);
                for (var i = 0; i < neighbours.Count; i++)
                {
                    var edge = neighbours[i];
                    var next = edge.Position;
                    if (closed.Contains(next))
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
            _blockedCells.Clear();
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
                {
                    open.Add(position);
                    if (_runtimeRegistry.Contains<IEnemyNavigationBlocker>(
                            position.ToVector3Int()))
                        _blockedCells.Add(position);
                }
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

        private void HandleRuntimeRegistered(
            Vector3Int cellPosition,
            IPlaceableRuntime runtime)
        {
            if (runtime is not IEnemyNavigationBlocker)
                return;

            var position = new GridPosition(cellPosition.x, cellPosition.y);
            if (!_blockedCells.Add(position))
                return;

            PublishNavigationChange(position);
        }

        private void HandleRuntimeUnregistered(
            Vector3Int cellPosition,
            IPlaceableRuntime runtime)
        {
            if (runtime is not IEnemyNavigationBlocker ||
                _runtimeRegistry.Contains<IEnemyNavigationBlocker>(cellPosition))
                return;

            var position = new GridPosition(cellPosition.x, cellPosition.y);
            if (!_blockedCells.Remove(position))
                return;

            PublishNavigationChange(position);
        }

        private void PublishNavigationChange(GridPosition position)
        {
            _navigationRevision++;
            _navigationChanged.OnNext(position);
        }

        private int CountAvailable(
            HashSet<GridPosition> terrainCells,
            int terrainCount)
        {
            if (terrainCells == null || terrainCount <= 0)
                return 0;

            var blockedCount = 0;
            foreach (var blockedCell in _blockedCells)
            {
                if (terrainCells.Contains(blockedCell))
                    blockedCount++;
            }

            return Math.Max(0, terrainCount - blockedCount);
        }

        private bool TryFindAvailable(
            IReadOnlyList<GridPosition> candidates,
            int startOffset,
            out GridPosition position)
        {
            position = default;
            var start = NormalizeStart(startOffset, candidates.Count);
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[(start + i) % candidates.Count];
                if (_blockedCells.Contains(candidate))
                    continue;

                position = candidate;
                return true;
            }

            return false;
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
            HashSet<GridPosition> blockedCells,
            GridPosition current,
            EnemyMovementType movementType,
            int maxFall,
            int routeVariant,
            List<EnemyPathStep> output)
        {
            if (movementType == EnemyMovementType.Flying)
            {
                AddFlyingNeighbours(
                    snapshot,
                    blockedCells,
                    current,
                    routeVariant,
                    output);
                return;
            }

            for (var direction = -1; direction <= 1; direction += 2)
            {
                var horizontal = new GridPosition(current.X + direction, current.Y);
                if (snapshot.WalkableCells.Contains(horizontal) &&
                    !blockedCells.Contains(horizontal))
                {
                    output.Add(new EnemyPathStep(horizontal, EnemyPathStepType.Walk));
                    continue;
                }
                if (!snapshot.OpenCells.Contains(horizontal) ||
                    blockedCells.Contains(horizontal))
                    continue;
                for (var depth = 1; depth <= Math.Max(0, maxFall); depth++)
                {
                    var fall = new GridPosition(horizontal.X, horizontal.Y - depth);
                    if (!snapshot.OpenCells.Contains(fall) ||
                        blockedCells.Contains(fall))
                        break;
                    if (!snapshot.WalkableCells.Contains(fall))
                        continue;
                    output.Add(new EnemyPathStep(fall, EnemyPathStepType.Fall));
                    break;
                }
            }
        }

        private static void AddFlyingNeighbours(
            EnemyNavigationSnapshot snapshot,
            HashSet<GridPosition> blockedCells,
            GridPosition current,
            int routeVariant,
            List<EnemyPathStep> output)
        {
            var normalizedVariant = NormalizeStart(routeVariant, 8);
            var startDirection = normalizedVariant % 4;
            var directionStep = normalizedVariant < 4 ? 1 : -1;
            for (var i = 0; i < 4; i++)
            {
                var direction = NormalizeStart(
                    startDirection + directionStep * i,
                    4);
                switch (direction)
                {
                    case 0:
                        AddFlyingNeighbour(
                            snapshot,
                            blockedCells,
                            current,
                            -1,
                            0,
                            output);
                        break;
                    case 1:
                        AddFlyingNeighbour(
                            snapshot,
                            blockedCells,
                            current,
                            1,
                            0,
                            output);
                        break;
                    case 2:
                        AddFlyingNeighbour(
                            snapshot,
                            blockedCells,
                            current,
                            0,
                            -1,
                            output);
                        break;
                    default:
                        AddFlyingNeighbour(
                            snapshot,
                            blockedCells,
                            current,
                            0,
                            1,
                            output);
                        break;
                }
            }
        }

        private static void AddFlyingNeighbour(
            EnemyNavigationSnapshot snapshot,
            HashSet<GridPosition> blockedCells,
            GridPosition current,
            int offsetX,
            int offsetY,
            List<EnemyPathStep> output)
        {
            var candidate = new GridPosition(
                current.X + offsetX,
                current.Y + offsetY);
            if (snapshot.OpenCells.Contains(candidate) &&
                !blockedCells.Contains(candidate))
            {
                output.Add(new EnemyPathStep(
                    candidate,
                    EnemyPathStepType.Fly));
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

        private static bool Contains(
            IReadOnlyList<GridPosition> positions,
            GridPosition position)
        {
            if (positions == null)
                return false;
            for (var i = 0; i < positions.Count; i++)
            {
                if (positions[i] == position)
                    return true;
            }
            return false;
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
            _runtimeRegistry.RuntimeRegistered -= HandleRuntimeRegistered;
            _runtimeRegistry.RuntimeUnregistered -= HandleRuntimeUnregistered;
            _disposables.Dispose();
            _navigationChanged.OnCompleted();
            _navigationChanged.Dispose();
            _blockedCells.Clear();
            _snapshot = null;
        }
    }
}
