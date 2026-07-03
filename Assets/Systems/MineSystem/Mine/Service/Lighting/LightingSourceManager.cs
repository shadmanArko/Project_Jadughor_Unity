using System;
using System.Collections.Generic;
using Systems.MineSystem.Mine.Scriptable;
using Systems.MineSystem.Mine.Signal;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.Utilities.EventBus;
using UniRx;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zenject;

namespace Systems.MineSystem.Mine.Service.Lighting
{
    public sealed class LightingSourceManager : IInitializable, IDisposable
    {
        private sealed class SourceState
        {
            public LightingSourceReporter Reporter;
            public bool Active;
            public bool Visible;
        }

        private readonly Camera _camera;
        private readonly MineLightingConfig _config;
        private readonly RuntimeDataScriptable _playerRuntime;
        private readonly List<SourceState> _sources = new();
        private readonly CompositeDisposable _subscriptions = new();
        private Vector2 _cameraCenter;
        private Vector2 _lastEvaluatedCenter;
        private bool _hasEvaluatedCenter;

        public LightingSourceManager(
            Camera camera,
            MineLightingConfig config,
            RuntimeDataScriptable playerRuntime)
        {
            _camera = camera;
            _config = config;
            _playerRuntime = playerRuntime;
        }

        public void Initialize()
        {
            GlobalEventBus.OnSignal<LightingSourceRegisteredSignal>()
                .Subscribe(signal => Register(signal.Source))
                .AddTo(_subscriptions);
            GlobalEventBus.OnSignal<LightingSourceActivationChangedSignal>()
                .Subscribe(signal => SetActive(signal.Source, signal.IsActive))
                .AddTo(_subscriptions);
            GlobalEventBus.OnSignal<LightingSourceUnregisteredSignal>()
                .Subscribe(signal => Unregister(signal.Source))
                .AddTo(_subscriptions);
            _config.ObserveChanged()
                .Subscribe(_ => EvaluateSources())
                .AddTo(_subscriptions);
            _playerRuntime.worldPosition
                .Subscribe(OnPlayerPositionChanged)
                .AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            for (var i = 0; i < _sources.Count; i++)
                _sources[i].Reporter?.RestoreAuthoredState();
            _sources.Clear();
        }

        private void OnPlayerPositionChanged(Vector2 position)
        {
            _cameraCenter = position;
            var threshold = _config.MovementThreshold;
            if (_hasEvaluatedCenter &&
                (position - _lastEvaluatedCenter).sqrMagnitude <
                threshold * threshold)
                return;

            EvaluateSources();
        }

        private void Register(LightingSourceReporter reporter)
        {
            if (reporter == null || !reporter.ManagedByLightingManager ||
                Find(reporter) != null)
                return;

            _sources.Add(new SourceState
            {
                Reporter = reporter,
                Active = reporter.IsSourceActive
            });
            EvaluateSources();
        }

        private void SetActive(LightingSourceReporter reporter, bool active)
        {
            var source = Find(reporter);
            if (source == null)
            {
                if (active)
                    Register(reporter);
                return;
            }

            source.Active = active;
            EvaluateSources();
        }

        private void Unregister(LightingSourceReporter reporter)
        {
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                if (_sources[i].Reporter != reporter)
                    continue;

                reporter?.RestoreAuthoredState();
                _sources.RemoveAt(i);
                return;
            }
        }

        private SourceState Find(LightingSourceReporter reporter)
        {
            for (var i = 0; i < _sources.Count; i++)
                if (_sources[i].Reporter == reporter)
                    return _sources[i];

            return null;
        }

        private void EvaluateSources()
        {
            if (_camera == null)
                return;

            var halfHeight = _camera.orthographicSize;
            var halfWidth = halfHeight * _camera.aspect;

            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                var reporter = source.Reporter;
                if (reporter == null)
                {
                    _sources.RemoveAt(i);
                    continue;
                }

                var sourcePosition = (Vector2)reporter.CachedTransform.position;
                var margin = _config.SafeAreaMargin +
                             reporter.GetEffectiveRadius();
                if (source.Visible)
                    margin += _config.ExitHysteresis;

                source.Visible = source.Active &&
                    Mathf.Abs(sourcePosition.x - _cameraCenter.x) <=
                    halfWidth + margin &&
                    Mathf.Abs(sourcePosition.y - _cameraCenter.y) <=
                    halfHeight + margin;
                ApplyVisibility(source);
            }

            _lastEvaluatedCenter = _cameraCenter;
            _hasEvaluatedCenter = true;
        }

        private static void ApplyVisibility(SourceState source)
        {
            var lights = source.Reporter.Lights;
            for (var i = 0; i < lights.Count; i++)
            {
                var light = lights[i];
                if (light == null || light.lightType == Light2D.LightType.Global)
                    continue;

                light.enabled = source.Visible &&
                                source.Reporter.WasAuthoredEnabled(i);
            }
        }
    }
}
