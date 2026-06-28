using System;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.MinePlayerSystem.View;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Elevator.Script
{
    public sealed class ElevatorController : IDisposable
    {
        private readonly ElevatorModel _model;
        private readonly ElevatorLiftRuntime _lift;
        private readonly ElevatorConfig _config;
        private readonly PlayerView _player;
        private readonly RuntimeDataScriptable _runtime;
        private readonly ElevatorNetworkService _network;
        private readonly ElevatorInputService _input;
        private readonly PlayerClimbService _climbService;
        private readonly CompositeDisposable _disposables = new();

        private bool _disposed;
        private float _previousGravityScale = 1f;
        private bool _ownsRider;
        private bool _motionLoopRunning;
        private bool _riderFollowRunning;
        private int _heldDirection;
        private int _lastTravelDirection;

        public ElevatorController(
            ElevatorModel model,
            ElevatorLiftRuntime lift,
            ElevatorConfig config,
            PlayerView player,
            RuntimeDataScriptable runtime,
            ElevatorNetworkService network,
            ElevatorInputService input,
            PlayerClimbService climbService)
        {
            _model = model;
            _lift = lift;
            _config = config;
            _player = player;
            _runtime = runtime;
            _network = network;
            _input = input;
            _climbService = climbService;

            _input.VerticalDirection
                .Subscribe(SetHeldDirection)
                .AddTo(_disposables);
            _input.InteractRequested
                .Subscribe(_ => TryExit())
                .AddTo(_disposables);
        }

        public bool HasRider => _model.HasRider;
        public Vector3Int CurrentCell => _model.CurrentLiftCell;

        public void RefreshNetwork()
        {
            _model.ReplaceShaftCells(
                _network.GetConnectedShaftCells(_model.CurrentLiftCell));
        }

        public bool TryMount()
        {
            if (_disposed || _model.HasRider || _model.IsMoving)
                return false;

            RefreshNetwork();
            if (!_network.HasShaft(_model.CurrentLiftCell))
                return false;

            _climbService.PrepareForTransport();
            _previousGravityScale = _player.Body.gravityScale;
            _ownsRider = true;
            _model.SetRider(true);
            RestrictPlayer(true);
            _player.SetGravityScale(0f);
            TeleportRiderToLift();
            KeepRiderAnchoredAsync().Forget();
            _input.EnableElevator();
            _network.SetActiveController(this);
            return true;
        }

        public void ForceDismount()
        {
            if (!_model.HasRider)
                return;

            if (!TryExit())
                RestorePlayerControls();
        }

        public bool TryExit()
        {
            if (_disposed || !_model.HasRider || _model.IsMoving)
                return false;

            if (!_network.TryGetExitCell(
                    _model.CurrentLiftCell,
                    out var exitCell))
                return false;

            var exitWorld = _network.CellToWorldCenter(exitCell) +
                            (Vector3)(_config != null
                                ? _config.ExitOffset
                                : Vector2.zero);
            _player.Teleport(exitWorld);
            RestorePlayerControls();
            _climbService.TryBeginClimbImmediately();
            return true;
        }

        private void SetHeldDirection(int direction)
        {
            _heldDirection = Math.Sign(direction);
            if (_heldDirection != 0)
                _lastTravelDirection = _heldDirection;

            if (_disposed || !_model.HasRider || _motionLoopRunning)
                return;

            MoveContinuouslyAsync().Forget();
        }

        private async UniTaskVoid MoveContinuouslyAsync()
        {
            _motionLoopRunning = true;

            try
            {
                while (!_disposed && _model.HasRider)
                {
                    if (!TryResolveMotionTarget(out var targetCell))
                    {
                        _model.EndMove();
                        break;
                    }

                    _model.BeginMove();
                    var target = _network.CellToWorldCenter(targetCell);
                    _lift.transform.position = Vector3.MoveTowards(
                        _lift.transform.position,
                        target,
                        GetCellTravelSpeed() * Time.fixedDeltaTime);
                    TeleportRiderToLift();

                    if (Vector3.SqrMagnitude(
                            _lift.transform.position - target) <= 0.00000001f)
                    {
                        _lift.transform.position = target;
                        if (targetCell != _model.CurrentLiftCell)
                        {
                            _model.SetLiftCell(targetCell);
                            _lift.SetCurrentCell(targetCell);
                        }

                        if (_heldDirection == 0)
                        {
                            _model.EndMove();
                            break;
                        }
                    }

                    await UniTask.Yield(PlayerLoopTiming.FixedUpdate);
                }
            }
            finally
            {
                _motionLoopRunning = false;
                if (_disposed || !_model.HasRider)
                    _model.EndMove();
            }
        }

        private bool TryResolveMotionTarget(out Vector3Int targetCell)
        {
            const float positionTolerance = 0.0001f;
            var currentCell = _model.CurrentLiftCell;
            var currentCenter = _network.CellToWorldCenter(currentCell);
            var verticalOffset = _lift.transform.position.y - currentCenter.y;

            if (_heldDirection != 0)
            {
                _lastTravelDirection = _heldDirection;
                if (verticalOffset * _heldDirection < -positionTolerance)
                {
                    targetCell = currentCell;
                    return true;
                }

                return _model.TryGetAdjacentCell(
                    _heldDirection,
                    out targetCell);
            }

            if (Mathf.Abs(verticalOffset) <= positionTolerance)
            {
                targetCell = currentCell;
                return false;
            }

            var travelDirection = Math.Sign(verticalOffset);
            if (!_model.TryGetAdjacentCell(travelDirection, out var adjacent))
            {
                targetCell = currentCell;
                return true;
            }

            var adjacentCenter = _network.CellToWorldCenter(adjacent);
            var cellDistance = Mathf.Abs(adjacentCenter.y - currentCenter.y);
            var progress = cellDistance > Mathf.Epsilon
                ? Mathf.Abs(verticalOffset) / cellDistance
                : 0f;
            targetCell = progress > 0.5f ||
                         Mathf.Approximately(progress, 0.5f) &&
                         travelDirection == _lastTravelDirection
                ? adjacent
                : currentCell;
            return true;
        }

        private float GetCellTravelSpeed()
        {
            var currentCenter =
                _network.CellToWorldCenter(_model.CurrentLiftCell);
            var neighboringCenter = _network.CellToWorldCenter(
                _model.CurrentLiftCell + Vector3Int.up);
            var cellDistance = Mathf.Abs(
                neighboringCenter.y - currentCenter.y);
            var duration = _config != null
                ? _config.MoveDurationSeconds
                : 0.22f;
            return Mathf.Max(cellDistance, 0.01f) / duration;
        }

        private void TeleportRiderToLift()
        {
            var offset = _config != null
                ? _config.RiderOffset
                : Vector2.zero;
            _player.Teleport(_lift.transform.position + (Vector3)offset);
        }

        private async UniTaskVoid KeepRiderAnchoredAsync()
        {
            if (_riderFollowRunning)
                return;

            _riderFollowRunning = true;
            try
            {
                while (!_disposed && _model.HasRider)
                {
                    await UniTask.Yield(PlayerLoopTiming.FixedUpdate);
                    if (_disposed || !_model.HasRider)
                        break;

                    TeleportRiderToLift();
                }
            }
            finally
            {
                _riderFollowRunning = false;
            }
        }

        private void RestorePlayerControls()
        {
            _model.SetRider(false);
            _heldDirection = 0;
            RestrictPlayer(false);
            _runtime.isClimbing.Value = false;
            _runtime.movementInput.Value = Vector2.zero;
            _player.SetGravityScale(_previousGravityScale);
            _player.Stop();
            _input.DisableElevator();
            _network.ClearActiveController(this);
            _ownsRider = false;
        }

        private void RestrictPlayer(bool restricted)
        {
            _runtime.SetRestriction(PlayerRestrictionFlags.Movement, restricted);
            _runtime.SetRestriction(PlayerRestrictionFlags.Climbing, restricted);
            _runtime.SetRestriction(PlayerRestrictionFlags.Action, restricted);
            _runtime.canMove.Value = !restricted;
            _runtime.canClimb.Value = !restricted;
            _runtime.canPerformAction.Value = !restricted;
            _runtime.canUsePickaxe.Value = !restricted;
            _runtime.canUseWeapon.Value = !restricted;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _disposables.Dispose();

            if (_ownsRider)
                RestorePlayerControls();
        }
    }
}
