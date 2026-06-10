using System;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.View;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    public sealed class PlayerMovementService :
        IPlayerFixedTickService,
        IDisposable
    {
        private readonly PlayerView _view;
        private readonly MinePlayerScriptable _playerData;
        private readonly RuntimeDataScriptable _runtimeData;

        private Vector2 _input;

        public PlayerMovementService(
            PlayerView view,
            MinePlayerScriptable playerData,
            RuntimeDataScriptable runtimeData)
        {
            _view = view;
            _playerData = playerData;
            _runtimeData = runtimeData;
        }

        public void SetInput(Vector2 direction)
        {
            _input = Vector2.ClampMagnitude(direction, 1f);
        }

        public void OnFixedTick()
        {
            if (!_runtimeData.canMove.Value)
            {
                _view.Stop();
                return;
            }

            var distance =
                _playerData.playerData.moveSpeed.Value * Time.fixedDeltaTime;
            _view.MoveTo(_view.Body.position + _input * distance);
        }

        public void Dispose()
        {
            _input = Vector2.zero;
            _view.Stop();
        }
    }
}
