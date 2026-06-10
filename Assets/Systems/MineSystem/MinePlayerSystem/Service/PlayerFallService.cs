using Systems.MineSystem.MinePlayerSystem.Config;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.View;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    public sealed class PlayerFallService : IPlayerFixedTickService
    {
        private readonly PlayerView _view;
        private readonly RuntimeDataScriptable _runtime;
        private readonly MinePlayerScriptable _playerData;
        private readonly MinePlayerDataConfig _config;

        private bool _trackingFall;
        private bool _wasGrounded;

        public PlayerFallService(
            PlayerView view,
            RuntimeDataScriptable runtime,
            MinePlayerScriptable playerData,
            MinePlayerDataConfig config)
        {
            _view = view;
            _runtime = runtime;
            _playerData = playerData;
            _config = config;
        }

        public void OnFixedTick()
        {
            if (_runtime.lifeState.Value == PlayerLifeState.Dead)
            {
                ResetVerticalState();
                return;
            }

            if (_runtime.isClimbing.Value)
            {
                CancelFall();
                _wasGrounded = false;
                return;
            }

            var grounded = _runtime.isGrounded.Value;
            var currentY = _view.Body.position.y;

            if (!grounded)
            {
                if (!_trackingFall)
                {
                    _trackingFall = true;
                    _runtime.highestAirborneY = currentY;
                }
                else
                {
                    _runtime.highestAirborneY =
                        Mathf.Max(_runtime.highestAirborneY, currentY);
                }

                _runtime.currentFallDistance =
                    Mathf.Max(0f, _runtime.highestAirborneY - currentY);

                if (_view.Body.linearVelocity.y < -0.01f)
                    _runtime.locomotionState.Value =
                        PlayerLocomotionState.Falling;
            }
            else if (_trackingFall && !_wasGrounded)
            {
                ApplyLandingDamage(
                    Mathf.Max(0f, _runtime.highestAirborneY - currentY));
                ResetVerticalState();
            }

            _wasGrounded = grounded;
        }

        public void BeginFallFromCurrentPosition()
        {
            _trackingFall = true;
            _wasGrounded = false;
            _runtime.highestAirborneY = _view.Body.position.y;
            _runtime.currentFallDistance = 0f;
        }

        public void CancelFall()
        {
            _trackingFall = false;
            _runtime.currentFallDistance = 0f;
            _runtime.highestAirborneY = _view.Body.position.y;
        }

        public void ResetVerticalState()
        {
            CancelFall();
            _wasGrounded = _runtime.isGrounded.Value;
        }

        private void ApplyLandingDamage(float fallDistance)
        {
            if (fallDistance <= _config.safeFallDistance)
                return;

            var damage = 0f;
            for (var i = 0; i < _config.fallDamageThresholds.Count; i++)
            {
                var threshold = _config.fallDamageThresholds[i];
                if (fallDistance >= threshold.minimumDistance)
                    damage = Mathf.Max(damage, threshold.damage);
            }

            if (damage <= 0f)
                return;

            var health = _playerData.playerData.health;
            health.Value = Mathf.Max(0f, health.Value - damage);
        }
    }
}
