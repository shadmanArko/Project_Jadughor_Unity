using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Enum;
using Systems.MineSystem.MineTransitionSystem.Config;
using Systems.MineSystem.MineTransitionSystem.Model;
using Systems.MineSystem.MineTransitionSystem.View;
using Systems.MineSystem.Utilities.Camera;
using UnityEngine;

namespace Systems.MineSystem.MineTransitionSystem.Service
{
    [Serializable]
    public sealed class CampToMineService
    {
        private readonly PlayerTransitionService _player;
        private readonly MineCameraController _camera;
        private readonly MineView _mineView;
        private readonly MineTransitionConfig _config;
        private readonly CampView _campView;

        public CampToMineService(
            PlayerTransitionService player,
            MineCameraController camera, 
            MineView mineView,
            MineTransitionConfig config)
        {
            _player = player;
            _camera = camera;
            _mineView = mineView;
            _config = config;
        }

        public async UniTask<MineTransitionResult> ExecuteAsync(
            CancellationToken cancellationToken)
        {
            _player.SetManualControlsEnabled(false);
            _camera.SetFreeMovement(true);
            _camera.SetFollowTarget(_player.PlayerTransform);

            try
            {
                _player.PlayForcedAnimation(PlayerAnimationId.Move,
                    PlayerFacingDirection.Right);
                await _player.AutoMoveAsync(_config.campWalkTarget,
                    _config.campWalkDuration, _config.playerMovementEase,
                    cancellationToken);
                _player.ClearForcedAnimation();

                _camera.ClearFollowTarget();
                _camera.SetFreeMovement(true);
                var entranceCell = new Vector3Int(0, 0, 0);
                var entranceCenter = _mineView.grid.GetCellCenterWorld(entranceCell);
                var entranceTop = entranceCenter.y + _mineView.grid.cellSize.y * 0.5f;
                var cameraFrom = _camera.Position;
                var cameraTo = new Vector3(cameraFrom.x,
                    entranceTop - _camera.OrthographicSize, cameraFrom.z);
                await _camera.PanAsync(cameraFrom, cameraTo,
                    _config.cameraPanDuration, cancellationToken);

                _camera.SetFreeMovement(false);
                _camera.SetFollowTarget(_player.PlayerTransform);

                var startCell = new Vector3Int(_config.mineEntryStartCell.x,
                    _config.mineEntryStartCell.y, 0);
                var landingCell = new Vector3Int(_config.mineLandingCell.x,
                    _config.mineLandingCell.y, 0);
                _player.Teleport(_mineView.grid.GetCellCenterWorld(startCell));
                _player.PlayForcedAnimation(PlayerAnimationId.ClimbVertical,
                    PlayerFacingDirection.Right);
                await _player.AutoMoveAsync(
                    _mineView.grid.GetCellCenterWorld(landingCell),
                    _config.mineEntryDuration, _config.playerMovementEase,
                    cancellationToken);

                _player.ClearForcedAnimation();
                _player.SetManualControlsEnabled(true);
                return MineTransitionResult.Completed();
            }
            catch (OperationCanceledException)
            {
                return MineTransitionResult.Cancelled();
            }
            finally
            {
                _player.ClearForcedAnimation();
            }
        }
    }
}
