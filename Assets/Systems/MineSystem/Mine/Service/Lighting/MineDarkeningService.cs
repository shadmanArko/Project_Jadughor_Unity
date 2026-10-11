using System;
using Systems.MineSystem.Mine.Scriptable;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.Mine.Service.Lighting
{
    /// <summary>
    /// Darkens the mine by driving the Global Light 2D intensity from the player's
    /// depth. The global light shares blend style 0 (Multiply) with every reveal
    /// light, and lights on a shared blend style accumulate additively before the
    /// style is applied - so a MineLightSource near the player adds on top of the
    /// low ambient value and restores normal brightness inside its radius.
    /// </summary>
    public sealed class MineDarkeningService : IInitializable, IDisposable
    {
        private const float IntensityEpsilon = 0.001f;

        private readonly MineView _view;
        private readonly RuntimeDataScriptable _playerRuntime;
        private readonly MineDarkeningConfig _config;
        private readonly CompositeDisposable _disposables = new();
        private float _appliedIntensity = float.NaN;
        private int _playerCellY;

        public MineDarkeningService(
            MineView view,
            RuntimeDataScriptable playerRuntime,
            MineDarkeningConfig config)
        {
            _view = view;
            _playerRuntime = playerRuntime;
            _config = config;
        }

        public void Initialize()
        {
            if (_view.globalLight == null)
                throw new InvalidOperationException(
                    "MineView requires a Global Light 2D reference.");

            // The legacy AllIn1 quad is unlit, so no Light2D can ever cut through
            // it. Left in MineView (disabled) rather than deleted so the old look
            // stays one checkbox away for comparison.
            if (_view.darkeningShaderRenderer != null)
                _view.darkeningShaderRenderer.enabled = false;

            // worldPosition is a ReactiveProperty, so this fires immediately with
            // the current value and seeds the ambient light.
            _playerRuntime.worldPosition
                .Subscribe(OnPlayerPositionChanged)
                .AddTo(_disposables);
            _config.ObserveChanged()
                .Subscribe(_ => ApplyAmbient(true))
                .AddTo(_disposables);

            if (float.IsNaN(_appliedIntensity))
                ApplyAmbient(true);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }

        private void OnPlayerPositionChanged(Vector2 playerWorldPosition)
        {
            _playerCellY = _view.grid.WorldToCell(playerWorldPosition).y;
            ApplyAmbient(false);
        }

        private void ApplyAmbient(bool force)
        {
            var fade = Mathf.InverseLerp(
                _config.fadeStartCellY,
                _config.maxAlphaCellY,
                _playerCellY);
            var intensity = Mathf.Lerp(
                _config.surfaceAmbientIntensity,
                _config.deepAmbientIntensity,
                fade);

            // worldPosition ticks every frame and writing to a Light2D dirties it,
            // so only push a value that actually moved.
            if (!force &&
                Mathf.Abs(intensity - _appliedIntensity) < IntensityEpsilon)
                return;

            var light = _view.globalLight;
            light.intensity = intensity;
            light.color = _config.ambientColor;
            _appliedIntensity = intensity;
        }
    }
}
