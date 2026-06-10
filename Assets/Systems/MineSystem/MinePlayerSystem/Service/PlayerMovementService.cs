using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.View;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    public sealed class PlayerMovementService : IPlayerFixedTickService
    {
        private readonly PlayerView _view;
        private readonly MinePlayerScriptable _playerData;
        private readonly RuntimeDataScriptable _runtimeData;

        public PlayerMovementService(
            PlayerView view,
            MinePlayerScriptable playerData,
            RuntimeDataScriptable runtimeData)
        {
            _view = view;
            _playerData = playerData;
            _runtimeData = runtimeData;
        }

        public void OnFixedTick()
        {
            var velocity = _view.Body.linearVelocity;
            if (_runtimeData.lifeState.Value == PlayerLifeState.Dead ||
                _runtimeData.isClimbing.Value ||
                !_runtimeData.canMove.Value ||
                _runtimeData.HasRestriction(PlayerRestrictionFlags.Movement))
            {
                velocity.x = 0f;
                _view.SetVelocity(velocity);
                _runtimeData.velocity.Value = velocity;
                return;
            }

            var horizontalInput = Mathf.Clamp(
                _runtimeData.movementInput.Value.x,
                -1f,
                1f);
            velocity.x =
                horizontalInput * _playerData.playerData.moveSpeed.Value;
            _view.SetVelocity(velocity);
            _runtimeData.velocity.Value = velocity;

            if (Mathf.Abs(horizontalInput) > 0.01f)
            {
                _runtimeData.facingDirection.Value = horizontalInput < 0f
                    ? PlayerFacingDirection.Left
                    : PlayerFacingDirection.Right;
            }

            if (_runtimeData.isGrounded.Value)
            {
                _runtimeData.locomotionState.Value =
                    Mathf.Abs(horizontalInput) > 0.01f
                        ? PlayerLocomotionState.Moving
                        : PlayerLocomotionState.Idle;
            }
        }
    }
}
