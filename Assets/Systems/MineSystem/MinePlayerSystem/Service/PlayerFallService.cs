using Systems.MineSystem.Mine.Config;
using Systems.MineSystem.Mine.View;
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
        private readonly MinePlayerDataConfig _config;
        private readonly PlayerActionService _actionService;
        private readonly IPlayerDamageService _damageService;
        private readonly Grid _grid;
        private readonly float _cellWorldHeight;

        private bool _trackingFall;
        private bool _wasGrounded;
        private int _highestAirborneCellY;

        public PlayerFallService(
            PlayerView view,
            RuntimeDataScriptable runtime,
            MinePlayerDataConfig config,
            PlayerActionService actionService,
            IPlayerDamageService damageService,
            MineView mineView,
            MineGenerationConfig mineGenerationConfig)
        {
            _view = view;
            _runtime = runtime;
            _config = config;
            _actionService = actionService;
            _damageService = damageService;
            _grid = mineView != null ? mineView.grid : null;
            _cellWorldHeight = ResolveCellWorldHeight(
                mineView,
                mineGenerationConfig);
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
            var currentCellY = GetCurrentCellY(currentY);

            if (!grounded)
            {
                if (!_trackingFall)
                {
                    _trackingFall = true;
                    _runtime.highestAirborneY = currentY;
                    _highestAirborneCellY = currentCellY;
                }
                else
                {
                    _runtime.highestAirborneY =
                        Mathf.Max(_runtime.highestAirborneY, currentY);
                    _highestAirborneCellY =
                        Mathf.Max(_highestAirborneCellY, currentCellY);
                }

                _runtime.currentFallCells =
                    Mathf.Max(0, _highestAirborneCellY - currentCellY);
                _runtime.currentFallDistance =
                    _runtime.currentFallCells * _cellWorldHeight;

                if (!_runtime.isDamagingFall.Value &&
                    _view.Body.linearVelocity.y < -0.01f &&
                    _runtime.currentFallCells >
                    _config.safeFallCells)
                {
                    _runtime.isDamagingFall.Value = true;
                    _actionService.InterruptForFall();
                }

                if (_runtime.isDamagingFall.Value)
                    _runtime.locomotionState.Value =
                        PlayerLocomotionState.Falling;
            }
            else if (_trackingFall && !_wasGrounded)
            {
                if (_runtime.isDamagingFall.Value)
                {
                    ApplyLandingDamage(
                        Mathf.Max(
                            0,
                            _highestAirborneCellY - currentCellY));
                }

                ResetVerticalState();
            }

            _wasGrounded = grounded;
        }

        public void BeginFallFromCurrentPosition()
        {
            _trackingFall = true;
            _wasGrounded = false;
            _runtime.highestAirborneY = _view.Body.position.y;
            _highestAirborneCellY =
                GetCurrentCellY(_runtime.highestAirborneY);
            _runtime.currentFallDistance = 0f;
            _runtime.currentFallCells = 0f;
            _runtime.isDamagingFall.Value = false;
        }

        public void CancelFall()
        {
            _trackingFall = false;
            _runtime.currentFallDistance = 0f;
            _runtime.currentFallCells = 0f;
            _runtime.isDamagingFall.Value = false;
            _runtime.highestAirborneY = _view.Body.position.y;
            _highestAirborneCellY =
                GetCurrentCellY(_runtime.highestAirborneY);
        }

        public void ResetVerticalState()
        {
            CancelFall();
            _wasGrounded = _runtime.isGrounded.Value;
        }

        private void ApplyLandingDamage(float fallenCells)
        {
            if (fallenCells <= _config.safeFallCells)
                return;

            var damage = 0f;
            for (var i = 0; i < _config.fallDamageThresholds.Count; i++)
            {
                var threshold = _config.fallDamageThresholds[i];
                if (fallenCells >= threshold.minimumCells)
                    damage = Mathf.Max(damage, threshold.damage);
            }

            if (damage <= 0f)
                return;

            _damageService.ApplyDamage(
                damage,
                PlayerDamageKind.Fall);
        }

        private int GetCurrentCellY(float fallbackWorldY)
        {
            if (_grid == null)
            {
                return Mathf.FloorToInt(
                    fallbackWorldY / _cellWorldHeight);
            }

            Vector3 position = _view.PlayerCollider != null
                ? _view.PlayerCollider.bounds.center
                : (Vector3)_view.Body.position;
            return _grid.WorldToCell(position).y;
        }

        private static float ResolveCellWorldHeight(
            MineView mineView,
            MineGenerationConfig mineGenerationConfig)
        {
            if (mineView != null && mineView.grid != null)
            {
                var origin = mineView.grid.CellToWorld(Vector3Int.zero);
                var oneCellUp =
                    mineView.grid.CellToWorld(Vector3Int.up);
                var gridHeight = Mathf.Abs(oneCellUp.y - origin.y);
                if (gridHeight > Mathf.Epsilon)
                    return gridHeight;
            }

            const float defaultPixelsPerUnit = 100f;
            var configuredPixels = mineGenerationConfig != null
                ? mineGenerationConfig.cellSize
                : 0;
            return configuredPixels > 0
                ? configuredPixels / defaultPixelsPerUnit
                : 1f;
        }
    }
}
