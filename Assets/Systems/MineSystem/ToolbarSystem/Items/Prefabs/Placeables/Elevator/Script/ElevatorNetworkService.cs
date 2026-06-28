using System;
using System.Collections.Generic;
using System.Linq;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.MinePlayerSystem.Signal.InputSignal;
using Systems.MineSystem.MinePlayerSystem.View;
using Systems.Utilities.EventBus;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Elevator.Script
{
    public sealed class ElevatorNetworkService : IInitializable, IDisposable
    {
        private readonly MineModel _mine;
        private readonly MineView _mineView;
        private readonly PlayerView _player;
        private readonly RuntimeDataScriptable _runtime;
        private readonly ElevatorInputService _input;
        private readonly PlayerClimbService _climbService;
        private readonly CompositeDisposable _disposables = new();
        private readonly Dictionary<Vector3Int, ElevatorShaftRuntime> _shafts =
            new();
        private readonly Dictionary<Vector3Int, ElevatorLiftRuntime> _lifts =
            new();
        private readonly Dictionary<ElevatorLiftRuntime, ElevatorController>
            _controllers = new();

        private ElevatorController _activeController;

        public ElevatorNetworkService(
            MineModel mine,
            MineView mineView,
            PlayerView player,
            RuntimeDataScriptable runtime,
            ElevatorInputService input,
            PlayerClimbService climbService)
        {
            _mine = mine;
            _mineView = mineView;
            _player = player;
            _runtime = runtime;
            _input = input;
            _climbService = climbService;
        }

        public void Initialize()
        {
            GlobalEventBus.OnSignal<InteractInputSignal>()
                .Subscribe(_ => TryMountFromPlayerPosition())
                .AddTo(_disposables);
        }

        public bool HasShaft(Vector3Int cell) => _shafts.ContainsKey(cell);

        public bool HasLift(Vector3Int cell) => _lifts.ContainsKey(cell);

        public bool ConnectedNetworkHasLift(Vector3Int shaftCell)
        {
            var cells = GetConnectedShaftCells(shaftCell);
            foreach (var cell in cells)
            {
                if (_lifts.ContainsKey(cell))
                    return true;
            }

            return false;
        }

        public IReadOnlyList<Vector3Int> GetConnectedShaftCells(
            Vector3Int start)
        {
            var result = new List<Vector3Int>();
            if (!_shafts.ContainsKey(start))
                return result;

            var minY = start.y;
            while (_shafts.ContainsKey(new Vector3Int(start.x, minY - 1, 0)))
                minY--;

            var y = minY;
            while (_shafts.ContainsKey(new Vector3Int(start.x, y, 0)))
            {
                result.Add(new Vector3Int(start.x, y, 0));
                y++;
            }

            return result;
        }

        public void RegisterShaft(ElevatorShaftRuntime shaft)
        {
            if (shaft == null)
                return;

            _shafts[shaft.CellPosition] = shaft;
            RefreshControllers();
        }

        public void UnregisterShaft(ElevatorShaftRuntime shaft)
        {
            if (shaft == null)
                return;

            if (_shafts.TryGetValue(shaft.CellPosition, out var existing) &&
                existing == shaft)
                _shafts.Remove(shaft.CellPosition);

            RefreshControllers();
        }

        public void RegisterLift(ElevatorLiftRuntime lift, ElevatorConfig config)
        {
            if (lift == null || !_shafts.ContainsKey(lift.CellPosition))
                return;

            _lifts[lift.CellPosition] = lift;
            var model = new ElevatorModel(
                GetConnectedShaftCells(lift.CellPosition),
                lift.CellPosition);
            var controller = new ElevatorController(
                model,
                lift,
                config,
                _player,
                _runtime,
                this,
                _input,
                _climbService);
            _controllers[lift] = controller;
        }

        public void UnregisterLift(ElevatorLiftRuntime lift)
        {
            if (lift == null)
                return;

            if (_lifts.TryGetValue(lift.CellPosition, out var existing) &&
                existing == lift)
                _lifts.Remove(lift.CellPosition);

            if (!_controllers.Remove(lift, out var controller))
                return;

            controller.ForceDismount();
            controller.Dispose();
        }

        public void NotifyLiftCellChanged(
            ElevatorLiftRuntime lift,
            Vector3Int previousCell,
            Vector3Int currentCell)
        {
            if (lift == null)
                return;

            if (_lifts.TryGetValue(previousCell, out var existing) &&
                existing == lift)
                _lifts.Remove(previousCell);

            _lifts[currentCell] = lift;
        }

        public Vector3 CellToWorldCenter(Vector3Int cell)
        {
            if (_mineView?.grid != null)
                return _mineView.grid.GetCellCenterWorld(cell);

            return _mineView.wallTileMap != null
                ? _mineView.wallTileMap.GetCellCenterWorld(cell)
                : cell;
        }

        public bool TryGetExitCell(
            Vector3Int liftCell,
            out Vector3Int exitCell)
        {
            var left = liftCell + Vector3Int.left;
            if (IsValidExitCell(left))
            {
                exitCell = left;
                return true;
            }

            var right = liftCell + Vector3Int.right;
            if (IsValidExitCell(right))
            {
                exitCell = right;
                return true;
            }

            exitCell = default;
            return false;
        }

        public void SetActiveController(ElevatorController controller)
        {
            _activeController = controller;
        }

        public void ClearActiveController(ElevatorController controller)
        {
            if (_activeController == controller)
                _activeController = null;
        }

        private void TryMountFromPlayerPosition()
        {
            if (_activeController != null)
                return;

            var playerCell = GetPlayerCell();
            var leftCell = playerCell + Vector3Int.left;
            var rightCell = playerCell + Vector3Int.right;
            var hasLeftLift = CanMountLiftAt(leftCell);
            var hasRightLift = CanMountLiftAt(rightCell);

            if (hasLeftLift && !hasRightLift)
            {
                TryMountLiftAt(leftCell);
                return;
            }

            if (hasRightLift && !hasLeftLift)
            {
                TryMountLiftAt(rightCell);
                return;
            }

            if (!hasLeftLift)
                return;

            var facingOffset = _runtime.facingDirection.Value ==
                               PlayerFacingDirection.Left
                ? Vector3Int.left
                : Vector3Int.right;
            TryMountLiftAt(playerCell + facingOffset);
        }

        private bool CanMountLiftAt(Vector3Int cell)
        {
            return _shafts.ContainsKey(cell) &&
                   _lifts.TryGetValue(cell, out var lift) &&
                   _controllers.ContainsKey(lift);
        }

        private bool TryMountLiftAt(Vector3Int cell)
        {
            if (!_shafts.ContainsKey(cell) ||
                !_lifts.TryGetValue(cell, out var lift) ||
                !_controllers.TryGetValue(lift, out var controller))
                return false;

            return controller.TryMount();
        }

        private Vector3Int GetPlayerCell()
        {
            var source = _player.PlayerCollider != null
                ? _player.PlayerCollider.bounds.center
                : _player.transform.position;

            if (_mineView.wallTileMap != null)
                return _mineView.wallTileMap.WorldToCell(source);

            return _mineView.grid.WorldToCell(source);
        }

        private bool IsValidExitCell(Vector3Int cellPosition)
        {
            var data = _mine.MineData.Value;
            var cell = data?.GetCell(cellPosition);
            return cell != null &&
                   cell.IsRevealed &&
                   cell.IsBroken &&
                   !cell.HasWallPlaceable &&
                   !_lifts.ContainsKey(cellPosition);
        }

        private void RefreshControllers()
        {
            foreach (var controller in _controllers.Values.ToArray())
                controller.RefreshNetwork();
        }

        public void Dispose()
        {
            foreach (var controller in _controllers.Values.ToArray())
                controller.Dispose();

            _controllers.Clear();
            _lifts.Clear();
            _shafts.Clear();
            _disposables.Dispose();
        }
    }
}
