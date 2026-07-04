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
        private EnemyNavigationSnapshot _snapshot;

        public EnemyPathfindingService(MineModel mine)
        {
            _mine = mine;
        }

        public void Initialize()
        {
            _mine.MineData.Subscribe(Rebuild).AddTo(_disposables);
            _mine.OnCellModified
                .Subscribe(_ => Rebuild(_mine.MineData.Value))
                .AddTo(_disposables);
        }

        public bool IsWalkable(GridPosition position) =>
            _snapshot != null && _snapshot.WalkableCells.Contains(position);

        public async UniTask<PathResult> FindPathAsync(
            EnemyPathRequest request,
            CancellationToken cancellationToken)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            var snapshot = _snapshot;
            if (snapshot == null ||
                !snapshot.WalkableCells.Contains(request.Start) ||
                !snapshot.WalkableCells.Contains(request.Destination))
            {
                return PathResult.Failure(
                    request.Destination,
                    request.Generation,
                    "Path endpoints are not walkable.");
            }

            var occupied = request.OccupiedPositions == null
                ? null
                : new HashSet<GridPosition>(request.OccupiedPositions);
            occupied?.Remove(request.Start);
            occupied?.Remove(request.Destination);

            var open = new List<GridPosition> { request.Start };
            var closed = new HashSet<GridPosition>();
            var cameFrom = new Dictionary<GridPosition, GridPosition>();
            var stepTypes = new Dictionary<GridPosition, EnemyPathStepType>();
            var gScore = new Dictionary<GridPosition, int> { [request.Start] = 0 };
            var fScore = new Dictionary<GridPosition, int>
            {
                [request.Start] = Heuristic(request.Start, request.Destination)
            };
            var neighbours = new List<EnemyPathStep>(4);
            var expansions = 0;

            while (open.Count > 0)
            {
                if ((expansions++ & 63) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
                var currentIndex = FindBestIndex(open, fScore);
                var current = open[currentIndex];
                open.RemoveAt(currentIndex);
                if (current == request.Destination)
                {
                    return PathResult.Success(
                        request.Destination,
                        request.Generation,
                        Reconstruct(current, cameFrom, stepTypes));
                }

                closed.Add(current);
                neighbours.Clear();
                AddNeighbours(
                    snapshot,
                    current,
                    request.MaxFallDistanceInTiles,
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
                    fScore[next] = tentative + Heuristic(next, request.Destination);
                    if (!open.Contains(next))
                        open.Add(next);
                }
            }

            return PathResult.Failure(
                request.Destination,
                request.Generation,
                "No platform path exists.");
        }

        private void Rebuild(MineData mineData)
        {
            if (mineData?.Cells == null)
            {
                _snapshot = null;
                return;
            }

            var open = new HashSet<GridPosition>();
            for (var i = 0; i < mineData.Cells.Count; i++)
            {
                var cell = mineData.Cells[i];
                if (cell.IsRevealed && (cell.IsBroken || cell.IsBlank))
                    open.Add(cell.Position);
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
            Dictionary<GridPosition, int> scores)
        {
            var bestIndex = 0;
            var bestScore = scores[open[0]];
            for (var i = 1; i < open.Count; i++)
            {
                var score = scores[open[i]];
                if (score >= bestScore)
                    continue;
                bestIndex = i;
                bestScore = score;
            }
            return bestIndex;
        }

        private static int Heuristic(GridPosition from, GridPosition to) =>
            Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);

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
            _snapshot = null;
        }
    }
}
