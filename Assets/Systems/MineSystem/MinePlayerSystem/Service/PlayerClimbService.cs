using Systems.MineSystem.MinePlayerSystem.Config;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.View;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    public sealed class PlayerClimbService : IPlayerFixedTickService
    {
        private readonly PlayerView _view;
        private readonly RuntimeDataScriptable _runtime;
        private readonly MinePlayerScriptable _playerData;
        private readonly MinePlayerDataConfig _config;
        private readonly PlayerFallService _fallService;

        private bool _toggleRequested;

        public PlayerClimbService(
            PlayerView view,
            RuntimeDataScriptable runtime,
            MinePlayerScriptable playerData,
            MinePlayerDataConfig config,
            PlayerFallService fallService)
        {
            _view = view;
            _runtime = runtime;
            _playerData = playerData;
            _config = config;
            _fallService = fallService;
        }

        public void ToggleClimb()
        {
            _toggleRequested = true;
        }

        public void OnFixedTick()
        {
            _runtime.isInsideClimbable.Value = _view.IsInsideClimbable;

            if (_runtime.lifeState.Value == PlayerLifeState.Dead)
            {
                if (_runtime.isClimbing.Value)
                    EndClimb();
                _toggleRequested = false;
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
                return;

            if (!_view.IsInsideClimbable ||
                _runtime.HasRestriction(PlayerRestrictionFlags.Climbing))
            {
                EndClimb();
                return;
            }

            var input = Vector2.ClampMagnitude(
                _runtime.movementInput.Value,
                1f);
            var velocity = input * _playerData.playerData.climbSpeed.Value;
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
            return _view.IsInsideClimbable &&
                   _runtime.canClimb.Value &&
                   !_runtime.HasRestriction(PlayerRestrictionFlags.Climbing);
        }

        private void BeginClimb()
        {
            _runtime.isClimbing.Value = true;
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
