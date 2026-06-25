using System;
using Systems.MineSystem.Mine.Config;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.Config;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.View;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    [Serializable]
    public sealed class PlayerClimbService : IPlayerFixedTickService
    {
        private readonly PlayerView _view;
        private readonly MineView _mineView;
        private readonly RuntimeDataScriptable _runtime;
        private readonly MinePlayerScriptable _playerData;
        private readonly MinePlayerDataConfig _config;
        private readonly MineModel _mineModel;
        private readonly VineConfig _vineConfig;
        private readonly PlayerFallService _fallService;

        private bool _toggleRequested;
        private bool _wasGrounded;

        public PlayerClimbService(
            PlayerView view,
            MineView mineView,
            RuntimeDataScriptable runtime,
            MinePlayerScriptable playerData,
            MinePlayerDataConfig config,
            MineModel mineModel,
            VineConfig vineConfig,
            PlayerFallService fallService)
        {
            _view = view;
            _mineView = mineView;
            _runtime = runtime;
            _playerData = playerData;
            _config = config;
            _mineModel = mineModel;
            _vineConfig = vineConfig;
            _fallService = fallService;
        }

        public void ToggleClimb()
        {
            _toggleRequested = true;
        }

        public void OnFixedTick()
        {
            var isInsideClimbable = IsInsideClimbableCell();
            var isGrounded = _runtime.isGrounded.Value;
            _runtime.isInsideClimbable.Value = isInsideClimbable;

            if (_runtime.lifeState.Value == PlayerLifeState.Dead)
            {
                if (_runtime.isClimbing.Value)
                    EndClimb();
                _toggleRequested = false;
                _wasGrounded = isGrounded;
                return;
            }

            if (_toggleRequested)
            {
                _toggleRequested = false;
                if (_runtime.isClimbing.Value)
                    EndClimb();
                else if (CanStartClimb())
                    BeginClimb();
            }

            if (!_runtime.isClimbing.Value)
            {
                _wasGrounded = isGrounded;
                return;
            }

            var landed = !_wasGrounded && isGrounded;
            _wasGrounded = isGrounded;

            if (landed ||
                !isInsideClimbable ||
                _runtime.HasRestriction(PlayerRestrictionFlags.Climbing))
            {
                EndClimb();
                return;
            }

            if (_runtime.HasRestriction(PlayerRestrictionFlags.Movement))
            {
                _view.SetVelocity(Vector2.zero);
                _runtime.velocity.Value = Vector2.zero;
                _runtime.locomotionState.Value =
                    PlayerLocomotionState.Climbing;
                return;
            }

            var input = Vector2.ClampMagnitude(
                _runtime.movementInput.Value,
                1f);
            var velocity = input * _playerData.playerData.climbSpeed.Value * GetCurrentVineClimbMultiplier();
            _view.SetVelocity(velocity);
            _runtime.velocity.Value = velocity;
            _runtime.locomotionState.Value =
                PlayerLocomotionState.Climbing;

            if (Mathf.Abs(input.x) > 0.01f)
            {
                _runtime.facingDirection.Value = input.x < 0f
                    ? PlayerFacingDirection.Left
                    : PlayerFacingDirection.Right;
            }
        }

        private bool CanStartClimb()
        {
            return _runtime.isInsideClimbable.Value &&
                   _runtime.canClimb.Value &&
                   !_runtime.HasRestriction(PlayerRestrictionFlags.Climbing);
        }

        private bool IsInsideClimbableCell()
        {
            var wallTileMap = _mineView.wallTileMap;
            if (wallTileMap == null)
                return false;

            var cellPosition = GetCurrentCellPosition();
            return cellPosition != Vector3Int.zero &&
                   !wallTileMap.HasTile(cellPosition);
        }

        private float GetCurrentVineClimbMultiplier()
        {
            var mineData = _mineModel.MineData.Value;
            if (mineData == null || _vineConfig == null)
                return 1f;

            var cell = mineData.GetCell(GetCurrentCellPosition());
            if (cell == null || !cell.HasVine)
                return 1f;

            var vine = mineData.GetVine(cell.Id);
            return vine == null
                ? 1f
                : _vineConfig.GetClimbSpeedMultiplier(vine.SourceId);
        }

        private Vector3Int GetCurrentCellPosition()
        {
            return _mineView.wallTileMap.WorldToCell(
                _view.PlayerCollider.bounds.center);
        }

        private void BeginClimb()
        {
            _runtime.isClimbing.Value = true;
            _wasGrounded = _runtime.isGrounded.Value;
            _runtime.locomotionState.Value =
                PlayerLocomotionState.Climbing;
            _view.SetGravityScale(0f);
            _fallService.CancelFall();
        }

        private void EndClimb()
        {
            _runtime.isClimbing.Value = false;
            _view.SetGravityScale(_config.normalGravityScale);

            if (!_runtime.isGrounded.Value)
                _fallService.BeginFallFromCurrentPosition();
        }
    }
}
