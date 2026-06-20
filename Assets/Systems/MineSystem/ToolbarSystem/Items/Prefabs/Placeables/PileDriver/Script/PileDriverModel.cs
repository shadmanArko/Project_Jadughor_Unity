using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.ToolbarSystem.Model;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.PileDriver.Script
{
    public sealed class PileDriverModel : IDisposable
    {
        private readonly MineModel _mine;
        private readonly PlaceableSpawnContext _context;
        private readonly PileDriverConfig _config;
        private readonly ReactiveProperty<PileDriverState> _state =
            new(PileDriverState.Inactive);

        public IReadOnlyReactiveProperty<PileDriverState> State => _state;

        public PileDriverModel(
            MineModel mine,
            PlaceableSpawnContext context,
            PileDriverConfig config)
        {
            _mine = mine;
            _context = context;
            _config = config;
        }

        public async UniTask RunAsync(
            PileDriverController controller,
            CancellationToken cancellationToken)
        {
            _state.Value = PileDriverState.TurningOn;
            await controller.PlayTurnOnAsync(cancellationToken);
            controller.PlayActive();

            var direction = PileDriverPlacementValidator.ToOffset(
                _context.PileDriverDirection);
            var data = _mine.MineData.Value;
            if (data == null)
            {
                await TurnOffAsync(controller, cancellationToken);
                return;
            }

            for (var index = 0;
                 index < _config.ProcessedCellCount;
                 index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var offset = _config.FirstTargetOffset + index;
                var targetPosition =
                    _context.CellPosition + direction * offset;
                var cell = data.GetCell(targetPosition);

                if (IsBlocking(cell))
                    break;

                // Broken cells still consume their offset in the fixed
                // five-cell processing range.
                if (cell.IsBroken)
                    continue;

                _state.Value = PileDriverState.Extending;
                await controller.ExtendAsync(
                    targetPosition - direction,
                    cancellationToken);

                while (!cell.IsBroken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _state.Value = PileDriverState.Stomping;
                    await controller.StompAsync(
                        targetPosition,
                        _config.DamagePerStomp,
                        cancellationToken);
                }

                _state.Value = PileDriverState.Retracting;
                await controller.RetractAsync(cancellationToken);
            }

            await TurnOffAsync(controller, cancellationToken);
        }

        private async UniTask TurnOffAsync(
            PileDriverController controller,
            CancellationToken cancellationToken)
        {
            _state.Value = PileDriverState.Retracting;
            await controller.RetractAsync(cancellationToken);
            _state.Value = PileDriverState.TurningOff;
            await controller.PlayTurnOffAsync(cancellationToken);
            _state.Value = PileDriverState.Complete;
        }

        private static bool IsBlocking(Cell cell)
        {
            return cell == null ||
                   !cell.IsRevealed ||
                   !cell.IsBreakable ||
                   cell.HasCellPlaceable ||
                   cell.HasWallPlaceable;
        }

        public void Dispose()
        {
            _state.Dispose();
        }
    }
}
