using System;
using System.Collections.Generic;
using System.Linq;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.Interface;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.MinePlayerSystem.View;
using UnityEngine;
using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.PauseSystem.Enum;
using Systems.MineSystem.PauseSystem.Service;
using Zenject;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Elevator.Script
{
    /// <summary>
    /// Owns every placed elevator. Lifts can stand in the mine or inside the
    /// boss lair, so pause state is per <see cref="PauseArea"/> - a lift placed
    /// in the arena must not be born frozen by the mine freeze.
    /// </summary>
    public sealed class ElevatorNetworkService :
        IPlayerInteractionHandler,
        IInitializable,
        IDisposable
    {
        private readonly MineModel _mine;
        private readonly MineView _mineView;
        private readonly PlayerView _player;
        private readonly RuntimeDataScriptable _runtime;
        private readonly ElevatorInputService _input;
        private readonly PlayerClimbService _climbService;
        private readonly BossLairModel _bossLair;
        private readonly Dictionary<Vector3Int, ElevatorShaftRuntime> _shafts =
            new();
        private readonly Dictionary<Vector3Int, ElevatorLiftRuntime> _lifts =
            new();
        private readonly Dictionary<ElevatorLiftRuntime, ElevatorController>
            _controllers = new();

        private ElevatorController _activeController;
        private bool _minePaused;
        private bool _lairPaused;
        private DelegatePausable _minePausable;
        private DelegatePausable _lairPausable;
        private bool _disposed;

        public ElevatorNetworkService(
            MineModel mine,
            MineView mineView,
            PlayerView player,
            RuntimeDataScriptable runtime,
            ElevatorInputService input,
            PlayerClimbService climbService,
            BossLairModel bossLair)
        {
            _mine = mine;
            _mineView = mineView;
            _player = player;
            _runtime = runtime;
            _input = input;
            _climbService = climbService;
            _bossLair = bossLair;
        }

        public int Priority => 100;

        public void Initialize()
        {
            _minePausable = new DelegatePausable(
                () => SetAreaPaused(PauseArea.MineView, true),
                () => SetAreaPaused(PauseArea.MineView, false));
            _lairPausable = new DelegatePausable(
                () => SetAreaPaused(PauseArea.BossLair, true),
                () => SetAreaPaused(PauseArea.BossLair, false));
            _minePausable.Register(PauseArea.MineView);
            _lairPausable.Register(PauseArea.BossLair);
        }

        private bool IsPaused(PauseArea area) =>
            area == PauseArea.BossLair ? _lairPaused : _minePaused;

        private PauseArea ResolveArea(Vector3Int cell) =>
            _bossLair.HasGate &&
            _bossLair.Placement.IsValid &&
            _bossLair.Placement.InteriorCells.Contains(cell)
                ? PauseArea.BossLair
                : PauseArea.MineView;

        public bool TryInteract()
        {
            // Gated by the area the player is standing in, so a frozen mine
            // cannot be operated from the arena and vice versa.
            return !IsPaused(ResolveArea(GetPlayerCell())) &&
                   TryUseElevatorFromPlayerPosition();
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
            if (IsPaused(ResolveArea(lift.CellPosition)))
                controller.OnPause();
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

        private bool TryUseElevatorFromPlayerPosition()
        {
            if (_activeController != null)
                return false;

            var playerCell = GetPlayerCell();
            var leftCell = playerCell + Vector3Int.left;
            var rightCell = playerCell + Vector3Int.right;
            var hasLeftElevator = TryGetControllerForShaft(
                leftCell,
                out var leftController);
            var hasRightElevator = TryGetControllerForShaft(
                rightCell,
                out var rightController);

            if (hasLeftElevator && !hasRightElevator)
                return TryUseElevatorAt(leftCell, leftController);

            if (hasRightElevator && !hasLeftElevator)
                return TryUseElevatorAt(rightCell, rightController);

            if (!hasLeftElevator)
                return false;

            return _runtime.facingDirection.Value ==
                   PlayerFacingDirection.Left
                ? TryUseElevatorAt(leftCell, leftController)
                : TryUseElevatorAt(rightCell, rightController);
        }

        private bool TryGetControllerForShaft(
            Vector3Int shaftCell,
            out ElevatorController controller)
        {
            controller = null;
            if (!_shafts.ContainsKey(shaftCell))
                return false;

            foreach (var candidate in _controllers.Values)
            {
                if (!candidate.ServesCell(shaftCell))
                    continue;

                controller = candidate;
                return true;
            }

            return false;
        }

        private bool TryUseElevatorAt(
            Vector3Int shaftCell,
            ElevatorController controller)
        {
            if (controller == null)
                return false;

            if (controller.CurrentCell == shaftCell &&
                _lifts.ContainsKey(shaftCell))
            {
                controller.TryMount();
                return true;
            }

            controller.TryCallTo(shaftCell);
            return true;
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
            if (_disposed) return;
            _disposed = true;
            _minePausable?.Unregister();
            _lairPausable?.Unregister();
            foreach (var controller in _controllers.Values.ToArray())
                controller.Dispose();

            _controllers.Clear();
            _lifts.Clear();
            _shafts.Clear();
        }

        private void SetAreaPaused(PauseArea area, bool paused)
        {
            if (IsPaused(area) == paused)
                return;

            if (area == PauseArea.BossLair)
                _lairPaused = paused;
            else
                _minePaused = paused;

            foreach (var pair in _controllers)
            {
                if (ResolveArea(pair.Key.CellPosition) != area)
                    continue;
                if (paused)
                    pair.Value.OnPause();
                else
                    pair.Value.OnUnpause();
            }
        }
    }
}
